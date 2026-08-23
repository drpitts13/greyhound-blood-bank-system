# Blood Bank LIS — Key Workflows

Status: Phase 0 (design). Each workflow names the use case(s), the safety checks invoked (see `safety-rules.md`), the state changes, and the audit events produced. Every clinical state change writes to its append-only history table and an `AuditEvent` in the same transaction.

Status legend for blood units: `Quarantine -> Available -> Allocated -> Issued -> Transfused`, with side states `Returned`, `Discarded`, `Expired`.

---

## 1. Unit intake (receiving)

```mermaid
flowchart TD
    a([Receive shipment]) --> b[Enter/scan unit number + product code + ABO/Rh + expiration]
    b --> c{Validate fields}
    c -->|Invalid| r[Reject entry, no record]
    c -->|Valid| d[Create BloodProduct in Quarantine]
    d --> e[Record InventoryStatusHistory: null -> Quarantine]
    e --> f{Release checks pass?}
    f -->|No| q[Remain in Quarantine]
    f -->|Yes| g[Status -> Available]
    g --> h[Audit + status history]
```

- Use case: `ReceiveUnitCommand`, `ReleaseUnitFromQuarantineCommand`.
- Checks: unit number unique; expiration in the future; product type known; ABO/Rh present.
- Audit: `Create` (unit), `Update` (status change) with location.

---

## 2. Specimen accessioning and order linkage

```mermaid
flowchart TD
    a([Order arrives: HL7 or manual]) --> b[Create/Update Order]
    b --> c[Accession specimen: assign AccessionNumber + barcode]
    c --> d[Set CollectedUtc, ReceivedUtc]
    d --> e[Compute ExpiresUtc per specimen rule]
    e --> f[Link Order to Specimen]
    f --> g{Acceptable?}
    g -->|No| h[Status -> Rejected with reason]
    g -->|Yes| i[Status -> Accepted]
    i --> j[Audit + ready for testing]
```

- Use cases: `CreateOrderCommand`, `AccessionSpecimenCommand`, `LinkOrderSpecimenCommand`, `RejectSpecimenCommand`.
- Checks: patient identity resolved; collection date/time present and not in the future; specimen type valid.
- Expiration: computed from policy (e.g. type-and-screen specimen valid 3 days when patient may have been transfused/pregnant; configurable in `SystemConfiguration`). See `safety-rules.md`.

---

## 3. Result entry, verification, correction

```mermaid
flowchart TD
    a([Enter result]) --> b[TestResult v1, Status=Entered]
    b --> c[Delta check vs history]
    c -->|Discrepancy| w[Warning surfaced to verifier]
    c -->|Consistent| v
    w --> v{Verify?}
    v -->|Verify| d[Status=Verified, VerifiedBy/Utc set]
    d --> e[If ABO/Rh: append PatientBloodTypeHistory IsCurrent]
    e --> f[Billing trigger: TestVerified]
    d --> g{Need correction later?}
    g -->|Yes reason + e-sign| h[New version, prior superseded]
    h --> i[Audit Correct, history preserved]
```

- Use cases: `EnterResultCommand`, `VerifyResultCommand`, `CorrectResultCommand`.
- Checks: result cannot be verified by entry of unknown test; ABO/Rh discrepancy vs history (`RES-ABORH-DELTA`) blocks verify until an authorized override chooses Retain or Replace (gated by exception `MinSecurityLevel`); correcting a verified result requires reason + e-signature.
- No silent change: corrections always create a new `TestResults` version; the old row is retained and marked superseded.

---

## 4. Crossmatch, allocation, issue, transfusion (primary path)

```mermaid
flowchart TD
    a([Type and screen complete]) --> b[Select candidate units by ABO/Rh + attributes]
    b --> c[Crossmatch: serologic or electronic]
    c -->|Incompatible| x[Cannot allocate; document]
    c -->|Compatible| d[Allocate/Reserve unit to patient]
    d --> e([Request to issue])
    e --> f[Run issue gate: full safety check]
    f -->|HardStop| block[Block + audit attempt]
    f -->|Warning| ovr{Authorized override?}
    ovr -->|No| block
    ovr -->|Yes reason + e-sign| g
    f -->|Pass| g[Record Issue, Status -> Issued]
    g --> p[Generate + print P-tag]
    p --> t[Transfusion documentation]
    t --> u[Final disposition: Completed / Stopped]
    u --> bill[Billing trigger: UnitIssued]
```

