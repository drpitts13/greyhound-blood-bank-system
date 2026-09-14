# Current state assessment

Status: living assessment as of the Cycle 1 identity-hardening loop.
This product is **compliance-supporting and validation-ready**. It is **not**
FDA-cleared, AABB-accredited, or CAP-certified by virtue of existing in this
repository.

## Product

Greyhound Blood Bank LIS is a hospital transfusion-service laboratory
information system: patients, specimens, immunohematology, compatibility,
inventory, issue, transfusion, lookback, reactions, and quality deviations.
It is **not** a blood-collection establishment and does not implement donor
eligibility under 21 CFR 630/640.

## Architecture

Layered .NET 10 solution. Dependencies point inward.

| Project | Role |
|---|---|
| `BloodBankLIS.Domain` | Entities, value objects, pure safety rules |
| `BloodBankLIS.Application` | Use-case orchestration, DTOs, authorization at the service |
| `BloodBankLIS.Infrastructure` | EF Core, SQL Server / SQLite, audit pipeline, seed |
| `BloodBankLIS.Security` | Permission evaluation, electronic signatures |
| `BloodBankLIS.HL7` | In-house v2.x ADT/ORM/ORU, ACK/NAK, MLLP |
| `BloodBankLIS.Printing` | ZPL and preview labels / P-tags |
| `BloodBankLIS.Api` | HTTP boundary, default-deny permission filters |
| `BloodBankLIS.Web` | Blazor Server clinical and admin UI |

Safety rules are pure functions that return `RuleResult` (`HardStop` /
`Warning` / `Pass`) with a stable code and explainable message. Application
services assemble facts. The UI does not decide compatibility.

## Identifiers

Distinct identifiers exist for patients (MRN, enterprise/historical ids,
encounters), specimens (accession, collection metadata, status), orders,
products (facility unit number plus ISBT DIN / product code / expiration),
results (versioned, sourced, verified), and transfusions (issue, ward
receipt, documentation, disposition). Display names are not primary keys.

## Safety-critical controls already present

- `IssueGate` — identity, specimen, ABO/Rh, allocation, XM, special
  requirements, appearance, autologous/directed, merged patient, second ABO
- `ElectronicCrossmatchEligibilityService` — eligible plus reasons; not a checkbox
- Emergency / MTP as a distinct issue type with override, signature, and
  retrospective XM — not “ignore all rules”
- Antibody history is append-only; deactivated rows still block electronic XM
- Antibody-identification workups: assist never Identifies; supervisor review
- Inventory allow-listed state machine + filtered unique indexes on reserve/issue
- Versioned result correction / invalidation
- Named append-only audit in the same save transaction, with a SHA-256 hash
  chain on new rows (pre-chain rows not rewritten; no purge, OCD-007)
- Application-layer permission HardStops in addition to API filters

Authoritative rule catalog: [`docs/safety-rules.md`](safety-rules.md).

## Authentication (Cycle 1)

`POST /api/auth/login` verifies the password hash (fail-closed if missing) and
issues a session token once. Subsequent API calls send `Authorization: Bearer`.
`HttpCurrentUser` reads only the middleware-resolved session. A self-asserted
`X-User` header is ignored unless `Auth:AllowLegacyIdentityHeader` is explicitly
enabled for a test host (forced off in Production). Logout, lock, deactivate,
idle timeout (30 minutes), absolute expiry (12 hours), and five failed sign-ins
revoke or reject the session. Application permission HardStops still run after
authentication. Development-only `DevMode` still resolves unauthenticated
callers as a configured admin and hard-fails outside Development.

## Tests

Approximately 1,966 automated tests across Domain, Application, HL7, Printing,
and Integration (SQLite). Permanent suite: `tests/safety_regression/`
(includes `IdentitySpoofingRegressionTests`). Formal `TEST-BB-*` evidence IDs
are not yet assigned; tests are referenced by class name. There is no
Blazor/UI E2E suite.

## Documentation that already exists

User / functional / design requirements, living risk register
(`docs/risk/RISK_REGISTER.md`), 32 open clinical decisions
(`docs/OPEN_CLINICAL_DECISIONS.md`), downtime plan, workflows, ERD, HL7, ISBT
module, validation plan/test plan/change control/release checklist.

## Regulatory posture

Regulations and facility interpretations vary. Items that depend on an
external standard not present in this repository are labeled
**REQUIRES REGULATORY / SME VERIFICATION** in
[`docs/OPEN_CLINICAL_DECISIONS.md`](OPEN_CLINICAL_DECISIONS.md).
ISBT lookup tables are placeholders pending an ICCBBA license (OCD-004).
