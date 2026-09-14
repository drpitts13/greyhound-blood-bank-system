# Iteration log

Append-only record of autonomous improvement cycles. Do not delete history.

## Iteration 1 — Identity hardening (2026-09-05)

Selected residual: API identity spoofing (gap 1). After password login the API
trusted a client-supplied `X-User` header, and an empty `PasswordHash` signed in.
That path could impersonate `supervisor` for emergency release, override, merge,
or result correction.

### Implemented

- `AuthSession` entity (token hash only; issued / last-activity / absolute / idle /
  revoke). Append-only revoke; never store the raw token.
- `AuthSessionService` login fail-closed; idle 30 minutes; absolute 12 hours;
  logout, lock, deactivate, and five failed sign-ins revoke sessions.
- `AuthSessionMiddleware` + `RequestIdentityResolver`: `Authorization: Bearer`
  wins. `X-User` ignored unless `Auth:AllowLegacyIdentityHeader` (default off;
  Production forced off).
- Web `UserSession` holds the token in circuit memory; `IdentityHeaderHandler`
  sends Bearer and no longer sends `X-User` as identity.
- Assessment set: `docs/CURRENT_STATE_ASSESSMENT.md`,
  `docs/BLOOD_BANK_GAP_ANALYSIS.md`, `docs/SAFETY_ARCHITECTURE.md`,
  `docs/validation/TRACEABILITY_MATRIX.md`.

### Requirements / risk

- URS-BB-147, FRS-BB-181, SRS-BB-142, RISK-BB-254.

### Tests

- `IdentitySpoofingRegressionTests`, `AuthSessionServiceTests`,
  `AuthSessionValidityRuleTests`.
- Full suite green: Domain 1178, Application 20, HL7 48, Printing 11,
  Integration 692 (1949). Formal `TEST-BB-*` IDs still not assigned.

### Red-team (this slice)

- Spoofed `X-User: supervisor` without a session is anonymous.
- Valid session is not overridden by a spoofed header.
- Wrong password, missing hash, locked user: no session.
- Idle, absolute expiry, logout, admin lock, and lockout reject the token.
- `ISS-EMERG-PERM` and other Application HardStops unchanged.

### Next ranked non-SME residual

Re-score at the start of Iteration 2. Leading candidates: HL7 inbound
authentication (gap 5) or stale circuit permissions after role change (gap 6).
Do not invent ICCBBA tables (OCD-004) or close OCDs without SME.

## Iteration 2 — Privilege-change session revoke (2026-09-05)

Re-score: API `GetPermissionsAsync` / `RequirePermission` already read the
directory on each request, so gap 6 was smaller than Cycle 1 assumed. HTTP
`POST /api/hl7/inbound` already requires a session and `hl7.manage`. MLLP and
file-drop remain transport-trust; inventing an interface credential model
needs SME. Selected gap 6: revoke sessions when roles or role permissions
change, and refresh the circuit from `/api/me`.

### Implemented

- `UserAdminService` revokes outstanding sessions after role assign, user
  update with a role list, and role-permission update (all members of that role).
- `UserSession.TryApplyLiveProfile` plus `MainLayout` `/api/me` refresh
  (15 s throttle); 401/404 signs the circuit out.

### Requirements / risk

- URS-BB-148, FRS-BB-182, SRS-BB-143, RISK-BB-255.

### Tests

- `AuthSessionServiceTests.AssignRoles_RevokesOutstandingSessions`
- `AuthSessionServiceTests.UpdateRolePermissions_RevokesSessionsForRoleMembers`

### Next ranked non-SME residual

HL7 MLLP/file-drop inbound authentication remains transport-trust (gap 5) —
do not invent a facility interface credential. Next implementable candidates:
concurrent antibody-history vs XM race coverage (gap 7) or audit hash chain
(gap 8). Do not invent ICCBBA tables (OCD-004).

## Iteration 3 — Electronic XM history re-read (2026-09-05)