- Use cases: `RecordCrossmatchCommand`, `AllocateUnitCommand`, `IssueUnitCommand`, `DocumentTransfusionCommand`.
- The **issue gate** (`safety-rules.md` section 1) runs the full check set before any unit leaves inventory.
- Electronic crossmatch path is allowed only when its preconditions are met (current ABO/Rh confirmed, negative antibody screen current/historical, no antibody history); otherwise serologic crossmatch is required (HardStop). Positive antibody screen (current or historical) or antibody history requires a complex crossmatch unless an authorized `ALLOC-XM-AB-HISTORY` override is recorded.
- Compatibility evaluation order: (1) ABO/Rh antigen/antibody conflict, (2) non-ABORH antigen-negative for RBC/WB (`ISS-ANTIGEN-NEG` Warning, supervisor+ override), (3) complex XM when indicated, (4) compatible XM required for RBC/WB.

---

## 5. Issue-to-patient gate (detail)

```mermaid
flowchart TD
    start([Issue request]) --> id[Verify patient identity vs specimen + unit tag]
    id --> spec[Specimen exists, correct patient, not expired]
    spec --> abo[Patient ABO/Rh known + unit ABO/Rh present]
    abo --> comp[ABO/Rh Ag/Ab compatibility]
    comp --> ptype[Product type matches order]
    ptype --> ustat[Unit status Available/Allocated, not expired/discarded]
    ustat --> alloc[Unit allocated to THIS patient]
    alloc --> xm[Crossmatch satisfied OR valid emergency release]
    xm --> spec2[Special requirements + antigen-negative satisfied]
    spec2 --> ab[Antibody history checked]
    ab --> out{Aggregate outcome}
    out -->|Any HardStop| stop[Block + audit]
    out -->|Warnings only| ovr[Override path]
    out -->|All pass| ok[Issue allowed]
```

---

## 6. Emergency release (uncrossmatched)

```mermaid
flowchart TD
    a([Emergency request]) --> b[Select group O / Rh per policy]
    b --> c[Issue gate runs but flags uncrossmatched]
    c --> d{Emergency release authorized?}
    d -->|No| stop[Block]
    d -->|Yes reason + authorizer + e-sign| e[Create Override record]
    e --> f[Issue with IssueType=EmergencyRelease]
    f --> g[Print P-tag marked EMERGENCY/UNCROSSMATCHED]
    g --> h[Audit + flag for retrospective crossmatch]
```

- Crossmatch-not-performed becomes an overridable Warning only inside the emergency-release workflow; outside it, missing crossmatch on a crossmatch-required product is a HardStop.
- Records `Overrides` + `ElectronicSignatures`; flags the unit/patient for retrospective compatibility testing.

---

## 7. Return to inventory

```mermaid
flowchart TD
    a([Return issued unit]) --> b[Re-evaluate reissue eligibility]
    b --> c{Within storage limits? Integrity intact? Time/temp ok?}
    c -->|No| d[Status -> Quarantine or Discard, ReissueEligible=false]
    c -->|Yes| e[Status -> Available, ReissueEligible=true]
    e --> f[Release prior allocation if appropriate]
    d --> g[Audit + InventoryStatusHistory]
    e --> g
```

- Use case: `ReturnUnitCommand`. Stores the per-check evaluation JSON so reissue decisions are auditable.

---

## 8. Discard

```mermaid
flowchart TD
    a([Discard request]) --> b{Reason provided?}
    b -->|No| stop[Block]
    b -->|Yes + confirmation| c[Status -> Discarded]
    c --> d[InventoryStatusHistory + Audit Discard]
    d --> e[Unit no longer selectable]
```

- Dangerous action: requires reason, confirmation, audit. Discarded units are excluded from all selection queries.

---

## 8a. Product modification (divide / pool / irradiate / thaw / volume-reduce / leukoreduce)

