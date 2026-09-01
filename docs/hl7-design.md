# Blood Bank LIS — HL7 Interface Design

Status: Implemented in Phase 5. The HL7 layer (`BloodBankLIS.HL7`) is an **original, in-house** HL7 v2.x parser/generator. It is fully isolated from business logic: it parses/serializes messages and maps fields, but all clinical actions go through the same Application use cases the API uses, so the safety checks in `safety-rules.md` always apply.

Scope for the project: inbound **ADT** (demographics/encounter) and **ORM/OML** (orders); outbound **ORU** (results) and a standard outbound **DFT^P03** (billing). MLLP send of outbound DFT (and ORU) remains a later-phase transport item.

---

## 1. Parser/generator design

### 1.1 Tokenizing
- Read encoding characters from `MSH-1` (field separator, default `|`) and `MSH-2` (component `^`, repetition `~`, escape `\`, subcomponent `&`). Never hard-code separators; honor what the message declares.
- Structure: `Message -> Segment[] -> Field[] -> Repetition[] -> Component[] -> Subcomponent[]`.
- Preserve the raw message verbatim alongside the parsed structure (stored in `HL7Messages.RawMessage`).

### 1.2 Object model (in `BloodBankLIS.HL7`)
- `Hl7Message`, `Hl7Segment`, `Hl7Field` with safe accessors like `Get("PID-3-1")` returning empty string (not null/exception) for missing fields.
- Builders (`Hl7MessageBuilder`) for outbound messages that apply the configured encoding characters and escape rules.
- Pure functions; no database or network access in this project. Network transport lives in the Api hosted services.

### 1.3 Validation
- Structural validation: required segments present (e.g. MSH, PID), `MSH-9` message type recognized, `MSH-10` control id present.
- Semantic mapping validation happens in the mapping layer; failures produce a NAK with a meaningful `ERR`/`MSA` text.

---

## 2. Message mapping (configurable, not hard-coded)

Field mappings live on each `InterfaceEndpoint` as `InterfaceFieldMappings` rows (data item key → HL7 path such as `PID-3-1`). Admin **Interface Setup** (`/admin/hl7`) can apply an EHR/billing vendor preset (Epic, Cerner, Meditech, Epic Resolute, Cerner Patient Accounting) or edit paths by hand. Inbound ADT/ORM/BPAM mappers and outbound ORU/DFT builders read those paths; catalog defaults match the original hard-coded locations when no rows are stored.

### 2.1 Inbound ADT (A01 admit, A04 register, A08 update)
Default catalog (customizable per endpoint):
| Data item | Default HL7 |
|---|---|
| `Patient.MedicalRecordNumber` | `PID-3-1` |
| `Patient.LastName` / `FirstName` / `MiddleName` | `PID-5-1` / `PID-5-2` / `PID-5-3` |
| `Patient.DateOfBirth` | `PID-7` |
| `Patient.Sex` | `PID-8` |
| `Encounter.AccountNumber` | `PID-18-1` |
| `Encounter.VisitNumber` | `PV1-19-1` |
| `Encounter.CurrentLocation` | `PV1-3-1` |

Action: upsert patient demographics and visit. Demographics updates never overwrite clinical immunohematology history.

### 2.2 Inbound ORM/OML (orders)
| Data item | Default HL7 |
|---|---|
| `Order.OrderControl` | `ORC-1` |
| `Order.OrderNumber` | `ORC-2-1` |
| `Patient.MedicalRecordNumber` | `PID-3-1` |
| `Order.TestCode` | `OBR-4-1` |

Action: create or cancel the order through `OrderService` (same safety checks as the UI).

### 2.3 Outbound ORU (results)
Builds `MSH + PID + OBR + OBX` from a verified `TestResult` using the enabled Results outbound endpoint's MSH identity and field map. Stored in `HL7Messages` with direction Outbound.

### 2.4 Outbound DFT (billing)
Triggered when charge capture creates a `BillingEvent`. Builds `MSH + EVN + PID + FT1` DFT^P03. Default `FT1-6` is `CG`, `FT1-7` is the billing code, `FT1-4` is the service date. Transaction amount is omitted — catalog price is internal only.

### 2.5 Inbound BPAM (RAS/BPS)
Thin blood-product administration intake (not IHE/Epic certification). Mapped fields include patient MRN, unit number/DIN, start/stop, volume, location, transfusionist, and reaction flag. When an issued unit for that patient can be matched, a `TransfusionEvent` is documented; otherwise the message is NAK/`AE` and queued.

---

## 3. ACK/NAK handling

```mermaid
flowchart TD
    a([Inbound message]) --> b[Persist to HL7Messages: Received]
    b --> c{Parse OK?}
    c -->|No| n[Build NAK MSA=AR, log ERR]
    c -->|Yes| d{Map + execute use case}
    d -->|Success| k[Build ACK MSA=AA, Status=Processed]
    d -->|App error| e[Build NAK MSA=AE, enqueue error]
    n --> r([Return response])
    e --> r
    k --> r
```

- `MSA-1` codes: `AA` (accept), `AE` (application error), `AR` (reject).
- ACK `MSA-2` echoes the inbound `MSH-10` control id.
- Application errors that are retryable go to `InterfaceErrorQueue` (AE); structural rejects (AR) are logged but not retried automatically.

---

## 4. Reliability: transport, retry, replay

- **Transport**: MLLP over TCP (framing bytes 0x0B ... 0x1C 0x0D). Configurable host/port per `InterfaceEndpoints`. File-drop transport is supported as an alternative.
- **Hosted services** in `BloodBankLIS.Api`: an inbound MLLP listener and an outbound sender, both thin adapters that call `BloodBankLIS.HL7` for parse/build and the Application layer for actions. The listener binds **each enabled inbound MLLP `InterfaceEndpoint` port** at API startup (restart the API after enabling or changing a port). `Hl7:Mllp:Enabled=true` optionally adds a fallback port (`Hl7:Mllp:Port`, default 2575). Enabling an endpoint in Admin does not bind TCP by itself.
- **Retry**: failed outbound sends and retryable inbound processing use exponential backoff with `RetryCount` and `NextRetryUtc` in `InterfaceErrorQueue`.
- **Replay**: any stored message in `HL7Messages` can be re-submitted through the same pipeline (`ReplayMessageCommand`); replays are marked `Replayed` and audited. Idempotency is protected by `MessageControlId` plus business-key checks so replays do not duplicate orders/patients.

---

## 5. Logging and observability

- Every message (in/out) is persisted with raw text, parsed JSON, status, timestamps, ack code, and error detail.
- `InterfaceErrorQueue` is the operational work list for interface failures, with resolve/replay actions that are audited.
- Indexes on `MessageControlId`, `Status`, `ReceivedUtc`, `MessageType` support the error-queue and replay UIs.

---

## 6. Endpoint configuration

Admin **Interface Setup** (`/admin/hl7`, permission `admin.hl7.manage`) configures each `InterfaceEndpoint`:

- **Connection** — name, interface type (`ADT`, `Billing`, `Orders`, `Results`, `BPAM`), direction, transport, host/IP and port, MSH sending/receiving application and facility, ACK/retry/logging.
- **Vendor preset** — Epic, Cerner, Meditech (and billing variants Epic Resolute / Cerner Patient Accounting) fill MSH identities and a field map for the selected type.
- **Custom mapping** — each application data item is paired with an HL7 path (`PID-3-1`, `OBR-4-1`, `FT1-7`, `RXA-15`, …).

`MessageTypes` is derived from interface type (`ADT`; `ORM,OML`; `ORU`; `DFT`; `RAS,BPS`) so inbound dispatch still keys off `MSH-9`. `VendorCode` is also stored on `MappingProfile` for backward compatibility. Host/port remain facility-entered; no sending/receiving identifiers are hard-coded.