Re-score: gap 7 is the next non-SME patient-safety item (computer XM after a
late antibody post). Gap 8 (audit hash chain) is integrity-only and does not
block a wrong-blood path. Selected gap 7.

### Implemented

- `RecordCrossmatchAsync` evaluates electronic eligibility twice (start and
  immediately before insert).
- History criterion uses `HasAntibodyHistoryAsync` (all rows, including
  deactivated), not the broader complex-XM flag.

### Requirements / risk

- URS-BB-149, FRS-BB-183, SRS-BB-144, RISK-BB-256.

### Tests

- `ElectronicXmHistoryRegressionTests.AntibodyAddedAfterEligibleAssess_BlocksElectronicXmRecord`
- Existing `Phase4IssuingTests.ElectronicCrossmatch_WithAntibodyHistory_IsBlocked`

### Next ranked non-SME residual

Audit hash chain (gap 8) or leave SME-blocked items (ISBT catalogs, MLLP
credentials, OCD defaults). Do not invent ICCBBA tables or close OCDs.

## Iteration 4 — Audit hash chain (2026-09-05)

Re-score: remaining higher ranks are SME-blocked (ISBT catalogs, phenotype
in-place, merge second authorizer, MLLP credentials). Gap 8 is the next
non-SME residual. Hash linking is integrity-only and does not set retention
years or implement purge (OCD-007 stays open).

### Implemented

- `AuditEvent.PreviousHash` / `RecordHash` (SHA-256 hex). First hashed row
  uses `GENESIS`. Pre-chain rows stay null and are not rewritten.
- `BloodBankDbContext` stamps hashes on every added audit row immediately
  before persist (named `AuditWriter` and interceptor Create/Update/Delete).
- Unique filtered indexes on both hash columns; concurrent tip collision
  retries the stamp.
- `AuditHashChainRule.Verify` detects a broken link or a payload that no
  longer matches `RecordHash`.
- Migration `20260905211400_AuditHashChain` adds nullable columns only.

### Requirements / risk

- URS-BB-150, FRS-BB-184, SRS-BB-145, RISK-BB-257.

### Tests

- `AuditHashChainRuleTests`
- `AuditHashChainTests`
- Full suite green: Domain 1184, Application 20, HL7 48, Printing 11,
  Integration 699 (1962). Formal `TEST-BB-*` IDs still not assigned.

### Next ranked non-SME residual

Configuration effective-dating (gap 9) or leave SME-blocked items. Do not
invent effective dates, ICCBBA tables, MLLP credentials, or a retention
purge (OCD-007).

## Iteration 5 — Stop (2026-09-05)

Re-score: remaining higher ranks are SME-blocked (ISBT catalogs, phenotype
in-place, merge second authorizer, MLLP credentials). Gap 9 (catalog
effective-dating) is the next non-SME-looking item, but completing it would
invent policy.

### Why this cycle did not implement

`VersionedConfigEntity` already models `Version`, `EffectiveUtc`, `RetiredUtc`,
draft/active, and `ConfigurationChangeHistory`. Activate already stamps
`EffectiveUtc` to now when empty. Clinical reads already use
`IsActive && !IsDraft`. The row is documented as the single live version.

Closing FRS-BB-070 further would require choosing whether:

- a future `EffectiveUtc` delays go-live,
- `null` EffectiveUtc means live or not-yet,
- an edit creates a new dated row versus the current in-place version bump,
- issue/result time must reconstruct the catalog as-of that instant.

Those are facility/SOP questions (OCD-033). Special-requirement windows
already have their own dated filter and were not changed. Purge remains
unimplemented (OCD-007).

### Stop

The continuous-improvement loop stops here. Remaining residuals need SME,
a license, or an invented dating/retention policy. Do not re-arm until a
human picks the next item.

## Iteration 6 — Bedside identity tokens (2026-09-14)