```mermaid
flowchart TD
    admin["Admin: ModificationRules + ExpirationModificationCodes\n(source, type, target, expiration code)"] --> eligible
    tech["Technologist selects source unit(s)"] --> eligible["GET eligible-modifications\n(active rules matching unit's product)"]
    eligible --> guard["UnitModificationEligibilityRule:\nstatus=Available, unexpired, product match;\nPool: >=2 sources + same product/ABO/Rh;\nDivide: >=2 result units, volumes <= source"]
    guard -->|HardStop| blocked[422 blocked, hardStops/warnings]
    guard -->|Pass| execute[Compute ResultExpiresUtc via\nModificationExpirationRule, capped at\nearliest source ExpiresUtc]
    execute --> source[Source unit(s) -> Modified\n+ InventoryStatusHistory]
    execute --> result[Result unit(s) created -> Quarantine\n+ DerivedFromModificationId]
    execute --> header[UnitModification header +\nUnitModificationUnit link rows]
    execute --> audit[Audit: Modify]
```

- Use cases: `DivideAsync`, `PoolAsync`, `ApplySingleAsync` (`BloodProductModificationService`).
- Every modification retires its source unit(s) into the terminal `Modified` status and creates new result unit(s) in `Quarantine` (new units always need release, same convention as intake) — this keeps 1→N (Divide), N→1 (Pool), and 1→1 (Irradiate/Thaw/Volume Reduction/Leukoreduction) on one execution path.
- Checks: source unit(s) `Available`, unexpired, and on the modification rule's source product; Pool additionally requires ≥2 sources with identical product/ABO/Rh (`MOD-POOL-ABO-MISMATCH` otherwise); Divide requires ≥2 result units and (if volumes are supplied) their sum must not exceed the source's volume.
- Expiration: `ResultExpiresUtc = min(anchor + offset, earliest source ExpiresUtc)` — the expiration modification code supplies the offset and whether the anchor is the modification date/time or the earliest source collection date/time. A result can never outlive its source(s), including the shortest-lived component pooled in. Collection-relative codes hard-stop (`MOD-COLLECTION-REQUIRED`) when a source has no collection timestamp.
- Dangerous action: requires a reason; records `AuditEventType.Modify` in addition to the automatic Create/Update audit on every touched/created row. Gated by `inventory.modify`; the rules table itself is gated by `admin.modification-rules.manage`.

---

## 9. Reaction investigation

```mermaid
flowchart TD
    a([Transfusion event flags ReactionSuspected]) --> b[Open ReactionInvestigation]
    b --> c[Link to transfusion + patient + unit]
    c --> d[Record findings, type, severity]
    d --> e[Status Open -> UnderReview -> Closed]
    e --> f[Disposition recorded + audit]
```

---

## 10. HL7 message flows

```mermaid
flowchart LR
    subgraph inbound [Inbound]
      adt[ADT A01/A04/A08] --> pat[Update Patient/Encounter]
      orm[ORM/OML] --> ord[Create Order]
    end
    subgraph outbound [Outbound]
      ver[Result verified] --> oru[Build + send ORU]
    end
    pat --> ack[Send ACK/NAK]
    ord --> ack
    oru --> log[Log to HL7Messages]
```

- Inbound messages are persisted to `HL7Messages` first, then parsed, then mapped to Application commands (which run the same safety checks as the API). Failures go to `InterfaceErrorQueue` and produce a NAK.
- Detailed mapping, ACK/NAK, retry, and replay are specified in `hl7-design.md`.

---

## 11. Audit and signature touchpoints (summary)

| Workflow | Audit event(s) | E-signature required |
|---|---|---|
| Unit intake/release | Create, Update(status) | No |
| Accessioning/reject | Create, Update(status) | No |
| Result verify | Verify | Per policy |
| Result correction | Correct | Yes |
| Allocation | Update(status) | No |
| Issue (standard) | Issue | No (unless override) |
| Issue (emergency release) | Issue, Override | Yes |
| Warning override | Override | Yes |
| Return | Return, Update(status) | No |
| Discard | Discard, Update(status) | Confirmation + reason |
| Product modification | Modify, Create/Update(status) | Reason |
| ABO/Rh manual edit | Update(blood type history) | Yes |
| P-tag reprint | Reprint | Reason |
