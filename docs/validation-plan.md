# Blood Bank LIS — Validation and Testing Strategy

Status: Phase 0 (design). This plan defines how the system is validated. As a safety-critical healthcare application, the highest-priority tests cover the pure safety rules, which run with no infrastructure and can be exhaustively enumerated.

## 1. Test layers

| Layer | Project | Scope | Infrastructure |
|---|---|---|---|
| Domain (rules) | `BloodBankLIS.Domain.Tests` | Compatibility matrix, specimen expiration, status transitions, rule-outcome aggregation | None (pure, injected clock) |
| Application | `BloodBankLIS.Application.Tests` | Use cases enforce permissions and write audit; billing triggers fire correctly | In-memory fakes for repos/clock/audit |
| HL7 | `BloodBankLIS.HL7.Tests` | Parse/serialize round-trip, ADT/ORM/ORU mapping, ACK/NAK building | None |
| Integration | `BloodBankLIS.Integration.Tests` | EF Core migrations, constraints, indexes, append-only behavior, dedupe uniqueness | SQL Server (LocalDB / Testcontainers) |

Framework: xUnit. Deterministic time via injected `IClock`. No test depends on wall-clock time.

## 2. Priority test cases (must exist before the related feature ships)

### 2.1 Compatibility logic
- ABO RBC matrix: all recipient/donor pairs return the correct Pass/HardStop.
- Rh(D): Rh-positive RBC to Rh-negative recipient is HardStop (outside emergency policy).
- Plasma direction differs from RBC and is tested separately.
- Special requirement checks: irradiated/CMV-neg/leukoreduced/washed each enforced.
- Antigen-negative: a known anti-K requires K-negative units (HardStop otherwise).

### 2.2 Specimen expiration
- Specimen at exactly `ExpiresUtc` and one tick past: not-expired vs expired boundaries.
- Near-expiry window raises a Warning, not a HardStop.
- Recompute on input change is audited.

### 2.3 Inventory status transitions
- Every allowed transition succeeds; every disallowed transition is a HardStop.
- Expired unit cannot move to Allocated/Issued.
- Each transition writes `InventoryStatusHistory` + `AuditEvent`.

### 2.4 Billing triggers
- TestVerified and UnitIssued each create exactly one `BillingEvent`.
- A duplicate trigger does not create a second event (unique `DedupeKey`).
- Cancellation requires a reason and is audited.

### 2.5 HL7 parsing
- Round-trip: parse then serialize reproduces an equivalent message honoring `MSH-1/2` encoding chars.
- Missing optional fields return empty (no exception).
- Inbound ADT updates demographics without touching immunohematology history.
- Malformed message -> NAK (AR); application error -> NAK (AE) + error-queue entry.

### 2.6 Audit and override
- Every create/update/verify/issue/return/discard/override/reprint use case writes an `AuditEvent`.
- A blocked HardStop on issue writes an audited attempt and does not change unit status.
- Override path requires reason + signature; absence blocks the action.

## 3. Seed / demo data

Provided by `BloodBankLIS.Infrastructure` seeding for demo and workflow validation:
- Users + roles + permissions (admin, technologist, read-only).
- Demo patients including one with an antibody (anti-K) and special requirements.
- Inventory units across ABO/Rh and product types, including near-expiry and quarantined examples.
- Sample orders, specimens, and verified results.

## 4. Validation scripts (Phase 8) — Implemented

Implemented in Phase 8. For each primary workflow in `workflows.md`, a documented
step-by-step validation script with expected results and pass/fail criteria lives in
[`validation-scripts.md`](validation-scripts.md): intake, accessioning,
result/verify/correct, crossmatch/allocate/issue/transfuse, emergency release, return,
discard, HL7 inbound/outbound, label/P-tag print + reprint, billing review, and
security/authorization.

## 5. Definition of done (per business rule)

1. Pure rule implemented in Domain with stable code + severity.
2. Positive and negative unit tests added and passing.
3. Use case wires the rule and writes audit.
4. Traceability matrix updated (`traceability-matrix.md`).
5. Build + tests green.