Re-score after human re-arm: remaining higher ranks are still SME-blocked
(ISBT catalogs, phenotype in-place, merge second authorizer, MLLP credentials,
catalog as-of dating). Gap 15 is the highest remaining P1 that can be
tightened without inventing policy. Issue already required two matching
identity tokens; transfusion documentation accepted a PPID checkbox plus any
unit scan as electronic dual-ID.

### Implemented

- `DocumentTransfusionRequest` carries the same two patient identity tokens
  as issue. The boolean `PositivePatientIdentification` flag is removed.
- `IssuingService.DocumentTransfusionAsync` evaluates `PatientIdentityMatchRule`
  before scan, ward-receipt, or dual-ID. Missing or mismatched tokens are
  `ISS-IDENTITY` HardStops.
- Electronic `TX-DUAL-ID` is complete only when identity matches and an ISBT
  bedside unit scan verifies. Legacy units without `ComponentIdentity` do not
  invent a new scan requirement. `RequireSecondVerifier` and directory
  second-user checks are unchanged (OCD-008).
- `/issuing` collects MRN and DOB; the PPID checkbox is gone.
- Interface BPAM documentation is unchanged.

### Requirements / risk

- URS-BB-151, FRS-BB-185, SRS-BB-146, RISK-BB-258.

### Tests

- `Phase4IssuingTests.Transfusion_MissingPatientIdentifiers_IsHardStopped`
- `Phase4IssuingTests.Transfusion_MismatchedPatientIdentifiers_IsHardStopped`
- `Phase4IssuingTests.Transfusion_IsbtUnit_RequiresIdentityAndMatchingScan`
- `Phase4IssuingTests.Transfusion_RequireSecondVerifier_WithoutElectronicId_NeedsDirectoryUser`
- Existing transfusion / ward-receipt tests now supply matching tokens.
- Full suite green: Domain 1184, Application 20, HL7 48, Printing 11,
  Integration 703 (1966). Formal `TEST-BB-*` IDs still not assigned.

### Red-team (this slice)

- Empty tokens cannot document a transfusion.
- Wrong MRN cannot document a transfusion.
- A checkbox no longer exists on the request or UI.
- ISBT units still require a matching bedside scan after identity.
- When `RequireSecondVerifier` is on and electronic ID is incomplete (legacy
  unit, no verified scan), a directory second user is required.

### Next ranked residual

SME / license items stay in the loop. Ask the decision in conversation
before implementing. Leading SME questions: ICCBBA tables (OCD-004),
merge second authorizer (OCD-010), MLLP credentials (gap 5). Leading
non-SME candidates if no answer yet: CSRF/XSS/secrets (gap 18),
`TEST-BB-*` packaging (gap 19), downtime reconciliation (gap 16). Do not
invent tables, credentials, or dating/retention policy. Administration-
device protocol and required vitals stay out of scope (gap 15 residual).

## Iteration 7 — Licensed ISBT catalog import (2026-09-14)

Re-score after SME answers: OCD-010 stays closed (no second merge
authorizer). OCD-004: the facility will obtain an ICCBBA license and
supply official tables. Highest remaining implementable residual is the
import path itself — do not invent product or ABO/RhD codes.

### Implemented

- `IsbtLicensedCatalogImportRule` HardStops missing `admin.config.edit`,
  missing license acknowledgment, placeholder / pending-ICCBBA versions,
  and an empty payload.
- `IsbtProductCodeAdminService.ImportLicensedAsync` upserts licensee
  rows, replaces matching placeholders, marks them not-placeholder, and
  writes `AuditEventType.Import`.
- `POST /api/admin/isbt-product-codes/import-licensed` (`admin.config.edit`).
- `/admin/isbt-product-codes` accepts version, acknowledgment, and
  licensee JSON. No ICCBBA tables are shipped in source.
- OCD-004 remains open until a licensed extract is loaded and validated.

### Requirements / risk

- URS-BB-152, FRS-BB-186, SRS-BB-147, RISK-BB-259 (RISK-BB-014 residual).

### Tests

