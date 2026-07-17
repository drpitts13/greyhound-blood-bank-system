# Blood Bank LIS

An original, safety-critical Blood Bank Laboratory Information System built on C# / .NET, SQL Server, and EF Core, using a layered, API-first architecture. Accuracy, traceability, auditability, and workflow controls are prioritized over development speed.

This repository is being built in phases. See [`docs/`](docs/) for the full design:
[architecture](docs/architecture.md), [data model](docs/erd.md), [workflows](docs/workflows.md),
[safety rules](docs/safety-rules.md), [HL7](docs/hl7-design.md), [printing & billing](docs/printing-billing.md),
[validation plan](docs/validation-plan.md), [traceability matrix](docs/traceability-matrix.md), [risk register](docs/risk-register.md).

## Status

- Phase 0 — Architecture and planning: complete (`docs/`).
- Phase 1 — Foundation: complete (solution, core entities, DbContext, audit pipeline, initial migration, seed data, tests).
- Phase 2 — Inventory: complete (intake, search, guarded status transitions, location transfers, expiration sweep, discard workflow, append-only status history).
- Phase 3 — Patient / specimen / results: complete (specimen accessioning + rejection with computed expiry, versioned result entry/verification/correction, append-only ABO/Rh blood-type history with delta check, append-only antibody history, manual ABO/Rh override as an audited dangerous action).
- Phase 4 — Compatibility & issuing: complete (ABO/Rh compatibility matrix by component class, serologic + electronic crossmatch with eligibility gating, allocation/reservation, the full issue gate, standard + emergency-release issue with authorized overrides, return-to-inventory with reissue evaluation, transfusion documentation; every state change guarded, history-tracked, and audited).
- Phase 5 — HL7 interface: complete (original in-house v2.x parser/generator that honors message-declared encoding characters, safe location-path accessors, escape/unescape, MLLP framing; inbound ADT demographics upsert and ORM/OML order creation through the same audited use cases; ACK/NAK with `AA`/`AE`/`AR`; persisted message log with parsed status and error queue; control-id + business-key idempotency; message replay; outbound ORU builder for verified results; optional, config-gated MLLP TCP listener).
- Phase 6 — Printing: complete (isolated printing layer with a renderer-agnostic `LabelDocument`, `ILabelRenderer` behind which a `ZplLabelRenderer` — ZPL II with `^FH` hex-escaping of control characters — and a `PreviewLabelRenderer` proof renderer sit; standard specimen-label and compatibility-tag/P-tag templates selected by `TemplateCode`; a `PrintService` that assembles the data model from audited domain records, renders, and records every print to `PrintJobs` with payload + rendered output; reprint as a reason-gated dangerous action that emits a `Reprint` audit event).
- Phase 7 — Billing: complete (event-driven charge capture isolated from clinical decisions: data-driven `ChargeCodes` + `ChargeRules` map triggers — `TestVerified`, `UnitIssued` — to charges; a `BillingService` translates committed clinical actions into `BillingEvents` with a deterministic, unique `DedupeKey` so a repeated trigger can never double-charge; capture is wired into the verify-result and issue endpoints so a blocked/failed action never bills; a review queue with `Pending -> Reviewed -> Exported` flow plus reason-gated, audited `Cancelled`; CPT/export left as documented placeholders).
- Phase 8 — Security & validation: complete (identity model — `Users`/`Roles`/`Permissions`/`RolePermissions`/`UserRoles` — with a stable permission-code catalog and seeded roles `Administrator`/`Supervisor`/`Technologist`/`ReadOnly`; permission-based, default-deny authorization enforced at the API boundary via a `RequirePermission` endpoint filter and a pure `PermissionPolicy` so the same decision applies to every HTTP caller; request-scoped, header-based current-user resolution that still falls back to the system account for startup/seed; append-only `ElectronicSignatures` with a `SignatureService`, and an issue-override path gated on reason + a valid, owner-bound electronic signature; per-workflow validation scripts in `docs/validation-scripts.md`).

## Tech stack

- .NET 10 (LTS) — note: the design docs reference .NET 8; .NET 10 is used because it is the installed LTS SDK and the "LTS for a regulated system" rationale still holds.
- SQL Server (runtime) with EF Core 10 code-first migrations.
- xUnit tests. Integration tests run against in-memory SQLite so they execute anywhere; SQL Server is the deployment/CI target.

## Solution layout

```
src/
  BloodBankLIS.Domain          entities, value objects, enums, pure safety rules
  BloodBankLIS.Application      use-case orchestration, abstractions, DTOs, CRUD services
  BloodBankLIS.Infrastructure   EF Core DbContext, configs, migrations, repositories, audit pipeline, seed
  BloodBankLIS.Security         permission evaluation + electronic signatures (persistence-agnostic)
  BloodBankLIS.HL7              in-house v2.x parser/generator, ADT/ORM/ORU mapping, ACK/NAK, replay
  BloodBankLIS.Printing         ZPL + preview renderers, label/P-tag templates, audited PrintService
  BloodBankLIS.Api              ASP.NET Core Web API boundary + composition root
tests/
  BloodBankLIS.Domain.Tests        pure rule tests
  BloodBankLIS.Application.Tests   service orchestration tests (no infrastructure)
  BloodBankLIS.HL7.Tests           parser/encoding/ACK/MLLP/mapper tests
  BloodBankLIS.Printing.Tests      ZPL renderer + template tests
  BloodBankLIS.Integration.Tests   DbContext + audit pipeline tests (SQLite)
```

## Build and test

```bash
dotnet build
dotnet test
```

## Run the API

The API reads the connection string `ConnectionStrings:BloodBankLIS` (defaults to SQL Server Express on `localhost\SQLEXPRESS02`). In Development it will apply migrations and seed demo data when a database is reachable (controlled by `Database:AutoMigrate`). Integration tests continue to use in-memory SQLite independently.

```bash
dotnet run --project src/BloodBankLIS.Api
```

Endpoints:
- Patients: `GET/POST/PUT /api/patients`
- Inventory: `GET /api/inventory/units` (search), `GET /api/inventory/units/{id}`, `GET /api/inventory/units/{id}/history`, `POST /api/inventory/units` (intake), `POST /api/inventory/units/{id}/release`, `POST /api/inventory/units/{id}/transfer`, `POST /api/inventory/units/{id}/discard`, `POST /api/inventory/expire-due`
- Specimens: `POST /api/specimens` (accession), `GET /api/specimens/{id}`, `POST /api/specimens/{id}/reject`, `GET /api/patients/{patientId}/specimens`
- Results: `POST /api/results` (enter), `POST /api/results/abo-rh`, `GET /api/results/{id}`, `POST /api/results/{id}/verify`, `POST /api/results/{id}/correct`, `GET /api/specimens/{id}/results`
- Immunohematology: `GET /api/patients/{patientId}/blood-type` (current), `GET /api/patients/{patientId}/blood-type/history`, `POST /api/patients/{patientId}/blood-type` (manual override), `GET /api/patients/{patientId}/antibodies`, `GET /api/patients/{patientId}/antibodies/history`, `POST /api/patients/{patientId}/antibodies`, `POST /api/antibodies/{id}/deactivate`
- Compatibility: `POST /api/crossmatches`, `POST /api/allocations`, `POST /api/allocations/{id}/release`
- Issuing: `POST /api/issues` (runs the full issue gate), `POST /api/issues/{id}/return`, `POST /api/issues/{id}/transfusion`
- HL7: `POST /api/hl7/inbound` (accepts a raw `text/plain` HL7 v2.x message, returns the ACK/NAK; `200` on `AA`, `422` on `AE`/`AR`), `GET /api/hl7/messages`, `GET /api/hl7/messages/{id}` (includes raw text), `POST /api/hl7/messages/{id}/replay`, `GET /api/hl7/errors`, `POST /api/hl7/outbound/results/{resultId}` (queue an ORU for a verified result)
- Printing: `POST /api/print/specimen-labels/{specimenId}`, `POST /api/print/compatibility-tags/{issueId}` (build the P-tag from the issue record), `POST /api/print/jobs/{id}/reprint` (requires `{ reason }`), `GET /api/print/jobs`, `GET /api/print/jobs/{id}` (includes rendered output). Each accepts an optional `{ format, templateCode, targetPrinter }` body; `format` is `Zpl` (default) or `Preview`.
- Billing: `GET /api/billing/charges` (review queue), `POST /api/billing/charges/{id}/review`, `POST /api/billing/charges/{id}/cancel` (requires `{ reason }`), `POST /api/billing/charges/{id}/export`, `POST /api/billing/capture/result/{resultId}`, `POST /api/billing/capture/issue/{issueId}` (manual, idempotent recapture). Verifying a result and issuing a unit automatically capture charges after they commit.
- Signatures: `POST /api/signatures` records an append-only electronic signature for the current user (`{ action, meaningOfSignature, contextType?, contextId? }`) and returns its id.
- Audit: read-only `GET /api/audit-events`