- `IsbtLicensedCatalogImportRuleTests`
- `IsbtLicensedCatalogImportTests`
- Full suite green: Domain 1190, Application 20, HL7 48, Printing 11,
  Integration 707 (1976). Formal `TEST-BB-*` IDs still not assigned.

### Red-team (this slice)

- No acknowledgment: no rows written.
- `admin.config.view` only: `ISBT-IMPORT-PERM`.
- `PLACEHOLDER` / `PENDING-ICCBBA` versions rejected.
- Empty JSON rejected.
- Test fixtures use clearly fake `T0001` / `T1` codes, not ICCBBA tables.

### Next ranked residual

Ask in conversation: gap 5 MLLP/file-drop credentials; OCD-033 catalog
as-of; OCD-007 purge; gap 12 neonatal defaults; OCD-001/006 eXM;
OCD-022 phenotype versioning; OCD-008 dual-ID policy. Do not claim a
licensed extract is loaded. Leading non-SME if no answer: gap 18
CSRF/XSS/secrets, gap 19 `TEST-BB-*` packaging, gap 16 downtime
reconciliation.

## Iteration 8 — HL7 transport-trust recorded (2026-09-14)

SME answer for gap 5: keep MLLP and file-drop as transport-trust. Do not
invent a shared interface credential. HTTP inbound stays session +
`hl7.manage`.

### Implemented

- Documented the split in `docs/hl7-design.md` and listener remarks
  (`MllpListenerService`, `Hl7FileDropService`, `Hl7Endpoints`).
- Closed OCD-034 and gap 5. No secret or mTLS stack was added.

### Requirements / risk

- URS-BB-153, FRS-BB-187, SRS-BB-148, RISK-BB-260 (accepted residual).

### Tests

- Existing `Hl7EndpointAuthorizationTests` still cover endpoint admin
  privilege. No new credential path to test. Full suite was already green
  at Cycle 7 (1976); this slice is documentation of the decided residual.

### Next ranked residual

Ask in conversation: OCD-033 catalog as-of; OCD-007 purge; gap 12
neonatal defaults; OCD-001/006 eXM; OCD-022 phenotype versioning;
OCD-008 dual-ID policy. OCD-004 remains open until a licensed extract is
loaded. Leading non-SME: gap 18 CSRF/XSS/secrets, gap 19 `TEST-BB-*`
packaging, gap 16 downtime reconciliation.

## Iteration 9 — Catalog live-row + security headers (2026-09-14)

SME answer for OCD-033: keep the single live catalog row. Do not invent
as-of dating. Highest remaining non-SME residual is gap 18 (browser
headers / CORS). Identity is already Bearer in circuit memory.

### Implemented

- Closed OCD-033 / gap 9. `VersionedConfigEntity` stays the single live
  row. No as-of selector, scheduled activation, or overlapping versions.
- `HttpSecurityHeaderPolicy` plus API/Web `SecurityHeadersMiddleware`
  (nosniff, DENY framing, CSP, Permissions-Policy; API `Cache-Control:
  no-store`).
- `Cors:AllowAnyOrigin` HardStops outside Development (`SEC-CORS-ANY`).
  Development lists localhost Web origins. No cookie session was added.

### Requirements / risk

- URS-BB-154, FRS-BB-188, SRS-BB-149, RISK-BB-261. FRS-BB-070 notes
  OCD-033.

### Tests

- `HttpSecurityHeaderPolicyTests`
- Full suite green: Domain 1195, Application 20, HL7 48, Printing 11,
  Integration 707 (1981). Formal `TEST-BB-*` IDs still not assigned.

### Next ranked residual

Ask in conversation: OCD-007 purge; gap 12 neonatal defaults;
OCD-001/006 eXM; OCD-022 phenotype versioning; OCD-008 dual-ID.
OCD-004 remains open until a licensed extract is loaded. Leading
non-SME: gap 19 `TEST-BB-*` packaging, gap 16 downtime reconciliation.
Blazor CSP inline/eval remains an accepted residual of gap 18.