### Authorization

Authorization is permission-based and default-deny, enforced at the API boundary. Every request must carry an identity header `X-User: <username>` (and optionally `X-Workstation`); requests with no identity get `401`, and an authenticated user lacking the required permission gets `403`. In a real deployment the gateway terminates authentication (OIDC/Windows/smartcard) and forwards the verified identity — the LIS does not trust a self-asserted header from an untrusted client. The permission catalog lives in `PermissionCodes`; seeded demo accounts are `admin` (Administrator), `supervisor`, `tech1` (Technologist), and `viewer` (ReadOnly).

Issue overrides additionally require an electronic signature: record one via `POST /api/signatures` with `action: "IssueOverride"`, then re-issue with the returned id in the `X-Esignature-Id` header. An override without a reason and a valid, owner-bound signature is rejected.

Safety-gated endpoints return `422 Unprocessable Entity` with `{ blocked, overridable, hardStops[], warnings[] }` when the issue/compatibility gate blocks an action; a Warning-only block is `overridable` via an authorized override (reason + authorizer + electronic signature).

An inbound MLLP TCP listener is available as a hosted service but is disabled by default; enable it with `Hl7:Mllp:Enabled=true` (port via `Hl7:Mllp:Port`, default `2575`). The HTTP `POST /api/hl7/inbound` endpoint exercises the same processing pipeline without binding a socket.

## Database migrations

The EF Core CLI is pinned as a local tool (`dotnet-tools.json`).

```bash
# apply migrations to the configured database
dotnet ef database update --project src/BloodBankLIS.Infrastructure --startup-project src/BloodBankLIS.Infrastructure

# add a new migration
dotnet ef migrations add <Name> --project src/BloodBankLIS.Infrastructure --startup-project src/BloodBankLIS.Infrastructure --output-dir Persistence/Migrations
```

## Migrate from SQLite to SQL Server Express

Development previously used SQLite at `%LOCALAPPDATA%\BloodBankLIS\bloodbank.dev.db`. To copy that data into SQL Server Express:

1. Ensure the `MSSQL$SQLEXPRESS02` service is running.
2. Stop the API so the SQLite file is not locked.
3. Run the migrator:

```powershell
.\scripts\database\Migrate-SqliteToSqlServer.ps1
```

Or directly:

```bash
dotnet run --project src/BloodBankLIS.DbMigrator
```

The tool applies EF migrations to SQL Server, copies all tables in FK-safe order (preserving IDs, omitting `RowVersion`), and aborts if the target database already contains data.

4. Start the API — startup runs `MigrateAsync()` plus the idempotent seeder (which skips tables that already have rows).
5. Verify patients, inventory, and orders in the UI or with:

```sql
SELECT COUNT(*) FROM Patients;
SELECT COUNT(*) FROM BloodProducts;
SELECT COUNT(*) FROM Orders;
```

6. Optionally archive or delete `bloodbank.dev.db` after verifying SQL Server data.

**Schema only** (empty or missing SQLite file):

```powershell
$env:BLOODBANK_CONNECTION = "Server=localhost\SQLEXPRESS02;Database=BloodBankLIS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet tool restore
dotnet ef database update --project src/BloodBankLIS.Infrastructure --startup-project src/BloodBankLIS.Infrastructure
```
