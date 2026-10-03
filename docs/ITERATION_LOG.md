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

## Iteration 10 — TEST-BB catalog and no-purge (2026-09-14)

SME answer for OCD-007: do not purge clinical or audit rows. Retention
years stay metadata. Highest remaining non-SME residual is gap 19
(validation packaging).

### Implemented

- Closed OCD-007 / gap 10. No purge job was added.
- `docs/validation/TEST_CATALOG.md` assigns TEST-BB-001–017.
- `TestCatalogPackagingTests` asserts those table IDs are unique.
- Living headers on architecture, safety-rules, workflows, ERD, and
  validation-plan no longer say unimplemented Phase 0.

### Requirements / risk

- URS-BB-155, FRS-BB-189, SRS-BB-150, RISK-BB-262. FRS-BB-184 notes
  OCD-007.

### Tests

- TEST-BB-017 `TestCatalogPackagingTests.FormalIds_AreUnique`
- Full suite green: Domain 1195, Application 20, HL7 48, Printing 11,
  Integration 708 (1982). Remaining tests stay class-name cited.

### Next ranked residual

Ask in conversation: gap 12 neonatal defaults; OCD-001/006 eXM;
OCD-022 phenotype versioning; OCD-008 dual-ID. OCD-004 remains open
until a licensed extract is loaded. Leading non-SME: gap 16 downtime
reconciliation. Remaining tests stay class-name cited until assigned.

## Iteration 11 — Downtime snapshot + neonatal hooks kept (2026-09-14)

SME answer for gap 12: keep current order-rule hooks. Do not invent
neonatal irradiation or CMV product-selection defaults or age cutoffs.
Highest remaining non-SME residual is gap 16 (downtime reconciliation).

### Implemented

- Closed OCD-035 / gap 12. No neonatal product-default catalog was added.
- `DowntimeReconciliationAuthorizationRule` HardStops without `audit.read`
  (`DT-RECON-PERM`).
- `DowntimeReconciliationService` returns a read-only snapshot: unresolved
  interface errors, pending outbound HL7, open issues, pending
  retrospective XM, hashed/unhashed audit rows, and
  `AuditHashChainRule.Verify`.
- `GET /api/compliance/downtime-reconciliation` and `/downtime`.
- `docs/DOWNTIME_PLAN.md` points at the snapshot. No paper OCR, failover,
  or purge job was added.

### Requirements / risk

- URS-BB-156, FRS-BB-190, SRS-BB-151, RISK-BB-263. FRS-BB-090 notes the
  snapshot. OCD-035 / URS-BB-014.

### Tests

- TEST-BB-018 `DowntimeReconciliationAuthorizationRuleTests`
- TEST-BB-019 `DowntimeReconciliationTests`
- Full suite green: Domain 1197, Application 20, HL7 48, Printing 11,
  Integration 710 (1986).

### Red-team (this slice)

- `patient.write` only: `DT-RECON-PERM`.
- Resolved interface errors and acked outbound HL7 are not counted.
- Snapshot does not write or purge rows.
- No invented ICCBBA, eXM, phenotype, or dual-ID defaults.

### Next ranked residual

Ask in conversation: OCD-001/006 eXM policy defaults; OCD-022 antigen
phenotype versioning; OCD-008 dual-ID policy defaults. OCD-004 remains
open until a licensed extract is loaded. Leading non-SME: leftover
TEST-BB class-name citations, quality metrics (gap 13), FHIR (gap 14).
RhIG (gap 11) stays SME-blocked.

## Iteration 12 — Core safety TEST-BB IDs (2026-09-14)

No SME answer yet on OCD-001/006 eXM defaults. Highest remaining
non-SME residual is leftover TEST-BB class-name citations for
high-safety controls.

### Implemented

- Assigned TEST-BB-020–024 to issue-gate, merged-patient, open-workup
  electronic XM, and lookback tests already in the matrix.
- `TestCatalogPackagingTests.CitedClasses_HaveSourceFiles` fails if a
  catalog class is missing under `tests/`.
- No clinical default, ICCBBA table, phenotype versioning, or dual-ID
  policy was changed.

### Requirements / risk

- URS-BB-157, FRS-BB-191, SRS-BB-152, RISK-BB-264.

### Tests

- TEST-BB-017 and TEST-BB-025 packaging.
- Full suite green: Domain 1197, Application 20, HL7 48, Printing 11,
  Integration 711 (1987).

### Next ranked residual

Ask in conversation: OCD-001/006 eXM policy defaults; OCD-022 antigen
phenotype versioning; OCD-008 dual-ID policy defaults. OCD-004 remains
open until a licensed extract is loaded. Leading non-SME: quality
metrics (gap 13), FHIR (gap 14), remaining class-name citations.
RhIG (gap 11) stays SME-blocked.

## Iteration 13 — More P0/P1 TEST-BB IDs (2026-09-14)

No SME answer yet on OCD-001/006 eXM defaults. Did not invent quality
metrics (gap 13) or FHIR (gap 14). Next leftover non-SME is more
class-name citations for P0/P1 uniqueness, merge, retype, and privilege
gates.

### Implemented

- Assigned TEST-BB-026–030 to allocation/issue uniqueness, patient
  merge, unit retype, emergency-issue privilege, and immuno privilege.
- No clinical default, QI definition, ICCBBA table, or FHIR profile
  was added.

### Requirements / risk

- URS-BB-158, FRS-BB-192, SRS-BB-153, RISK-BB-265.

### Tests

- TEST-BB-025 packaging still covers the new catalog rows.
- Full suite green: Domain 1197, Application 20, HL7 48, Printing 11,
  Integration 711 (1987).

### Next ranked residual

Ask in conversation: OCD-001/006 eXM policy defaults; OCD-022 antigen
phenotype versioning; OCD-008 dual-ID policy defaults. OCD-004 remains
open until a licensed extract is loaded. Leading non-SME: quality
metrics only as existing-queue counts (do not invent AABB QI), FHIR
(gap 14), remaining class-name citations. RhIG (gap 11) stays
SME-blocked.

## Iteration 14 — ICCBBA extract import if available (2026-09-14)

OCD-004 remains open: do not invent ICCBBA tables and do not claim a
licensed extract is loaded. The implementable residual was native
extract files when a facility has exported them.

### Implemented

- `IccbbaExtractParser` reads JSON/CSV/TSV with documented header aliases
  and rejects Access/Excel.
- Drop folder `Iccbba:ExtractDirectory` (default `testdata/isbt128/extracts`)
  lists available files; empty folder is a no-op. Admin upload is
  multipart. Both reuse `IsbtLicensedCatalogImportRule`.
- `/admin/isbt-product-codes` lists detected extracts, uploads files,
  and keeps JSON paste.
- Seeder no longer reverts licensed non-placeholder product rows.
  `IsbtLookupCatalog` prefers licensed then non-retired rows.

### Requirements / risk

- URS-BB-159, FRS-BB-193, SRS-BB-154, RISK-BB-266 (RISK-BB-014 / 259 residual).

### Tests

- TEST-BB-031 (`IccbbaExtractParserTests`), TEST-BB-032 (`IccbbaExtractImportTests`),
  TEST-BB-033 (`SeederTests.Seed_DoesNotRevertLicensedProductCode`).
- Application 34 (was 20). Integration extract/seeder/import/packaging tests green.
  Domain `CrossmatchRuleTests.ElectronicEligibility_Criteria_ReportEachCheck` and
  two special-requirement integration tests failed in this run; they are outside
  this slice.

### Next ranked residual

Ask in conversation: OCD-001/006 eXM policy defaults; OCD-022 antigen
phenotype versioning; OCD-008 dual-ID policy defaults. OCD-004 remains
open until a licensed extract is loaded.

## Iteration 15 — Patient-testing bench context (2026-09-16)

Workflow-audit loop cycle 1 (patient testing). Iteration 14 leftovers
(`ElectronicEligibility_Criteria_ReportEachCheck` and special-requirement
integration tests) are green. Simulated T&S / worklist / compatibility:
the pending worklist hid current ABO/Rh, antibody history, and specimen
expiry, and `/compatibility` accepted raw numeric patient and specimen ids.

### Implemented

- `TestWorkItemDto` carries current blood type, antibody-history summary,
  specimen expiration, and an expired flag. Entry remains HardStopped when
  the specimen is expired.
- `/test-worklist` and the patient Tests tab show those fields. Patient
  opens `/patients/{id}?tab=tests`. Result entry repeats the same context.
- `/compatibility` identifies the recipient by MRN/name and the specimen
  by accession. No clinical default, ICCBBA table, or AABB window was
  invented.

### Requirements / risk

- URS-BB-161, FRS-BB-195, SRS-BB-156, RISK-BB-267.

### Tests

- TEST-BB-047 (`TestWorklistTests.PendingWorklist_SurfacesBloodTypeAntibodyHistoryAndSpecimenExpiry`).

### Next ranked residual

Next workflow family: unit preparation (receive, retype, irradiate, wash,
divide, pool). SME items remain: OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 16 — Expected inbound bench confirm (2026-09-16)

Workflow-audit loop tick 2 (unit preparation). Simulated receive, expected
ASN, retype, quarantine, and modification. The packing-list worklist
required Open → Manage before visual inspection, temperature, and second
verifier could be recorded.

### Implemented

- `ExpectedInboundWorkItemDto` includes product type. `/inventory` Expected
  inbound shows product and an inline Confirm that calls the existing
  `ReceiveExpectedUnitAsync` gates. Manage drawer confirm remains.
- Demo seed adds `W000123ASN0001` (on time) and `W000123ASN0002` (overdue).
- No new temperature range, verifier default, or AABB window was invented.

### Requirements / risk

- URS-BB-162, FRS-BB-196, SRS-BB-157, RISK-BB-268.

### Tests

- TEST-BB-048 (`InventoryServiceTests.ListExpected_FlagsOverdueWhenPastDue`).
- `SeederTests` expects 50 units including two `Expected`.

### Next ranked residual

Next workflow family: assignment and issuing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 17 — Issue worklists by unit number (2026-09-16)

Workflow-audit loop tick 3 (assignment and issuing). Simulated allocate,
emergency/MTP, in-transit cooler, ward receipt, and retrospective XM.
The in-transit and retrospective boards showed raw `BloodUnitId`, the
ward-receipt form required a typed issue id, and generic issue accepted
a raw patient id.

### Implemented

- In-transit and retrospective DTOs carry unit number, patient name,
  current ABO/Rh, antibody-history summary, and specimen expiration.
- `/issuing` displays those fields. Receive on an in-transit row prefills
  ward receipt. The issue form identifies the recipient by MRN/name and
  fills MRN/DOB tokens. Issue stays disabled until a patient is selected.
- Issue-gate order, identity tokens, and clinical defaults are unchanged.

### Requirements / risk

- URS-BB-163, FRS-BB-197, SRS-BB-158, RISK-BB-269.

### Tests

- TEST-BB-049 (`Phase4IssuingTests.Issue_SetsCoolerAndAppearsOnInTransitWorklist`).

### Next ranked residual

Next workflow family: transfusion reactions. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 18 — Reaction worklist identification (2026-09-16)

Workflow-audit loop tick 4 (reactions). Simulated suspected transfusion,
auto-open, remainder quarantine, and AABB close gate. The investigation
board showed raw patient and unit ids; documenting a reaction left the
workup on another page with no deep link.

### Implemented

- `ReactionInvestigationDto` carries MRN, patient name, unit number,
  current ABO/Rh, antibody-history summary, and workup-incomplete from
  `ReactionWorkupCompletenessRule`. `/reactions` shows those fields plus
  remainder-held status. `?id=` and `?patientId=` open the matching case.
- After a suspected transfusion is documented, `/issuing` links to the
  opened workup. Patient product history Reaction opens `/reactions?patientId=`.
- Auto-open, remainder quarantine, and close gates are unchanged.

### Requirements / risk

- URS-BB-164, FRS-BB-198, SRS-BB-159, RISK-BB-270.

### Tests

- TEST-BB-050 (`ReactionInvestigationServiceTests.ListDtos_SurfacesPatientUnitTypeAndWorkupIncomplete`).

### Next ranked residual

Next workflow family: HL7 interface processing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 19 — HL7 error-queue replay (2026-09-16)

Workflow-audit loop tick 5 (HL7 interface processing). Simulated ADT/ORM
ACK/NAK, MSH-10 idempotency, mapping AE, and replay. The error queue
showed raw message ids and had no replay; a successful replay left the
mapping error open.

### Implemented

- Error-queue rows include control id, message type, trigger, and ACK.
  `/hl7` Replay and View run from that row. Successful replay (`AA`)
  resolves the original work item. MSH-10 idempotency and mapping NAKs
  are unchanged.

### Requirements / risk

- URS-BB-165, FRS-BB-199, SRS-BB-160, RISK-BB-271.

### Tests

- TEST-BB-051 (`Phase5Hl7Tests.Replay_AfterPatientExists_ResolvesMappingError`).

### Next ranked residual

Next workflow family: HL7 interface data load. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 20 — HL7 load verify without re-key (2026-09-16)

Workflow-audit loop tick 6 (HL7 data load). There was no demo ADT → ORM →
accepted specimen → ORU story. After a good ORU the worklist showed the
interface value but only offered Enter, so the bench re-keyed to verify.

### Implemented

- Idempotent seed `MRN0009` (Helen Interface): accepted specimen,
  HL7 type-and-screen, pending-verification ABSC, and ADT/ORM/ORU logs.
- Test worklist Verify opens the posted interface/instrument value.
  `TestResultEntryPanel` verifies that row without re-entry. Specimen and
  OBX-11 gates are unchanged.

### Requirements / risk

- URS-BB-166, FRS-BB-200, SRS-BB-161, RISK-BB-272.

### Tests

- TEST-BB-052 (`TestWorklistTests.PendingWorklist_InterfaceResult_CanVerifyWithoutReentry`).

### Next ranked residual

Next workflow family: billing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 21 — Billing capture on the review queue (2026-09-16)

Workflow-audit loop tick 7 (billing). Verify, issue, and completed
transfusion already capture charges, but the demo database had no events
and the queue showed raw patient and trigger ids.

### Implemented

- Idempotent seed from Patricia Demo (`MRN0001`): verified ABORH/ABSC,
  issued `W0001230000099`, completed transfusion, pending charges, and
  queued outbound DFT logs. No second capture POST.
- Review-queue DTOs include MRN, name, test code or unit number, and DFT
  control id. Review-before-export and dedupe keys are unchanged.

### Requirements / risk

- URS-BB-167, FRS-BB-201, SRS-BB-162, RISK-BB-273.

### Tests

- TEST-BB-053 (`Phase7BillingTests.ReviewQueueDtos_SurfaceMrnUnitAndQueuedDft`).

### Next ranked residual

Next workflow family: patient testing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 22 — Worklist eXM hold and open ABID (2026-09-16)

Workflow-audit loop tick 8 (patient testing). The pending worklist already
showed type, antibodies, and specimen expiry, but electronic-XM holds and
an open antibody-identification workup still required opening the chart.

### Implemented

- `TestWorkItemDto` carries existing AABB 5.16 eligibility, the first
  clinical hold, and the open ABID workup id. `/test-worklist` and the
  patient Tests tab show Eligible/Not eligible and an ABID link.
  Facility allow-EXM (OCD-006) is unchanged.

### Requirements / risk

- URS-BB-168, FRS-BB-202, SRS-BB-163, RISK-BB-274.

### Tests

- TEST-BB-054 (`TestWorklistTests.PendingWorklist_SurfacesElectronicXmHoldAndOpenAntibodyId`).

### Next ranked residual

Next workflow family: unit preparation. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 23 — Quarantine board product and retype (2026-09-16)

Workflow-audit loop tick 9 (unit preparation). The quality-quarantine
board already showed unit number, labeled ABO/Rh, and coded reason, but
not product or the latest retype interpretation. Release still required
opening the Manage drawer, so staff could release without seeing a
labeled-versus-interpreted mismatch on the worklist.

### Implemented

- `QuarantineWorkItemDto` includes product code and the latest
  `ProductRetypeResult` interpretation and mismatch flag.
  `/inventory` Quality quarantine shows those columns and Releases from
  the row with the existing second-verifier gate. Manage release and
  `ReleaseFromQuarantineAsync` are unchanged.

### Requirements / risk

- URS-BB-169, FRS-BB-203, SRS-BB-164, RISK-BB-275.

### Tests

- TEST-BB-055 (`InventoryServiceTests.ListQuarantine_SurfacesRetypeMismatch`).

### Next ranked residual

Next workflow family: assignment and issuing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 24 — Outstanding issued return and transfusion (2026-09-16)

Workflow-audit loop tick 10 (assignment and issuing). After ward receipt
the unit left the in-transit board, and return and transfusion still
required a typed issue id.

### Implemented

- `OutstandingIssueWorkItemDto` lists Issued units with unit number,
  patient, type, antibodies, specimen expiry, and ward-receipt status.
  `/issuing` Issued units prefills Return, Document, and Receive.
  Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-170, FRS-BB-204, SRS-BB-165, RISK-BB-276.

### Tests

- TEST-BB-056 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).

### Next ranked residual

Next workflow family: transfusion reactions. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 25 — Reaction remainder after completed transfusion (2026-09-16)

Workflow-audit loop tick 11 (reactions). Remainder quarantine already ran
for a stopped transfusion, but a completed transfusion moved the unit to
`Transfused` first, which cannot enter quality quarantine. The board also
hid the labeled unit ABO/Rh needed for the clerical check.

### Implemented

- Reaction remainder hold now applies from Issued, TransfusionStarted,
  TransfusionStopped, Transfused, and Returned. Checking remainder no
  longer marks held when quarantine failed. The worklist shows unit
  labeled type and the first `ReactionWorkupCompletenessRule` hold.
  Clerical/visual/DAT close gates are unchanged.

### Requirements / risk

- URS-BB-171, FRS-BB-205, SRS-BB-166, RISK-BB-277.

### Tests

- TEST-BB-057 (`Phase4IssuingTests.Transfusion_ReactionCompleted_QuarantinesRemainder`).

### Next ranked residual

Next workflow family: HL7 interface processing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 26 — HL7 error-queue patient identity (2026-09-16)

Workflow-audit loop tick 12 (HL7 interface processing). The error queue
already showed control id and replayed from the row, but staff could not
see the PID patient.

### Implemented

- Message log and error-queue DTOs read MRN and name from PID.
  `/hl7` lists those fields. Replay, MSH-10 idempotency, and mapping
  NAKs are unchanged.

### Requirements / risk

- URS-BB-172, FRS-BB-206, SRS-BB-167, RISK-BB-278.

### Tests

- TEST-BB-058 (`Phase5Hl7Tests.InboundOrm_UnknownPatient_ProducesApplicationErrorAndQueuesIt`).

### Next ranked residual

Next workflow family: HL7 interface data load. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 27 — HL7 BPAM transfusion load (2026-09-17)

Workflow-audit loop tick 13 (HL7 data load). ADT/ORM/ORU already landed on
Helen Interface, but a good RAS never produced a chart transfusion, so staff
re-documented administration on `/issuing`.

### Implemented

- Idempotent seed of issued `W000123BPAM001` and processed RAS on `MRN0009`.
  Product history shows the transfusion and `HL7-BPAM` identification.
  Inbound RAS documents against an issued unit through the existing
  `DocumentFromHl7Async` path. Issue and bedside-scan gates are unchanged.

### Requirements / risk

- URS-BB-173, FRS-BB-207, SRS-BB-168, RISK-BB-279.

### Tests

- TEST-BB-059 (`Phase5Hl7Tests.InboundRas_DocumentsTransfusionOnIssuedUnitWithoutRekey`).

### Next ranked residual

Next workflow family: billing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 28 — Billing review-to-export and RAS capture (2026-09-17)

Workflow-audit loop tick 14 (billing). Review-before-export already existed,
but the queue listed only Pending rows, so Review hid the charge and Export
could not run on `/billing`. Inbound RAS also skipped capture.

### Implemented

- The review queue keeps Pending and Reviewed charges until export or cancel.
  `/billing` offers Export only after Review. Completed inbound RAS calls the
  same `CaptureForTransfusionAsync` as Issuing Document. Helen Interface seed
  adds issue/transfusion charges and DFT stubs. Dedupe keys and export-only-
  after-review are unchanged.

### Requirements / risk

- URS-BB-174, FRS-BB-208, SRS-BB-169, RISK-BB-280.

### Tests

- TEST-BB-060 (`Phase7BillingTests.ReviewQueueDtos_KeepReviewedUntilExported`).

### Next ranked residual

Next workflow family: patient testing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 29 — Patient-testing Verify cue and posted result (2026-09-17)

Workflow-audit loop tick 15 (patient testing). The global worklist already
offered Verify for a posted interface value, but the patient Tests tab said
Enter and neither grid showed the posted result.

### Implemented

- `TestWorkItemDto` exposes `BenchActionLabel`, posted value, enterer, and
  ABO self-verify hold. `/test-worklist` and the patient Tests tab show Result
  and use Verify for interface/instrument PendingVerification. The entry panel
  hides Verify posted result when the ABO enterer is the current user.
  Specimen and OCD-018/019 gates are unchanged.

### Requirements / risk

- URS-BB-175, FRS-BB-209, SRS-BB-170, RISK-BB-281.

### Tests

- TEST-BB-061 (`TestWorklistTests.PendingWorklist_PostedInterface_SurfacesResultAndVerifyAction`).

### Next ranked residual

Next workflow family: unit preparation. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 30 — Unit-prep discrepancy and hold boards (2026-09-17)

Workflow-audit loop tick 16 (unit preparation). Expected inbound and
quarantine already identified product and acted inline. Missing, damaged,
and operational-hold rows still hid product and sent staff to Manage.

### Implemented

- `DiscrepancyWorkItemDto` includes product. `OnHoldWorkItemDto` lists
  held units with product, ABO/Rh, and reason. `/inventory` Locates,
  Inspects, and Releases hold from those rows through the existing
  Application methods. Locate-to-quarantine and `inventory.release`
  gates are unchanged.

### Requirements / risk

- URS-BB-176, FRS-BB-210, SRS-BB-171, RISK-BB-282.

### Tests

- TEST-BB-062 (`InventoryServiceTests.ListOnHold_SurfacesProductAndHoldReason`).
- Full suite green: Domain 1241, Application 35, HL7 48, Printing 11,
  Integration 772.

### Next ranked residual

Next workflow family: assignment / issuing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 31 — Retrospective XM follow-up from issuing (2026-09-17)

Workflow-audit loop tick 17 (assignment / issuing). The retrospective
crossmatch board already identified the emergency or MTP unit, but staff
had to re-key patient, unit, and specimen on `/compatibility`.

### Implemented

- `RetrospectiveCrossmatchWorkItemDto` includes specimen id and accession.
  `/issuing` Record crossmatch opens `/compatibility` with those fields
  prefilled. The XM unit picker includes Issued and Transfused so the
  released unit can be named. Compatible XM still closes the queue through
  existing `TryCloseRetrospectiveCrossmatchAsync`. Emergency defaults and
  due hours are unchanged.

### Requirements / risk

- URS-BB-177, FRS-BB-211, SRS-BB-172, RISK-BB-283.

### Tests

- TEST-BB-063 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1241, Application 35, HL7 48, Printing 11,
  Integration 772.

### Next ranked residual

Next workflow family: reactions. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 32 — Reaction board ABO/Rh incompatibility cue (2026-09-17)

Workflow-audit loop tick 18 (reactions). The investigation board already
showed patient and labeled unit types side by side, but did not apply the
existing issue-path ABO/Rh rule or show whether the issue was emergency
or uncrossmatched.

### Implemented

- `ReactionInvestigationDto` evaluates `AboCompatibilityRule` on current
  patient type vs labeled unit type and copies issue type, crossmatch
  status, and tests-incomplete. `/reactions` badges ABO/Rh incompatible
  and shows issue context. Close, clerical, visual, and DAT gates are
  unchanged.

### Requirements / risk

- URS-BB-178, FRS-BB-212, SRS-BB-173, RISK-BB-284.

### Tests

- TEST-BB-064 (`ReactionInvestigationServiceTests.ListDtos_SurfacesAboIncompatibilityAndEmergencyIssueContext`).
- Full suite green: Domain 1241, Application 35, HL7 48, Printing 11,
  Integration 773.

### Next ranked residual

Next workflow family: HL7 process. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 33 — HL7 error-queue order context and outbound Send (2026-09-17)

Workflow-audit loop tick 19 (HL7 process). The error queue already showed
PID MRN and name, but hid placer/test/unit and offered Replay on outbound
failures. Successful outbound AA also left the queue open.

### Implemented

- `Hl7MessageIdentity` reads placer, test, and unit from stored raw.
  `Hl7ErrorDto` includes direction, those fields, and optional patient
  id. `/hl7` Replays inbound rows and Sends outbound rows. Outbound AA
  resolves the queue item. ReplayAllowed and mapping NAKs are unchanged.

### Requirements / risk

- URS-BB-179, FRS-BB-213, SRS-BB-174, RISK-BB-285.

### Tests

- TEST-BB-065 (`Phase5Hl7Tests.OutboundSender_Aa_ResolvesErrorQueue`).
- Full suite green: Domain 1241, Application 35, HL7 50, Printing 11,
  Integration 774.

### Next ranked residual

Next workflow family: HL7 data load. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 34 — Orders-tab Verify for posted HL7 results (2026-09-17)

Workflow-audit loop tick 20 (HL7 data load). Helen's inbound ORU already
landed as a pending interface antibody screen, but the Orders tab hid
the posted value and offered only Edit.

### Implemented

- `PatientOrderDto` includes the current result value, source, and
  Verify label when the posted result is Interface/Instrument
  PendingVerification. `/patients/{id}` Orders shows Source and Result
  and Verifies through existing `VerifyResultAsync`. Specimen and
  OCD-018/019 gates are unchanged.

### Requirements / risk

- URS-BB-180, FRS-BB-214, SRS-BB-175, RISK-BB-286.

### Tests

- TEST-BB-066 (`SeederTests.Seed_HelenHl7Order_SurfacesPostedInterfaceValueAndVerifyAction`).
- Full suite green: Domain 1241, Application 35, HL7 50, Printing 11,
  Integration 775.

### Next ranked residual

Next workflow family: billing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 35 — One charge per catalog-mapped billing trigger (2026-09-17)

Workflow-audit loop tick 21 (billing). Verify, issue, and transfusion already
captured charges, but the demo seed left ChargeRules and catalogs both
active, so a live trigger could drop two events and two DFTs.

### Implemented

- `BillingService.CaptureAsync` runs test/product catalogs first and skips
  ChargeRules when an active catalog row matches the trigger. Demo seed
  deactivates ChargeRules that duplicate catalog test or product keys.
  Catch-all null-key rules remain for unmapped products. Review, export,
  and dedupe keys are unchanged.

### Requirements / risk

- URS-BB-181, FRS-BB-215, SRS-BB-176, RISK-BB-287.

### Tests

- TEST-BB-067 (`SeederTests.Seed_ChargeRulesDoNotOverlapBillingCatalogs`).
- Full suite green: Domain 1241, Application 35, HL7 50, Printing 11,
  Integration 777.

### Next ranked residual

Next workflow family: patient testing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 36 — Worklist ABO/Rh historical discrepancy (2026-09-17)

Workflow-audit loop tick 22 (patient testing). Verify already blocked
`RES-ABORH-DELTA`, but the pending worklist only showed the historical
type, so the bench did not see the disagreement until verify.

### Implemented

- `TestWorkItemDto` includes `AboRhDeltaHold` and detail from
  `AboRhDeltaRule` when a pending ABORH value disagrees with current
  history. `/test-worklist` and the patient Tests tab badge Discrepancy.
  Override and self-verify gates are unchanged.

### Requirements / risk

- URS-BB-182, FRS-BB-216, SRS-BB-177, RISK-BB-288.

### Tests

- TEST-BB-068 (`TestWorklistTests.PendingWorklist_SurfacesAboRhDeltaHold_OnEnteredAborh`).
- Full suite green: Domain 1241, Application 35, HL7 50, Printing 11,
  Integration 778.

### Next ranked residual

Next workflow family: unit preparation. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 37 — Inventory pending retype board (2026-09-17)

Workflow-audit loop tick 23 (unit preparation). Receive already left units
in Received until a matching retype, but `/inventory` hid that queue
behind `/test-worklist` or Manage.

### Implemented

- `/inventory` lists pending product ABO/Rh retype with product, labeled
  type, and assigned test. Enter expands the existing
  `ProductRetypeEntryPanel`. Match still releases to Available; mismatch
  still quarantines.

### Requirements / risk

- URS-BB-183, FRS-BB-217, SRS-BB-178, RISK-BB-289.

### Tests

- TEST-BB-069 (`SeederTests.Seed_PendingRetypeWorklist_IncludesRetDemoUnits`).
- Full suite green: Domain 1241, Application 35, HL7 50, Printing 11,
  Integration 779.

### Next ranked residual

Next workflow family: assignment and issuing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 38 — Ready-to-issue reserved units (2026-09-17)

Workflow-audit loop tick 24 (assignment and issuing). Reserve already
required an allocation before issue, but `/issuing` hid reserved units
behind the patient Products tab.

### Implemented

- `ReadyToIssueWorkItemDto` lists reserved allocations with MRN, unit
  number, XM, and display status. `/issuing` Issue prefills the existing
  form. `/compatibility` links to `/issuing?patientId=&bloodUnitId=`.
  IssueGate and emergency defaults are unchanged.

### Requirements / risk

- URS-BB-184, FRS-BB-218, SRS-BB-179, RISK-BB-290.

### Tests

- TEST-BB-070 (`Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate`).
- Full suite green: Domain 1241, Application 35, HL7 50, Printing 11,
  Integration 780.

### Next ranked residual

Next workflow family: reactions. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 39 — Reaction repeat ABO/Rh discrepancy cue (2026-09-17)

Workflow-audit loop tick 25 (reactions). The workup already stored
repeat patient and unit ABO/Rh as free text, and the board already
compared labeled types, but a recorded repeat that disagreed with the
current or labeled type was invisible.

### Implemented

- `ReactionRepeatAboRhRule` compares recorded repeat text to current
  patient type and labeled unit type. `ReactionInvestigationDto` carries
  the Warning flags and first messages. `/reactions` badges those cells
  and does not copy labeled type into the repeat fields. Close still
  requires only clerical, visual, and DAT.

### Requirements / risk

- URS-BB-185, FRS-BB-219, SRS-BB-180, RISK-BB-291.

### Tests

- TEST-BB-071 (`ReactionInvestigationServiceTests.ListDtos_SurfacesRepeatAboRhDiscrepancy`).
- Domain `ReactionRepeatAboRhRuleTests`.
- Full suite green: Domain 1247, Application 35, HL7 50, Printing 11,
  Integration 781.

### Next ranked residual

Next workflow family: HL7 process. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 40 — HL7 message-log order context (2026-09-17)

Workflow-audit loop tick 26 (HL7 process). The error queue already showed
placer, test, and unit, but the message log still hid those fields behind
View raw.

### Implemented

- `Hl7MessageDto` copies placer, test, and unit from `Hl7MessageIdentity`
  and joins the patient by MRN. `/hl7` lists that context and links the
  patient. Replay, Send, MSH-10 idempotency, and mapping NAKs are
  unchanged. ReplayAllowed stays unenforced.

### Requirements / risk

- URS-BB-186, FRS-BB-220, SRS-BB-181, RISK-BB-292.

### Tests

- TEST-BB-072 (`SeederTests.Seed_HelenHl7Messages_SurfacePlacerTestAndUnit`).
- Full suite green: Domain 1247, Application 35, HL7 50, Printing 11,
  Integration 782.

### Next ranked residual

Next workflow family: HL7 data load. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 41 — RAS administration details on product history (2026-09-18)

Workflow-audit loop tick 27 (HL7 data load). Inbound RAS already documented
the transfusion and showed `HL7-BPAM`, but volume, location, and
transfusionist stayed on the event row. Staff re-opened Issuing or the raw
message to see what the interface had already loaded.

### Implemented

- `PatientProductHistoryRowDto` includes transfusionist. Transfusion rows
  prefer event location over issue destination.
- `/patients/{id}` Product History shows Location, Volume, and Transfusionist.
- Default BPAM transfusionist path is `RXA-10` XCN (`Nurse, Pat`).
- Demo RAS seed includes `PV1`. Issue and bedside-scan gates are unchanged.

### Requirements / risk

- URS-BB-187, FRS-BB-221, SRS-BB-182, RISK-BB-293.

### Tests

- TEST-BB-073 (`SeederTests.Seed_HelenHl7Bpam_SurfacesVolumeLocationAndTransfusionist`).
- `Phase5Hl7Tests.InboundRas_DocumentsTransfusionOnIssuedUnitWithoutRekey`
  asserts volume, location, and transfusionist on the history row.
- Full suite green: Domain 1247, Application 35, HL7 50, Printing 11,
  Integration 783.

### Next ranked residual

Next workflow family: billing. SME items remain:
OCD-001/006, OCD-022, OCD-008, OCD-004.

## Iteration 42 — SME answers: eXM, phenotype lock, optional dual-ID, ISBT dialog (2026-09-18)

Conversation SME slice (not a workflow-audit tick). Answers: keep ICCBBA
placeholders and import via an admin dialog when an extract is available;
electronic XM off until Facility Policy; antigen phenotype stays in place
and result-sourced attributes stay unless `immuno.override`; second
verifier remains optional for small labs.

### Implemented

- `/admin/isbt-product-codes` **Import licensed extract…** dialog (drop
  folder, upload, JSON). Placeholders stay; no invented ICCBBA tables.
- `AllowElectronicCrossmatch` seeds and falls back to false. Enable on
  Facility Policy. Tests that need eXM turn the flag on.
- Result-sourced antigen change HardStops `IH-AG-RESULT-LOCK` without
  `immuno.override`. Chart-created rows stay editable in place.
- Second-verifier catalog text marked Optional (OCD-008).

### Requirements / risk

- URS-BB-188–191, FRS-BB-222–225, SRS-BB-183–186, RISK-BB-294–297.
- Closed OCD-006, OCD-008, OCD-022. OCD-004 and OCD-001 remain open.

### Tests

- TEST-BB-074 (`FdaAabbScenarioTests.PatriciaDemo_ElectronicXm_IsOffUntilFacilityAllows`).
- TEST-BB-075 (`Phase3ServicesTests.ResultSourcedAntigen_ChangeWithoutOverride_IsRejected`).
- TEST-BB-076 (`ImmunoAuthorizationRuleTests.ResultSourcedAntigenChange_WithoutOverride_IsHardStop`).
- TEST-BB-077 (`FacilityPolicyValidatorTests.SecondVerifierFlags_RemainOptional`).
- Full suite green: Domain 1251, Application 35, HL7 50, Printing 11,
  Integration 786.

### Next ranked residual

Next workflow family: billing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 43 — Antigen-negative allocation override (2026-09-27)

Workflow-audit loop tick (assignment and issuing). RBC and whole blood that
were untested or not antigen-negative for a patient antibody were either
hidden from the compatible-unit list or reservable through a client boolean
that skipped the security check and did not write an override.

### Implemented

- `ISS-ANTIGEN-NEG` still warns. The message says the unit is not tested or
  states the recorded non-negative result.
- Compatible-unit search keeps those units and flags Untested, NotNegative,
  or Mixed. ABO and other HardStops stay off the list.
- Patient chart, unit page, and `/compatibility` ask for reason and authorizer
  before that reserve succeeds.
- `AllocateUnitAsync` checks `ExceptionOverridePolicy` and writes the
  `Override` row. A client boolean no longer authorizes it. Users below the
  exception minimum stay blocked.
- Granulocyte `COMPAT-ATTR-*` warnings stay advisory.

### Requirements / risk

- URS-BB-196, FRS-BB-231, SRS-BB-193, RISK-BB-298.
- Does not claim AABB compliance. Granulocyte severity remains SME review.

### Tests

- TEST-BB-085 (`PatientAllocationTests.Allocate_AntiK_Level1Override_IsBlocked`,
  `AllocateUnit_AntiK_RequiresPrivilegedOverrideRow`,
  `ListCompatibleUnits_FlagsUntypedAndAntigenPositive`).
- TEST-BB-041 still covers the issue-gate warning for untyped and K-positive units.
- Domain message assertions in `BloodAttributeCompatibilityRuleTests`.
- Full suite green: Domain 1386, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 865.
- Patient chart: the compatible-unit error was bound as a string literal, so
  the unit list never rendered. It now binds the field. On Kellerman (anti-K),
  an untested RBC shows “not tested,” reserve without a reason is refused, and
  a level-3 user with a reason reserves the unit and writes an `ISS-ANTIGEN-NEG`
  Override.

### Next ranked residual

Next workflow family: billing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 44 — Return drops open issue charges (2026-09-27)

Workflow-audit loop tick (billing). Returning an unused unit left its
issue charge on the review queue, so staff had to find and cancel it by hand.

### Implemented

- `CancelOpenIssueChargesForReturnAsync` cancels pending and reviewed
  `UnitIssued` charges for that issue and audits the return reason. It does
  not require `billing.cancel`.
- Issue return and unit return both call it after the return commits.
- An already exported issue charge stays. The return warns
  `BILL-RETURN-EXPORTED`. Transfusion and test charges are unchanged.

### Requirements / risk

- URS-BB-197, FRS-BB-232, SRS-BB-194, RISK-BB-299.
- Does not claim AABB compliance.

### Tests

- TEST-BB-086 (`Phase7BillingTests.Return_CancelsOpenIssueCharge_AndLeavesExported`).
- Full suite green: Domain 1386, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 866.

### Next ranked residual

Next workflow family: reactions. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 45 — Reaction workup saves when remainder cannot be held (2026-09-27)

Workflow-audit loop tick (reactions). Checking remainder held on a unit that
cannot move to quarantine rejected the whole investigation save, so clerical
and other findings were dropped. The board also showed a red Not held for
those units.

### Implemented

- `UpdateAsync` still saves the investigation and warns `RXN-REMAINDER-HOLD`
  when the remainder cannot be quarantined.
- The board says Cannot hold, with the unit status, instead of a red Not held.
- Close still uses the existing clerical, visual, and DAT gate.

### Requirements / risk

- URS-BB-198, FRS-BB-233, SRS-BB-195, RISK-BB-300.
- Does not claim AABB compliance.

### Tests

- TEST-BB-087 (`ReactionInvestigationServiceTests.Update_DiscardedUnit_SavesWorkupWhenRemainderCannotBeHeld`).
- Full suite green: Domain 1386, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 867.

### Next ranked residual

Next workflow family: lookback. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 46 — Issued units get one lookback notification (2026-09-27)

Workflow-audit loop tick (lookback). Recalling a DIN moved an issued
component to Recalled and left the notification worklist empty, so staff
were not prompted to call the floor. Recalling again added another pending
row for a unit that was already transfused.

### Implemented

- Recall opens one Pending notification for Issued, TransfusionStarted,
  TransfusionStopped, or Transfused, and does not insert a second row.
- Issued components are still moved to Recalled. Transfused components are
  not status-changed. The worklist shows the unit number.

### Requirements / risk

- URS-BB-199, FRS-BB-234, SRS-BB-196, RISK-BB-301.
- Does not claim AABB compliance.

### Tests

- TEST-BB-088 (`ComplianceWorkflowTests.RecallByDin_IssuedUnit_OpensOneNotification_AndSecondRecallDoesNotDuplicate`).
- Full suite green: Domain 1386, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 868.

### Next ranked residual

Next workflow family: HL7 process. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 47 — HL7 replay honors the interface checkbox (2026-09-27)

Workflow-audit loop tick (HL7 process). Replay allowed was stored on the
interface and shown in admin, but Replay still reapplied the stored message.
A corrected chart could be overwritten by an old ADT or order.

### Implemented

- Replay of a message whose interface has Replay allowed unchecked does not
  reprocess. The HL7 log and error queue say Replay off.
- New interfaces start with replay allowed. A message with no interface can
  still be replayed. MSH-10 idempotency is unchanged.

### Requirements / risk

- URS-BB-200, FRS-BB-235, SRS-BB-197, RISK-BB-302.
- Does not claim AABB compliance.

### Tests

- TEST-BB-089 (`Phase5Hl7Tests.Replay_WhenEndpointDisallows_DoesNotReprocess`).
- Full suite green: Domain 1386, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 869.

### Next ranked residual

Next workflow family: HL7 data load. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 48 — ORU links a named accepted specimen (2026-09-27)

Workflow-audit loop tick (HL7 data load). An inbound result was rejected
when the order was not already linked to a specimen, even when the message
named an accepted accession. Staff had to link it and replay.

### Implemented

- ORU reads specimen accession from SPM-2.
- If that accession is Accepted for the same patient, the order is linked
  and the result posts as pending verification.
- A missing, unaccepted, or other-patient accession does not post.

### Requirements / risk

- URS-BB-201, FRS-BB-236, SRS-BB-198, RISK-BB-303.
- Does not claim AABB compliance.

### Tests

- TEST-BB-090 (`Phase5Hl7Tests.InboundOru_LinksNamedAcceptedSpecimen_AndPosts`;
  `Phase5Hl7Tests.InboundOru_RejectsNamedSpecimenForAnotherPatient`).
- Full suite green: Domain 1386, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 871.

### Next ranked residual

Next workflow family: billing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 49 — Invalidation drops an open test charge (2026-09-27)

Workflow-audit loop tick (billing). Verifying a result captures a test
charge. Invalidating that result left the charge on the review queue, and
cancelling it required `billing.cancel`.

### Implemented

- Invalidating a result cancels pending and reviewed `TestVerified` charges
  for that result.
- The invalidation does not require `billing.cancel`.
- An already exported test charge stays, and the response warns
  `BILL-INVALIDATE-EXPORTED`.
- Issue and transfusion charges are unchanged.

### Requirements / risk

- URS-BB-202, FRS-BB-237, SRS-BB-199, RISK-BB-304.
- Does not claim AABB compliance.

### Tests

- TEST-BB-091 (`Phase7BillingTests.Invalidate_CancelsOpenTestCharge_AndLeavesExported`).
- Full suite green: Domain 1386, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 872.

### Next ranked residual

Next workflow family: patient testing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 50 — Posted ABO discrepancy opens the override (2026-09-28)

Workflow-audit loop tick (patient testing). The specimen result already
asked for a history resolution when a verified type disagreed with history.
Verify posted result on the worklist and the Orders tab only showed the
block, so staff had to re-key the type to reach the override.

### Implemented

- A blocked, overridable `RES-ABORH-DELTA` opens the existing retain or
  replace override, with reason, authorizer, and signature.
- The worklist entry panel and the patient Orders tab use that prompt and
  then call verify. They do not re-save the result.
- A user below the exception security level stays blocked. The discrepancy
  remains a Warning.

### Requirements / risk

- URS-BB-203, FRS-BB-238, SRS-BB-200, RISK-BB-305.
- Does not claim AABB compliance.

### Tests

- TEST-BB-092 (`AboRhDeltaRuleTests.OpensOverride_OnlyForBlockedOverridableDelta`).
- Full suite green: Domain 1387, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 872.

### Next ranked residual

Next workflow family: unit preparation. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 51 — Expected inbound can return a refused unit (2026-09-28)

Workflow-audit loop tick (unit preparation). Appearance and temperature
already refuse a bad packing-list unit and say to return it to the supplier.
The expected-inbound row only offered Confirm and Open, so staff left the
unit on the packing list.

### Implemented

- The expected-inbound confirm panel returns the unit to the supplier.
- A coded defect suggests the return reason from the appearance or the
  arrival notes. An acceptable appearance still needs a typed reason.
- The receipt temperature label shows the 1–10 °C shipping range.
- Receive HardStops are unchanged.

### Requirements / risk

- URS-BB-204, FRS-BB-239, SRS-BB-201, RISK-BB-306.
- Does not claim AABB compliance.

### Tests

- TEST-BB-093 (`ReceiveAppearanceRuleTests.SuggestedSupplierReturnReason_UsesDefect_AndKeepsTypedReason`).
- Full suite green: Domain 1388, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 872.

### Next ranked residual

Next workflow family: assignment and issuing. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 52 — Ready to issue marks a missing second ABO (2026-09-28)

Workflow-audit loop tick (assignment and issuing). Routine red-cell issue
already hard-stops when a second concordant ABO/Rh is missing. Ready to
issue still offered Issue, so staff filled the form and then hit the block.

### Implemented

- Ready to issue sets `NeedsSecondAbo` for red cells and whole blood when
  that determination is missing.
- Those rows show Second ABO and a link to enter the type. They do not
  offer routine issue.
- Staff who can authorize emergency release can still open that form.
- Plasma is not flagged. `ISS-SECOND-ABO` stays a HardStop for routine issue.

### Requirements / risk

- URS-BB-205, FRS-BB-240, SRS-BB-202, RISK-BB-307.
- Does not claim AABB compliance.

### Tests

- TEST-BB-094 (`Phase4IssuingTests.ListReadyToIssue_FlagsCellularUnitWhenSecondAboIsMissing`).
- Full suite green: Domain 1388, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 873.

### Next ranked residual

Next workflow family: reactions. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 53 — Closed fatality stays on the reaction board (2026-09-28)

Workflow-audit loop tick (reactions). Closing a fatality left CBER
notification and the written report in a list of every finished
investigation. Staff could miss the follow-up.

### Implemented

- `RXN-FATALITY-FOLLOWUP` stays a Warning until both CBER notification and
  the written report are recorded.
- `/reactions` lists investigations still due. A closed fatality remains,
  with the missing step named.
- Show closed, and a patient or investigation link, still open a finished case.
- The close gate is unchanged.

### Requirements / risk

- URS-BB-206, FRS-BB-241, SRS-BB-203, RISK-BB-308.
- Does not claim AABB compliance.

### Tests

- TEST-BB-095 (`ReactionFatalityFollowUpRuleTests.ClosedFatality_StaysDueUntilCberAndWrittenReport`).
- Full suite green: Domain 1390, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 874.

### Next ranked residual

Next workflow family: lookback. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 54 — Lookback calls stay on the page (2026-09-28)

Workflow-audit loop tick (lookback). Recall already opened one notification
for an issued or transfused component. That row was visible only after
searching the same DIN again, so the call dropped off the page.

### Implemented

- `/lookback` lists Pending and Attempted notifications on open, with the
  unit number and patient name.
- Completed and not-applicable rows leave that board. They remain on the
  DIN report.
- Recording an attempt still uses the existing update. Recall is unchanged.

### Requirements / risk

- URS-BB-207, FRS-BB-242, SRS-BB-204, RISK-BB-309.
- Does not claim AABB compliance.

### Tests

- TEST-BB-096 (`ComplianceWorkflowTests.ListOpenNotifications_IncludesPending_AndOmitsCompleted`).
- Full suite green: Domain 1390, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 875.

### Next ranked residual

Next workflow family: HL7 process. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 55 — HL7 errors can be resolved without replay (2026-09-28)

Workflow-audit loop tick (HL7 process). Replay already stays off when the
interface disallows it. That row then had no way off the error queue, so a
message staff had already handled by hand stayed open.

### Implemented

- Resolve marks one unresolved queue row resolved when a reason is present.
- It does not reprocess the stored message.
- A blank reason is refused. Replay and Replay allowed are unchanged.

### Requirements / risk

- URS-BB-208, FRS-BB-243, SRS-BB-205, RISK-BB-310.
- Does not claim AABB compliance.

### Tests

- TEST-BB-097 (`Phase5Hl7Tests.ResolveQueueItem_ClearsTheRow_WithoutReprocessing`).
- Full suite green: Domain 1390, Application 38, HL7 50, Printing 21,
  ASTM 19, Integration 876.

### Next ranked residual

Next workflow family: billing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 56 — Inbound orders keep a stated priority (2026-09-28)

Workflow-audit loop tick (HL7 data load). A new ORM always created the
order as Routine. A STAT type and screen from the interface looked the
same as routine work on the chart and the test worklist.

### Implemented

- A new inbound order stores Stat, Urgent, Timed, or PreOp when the
  message says so (`OBR-5`, or the timing priority component).
- A blank or unrecognized code stays Routine.
- An order that already exists is not changed.

### Requirements / risk

- URS-BB-209, FRS-BB-244, SRS-BB-206, RISK-BB-311.
- Does not claim AABB compliance.

### Tests

- TEST-BB-098 (`Phase5Hl7Tests.InboundOrm_StatPriority_CreatesStatOrder`;
  `Hl7MapperTests.OrmMapper_MapsPriorityCodes`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 877.

### Next ranked residual

Next workflow family: patient testing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 57 — Action charges name the action and unit (2026-09-28)

Workflow-audit loop tick (billing). A completed order action already
captures a charge. The review queue showed a blank test/unit cell, so
staff could not tell which irradiation or thaw they were about to export.

### Implemented

- An `ActionCompleted` row shows the action code and the result or source
  unit number.
- That row opens the patient's orders.
- Test, issue, and transfusion labels are unchanged.

### Requirements / risk

- URS-BB-210, FRS-BB-245, SRS-BB-207, RISK-BB-312.
- Does not claim AABB compliance.

### Tests

- TEST-BB-099 (`Phase7BillingTests.ReviewQueueDtos_ShowActionCodeAndUnit`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 878.

### Next ranked residual

Next workflow family: unit preparation. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 58 — Rush tests stay above newer routine work (2026-09-28)

Workflow-audit loop tick (patient testing). The pending worklist already
shaded Stat and Urgent rows, but it listed newest orders first. An older
Stat type and screen sat under routine tests ordered later.

### Implemented

- The worklist and the patient Tests tab list Stat, Urgent, emergency
  release, and massive-transfusion orders first, then newest ordered time.
- Those priorities use a red badge. Entry and verify gates are unchanged.

### Requirements / risk

- URS-BB-211, FRS-BB-246, SRS-BB-208, RISK-BB-313.
- Does not claim AABB compliance.

### Tests

- TEST-BB-100 (`TestWorklistTests.PendingWorklist_ListsRushPriorityAheadOfNewerRoutine`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 879.

### Next ranked residual

Next workflow family: assignment and issuing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 59 — Near expiry can be discarded from the row (2026-09-28)

Workflow-audit loop tick (unit preparation). The near-expiry board already
listed units inside the warning window and said to discard them. The row
only offered Open, so staff left the board to find Discard on the unit.

### Implemented

- Near expiry expands the existing discard: reason and a different second
  verifier.
- A blank reason does not submit. A successful discard leaves the board.
- Discard permission and the second-verifier rule are unchanged.

### Requirements / risk

- URS-BB-212, FRS-BB-247, SRS-BB-209, RISK-BB-314.
- Does not claim AABB compliance.

### Tests

- TEST-BB-101 (`InventoryServiceTests.Discard_NearExpiryUnit_LeavesTheBoard`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 880.

### Next ranked residual

Next workflow family: assignment and issuing. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 60 — Issued-unit return records inspection (2026-09-28)

Workflow-audit loop tick (assignment and issuing). The unit page already
asked for temperature, seal, and appearance before changing an issued unit.
`/issuing` Return sent only a reason, so every check defaulted to acceptable
and a failed inspection could return to available stock.

### Implemented

- Return on `/issuing` posts temperature, seal, visual inspection, and a
  destination with the reason.
- Available is withheld when seal, appearance, or temperature fails. A
  failed seal or appearance moves the destination to Disposed.
- A blank reason does not submit. `RET-REISSUE` is unchanged.

### Requirements / risk

- URS-BB-213, FRS-BB-248, SRS-BB-210, RISK-BB-315.
- Does not claim AABB compliance.

### Tests

- TEST-BB-102 (`Phase4IssuingTests.Return_FailedVisual_DisposesAndLeavesOutstanding`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 881.

### Next ranked residual

Next workflow family: reactions. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 61 — Reaction close waits for the workup checklist (2026-09-28)

Workflow-audit loop tick (reactions). Closing an investigation already
hard-stops until clerical check, visual inspection, and DAT are recorded.
The form still listed Closed, so staff signed a close and then hit that block.

### Implemented

- An open investigation omits Closed while `RXN-WORKUP-INCOMPLETE` applies,
  and names the missing step.
- A completed checklist offers Closed again. The signature and the hard stop
  are unchanged.
- An already closed investigation still shows Closed.

### Requirements / risk

- URS-BB-214, FRS-BB-249, SRS-BB-211, RISK-BB-316.
- Does not claim AABB compliance.

### Tests

- TEST-BB-103 (`ReactionInvestigationServiceTests.Update_IncompleteWorkup_DoesNotClose`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 882.

### Next ranked residual

Next workflow family: lookback. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 62 — Lookback completion names the physician (2026-09-28)

Workflow-audit loop tick (lookback). Pending and attempted calls already
stay on the page. Completing one, or marking it not applicable, accepted a
blank physician and a blank note, so the call left the board with no record.

### Implemented

- Completing a notification requires a physician of record.
- Marking it not applicable requires a note.
- A blank value does not change the row. An attempt can still be saved
  without those fields.

### Requirements / risk

- URS-BB-215, FRS-BB-250, SRS-BB-212, RISK-BB-317.
- Does not claim AABB compliance.

### Tests

- TEST-BB-104 (`LookbackSearchAuthorizationTests.RecordAttempt_CompletedWithoutPhysician_StaysOnTheBoard`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 883.

### Next ranked residual

Next workflow family: HL7 process. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 63 — Outbound HL7 send stops at the retry limit (2026-09-28)

Workflow-audit loop tick (HL7 process). Queued outbound send already skipped
a message that had used its retry limit. The error queue and message log
still offered Send, so another click transmitted it again.

### Implemented

- `SendOneAsync` refuses an outbound message at the endpoint max retry count
  and does not increment the count or connect.
- `/hl7` shows Retries used instead of Send on those rows.
- Resolve and the configured limit are unchanged.

### Requirements / risk

- URS-BB-216, FRS-BB-251, SRS-BB-213, RISK-BB-318.
- Does not claim AABB compliance.

### Tests

- TEST-BB-105 (`Phase5Hl7Tests.OutboundSender_ExhaustedRetries_DoesNotSend`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 884.

### Next ranked residual

Next workflow family: HL7 data load. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 64 — Sparse ADT keeps the chart identity (2026-09-28)

Workflow-audit loop tick (HL7 data load). A later ADT copied blank name
fields and an omitted sex onto the patient. Date of birth was already left
alone when the message did not include it. A location-only update could
clear the name and sex staff use to confirm the chart.

### Implemented

- An update applies last name, first name, and middle name only when the
  ADT carries them.
- A missing date of birth still leaves the chart date in place.
- Sex Unknown, including a blank PID-8, does not replace a recorded sex.
- A stated name, date of birth, or sex still updates the chart. A new
  patient is still created from the message as sent.

### Requirements / risk

- URS-BB-217, FRS-BB-252, SRS-BB-214, RISK-BB-319.
- Does not claim AABB compliance.

### Tests

- TEST-BB-106 (`Phase5Hl7Tests.InboundAdt_SparseA08_KeepsNameSexAndDateOfBirth`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 885.

### Next ranked residual

Next workflow family: billing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 65 — Cancelling a charge withholds an unsent DFT (2026-09-28)

Workflow-audit loop tick (billing). Capture already queues a DFT. Cancelling
the charge left that message on the outbound queue, so a later send could
still transmit a charge staff had cancelled.

### Implemented

- Cancelling a pending or reviewed charge marks an unsent outbound DFT
  processed and records that it was withheld.
- `/hl7` says Charge cancelled instead of Send. A later send is refused
  and does not increment the retry count.
- A DFT that already left stays. The cancellation warns `BILL-DFT-SENT`.
  No credit transaction is created.
- Unused-unit return and result invalidation use the same withhold.

### Requirements / risk

- URS-BB-218, FRS-BB-253, SRS-BB-215, RISK-BB-320.
- Does not claim AABB compliance.

### Tests

- TEST-BB-107 (`Phase7BillingTests.Cancel_UnsentDft_IsWithheldFromSend`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 886.

### Next ranked residual

Next workflow family: patient testing. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 66 — Expired specimens do not offer Verify (2026-09-28)

Workflow-audit loop tick (patient testing). Verify already refused a result
when the specimen was missing, not accepted, or expired. The worklist still
labeled that row Verify, and the entry panel still offered Verify posted
result.

### Implemented

- A row that cannot take results says Blocked. The block reason is the
  button title.
- Verify posted result is not offered while that gate is closed.
- The server still refuses verify on an unusable specimen.

### Requirements / risk

- URS-BB-219, FRS-BB-254, SRS-BB-216, RISK-BB-321.
- Does not claim AABB compliance.

### Tests

- TEST-BB-108 (`TestWorklistTests.PendingWorklist_ExpiredSpecimen_DoesNotOfferVerify`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 887.

### Next ranked residual

Next workflow family: unit preparation. SME items remain: OCD-001 (undetectable
antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 67 — Outdated retype does not release the unit (2026-09-28)

Workflow-audit loop tick (unit preparation). A matching front-type retype
moved a Received unit to Available. That path did not look at the unit
expiration, so an outdated unit became available stock.

### Implemented

- A matching retype on an outdated unit records the type and moves the
  unit to Expired.
- The pending retype board and the worklist Retype tab mark that unit
  Outdated before it is recorded.
- The save message says the unit was not released.
- A mismatch still quarantines. An in-date match still becomes Available.

### Requirements / risk

- URS-BB-220, FRS-BB-255, SRS-BB-217, RISK-BB-322.
- Does not claim AABB compliance.

### Tests

- TEST-BB-109 (`ProductRetypeServiceTests.MatchingRetype_OutdatedUnit_DoesNotBecomeAvailable`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 888.

### Next ranked residual

Next workflow family: assignment and issuing. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 68 — Ready to issue withholds an unusable specimen (2026-09-28)

Workflow-audit loop tick (assignment and issuing). Ready to issue already
showed a specimen expiration, and the issue gate already hard-stops a
missing or expired specimen. The row still offered Issue, including when
emergency release cannot pass that stop.

### Implemented

- Ready to issue reads the latest accepted specimen, the same specimen
  the issue gate uses.
- A missing, undated, or expired specimen is marked Specimen. The row
  links to the chart specimens tab and does not offer Issue or Emergency.
- A later in-date specimen clears that hold. Second ABO emergency release
  is unchanged when the specimen can be used.
- `SPEC-EXPIRED` and `ISS-SPEC-EXISTS` stay hard stops.

### Requirements / risk

- URS-BB-221, FRS-BB-256, SRS-BB-218, RISK-BB-323.
- Does not claim AABB compliance.

### Tests

- TEST-BB-110 (`Phase4IssuingTests.ListReadyToIssue_ExpiredSpecimen_DoesNotOfferIssue`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 889.

### Next ranked residual

Next workflow family: transfusion reactions. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 69 — Fatality follow-up is recorded once (2026-09-28)

Workflow-audit loop tick (transfusion reactions). A closed fatality stays
on the board until CBER notification and the written report are both
recorded. Both buttons stayed available. A second click restamped the
time, and recording CBER after the written report cleared that status.

### Implemented

- CBER notification and the written report are each recorded once, and
  only on a fatality.
- Recording the written report first leaves the status Pending.
- Recording CBER afterward keeps the written-report time.
- `/reactions` shows the recorded time instead of the button.
- `RXN-FATALITY-FOLLOWUP` stays a warning. Close is unchanged.

### Requirements / risk

- URS-BB-222, FRS-BB-257, SRS-BB-219, RISK-BB-324.
- Does not claim AABB compliance.

### Tests

- TEST-BB-111 (`ReactionInvestigationServiceTests.RecordFatalityFollowUp_SecondStamp_DoesNotRewrite`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 890.

### Next ranked residual

Next workflow family: lookback. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 70 — Lookback names the current recipient (2026-09-28)

Workflow-audit loop tick (lookback). Recall already opened one call for an
issued or transfused unit. That call used the first issue on the unit, so a
unit returned and issued again named the earlier patient.

### Implemented

- The notification uses the issue that still accounts for the unit.
- A transfused unit uses the transfused issue. An issued unit uses the
  open issue.
- An earlier returned issue is not the recipient.
- The row stores that issue and its transfusion when one exists.
- A later recall still does not add a second row.

### Requirements / risk

- URS-BB-223, FRS-BB-258, SRS-BB-220, RISK-BB-325.
- Does not claim AABB compliance.

### Tests

- TEST-BB-112 (`LookbackSearchAuthorizationTests.Recall_ReturnedThenReissued_NotifiesTheCurrentRecipient`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 891.

### Next ranked residual

Next workflow family: HL7 process. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 71 — Applied HL7 messages are not replayed (2026-09-28)

Workflow-audit loop tick (HL7 process). The message log offered Replay on
an inbound message that had already applied. Running it again could put an
older ADT back onto the current chart. An errored message still needs replay.

### Implemented

- Replay returns before it runs when the stored message is Processed,
  Acked, or Replayed.
- `/hl7` says Applied on those rows.
- An errored message can still be replayed. Replay allowed and Resolve
  are unchanged.

### Requirements / risk

- URS-BB-224, FRS-BB-259, SRS-BB-221, RISK-BB-326.
- Does not claim AABB compliance.

### Tests

- TEST-BB-113 (`Phase5Hl7Tests.Replay_AlreadyApplied_DoesNotRunAgain`).
- Full suite green: Domain 1390, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 891.

### Next ranked residual

Next workflow family: HL7 data load. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 72 — Closed orders do not accept an interface result (2026-09-28)

Workflow-audit loop tick (HL7 data load). An inbound result still posted
when the order was cancelled or discontinued. The pending worklist then
omitted that row, so the value was stored where the bench no longer shows it.

### Implemented

- An ORU for a cancelled or discontinued order does not post a result.
- The message is queued as a mapping error.
- An open order, including one on hold, can still receive a pending result.
- Specimen linking and OBX-11 acceptance are unchanged.

### Requirements / risk

- URS-BB-225, FRS-BB-260, SRS-BB-222, RISK-BB-327.
- Does not claim AABB compliance.

### Tests

- TEST-BB-114 (`Phase5Hl7Tests.InboundOru_CancelledOrder_DoesNotPost`).
- Full suite green: Domain 1396, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 892.

### Next ranked residual

Next workflow family: billing. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 73 — Export waits until the DFT is accepted (2026-09-28)

Workflow-audit loop tick (billing). Review already had to happen before
export. Export still cleared a reviewed charge while its DFT was only
queued, or had been rejected, so the charge left the queue before the
hospital accepted it.

### Implemented

- Export is refused while the charge's DFT is not accepted.
- `/billing` says DFT unsent on that row instead of Export.
- A charge with no DFT can still be exported after review.
- Cancel and the unsent-DFT withhold are unchanged.

### Requirements / risk

- URS-BB-226, FRS-BB-261, SRS-BB-223, RISK-BB-328.
- Does not claim AABB compliance.

### Tests

- TEST-BB-115 (`Phase7BillingTests.Export_UnacceptedDft_StaysOnTheQueue`).
- Full suite green: Domain 1402, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 893.

### Next ranked residual

Next workflow family: patient testing. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 74 — Held orders do not take results (2026-09-28)

Workflow-audit loop tick (patient testing). The bench pending list already
omitted an order on hold. The patient Tests tab still offered Enter, and
verify still accepted a posted result on that order.

### Implemented

- A held order on the patient Tests tab says Blocked.
- Result entry and verify refuse that order.
- The bench pending list still omits held orders.
- Releasing the order from hold is unchanged.

### Requirements / risk

- URS-BB-227, FRS-BB-262, SRS-BB-224, RISK-BB-329.
- Does not claim AABB compliance.

### Tests

- TEST-BB-116 (`TestWorklistTests.PendingWorklist_HeldOrder_DoesNotOfferEnter`).
- Full suite green: Domain 1402, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 894.

### Next ranked residual

Next workflow family: unit preparation. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 75 — Outdated expected units are not received (2026-09-28)

Workflow-audit loop tick (unit preparation). A packing-list unit that
passed its expiration still offered Confirm arrival. Receipt put it on
the retype path instead of keeping it out of stock.

### Implemented

- Expected inbound marks that unit Outdated.
- Confirm arrival is refused and is not offered.
- The row and the unit page still return it to the supplier.
- An in-date late arrival can still be received.

### Requirements / risk

- URS-BB-228, FRS-BB-263, SRS-BB-225, RISK-BB-330.
- Does not claim AABB compliance.

### Tests

- TEST-BB-117 (`InventoryServiceTests.ReceiveExpected_OutdatedUnit_DoesNotEnterStock`).
- Full suite green: Domain 1402, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 895.

### Next ranked residual

Next workflow family: assignment and issuing. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 76 — Ready to issue withholds an outdated unit (2026-09-28)

Workflow-audit loop tick (assignment and issuing). A reserved unit that
passed its expiration still offered Issue on Ready to issue and on the
patient chart. The issue gate already refused that unit, including
emergency release.

### Implemented

- Ready to issue marks that unit Outdated and does not offer Issue or Emergency.
- The patient chart marks the same reservation Outdated and does not offer Issue.
- Release of the reservation is unchanged.
- An in-date reserved unit can still be issued. `UNIT-EXPIRED` stays a hard stop.

### Requirements / risk

- URS-BB-229, FRS-BB-264, SRS-BB-226, RISK-BB-331.
- Does not claim AABB compliance.

### Tests

- TEST-BB-118 (`Phase4IssuingTests.ListReadyToIssue_OutdatedUnit_DoesNotOfferIssue`).
- Full suite green: Domain 1402, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 896.

### Next ranked residual

Next workflow family: transfusion reactions. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 77 — Fatal severity starts fatality follow-up (2026-09-28)

Workflow-audit loop tick (transfusion reactions). The fatality checkbox
starts CBER and written-report follow-up. Choosing severity Fatal saved
the case as an ordinary reaction, so that follow-up never started.

### Implemented

- Severity Fatal marks the investigation as a fatality.
- The board shows the same follow-up that the fatality checkbox starts.
- A later save does not move the written-report due date.
- A non-fatal severity does not start that follow-up.
- Close and the once-only CBER and written-report steps are unchanged.

### Requirements / risk

- URS-BB-230, FRS-BB-265, SRS-BB-227, RISK-BB-332.
- Does not claim AABB compliance. The LIS does not file with FDA.

### Tests

- TEST-BB-119 (`ReactionInvestigationServiceTests.Update_FatalSeverity_StartsFatalityFollowUp`).
- Full suite green: Domain 1402, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 897.

### Next ranked residual

Next workflow family: lookback. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 78 — A finished lookback call stays finished (2026-09-28)

Workflow-audit loop tick (lookback). Completing a call removed it from
the open board. The DIN report still offered Record attempt, and that
save replaced the physician and put the call back in progress.

### Implemented

- A completed or not-applicable notification refuses another attempt.
- The DIN report says Finished on those rows.
- The open board still records an attempt on a pending or attempted call.
- The physician and note gates are unchanged.

### Requirements / risk

- URS-BB-231, FRS-BB-266, SRS-BB-228, RISK-BB-333.
- Does not claim AABB compliance.

### Tests

- TEST-BB-120 (`LookbackSearchAuthorizationTests.RecordAttempt_FinishedNotification_DoesNotChange`).
- Full suite green: Domain 1402, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 898.

### Next ranked residual

Next workflow family: HL7 process. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 79 — An applied control id is not replayed from an errored copy (2026-09-28)

Workflow-audit loop tick (HL7 process). A later copy of a control id can
apply after an earlier copy failed. The error queue and the message log
still offered Replay on that earlier copy, and replay ran it again.

### Implemented

- Replay refuses an errored inbound message when another inbound message
  with the same control id is processed, acknowledged, or replayed.
- The error queue and the message log say Applied on that copy.
- Resolve still clears the leftover error. An errored message with no
  applied copy can still be replayed.

### Requirements / risk

- URS-BB-232, FRS-BB-267, SRS-BB-229, RISK-BB-334.
- Does not claim AABB compliance.

### Tests

- TEST-BB-121 (`Phase5Hl7Tests.Replay_ErroredCopy_WhenSiblingAlreadyApplied_DoesNotRunAgain`).
- Full suite green: Domain 1402, Application 38, HL7 63, Printing 21,
  ASTM 19, Integration 899.

### Next ranked residual

Next workflow family: HL7 data load. SME items remain: OCD-001
(undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 80 — Compatible units rank like commercial selection (2026-09-29)

WellSky alerts when a patient has autologous or directed products. SafeTrace
and SoftBank put the best usable unit ahead of a flat expiry list. This list
still offered another patient's restricted unit and ranked a short-dated
group O unit ahead of type-specific stock.

### Implemented

- Another patient's autologous or directed unit is omitted from the list.
- This patient's restricted units come first, then ABO/Rh-identical stock,
  then a compatible substitute.
- Within a band, antigen-negative precedes untested, which precedes a unit
  that needs an antigen-negative override. Shortest expiration still orders
  the band.
- The Products tab warns when that patient has an available restricted unit.
  Both unit pickers show the cue.
- Reserve of another patient's restricted unit still hard-stops.

### Requirements / risk

- URS-BB-233, FRS-BB-268, SRS-BB-230, RISK-BB-335.
- Does not claim AABB compliance. This is a selection order, not a compatibility grade.

### Tests

- TEST-BB-122 (`PatientAllocationTests.ListCompatibleUnits_RanksOwnRestrictedAndIdenticalBeforeSubstitute`; `ProductSelectionOrderTests`).
- Full suite green: Domain 1406, Application 38, HL7 63, Printing 21, ASTM 19, Integration 900.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 81 — Outdated units are not offered for selection (2026-09-29)

WellSky, SafeTrace, and SoftBank do not offer an outdated component on the
selection list. This list still included an Available unit at or past
expiration. Issue already refused that unit.

### Implemented

- The compatible-unit list omits a unit at or past expiration.
- An in-date unit stays on the list.
- The unit status is unchanged.
- The Products tab says an outdated unit is not offered.
- `UNIT-EXPIRED` stays the issue-gate hard stop.

### Requirements / risk

- URS-BB-234, FRS-BB-269, SRS-BB-231, RISK-BB-336.
- Does not claim AABB compliance.

### Tests

- TEST-BB-123 (`PatientAllocationTests.ListCompatibleUnits_OmitsOutdatedUnit`).
- Full suite green: Domain 1406, Application 38, HL7 63, Printing 21, ASTM 19, Integration 901.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 82 — Short-dated units are marked at selection (2026-09-29)

SafeTrace and SoftBank show a unit that is inside the outdate window while
the technologist is still choosing it. This list already sorted by expiration
and the inventory board already used `Inventory.NearExpiryWarningHours`. The
picker did not say the unit expires soon.

### Implemented

- A compatible unit inside that window is marked expires soon.
- A unit outside the window is not marked.
- Both stay on the list. Status is unchanged.
- The window is the existing facility policy. When policy is absent, it is 24 hours.
- The Products and Orders pickers show the mark. The Products tab says so.
- `UNIT-NEAR-EXPIRY` stays the issue-gate warning.

### Requirements / risk

- URS-BB-235, FRS-BB-270, SRS-BB-232, RISK-BB-337.
- Does not claim AABB compliance.

### Tests

- TEST-BB-124 (`PatientAllocationTests.ListCompatibleUnits_MarksUnitInsideNearExpiryWindow`).
- Full suite green: Domain 1406, Application 38, HL7 63, Printing 21, ASTM 19, Integration 902.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 83 — Units that fail a special requirement are not offered (2026-09-29)

WellSky, SafeTrace, and SoftBank do not offer a unit that fails an irradiated,
CMV-negative, leukoreduced, washed, or antigen-negative special need. This list
still included that unit. Issue already refused it, and that hard stop has no
override definition.

### Implemented

- The compatible-unit list omits a unit that fails an active product-attribute or antigen-negative requirement.
- A requirement that is only an issue acknowledgment, an ABO subgroup, or a complex crossmatch does not remove units.
- The unit status is unchanged.
- The Products tab says those units are not offered.
- `ISS-SPECIAL-REQ` stays the issue-gate hard stop.

### Requirements / risk

- URS-BB-236, FRS-BB-271, SRS-BB-233, RISK-BB-338.
- Does not claim AABB compliance.

### Tests

- TEST-BB-125 (`SpecialTransfusionRequirementRuleTests` unit-selection cases; `PatientAllocationTests.ListCompatibleUnits_OmitsUnitThatFailsSpecialRequirement`; `PatientAllocationTests.ListCompatibleUnits_KeepsUnitForAcknowledgmentOnlyRequirement`).
- Full suite green: Domain 1408, Application 38, HL7 63, Printing 21, ASTM 19, Integration 904.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 84 — The chart shows the specimen the issue gate will read (2026-09-29)

WellSky and SafeTrace show the current specimen and its expiration on the
patient banner. Ready to issue already marks a missing or expired specimen.
The chart header did not.

### Implemented

- The header names the latest accepted specimen.
- A missing, undated, or expired specimen is a warning that blocks issue.
- A specimen inside the existing 12-hour issue-gate window says it expires soon.
- An older accepted specimen is not shown as current.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-237, FRS-BB-272, SRS-BB-234, RISK-BB-339.
- Does not claim AABB compliance.

### Tests

- TEST-BB-126 (`CurrentSpecimenBannerTests`).
- Full suite green: Domain 1412, Application 38, HL7 63, Printing 21, ASTM 19, Integration 904.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 85 — A current crossmatch is listed ahead of unmatched stock (2026-09-29)

SafeTrace and SoftBank put a unit that already has a compatible crossmatch
in front of unmatched stock. This list still ordered only by expiration
inside each selection group.

### Implemented

- Within each group, a unit with a current compatible crossmatch for this patient is listed first.
- The picker says Crossmatch compatible.
- Another patient's crossmatch does not count.
- An incompatible result does not count.
- A crossmatch at or past its expiration does not count.
- A unit without that crossmatch stays on the list.
- Issue uses the same predicate.

### Requirements / risk

- URS-BB-238, FRS-BB-273, SRS-BB-235, RISK-BB-340.
- Does not claim AABB compliance.

### Tests

- TEST-BB-127 (`CrossmatchRuleTests.CurrentCompatible_MatchesTheIssuePredicate`; `PatientAllocationTests.ListCompatibleUnits_ListsCurrentCrossmatchAheadOfShorterDatedUnit`).
- Full suite green: Domain 1413, Application 38, HL7 63, Printing 21, ASTM 19, Integration 905.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 86 — The chart shows a missing second ABO/Rh (2026-09-29)

SafeTrace and SoftBank show on the patient banner when red-cell issue still
needs a second concordant ABO/Rh. Ready to issue already badges that.
The chart header showed only the current type.

### Implemented

- When the facility requires a second ABO/Rh and the current type has no concordant historical row, the chart says red-cell issue needs that type.
- When the second type is on file, the blood-type stat says so.
- The notice is absent when the facility requirement is off, and when there is no current type.
- `ISS-SECOND-ABO` stays the issue-gate hard stop.

### Requirements / risk

- URS-BB-239, FRS-BB-274, SRS-BB-236, RISK-BB-341.
- Does not claim AABB compliance.

### Tests

- TEST-BB-128 (`SecondAboDeterminationRuleTests.ChartNotice_OnlyWhenRequiredAndCurrentTypeLacksAMatch`).
- Full suite green: Domain 1414, Application 38, HL7 63, Printing 21, ASTM 19, Integration 905.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 87 — An incompatible crossmatch is listed after unmatched stock (2026-09-30)

SafeTrace and SoftBank keep a unit that already crossmatched incompatible for this patient behind unmatched stock and label it. The compatible list ordered that unit with unmatched stock by expiration.

### Implemented

- A unit with an incompatible crossmatch for this patient, and no current compatible crossmatch, is listed after unmatched units in the same group and marked Crossmatch incompatible.
- The unit stays on the list. An expired incompatible result still counts.
- Another patient's incompatible result does not mark or demote the unit.
- A later current compatible crossmatch still ranks first and is marked Crossmatch compatible.
- Issue uses the same incompatible predicate. `ISS-XM-REQUIRED` stays the override warning.

### Requirements / risk

- URS-BB-240, FRS-BB-275, SRS-BB-237, RISK-BB-342.
- Does not claim AABB compliance.

### Tests

- TEST-BB-129 (`CrossmatchRuleTests.CountsAsIncompatible_MatchesTheIssuePredicate`; `PatientAllocationTests.ListCompatibleUnits_ListsIncompatibleCrossmatchAfterUnmatchedUnit`).
- Full suite green: Domain 1415, Application 38, HL7 63, Printing 21, ASTM 19, Integration 906.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 88 — The chart shows why electronic crossmatch is not eligible (2026-09-30)

SafeTrace and SoftBank show the computer-crossmatch block on the patient banner. This chart badge said Not eligible, and the reason stayed on the Overview tab.

### Implemented

- When electronic crossmatch is not eligible, the chart header shows the existing blocking reason on every tab.
- An eligible result, or a missing reason, shows no notice.
- The overview uses the same notice.
- Eligibility criteria are unchanged. A currently undetectable antibody still blocks electronic crossmatch (OCD-001).

### Requirements / risk

- URS-BB-241, FRS-BB-276, SRS-BB-238, RISK-BB-343.
- Does not claim AABB compliance.

### Tests

- TEST-BB-130 (`ElectronicCrossmatchBannerTests.Notice_ShowsBlockingReasonOnlyWhenNotEligible`).
- Full suite green: Domain 1416, Application 38, HL7 63, Printing 21, ASTM 19, Integration 906.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 89 — Ready to issue marks an open antibody identification (2026-09-30)

WellSky and SafeTrace flag an unfinished antibody identification on the issue worklist. Ready to issue showed antibodies, specimen, and second ABO, and the open-workup warning appeared only when issue ran.

### Implemented

- A reserved unit is marked Open ABID when the patient has an open antibody-identification workup.
- The mark uses the existing `ABID-ISSUE-OPEN` warning. A completed workup is not marked.
- The row stays on the board and can still be issued.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-242, FRS-BB-277, SRS-BB-239, RISK-BB-344.
- Does not claim AABB compliance.

### Tests

- TEST-BB-131 (`AntibodyIdentificationHistoryPostRuleTests.ReadyToIssueNotice_UsesTheIssueWarning`; `Phase4IssuingTests.ListReadyToIssue_FlagsOpenAntibodyWorkupWithoutRemovingTheRow`).
- Full suite green: Domain 1417, Application 38, HL7 63, Printing 21, ASTM 19, Integration 907.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 90 — Ready to issue marks a short-dated unit (2026-09-30)

WellSky and SafeTrace flag a short-dated unit on the issue worklist. Ready to issue marked only a unit already past expiration.

### Implemented

- A reserved unit inside the facility near-expiry window is marked Expires soon.
- A completed thaw uses the shorter post-thaw window, the same window issue uses.
- An outdated unit stays Outdated and is not marked Expires soon.
- The row stays issuable. `UNIT-NEAR-EXPIRY` stays the issue-gate warning.

### Requirements / risk

- URS-BB-243, FRS-BB-278, SRS-BB-240, RISK-BB-345.
- Does not claim AABB compliance.

### Tests

- TEST-BB-132 (`Phase4IssuingTests.ListReadyToIssue_MarksUnitInsideTheIssueNearExpiryWindow`).
- Full suite green: Domain 1417, Application 38, HL7 63, Printing 21, ASTM 19, Integration 908.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 91 — Ready to issue shows why electronic crossmatch is blocked (2026-09-30)

SafeTrace and SoftBank show the computer-crossmatch block on the issue worklist. Ready to issue showed the latest crossmatch result, and the blocking reason stayed on the patient chart.

### Implemented

- A reserved unit shows the existing electronic-crossmatch blocking reason.
- An eligible patient shows no notice.
- The row stays issuable for serologic issue.
- Eligibility criteria are unchanged. A currently undetectable antibody still blocks electronic crossmatch (OCD-001).

### Requirements / risk

- URS-BB-244, FRS-BB-279, SRS-BB-241, RISK-BB-346.
- Does not claim AABB compliance.

### Tests

- TEST-BB-133 (`Phase4IssuingTests.ListReadyToIssue_ShowsElectronicCrossmatchBlockingReason`).
- Full suite green: Domain 1417, Application 38, HL7 63, Printing 21, ASTM 19, Integration 909.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 92 — Ready to issue marks a unit that still needs a crossmatch (2026-09-30)

WellSky, SafeTrace, and SoftBank show a missing or incompatible crossmatch on the issue worklist. Ready to issue showed the latest result and still offered routine issue.

### Implemented

- A reserved unit with no current compatible crossmatch is marked with the existing standard-issue message and is not offered as a routine issue.
- An incompatible result is marked and stays issuable. The existing override still applies.
- A current compatible result, including one recorded after an incompatible result, is not marked. Another patient's result does not count.
- A product that does not require a crossmatch is not marked.
- Emergency release and the issue gate are unchanged.

### Requirements / risk

- URS-BB-245, FRS-BB-280, SRS-BB-242, RISK-BB-347.
- Does not claim AABB compliance.

### Tests

- TEST-BB-134 (`CrossmatchRuleTests.DescribeReadyToIssue_MatchesTheStandardIssueEvaluation`; `Phase4IssuingTests.ListReadyToIssue_MarksMissingCrossmatchAndKeepsIncompatibleIssuable`).
- Full suite green: Domain 1418, Application 38, HL7 63, Printing 21, ASTM 19, Integration 910.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 93 — Ready to issue marks an unmet special requirement (2026-09-30)

WellSky, SafeTrace, and SoftBank show an unmet special need on the issue worklist. Ready to issue still offered routine issue and emergency release for a reserved unit that fails an irradiated, CMV-negative, leukoreduced, washed, or antigen-negative requirement.

### Implemented

- A reserved unit that fails one of those active requirements is marked with the existing hard-stop message.
- Routine issue and emergency release are not offered. The issue gate already hard-stops both.
- A unit that meets the requirement is not marked.
- An acknowledgment-only requirement and an ended requirement are not marked.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-246, FRS-BB-281, SRS-BB-243, RISK-BB-348.
- Does not claim AABB compliance.

### Tests

- TEST-BB-135 (`SpecialTransfusionRequirementRuleTests.ReadyToIssueNotice_UsesTheUnitFactHardStop`; `Phase4IssuingTests.ListReadyToIssue_MarksUnmetSpecialRequirementWithoutOfferingIssue`).
- Full suite green: Domain 1419, Application 38, HL7 63, Printing 21, ASTM 19, Integration 911.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 94 — Ready to issue marks a unit that is not antigen-negative (2026-09-30)

WellSky, SafeTrace, and SoftBank flag an antigen warning on the issue worklist. Ready to issue showed the patient's antibodies and still offered issue without that warning.

### Implemented

- A reserved unit that is not antigen-negative for a clinically significant antibody is marked with the existing warning.
- The row stays on the board and can still be issued. The existing override still applies.
- A unit already antigen-negative for that antibody is not marked.
- A patient without that antibody is not marked.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-247, FRS-BB-282, SRS-BB-244, RISK-BB-349.
- Does not claim AABB compliance.

### Tests

- TEST-BB-136 (`BloodAttributeCompatibilityRuleTests.ReadyToIssueNotice_UsesTheIssueWarning`; `Phase4IssuingTests.ListReadyToIssue_MarksAntigenWarningWithoutRemovingTheRow`).
- Full suite green: Domain 1420, Application 38, HL7 63, Printing 21, ASTM 19, Integration 912.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 95 — Ready to issue marks an unresolved ABO/Rh discrepancy (2026-09-30)

WellSky, SafeTrace, and SoftBank show an ABO/Rh discrepancy on the issue worklist. Ready to issue still offered issue when the current type disagreed with the historical record.

### Implemented

- A reserved crossmatch-required unit is marked with the existing hard stop and is not offered for routine or emergency issue.
- A product that does not require a crossmatch is marked with the existing warning and stays issuable.
- A concordant history is not marked.
- The issue gate uses the same discrepancy result and the same history predicate.

### Requirements / risk

- URS-BB-248, FRS-BB-283, SRS-BB-245, RISK-BB-350.
- Does not claim AABB compliance.

### Tests

- TEST-BB-137 (`IssueGateTests.DescribeAboRhDiscrepancy_MatchesTheIssueGate`; `Phase4IssuingTests.ListReadyToIssue_MarksUnresolvedAboRhDiscrepancy`).
- Full suite green: Domain 1421, Application 38, HL7 63, Printing 21, ASTM 19, Integration 913.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 96 — The chart shows an unresolved ABO/Rh discrepancy (2026-09-30)

WellSky, SafeTrace, and SoftBank show an ABO/Rh discrepancy on the patient banner. This chart showed the disagreement when a result was entered and on the issue desk, and not on the other chart tabs.

### Implemented

- When the current ABO/Rh disagrees with history, the chart header shows the existing warning on every tab.
- A concordant history shows no notice.
- The product-specific hard stop stays on issue.

### Requirements / risk

- URS-BB-249, FRS-BB-284, SRS-BB-246, RISK-BB-351.
- Does not claim AABB compliance.

### Tests

- TEST-BB-138 (`IssueGateTests.PatientBannerNotice_UsesTheDisagreementWarning`).
- Full suite green: Domain 1422, Application 38, HL7 63, Printing 21, ASTM 19, Integration 913.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 97 — The chart shows a recorded transfusion reaction (2026-09-30)

WellSky, SafeTrace, and SoftBank keep a prior transfusion reaction on the patient banner. This chart showed that fact on product history and not on the other tabs.

### Implemented

- When product history already marks a transfusion reaction suspected, the chart header shows that fact on every tab.
- A history with no suspected reaction shows no notice.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-250, FRS-BB-285, SRS-BB-247, RISK-BB-352.
- Does not claim AABB compliance.

### Tests

- TEST-BB-139 (`ReactionHistoryBannerTests.Notice_NamesARecordedReactionAndStaysQuietOtherwise`).
- Full suite green: Domain 1423, Application 38, HL7 63, Printing 21, ASTM 19, Integration 913.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 98 — Ready to issue shows a recorded transfusion reaction (2026-09-30)

WellSky, SafeTrace, and SoftBank show a prior transfusion reaction on the issue queue. This desk showed that fact on the chart and not on Ready to issue.

### Implemented

- When a reserved unit's patient has a transfusion already recorded as reaction suspected, Ready to issue shows the same sentence as the chart.
- A transfusion that was not a suspected reaction is not marked.
- The row stays issuable.

### Requirements / risk

- URS-BB-251, FRS-BB-286, SRS-BB-248, RISK-BB-353.
- Does not claim AABB compliance.

### Tests

- TEST-BB-140 (`Phase4IssuingTests.ListReadyToIssue_MarksRecordedReactionWithoutRemovingTheRow`).
- Full suite green: Domain 1423, Application 38, HL7 63, Printing 21, ASTM 19, Integration 914.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 99 — The issue form repeats Ready to issue marks (2026-09-30)

WellSky, SafeTrace, and SoftBank keep the unit's warnings on the issue transaction. This desk showed those marks on the queue and not on the form after Issue or Emergency.

### Implemented

- Choosing Issue or Emergency repeats that row's existing marks on the issue form while the same unit and patient stay selected.
- A row with no marks adds no notice.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-252, FRS-BB-287, SRS-BB-249, RISK-BB-354.
- Does not claim AABB compliance.

### Tests

- TEST-BB-141 (`ReadyToIssueFormCueTests.Notices_RepeatTheReadyToIssueMarks`).
- Full suite green: Domain 1423, Application 39, HL7 63, Printing 21, ASTM 19, Integration 914.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 100 — Ready to issue names an autologous or directed unit (2026-09-30)

WellSky, SafeTrace, and SoftBank mark a patient's own autologous or directed unit on the issue queue. This desk showed that cue on the chart picker and not on Ready to issue.

### Implemented

- When a reserved unit is this patient's autologous or directed unit, Ready to issue shows the existing selection cue.
- An allogeneic unit is not marked.
- The row stays issuable. The issue gate still refuses another patient's restricted unit.
- Choosing Issue or Emergency repeats that cue on the issue form.

### Requirements / risk

- URS-BB-253, FRS-BB-288, SRS-BB-250, RISK-BB-355.
- Does not claim AABB compliance.

### Tests

- TEST-BB-142 (`Phase4IssuingTests.ListReadyToIssue_MarksThisPatientsAutologousAndDirectedUnits`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 101 — Ready to issue shows the product name (2026-09-30)

WellSky, SafeTrace, and SoftBank show the product on the issue queue. This desk showed the unit number and blood type, and not the product name.

### Implemented

- Ready to issue shows the product name of each reserved unit.
- Choosing Issue or Emergency repeats that name on the issue form.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-254, FRS-BB-289, SRS-BB-251, RISK-BB-356.
- Does not claim AABB compliance.

### Tests

- TEST-BB-143 (`Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 102 — Issued queues show the product name (2026-09-30)

WellSky, SafeTrace, and SoftBank keep the product name on a unit after it leaves the ready queue. Retrospective crossmatch due, in transit, and issued units showed the unit number and blood type, and not the product.

### Implemented

- Those three queues show the product name of the issued unit.
- Receive repeats that name on ward receipt. Return and Document repeat it on those forms.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-255, FRS-BB-290, SRS-BB-252, RISK-BB-357.
- Does not claim AABB compliance.

### Tests

- TEST-BB-144 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 103 — Ready to issue shows when the unit expires (2026-09-30)

WellSky, SafeTrace, and SoftBank show the unit expiration on the issue queue. This desk marked a unit only after it entered the near-expiry window, and did not show the expiration time.

### Implemented

- Ready to issue shows when each reserved unit expires.
- Choosing Issue or Emergency repeats that time on the issue form.
- The near-expiry mark and the outdated hard stop are unchanged.

### Requirements / risk

- URS-BB-256, FRS-BB-291, SRS-BB-253, RISK-BB-358.
- Does not claim AABB compliance.

### Tests

- TEST-BB-145 (`Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 104 — Issued queues show when the unit expires (2026-09-30)

WellSky, SafeTrace, and SoftBank keep the unit expiration visible after it leaves the ready queue. Retrospective crossmatch due, in transit, and issued units showed the product and not when it expires.

### Implemented

- Those three queues show when the issued unit expires.
- Receive repeats that time on ward receipt. Return and Document repeat it on those forms.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-257, FRS-BB-292, SRS-BB-254, RISK-BB-359.
- Does not claim AABB compliance.

### Tests

- TEST-BB-146 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 105 — The chart shows the unit expiration time (2026-09-30)

WellSky, SafeTrace, and SoftBank show the product and the expiration time when staff review or choose a unit. This chart listed the product code without the name or the expiration, and unit choices showed the date without the time.

### Implemented

- Allocated products show the product name and when the unit expires, including the time.
- Unit choices use that same expiration sentence.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-258, FRS-BB-293, SRS-BB-255, RISK-BB-360.
- Does not claim AABB compliance.

### Tests

- TEST-BB-147 (`PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 106 — Ready to issue shows the unit ABO/Rh (2026-09-30)

WellSky, SafeTrace, and SoftBank show the unit ABO/Rh beside the patient type on the issue queue. This desk labeled one Type column, and that column was the patient.

### Implemented

- Ready to issue shows the unit ABO/Rh next to the patient type.
- Choosing Issue or Emergency repeats that unit type on the issue form.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-259, FRS-BB-294, SRS-BB-256, RISK-BB-361.
- Does not claim AABB compliance.

### Tests

- TEST-BB-148 (`Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 107 — Issued queues show the unit ABO/Rh (2026-09-30)

WellSky, SafeTrace, and SoftBank keep the unit ABO/Rh visible after it leaves the ready queue. Retrospective crossmatch due, in transit, and issued units labeled one Type column, and that column was the patient.

### Implemented

- Those three queues show the unit ABO/Rh beside the patient type.
- Receive repeats that type on ward receipt. Return and Document repeat it on those forms.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-260, FRS-BB-295, SRS-BB-257, RISK-BB-362.
- Does not claim AABB compliance.

### Tests

- TEST-BB-149 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 108 — The chart shows the unit ABO/Rh (2026-09-30)

WellSky, SafeTrace, and SoftBank show the unit ABO/Rh when staff review a patient's allocated products. This chart listed the product and the expiration, and not the unit type.

### Implemented

- Allocated products show the unit ABO/Rh with the product name and expiration.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-261, FRS-BB-296, SRS-BB-258, RISK-BB-363.
- Does not claim AABB compliance.

### Tests

- TEST-BB-150 (`PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved`).
- Full suite green: Domain 1424, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 109 — Ready to issue shows when the crossmatch expires (2026-09-30)

WellSky, SafeTrace, and SoftBank show when a current compatible crossmatch expires. This desk showed the crossmatch result and not that time.

### Implemented

- Ready to issue shows the expiration of the current compatible crossmatch. An undated current compatible crossmatch has no clock. A lapsed or incompatible result is not given that time.
- Choosing Issue or Emergency repeats that time on the issue form.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-262, FRS-BB-297, SRS-BB-259, RISK-BB-364.
- Does not claim AABB compliance.

### Tests

- TEST-BB-151 (`Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate`).
- Full suite green: Domain 1425, Application 39, HL7 63, Printing 21, ASTM 19, Integration 915.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 110 — Ready to issue shows where the unit is stored (2026-09-30)

WellSky, SafeTrace, and SoftBank show the refrigerator or other storage site on the issue worklist. This desk showed the unit number and not that location.

### Implemented

- Ready to issue shows the current storage location of each reserved unit. A blank location name falls back to the location code. A unit with no location shows none.
- Choosing Issue or Emergency repeats that location on the issue form.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-263, FRS-BB-298, SRS-BB-260, RISK-BB-365.
- Does not claim AABB compliance.

### Tests

- TEST-BB-152 (`Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 111 — The chart shows where the unit is stored (2026-09-30)

WellSky, SafeTrace, and SoftBank show the storage site when staff select or review a unit for a patient. This chart showed the unit number and not that location.

### Implemented

- The patient product list shows the current storage location of each allocated unit. A blank location name falls back to the location code. A unit with no location shows none.
- Unit choices on the chart repeat that location. Selecting a unit shows the same location beside the expiration.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-264, FRS-BB-299, SRS-BB-261, RISK-BB-366.
- Does not claim AABB compliance.

### Tests

- TEST-BB-153 (`PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 112 — Ready to issue shows the specimen accession (2026-09-30)

WellSky, SafeTrace, and SoftBank show which specimen the issue is tied to. This desk showed when that specimen expires and not its accession.

### Implemented

- Ready to issue shows the accession of the current specimen for each reserved unit. A patient with no current specimen shows none.
- Choosing Issue or Emergency repeats that accession on the issue form.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-265, FRS-BB-300, SRS-BB-262, RISK-BB-367.
- Does not claim AABB compliance.

### Tests

- TEST-BB-154 (`Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 113 — Issued queues show the specimen accession (2026-09-30)

WellSky, SafeTrace, and SoftBank show which specimen an issued unit is tied to. In transit and issued units showed when that specimen expires and not its accession.

### Implemented

- In transit and issued units show the specimen accession tied to that issue. A unit with no linked specimen shows none.
- Receive repeats that accession on ward receipt. Return and Document repeat it on those forms.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-266, FRS-BB-301, SRS-BB-263, RISK-BB-368.
- Does not claim AABB compliance.

### Tests

- TEST-BB-155 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 114 — Issued queues show when the crossmatch expires (2026-09-30)

WellSky, SafeTrace, and SoftBank keep the crossmatch expiration visible after a unit leaves the ready queue. Retrospective crossmatch due, in transit, and issued units showed the specimen and not when a current compatible crossmatch ends.

### Implemented

- Those three queues show when a current compatible crossmatch for that patient and unit expires. A unit with no current compatible crossmatch shows none.
- Receive repeats that time on ward receipt. Return and Document repeat it on those forms.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-267, FRS-BB-302, SRS-BB-264, RISK-BB-369.
- Does not claim AABB compliance.

### Tests

- TEST-BB-156 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 115 — The chart shows when the crossmatch expires (2026-09-30)

WellSky, SafeTrace, and SoftBank show when a crossmatch expires while staff review a patient's allocated products. This chart showed the unit and not that time.

### Implemented

- Allocated products show when a current compatible crossmatch for that unit expires. A unit with no current compatible crossmatch shows none.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-268, FRS-BB-303, SRS-BB-265, RISK-BB-370.
- Does not claim AABB compliance.

### Tests

- TEST-BB-157 (`PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 116 — Unit choices show when the crossmatch expires (2026-09-30)

WellSky, SafeTrace, and SoftBank show when a crossmatch expires while staff choose a unit. This chart marked a choice Crossmatch compatible and did not show when that crossmatch ends.

### Implemented

- Unit choices show when a current compatible crossmatch expires, after the Crossmatch compatible cue. A choice with no current compatible crossmatch shows none.
- Selecting a unit repeats that time.
- The orders tab uses the same choice label.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-269, FRS-BB-304, SRS-BB-266, RISK-BB-371.
- Does not claim AABB compliance.

### Tests

- TEST-BB-158 (`PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 117 — Issued queues show the crossmatch result (2026-09-30)

WellSky, SafeTrace, and SoftBank show the crossmatch interpretation after a unit leaves the ready queue. Retrospective crossmatch due, in transit, and issued units showed a crossmatch time or a blank, so an incompatible result looked like testing that had not been done.

### Implemented

- Those three queues show the latest crossmatch result for that patient and unit. A unit with no crossmatch result shows none.
- A current compatible expiration stays beside that result.
- Receive repeats the result and that expiration. Return and Document repeat them on those forms.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-270, FRS-BB-305, SRS-BB-267, RISK-BB-372.
- Does not claim AABB compliance.

### Tests

- TEST-BB-159 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 118 — Allocated products show the crossmatch result (2026-09-30)

WellSky, SafeTrace, and SoftBank show the crossmatch interpretation on the patient product list. Allocated products showed a crossmatch time or a blank, so an incompatible or expired result looked like testing that had not been done.

### Implemented

- Allocated products show the latest crossmatch result for that patient and unit. A unit with no crossmatch result, including NotPerformed, shows none.
- A current compatible expiration stays beside that result.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-271, FRS-BB-306, SRS-BB-268, RISK-BB-373.
- Does not claim AABB compliance.

### Tests

- TEST-BB-160 (`PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 119 — Ready to issue repeats the crossmatch result (2026-09-30)

WellSky, SafeTrace, and SoftBank repeat the crossmatch interpretation on the issue form. Ready to issue showed the result on the row, but Issue and Emergency repeated only a crossmatch time, so an incompatible or expired result looked like testing that had not been done once the form opened.

### Implemented

- Ready to issue shows the latest crossmatch result for that patient and unit. A unit with no crossmatch result, including NotPerformed, shows none.
- A current compatible expiration stays beside that result.
- Issue and Emergency repeat the result and that expiration.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-272, FRS-BB-307, SRS-BB-269, RISK-BB-374.
- Does not claim AABB compliance.

### Tests

- TEST-BB-161 (`ReadyToIssueFormCueTests.Notices_RepeatTheReadyToIssueMarks`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 120 — Unit choices show the crossmatch result (2026-09-30)

WellSky, SafeTrace, and SoftBank show the crossmatch interpretation while a unit is being chosen. Unit choices showed a crossmatch time or a blank, so an incompatible or expired result looked like testing that had not been done.

### Implemented

- Unit choices and the selected unit show the latest crossmatch result for that patient and unit. A unit with no crossmatch result, including NotPerformed, shows none.
- A current compatible expiration stays beside a compatible result.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-273, FRS-BB-308, SRS-BB-270, RISK-BB-375.
- Does not claim AABB compliance.

### Tests

- TEST-BB-162 (`PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 121 — Product history shows the crossmatch result (2026-09-30)

WellSky, SafeTrace, and SoftBank show the crossmatch interpretation on the patient's product history. Assigned, issued, returned, and transfused rows left Compatibility blank, so an incompatible result looked like testing that had not been done.

### Implemented

- Those rows show the latest crossmatch result for that patient and unit. A unit with no crossmatch result, including NotPerformed, shows none.
- A crossmatch event shows that event's result. A blank event shows none.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-274, FRS-BB-309, SRS-BB-271, RISK-BB-376.
- Does not claim AABB compliance.

### Tests

- TEST-BB-163 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 122 — Product history shows the linked specimen (2026-09-30)

WellSky, SafeTrace, and SoftBank show which specimen was used when staff review what was issued. Product history stored that accession on the allocation and left it off the issued, returned, and transfused rows.

### Implemented

- Issued, returned, and transfused rows show the specimen accession linked through that issue's allocation.
- A row with no linked specimen shows none.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-275, FRS-BB-310, SRS-BB-272, RISK-BB-377.
- Does not claim AABB compliance.

### Tests

- TEST-BB-164 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 123 — Product history shows when the unit expires (2026-09-30)

WellSky, SafeTrace, and SoftBank show the unit outdate when staff review what was issued. Product history stored that expiration on the unit and left it off the history rows.

### Implemented

- Each assigned, issued, returned, transfused, and crossmatch row shows when that unit expires.
- A row with no unit expiration shows none.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-276, FRS-BB-311, SRS-BB-273, RISK-BB-378.
- Does not claim AABB compliance.

### Tests

- TEST-BB-165 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 124 — Product history shows who performed the event (2026-09-30)

WellSky, SafeTrace, and SoftBank show who issued, assigned, crossmatched, returned, or documented a unit when staff review the patient's product history. That name was stored on the event and left off the chart.

### Implemented

- Each assigned, issued, returned, transfused, and crossmatch row shows who performed that event.
- A row with no performer shows none.
- Allocation and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-277, FRS-BB-312, SRS-BB-274, RISK-BB-379.
- Does not claim AABB compliance.

### Tests

- TEST-BB-166 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 125 — Product history shows the return reason (2026-09-30)

WellSky, SafeTrace, and SoftBank show why a unit came back and whether it can be reissued when staff review the patient's product history. That reason and decision were stored on the return and left off the chart.

### Implemented

- A returned row shows the return reason and whether the unit was marked eligible to reissue.
- A blank reason shows none. A row that is not a return shows none for those facts.
- Allocation, return, and reissue eligibility are unchanged.

### Requirements / risk

- URS-BB-278, FRS-BB-313, SRS-BB-275, RISK-BB-380.
- Does not claim AABB compliance.

### Tests

- TEST-BB-167 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 126 — Product history shows who the unit was issued to (2026-09-30)

WellSky, SafeTrace, and SoftBank show the person who received a unit when staff review the patient's product history. Product history showed the location and left that person off the issued, returned, and transfused rows.

### Implemented

- Issued, returned, and transfused rows show the person recorded on the issue.
- A row with no issued-to person shows none.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-279, FRS-BB-314, SRS-BB-276, RISK-BB-381.
- Does not claim AABB compliance.

### Tests

- TEST-BB-168 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 127 — Issue queues show who the unit was issued to (2026-09-30)

WellSky, SafeTrace, and SoftBank show the person who received a unit on the in-transit and issued worklists. Those queues showed the location and left that person off.

### Implemented

- The in-transit and issued queues show the person recorded on the issue.
- A unit with no issued-to person shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-280, FRS-BB-315, SRS-BB-277, RISK-BB-382.
- Does not claim AABB compliance.

### Tests

- TEST-BB-169 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 39, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 128 — Receive, Return, and Document name who the unit was issued to (2026-09-30)

WellSky, SafeTrace, and SoftBank repeat the person who received a unit when staff confirm custody, send it back, or document the transfusion. Those forms left that person off.

### Implemented

- Receive, Return, and Document repeat the person recorded on the issue.
- A unit with no issued-to person shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-281, FRS-BB-316, SRS-BB-278, RISK-BB-383.
- Does not claim AABB compliance.

### Tests

- TEST-BB-170 (`IssuedToNoticeTests.IssuedToNotice_NamesThePerson`).
- Full suite green: Domain 1447, Application 40, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 129 — Retrospective crossmatch names who the unit was issued to (2026-09-30)

WellSky, SafeTrace, and SoftBank show the person who received an uncrossmatched unit while staff still owe the retrospective crossmatch. That queue left the person off.

### Implemented

- The retrospective crossmatch queue shows the person recorded on the issue.
- A unit with no issued-to person shows none.
- Retrospective crossmatch completion is unchanged.

### Requirements / risk

- URS-BB-282, FRS-BB-317, SRS-BB-279, RISK-BB-384.
- Does not claim AABB compliance.

### Tests

- TEST-BB-171 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 40, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 130 — Product history shows the cooler (2026-09-30)

WellSky, SafeTrace, and SoftBank show which cooler or transport container left with an issued unit when staff review the patient's product history. That id was stored on the issue and left off the chart.

### Implemented

- Issued, returned, and transfused rows show the cooler or transport id recorded on the issue.
- A row with no cooler shows none.
- In-transit and issue eligibility are unchanged.

### Requirements / risk

- URS-BB-283, FRS-BB-318, SRS-BB-280, RISK-BB-385.
- Does not claim AABB compliance.

### Tests

- TEST-BB-172 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 40, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 131 — Receive, Return, and Document name the cooler (2026-09-30)

WellSky, SafeTrace, and SoftBank repeat the cooler or transport container when staff confirm custody, send a unit back, or document the transfusion. Those forms left that id off.

### Implemented

- Receive, Return, and Document repeat the cooler or transport id recorded on the issue.
- A unit with no cooler shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-284, FRS-BB-319, SRS-BB-281, RISK-BB-386.
- Does not claim AABB compliance.

### Tests

- TEST-BB-173 (`CoolerNoticeTests.CoolerNotice_NamesTheContainer`).
- Full suite green: Domain 1447, Application 41, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 132 — Issued queue shows the cooler (2026-09-30)

WellSky, SafeTrace, and SoftBank show which cooler or transport container left with a unit that is still issued. The in-transit queue already showed that id. The outstanding issued queue left it off.

### Implemented

- The issued-units queue shows the cooler or transport id recorded on the issue.
- A unit with no cooler shows none. The id stays after ward receipt.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-285, FRS-BB-320, SRS-BB-282, RISK-BB-387.
- Does not claim AABB compliance.

### Tests

- TEST-BB-174 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 41, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 133 — Retrospective queue shows the cooler (2026-09-30)

WellSky, SafeTrace, and SoftBank show which cooler or transport container left with an uncrossmatched emergency or MTP unit. The retrospective queue showed who received it and left the container off.

### Implemented

- The retrospective crossmatch queue shows the cooler or transport id recorded on the issue.
- A unit with no cooler shows none.
- Retrospective completion is unchanged.

### Requirements / risk

- URS-BB-286, FRS-BB-321, SRS-BB-283, RISK-BB-388.
- Does not claim AABB compliance.

### Tests

- TEST-BB-175 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 41, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 134 — Retrospective crossmatch names custody (2026-09-30)

WellSky, SafeTrace, and SoftBank keep who received an uncrossmatched unit, and which cooler carried it, in front of the technologist recording the follow-up crossmatch. That screen only said the form was prefilled from the retrospective queue.

### Implemented

- The retrospective crossmatch screen repeats the issued-to person and the cooler or transport id from the pending issue.
- A unit with neither shows none.
- Recording the crossmatch is unchanged.

### Requirements / risk

- URS-BB-287, FRS-BB-322, SRS-BB-284, RISK-BB-389.
- Does not claim AABB compliance.

### Tests

- TEST-BB-176 (`RetrospectiveFollowUpNoticeTests.RetrospectiveFollowUpNotice_NamesCustody`).
- Full suite green: Domain 1447, Application 42, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 135 — Issued queue shows the destination (2026-09-30)

WellSky, SafeTrace, and SoftBank show where an outstanding issued unit went. The in-transit queue already showed that location. The issued queue left it off.

### Implemented

- The issued-units queue shows the location recorded on the issue.
- A unit with no location shows none. The location stays after ward receipt.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-288, FRS-BB-323, SRS-BB-285, RISK-BB-390.
- Does not claim AABB compliance.

### Tests

- TEST-BB-177 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 42, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 136 — Receive, Return, and Document name the ward (2026-09-30)

WellSky, SafeTrace, and SoftBank repeat the destination when staff confirm custody, send a unit back, or document the transfusion. Those forms left the location off.

### Implemented

- Receive, Return, and Document repeat the location recorded on the issue.
- A unit with no location shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-289, FRS-BB-324, SRS-BB-286, RISK-BB-391.
- Does not claim AABB compliance.

### Tests

- TEST-BB-178 (`IssuedToLocationNoticeTests.IssuedToLocationNotice_NamesTheWard`).
- Full suite green: Domain 1447, Application 43, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 137 — Retrospective follow-up names the ward (2026-09-30)

WellSky, SafeTrace, and SoftBank show where an uncrossmatched emergency or MTP unit went while staff follow it up. The retrospective queue and the crossmatch screen left that location off.

### Implemented

- The retrospective queue shows the location recorded on the issue.
- The follow-up crossmatch screen repeats that location with the person and cooler.
- A unit with no location shows none.
- Retrospective completion and recording the crossmatch are unchanged.

### Requirements / risk

- URS-BB-290, FRS-BB-325, SRS-BB-287, RISK-BB-392.
- Does not claim AABB compliance.

### Tests

- TEST-BB-179 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 43, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 138 — Product history shows the issue type (2026-09-30)

WellSky, SafeTrace, and SoftBank show whether a unit left as a routine issue, an emergency release, or another recorded issue type when staff review the patient's product history. That history left the type off.

### Implemented

- Issued, returned, and transfused rows show the issue type recorded on the issue.
- A row that is not one of those events shows none.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-291, FRS-BB-326, SRS-BB-288, RISK-BB-393.
- Does not claim AABB compliance.

### Tests

- TEST-BB-180 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 43, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 139 — Issued queues show the issue type (2026-09-30)

WellSky, SafeTrace, and SoftBank show whether a unit still out was a routine issue, an emergency release, or another recorded issue type. The retrospective queue already showed that type. The in-transit and outstanding issued queues left it off.

### Implemented

- The in-transit and outstanding issued queues show the issue type recorded on the issue.
- The type stays after ward receipt.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-292, FRS-BB-327, SRS-BB-289, RISK-BB-394.
- Does not claim AABB compliance.

### Tests

- TEST-BB-181 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 43, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 140 — Issued queues show who released the unit (2026-09-30)

WellSky, SafeTrace, and SoftBank show which technologist released a unit that is still out. Product history already names the performer of an issue event. The in-transit and outstanding issued queues left that name off.

### Implemented

- The in-transit and outstanding issued queues show who issued the unit.
- The name stays after ward receipt. A blank name shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-293, FRS-BB-328, SRS-BB-290, RISK-BB-395.
- Does not claim AABB compliance.

### Tests

- TEST-BB-182 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 43, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 141 — Receive, Return, and Document name who released the unit (2026-09-30)

WellSky, SafeTrace, and SoftBank repeat the releasing technologist while staff receive, return, or document a unit still out. The in-transit and outstanding issued queues already show that name. Receive, Return, and Document left it off.

### Implemented

- Receive, Return, and Document repeat who issued the unit.
- A blank name shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-294, FRS-BB-329, SRS-BB-291, RISK-BB-396.
- Does not claim AABB compliance.

### Tests

- TEST-BB-183 (`IssuedByNoticeTests.IssuedByNotice_NamesTheTechnologist`).
- Full suite green: Domain 1447, Application 44, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 142 — Retrospective queue shows who released the unit (2026-09-30)

WellSky, SafeTrace, and SoftBank show which technologist released an uncrossmatched unit while staff complete the retrospective crossmatch. The in-transit and outstanding issued queues, and Receive, Return, and Document, already show that name. The retrospective queue left it off.

### Implemented

- The retrospective crossmatch queue shows who issued the unit.
- A blank name shows none.
- Retrospective completion is unchanged.

### Requirements / risk

- URS-BB-295, FRS-BB-330, SRS-BB-292, RISK-BB-397.
- Does not claim AABB compliance.

### Tests

- TEST-BB-184 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 44, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 143 — Retrospective follow-up names who released the unit (2026-09-30)

WellSky, SafeTrace, and SoftBank repeat the releasing technologist on the screen where staff record the retrospective crossmatch. The retrospective queue already shows that name. The follow-up screen left it off.

### Implemented

- The retrospective follow-up screen repeats who issued the unit, after the recipient, cooler, and ward.
- A blank name shows none.
- Recording the crossmatch is unchanged.

### Requirements / risk

- URS-BB-296, FRS-BB-331, SRS-BB-293, RISK-BB-398.
- Does not claim AABB compliance.

### Tests

- TEST-BB-185 (`RetrospectiveFollowUpNoticeTests.RetrospectiveFollowUpNotice_NamesCustody`).
- Full suite green: Domain 1447, Application 44, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 144 — Retrospective queue shows the release reason (2026-09-30)

WellSky, SafeTrace, and SoftBank show why an uncrossmatched unit was released while staff complete the retrospective crossmatch. The issue already records that reason. The retrospective queue left it off.

### Implemented

- The retrospective crossmatch queue shows the recorded emergency-release reason.
- A blank reason shows none.
- Retrospective completion is unchanged.

### Requirements / risk

- URS-BB-297, FRS-BB-332, SRS-BB-294, RISK-BB-399.
- Does not claim AABB compliance.

### Tests

- TEST-BB-186 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 44, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 145 — Retrospective follow-up shows the release reason (2026-09-30)

WellSky, SafeTrace, and SoftBank repeat why an uncrossmatched unit was released on the screen where staff record the retrospective crossmatch. The retrospective queue already shows that reason. The follow-up screen left it off.

### Implemented

- The retrospective follow-up screen repeats the recorded emergency-release reason.
- A blank reason shows none.
- Recording the crossmatch is unchanged.

### Requirements / risk

- URS-BB-298, FRS-BB-333, SRS-BB-295, RISK-BB-400.
- Does not claim AABB compliance.

### Tests

- TEST-BB-187 (`EmergencyReleaseReasonNoticeTests.EmergencyReleaseReasonNotice_NamesTheReason`).
- Full suite green: Domain 1447, Application 45, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 146 — Product history shows the release reason (2026-09-30)

WellSky, SafeTrace, and SoftBank show why an emergency or massive-transfusion unit was released when staff review that patient's product history. The issue already records that reason. Product history showed the issue type and left the reason off.

### Implemented

- Issued, returned, and transfused rows show the recorded emergency-release reason.
- A row that is not one of those events shows none. A blank reason shows none.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-299, FRS-BB-334, SRS-BB-296, RISK-BB-401.
- Does not claim AABB compliance.

### Tests

- TEST-BB-188 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 45, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 147 — Issued queues show the release reason (2026-09-30)

WellSky, SafeTrace, and SoftBank show why an emergency or massive-transfusion unit was released while that unit is still issued. The retrospective queue and product history already show that reason. The in-transit and outstanding issued queues showed the issue type and left the reason off.

### Implemented

- Both queues show the recorded emergency-release reason, including after ward receipt.
- A blank reason shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-300, FRS-BB-335, SRS-BB-297, RISK-BB-402.
- Does not claim AABB compliance.

### Tests

- TEST-BB-189 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 45, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 148 — Receive, Return, and Document repeat the release reason (2026-10-01)

WellSky, SafeTrace, and SoftBank repeat why an emergency or massive-transfusion unit was released when staff receive, return, or document it. The issued queues already show that reason. Receive, Return, and Document left it off.

### Implemented

- Receive, Return, and Document repeat the recorded emergency-release reason as its own sentence.
- A blank reason shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-301, FRS-BB-336, SRS-BB-298, RISK-BB-403.
- Does not claim AABB compliance.

### Tests

- TEST-BB-190 (`EmergencyReleaseReasonNoticeTests.EmergencyReleaseReasonNotice_NamesTheReason`).
- Full suite green: Domain 1447, Application 45, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 149 — Issued queues show the second verifier (2026-10-01)

WellSky, SafeTrace, and SoftBank show who witnessed a release while that unit is still issued. The issue already records that second verifier. The in-transit and outstanding issued queues showed who issued the unit and left the witness off.

### Implemented

- Both queues show the second verifier recorded at issue, including after ward receipt.
- A blank second verifier shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-302, FRS-BB-337, SRS-BB-299, RISK-BB-404.
- Does not claim AABB compliance.

### Tests

- TEST-BB-191 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 45, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Receive, Return, and Document still leave the second verifier off. The retrospective queue and product history do too.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 150 — Receive, Return, and Document name the second verifier (2026-10-01)

WellSky, SafeTrace, and SoftBank repeat who witnessed a release while staff receive, return, or document a unit still out. The issued queues already show that second verifier. Receive, Return, and Document left it off.

### Implemented

- Receive, Return, and Document repeat the second verifier recorded at issue.
- A blank second verifier shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-303, FRS-BB-338, SRS-BB-300, RISK-BB-405.
- Does not claim AABB compliance.

### Tests

- TEST-BB-192 (`SecondVerifierNoticeTests.SecondVerifierNotice_NamesTheWitness`).
- Full suite green: Domain 1447, Application 46, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The retrospective queue and product history still leave the second verifier off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 151 — Retrospective queue shows the second verifier (2026-10-01)

WellSky, SafeTrace, and SoftBank show who witnessed an uncrossmatched release while staff complete the retrospective crossmatch. The issued queues and Receive, Return, and Document already show that second verifier. The retrospective queue left it off.

### Implemented

- The retrospective crossmatch queue shows the second verifier recorded at issue.
- A blank second verifier shows none.
- Retrospective completion is unchanged.

### Requirements / risk

- URS-BB-304, FRS-BB-339, SRS-BB-301, RISK-BB-406.
- Does not claim AABB compliance.

### Tests

- TEST-BB-193 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 46, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The retrospective follow-up screen and product history still leave the second verifier off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 152 — Retrospective follow-up names the second verifier (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat who witnessed an uncrossmatched release on the screen where staff record the retrospective crossmatch. The retrospective queue already shows that second verifier. The follow-up screen left it off.

### Implemented

- The retrospective follow-up screen repeats the second verifier recorded at issue, after the recipient, cooler, ward, and issuing technologist.
- A blank second verifier shows none.
- Recording the crossmatch is unchanged.

### Requirements / risk

- URS-BB-305, FRS-BB-340, SRS-BB-302, RISK-BB-407.
- Does not claim AABB compliance.

### Tests

- TEST-BB-194 (`RetrospectiveFollowUpNoticeTests.RetrospectiveFollowUpNotice_NamesCustody`).
- Full suite green: Domain 1447, Application 46, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Product history still leaves the second verifier off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 153 — Product history shows the second verifier (2026-10-02)

WellSky, SafeTrace, and SoftBank show who witnessed a release when staff review that patient's product history. The issue already records that second verifier. Issued queues, custody forms, and the retrospective screens already show it. Product history left it off.

### Implemented

- Issued, returned, and transfused rows show the second verifier recorded at issue.
- A row that is not one of those events shows none. A blank second verifier shows none.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-306, FRS-BB-341, SRS-BB-303, RISK-BB-408.
- Does not claim AABB compliance.

### Tests

- TEST-BB-195 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 46, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 154 — Retrospective queue shows who authorized the release (2026-10-02)

WellSky, SafeTrace, and SoftBank show who authorized an uncrossmatched emergency or massive-transfusion release while staff complete the retrospective crossmatch. The override already records that person. The retrospective queue showed the reason and the second verifier and left the authorizer off.

### Implemented

- The retrospective crossmatch queue shows who authorized the emergency or massive-transfusion release.
- A blank authorizer shows none.
- Retrospective completion is unchanged.

### Requirements / risk

- URS-BB-307, FRS-BB-342, SRS-BB-304, RISK-BB-409.
- Does not claim AABB compliance.

### Tests

- TEST-BB-196 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 46, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The retrospective follow-up screen, issued queues, and product history still leave the release authorizer off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 155 — Retrospective follow-up names who authorized the release (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat who authorized an uncrossmatched release on the screen where staff record the retrospective crossmatch. The retrospective queue already shows that authorizer. The follow-up screen left it off.

### Implemented

- The retrospective follow-up screen repeats who authorized the emergency or massive-transfusion release.
- A blank authorizer shows none.
- Recording the crossmatch is unchanged.

### Requirements / risk

- URS-BB-308, FRS-BB-343, SRS-BB-305, RISK-BB-410.
- Does not claim AABB compliance.

### Tests

- TEST-BB-197 (`EmergencyReleaseAuthorizerNoticeTests.EmergencyReleaseAuthorizerNotice_NamesTheAuthorizer`).
- Full suite green: Domain 1447, Application 47, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The issued queues and product history still leave the release authorizer off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 156 — Issued queues show who authorized the release (2026-10-02)

WellSky, SafeTrace, and SoftBank show who authorized an emergency or massive-transfusion release while that unit is still issued. The retrospective queue and follow-up screen already show that authorizer. The in-transit and outstanding issued queues left it off.

### Implemented

- Both queues show who authorized the emergency or massive-transfusion release, including after ward receipt.
- A blank authorizer shows none.
- Return, ward-receipt, and bedside identity gates are unchanged.

### Requirements / risk

- URS-BB-309, FRS-BB-344, SRS-BB-306, RISK-BB-411.
- Does not claim AABB compliance.

### Tests

- TEST-BB-198 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 47, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Receive, Return, Document, and product history still leave the release authorizer off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 157 — Receive, Return, and Document show who authorized the release (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat who authorized an emergency or massive-transfusion release on the forms that receive, return, or document that unit. The issued queues already show the authorizer. Receive, Return, and Document left that name off.

### Implemented

- Receive, Return, and Document repeat who authorized the emergency or massive-transfusion release.
- A blank authorizer shows none.
- Ward receipt, return, and transfusion documentation gates are unchanged.

### Requirements / risk

- URS-BB-310, FRS-BB-345, SRS-BB-307, RISK-BB-412.
- Does not claim AABB compliance.

### Tests

- TEST-BB-199 (`EmergencyReleaseAuthorizerNoticeTests.EmergencyReleaseAuthorizerNotice_NamesTheAuthorizer`).
- Full suite green: Domain 1447, Application 47, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Product history still leaves the release authorizer off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 158 — Product history shows who authorized the release (2026-10-02)

WellSky, SafeTrace, and SoftBank show who authorized an emergency or massive-transfusion release on the patient's product history. Issued queues, the follow-up screen, and Receive, Return, and Document already show that authorizer. Issued, returned, and transfused history rows left it off.

### Implemented

- Issued, returned, and transfused product-history rows show who authorized the emergency or massive-transfusion release.
- Other rows and a blank authorizer show none.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-311, FRS-BB-346, SRS-BB-308, RISK-BB-413.
- Does not claim AABB compliance.

### Tests

- TEST-BB-200 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 47, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The release authorizer is now shown on the issued queues, the follow-up screen, Receive, Return, and Document, and product history.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 159 — Issued queues show the appearance recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the visual inspection recorded when a unit left the blood bank while that unit is still issued. The issue record already stores that coded appearance. The in-transit and outstanding issued queues left it off.

### Implemented

- Both queues show the appearance recorded at issue, including after ward receipt.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-312, FRS-BB-347, SRS-BB-309, RISK-BB-414.
- Does not claim AABB compliance.

### Tests

- TEST-BB-201 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 47, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Receive, Return, Document, the retrospective queue, and product history still leave the appearance recorded at issue off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 160 — Receive, Return, and Document show the appearance recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat the visual inspection recorded when a unit left the blood bank on the forms that receive, return, or document that unit. The issued queues already show that appearance. Receive, Return, and Document left it off.

### Implemented

- Receive, Return, and Document repeat the appearance recorded at issue.
- Ward receipt, return, transfusion documentation, and the issue gate are unchanged.

### Requirements / risk

- URS-BB-313, FRS-BB-348, SRS-BB-310, RISK-BB-415.
- Does not claim AABB compliance.

### Tests

- TEST-BB-202 (`IssueAppearanceNoticeTests.IssueAppearanceNotice_NamesTheCode`).
- Full suite green: Domain 1447, Application 48, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The retrospective queue and product history still leave the appearance recorded at issue off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 161 — Retrospective queue shows the appearance recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the visual inspection recorded when a unit left the blood bank on the retrospective crossmatch queue. The issued queues and Receive, Return, and Document already show that appearance. The retrospective queue left it off.

### Implemented

- The retrospective crossmatch queue shows the appearance recorded at issue.
- Retrospective completion is unchanged.

### Requirements / risk

- URS-BB-314, FRS-BB-349, SRS-BB-311, RISK-BB-416.
- Does not claim AABB compliance.

### Tests

- TEST-BB-203 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 48, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The follow-up screen and product history still leave the appearance recorded at issue off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 162 — Follow-up screen shows the appearance recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat the visual inspection recorded when a unit left the blood bank on the retrospective follow-up screen. The retrospective queue already shows that appearance. The follow-up screen left it off.

### Implemented

- The retrospective follow-up screen repeats the appearance recorded at issue.
- Recording the crossmatch is unchanged.

### Requirements / risk

- URS-BB-315, FRS-BB-350, SRS-BB-312, RISK-BB-417.
- Does not claim AABB compliance.

### Tests

- TEST-BB-204 (`IssueAppearanceNoticeTests.IssueAppearanceNotice_NamesTheCode`).
- Full suite green: Domain 1447, Application 48, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Product history still leaves the appearance recorded at issue off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 163 — Product history shows the appearance recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the visual inspection recorded when a unit left the blood bank on the patient's product history. Issued queues, Receive, Return, Document, the retrospective queue, and the follow-up screen already show that appearance. Issued, returned, and transfused history rows left it off.

### Implemented

- Issued, returned, and transfused product-history rows show the appearance recorded at issue.
- Other rows show none.
- Issue eligibility is unchanged.

### Requirements / risk

- URS-BB-316, FRS-BB-351, SRS-BB-313, RISK-BB-418.
- Does not claim AABB compliance.

### Tests

- TEST-BB-205 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 48, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The appearance recorded at issue is now shown on the issued queues, Receive, Return, and Document, the retrospective queue, the follow-up screen, and product history.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 164 — Outstanding queue shows the appearance recorded at ward receipt (2026-10-02)

WellSky, SafeTrace, and SoftBank show the visual inspection recorded when the receiving location acknowledged a unit that is still issued. Ward receipt already stores that coded appearance. The outstanding issued queue left it off.

### Implemented

- The outstanding issued queue shows the appearance recorded at ward receipt after the unit is received.
- A unit still awaiting receipt shows none.
- The ward-receipt gate is unchanged.

### Requirements / risk

- URS-BB-317, FRS-BB-352, SRS-BB-314, RISK-BB-419.
- Does not claim AABB compliance.

### Tests

- TEST-BB-206 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 48, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Return, Document, and product history still leave the ward appearance off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 165 — Return and Document show the appearance recorded at ward receipt (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat the visual inspection recorded when the receiving location acknowledged a unit on the forms that return or document that unit. The outstanding issued queue already shows that appearance. Return and Document left it off.

### Implemented

- Return and Document repeat the appearance recorded at ward receipt.
- A unit that has not been received shows none.
- Return, transfusion documentation, and the ward-receipt gate are unchanged.

### Requirements / risk

- URS-BB-318, FRS-BB-353, SRS-BB-315, RISK-BB-420.
- Does not claim AABB compliance.

### Tests

- TEST-BB-207 (`WardAppearanceNoticeTests.WardAppearanceNotice_NamesTheCode`).
- Full suite green: Domain 1447, Application 49, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Product history still leaves the ward appearance off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 166 — Product history shows the appearance recorded at ward receipt (2026-10-02)

WellSky, SafeTrace, and SoftBank show the visual inspection recorded when the receiving location acknowledged a unit on the patient's product history. The outstanding issued queue and Return and Document already show that appearance. Issued, returned, and transfused history rows left it off.

### Implemented

- Issued, returned, and transfused product-history rows show the appearance recorded at ward receipt after the unit is received.
- Other rows, and a unit that has not been received, show none.
- The ward-receipt gate is unchanged.

### Requirements / risk

- URS-BB-319, FRS-BB-354, SRS-BB-316, RISK-BB-421.
- Does not claim AABB compliance.

### Tests

- TEST-BB-208 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 49, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The ward appearance is now shown on the outstanding issued queue, Return, Document, and product history.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 167 — Issued queues show the pickup tag scanned at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the pickup tag scanned when a unit left the blood bank on the worklists for units still issued. Issue already stores that code. The in-transit and outstanding issued queues left it off.

### Implemented

- The in-transit and outstanding issued queues show the pickup tag scanned at issue.
- The code stays after ward receipt.
- A blank pickup tag shows none.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-320, FRS-BB-355, SRS-BB-317, RISK-BB-422.
- Does not claim AABB compliance.

### Tests

- TEST-BB-209 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 49, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Receive, Return, Document, the retrospective queue, the follow-up screen, and product history still leave the pickup tag off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 168 — Receive, Return, and Document show the pickup tag scanned at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat the pickup tag scanned when a unit left the blood bank on the forms that receive, return, or document that unit. The in-transit and outstanding issued queues already show that code. Receive, Return, and Document left it off.

### Implemented

- Receive, Return, and Document repeat the pickup tag scanned at issue.
- A blank pickup tag shows none.
- Ward receipt, return, transfusion documentation, and the issue gate are unchanged.

### Requirements / risk

- URS-BB-321, FRS-BB-356, SRS-BB-318, RISK-BB-423.
- Does not claim AABB compliance.

### Tests

- TEST-BB-210 (`PtagNoticeTests.PtagNotice_NamesTheCode`).
- Full suite green: Domain 1447, Application 50, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The retrospective queue, the follow-up screen, and product history still leave the pickup tag off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 169 — Retrospective queue shows the pickup tag scanned at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the pickup tag scanned when a unit left the blood bank on the retrospective crossmatch worklist. The issued queues and Receive, Return, and Document already show that code. The retrospective queue left it off.

### Implemented

- The retrospective crossmatch queue shows the pickup tag scanned at issue.
- A blank pickup tag shows none.
- Retrospective completion is unchanged.

### Requirements / risk

- URS-BB-322, FRS-BB-357, SRS-BB-319, RISK-BB-424.
- Does not claim AABB compliance.

### Tests

- TEST-BB-211 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 50, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The follow-up screen and product history still leave the pickup tag off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 170 — Follow-up screen shows the pickup tag scanned at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat the pickup tag scanned when a unit left the blood bank on the screen where retrospective compatibility is recorded. The retrospective queue already shows that code. The follow-up screen left it off.

### Implemented

- The retrospective follow-up screen repeats the pickup tag scanned at issue.
- A blank pickup tag shows none.
- Recording the crossmatch is unchanged.

### Requirements / risk

- URS-BB-323, FRS-BB-358, SRS-BB-320, RISK-BB-425.
- Does not claim AABB compliance.

### Tests

- TEST-BB-212 (`PtagNoticeTests.PtagNotice_NamesTheCode`).
- Full suite green: Domain 1447, Application 50, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Product history still leaves the pickup tag off.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 171 — Product history shows the pickup tag scanned at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the pickup tag scanned when a unit left the blood bank on the patient's product history. The issued queues, Receive, Return, Document, the retrospective queue, and the follow-up screen already show that code. Issued, returned, and transfused history rows left it off.

### Implemented

- Issued, returned, and transfused product-history rows show the pickup tag scanned at issue.
- Other rows and a blank pickup tag show none.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-324, FRS-BB-359, SRS-BB-321, RISK-BB-426.
- Does not claim AABB compliance.

### Tests

- TEST-BB-213 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 50, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The pickup tag scanned at issue is now shown on the issued queues, Receive, Return, Document, the retrospective queue, the follow-up screen, and product history.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 172 — Issued queues show the crossmatch status recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the crossmatch status recorded when a unit left the blood bank, separate from the latest crossmatch result. Issue already stores that status. The in-transit and outstanding issued queues left it off.

### Implemented

- The in-transit and outstanding issued queues show the crossmatch status recorded at issue.
- The status stays after ward receipt.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-325, FRS-BB-360, SRS-BB-322, RISK-BB-427.
- Does not claim AABB compliance.

### Tests

- TEST-BB-214 (`Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt`).
- Full suite green: Domain 1447, Application 50, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Receive, Return, Document, the retrospective queue, the follow-up screen, and product history still leave off the crossmatch status recorded at issue.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 173 — Receive, Return, and Document show the crossmatch status recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat the crossmatch status recorded when a unit left the blood bank on the forms that receive, return, or document that unit. The in-transit and outstanding issued queues already show that status. Receive, Return, and Document left it off.

### Implemented

- Receive, Return, and Document repeat the crossmatch status recorded at issue.
- Ward receipt, return, transfusion documentation, and the issue gate are unchanged.

### Requirements / risk

- URS-BB-326, FRS-BB-361, SRS-BB-323, RISK-BB-428.
- Does not claim AABB compliance.

### Tests

- TEST-BB-215 (`CrossmatchStatusNoticeTests.CrossmatchStatusNotice_NamesTheStatus`).
- Full suite green: Domain 1447, Application 51, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The retrospective queue, the follow-up screen, and product history still leave off the crossmatch status recorded at issue.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 174 — Retrospective queue shows the crossmatch status recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the crossmatch status recorded when a unit left the blood bank on the retrospective crossmatch worklist, separate from the latest crossmatch result. The issued queues and Receive, Return, and Document already show that status. The retrospective queue left it off.

### Implemented

- The retrospective crossmatch queue shows the crossmatch status recorded at issue.
- Retrospective completion is unchanged.

### Requirements / risk

- URS-BB-327, FRS-BB-362, SRS-BB-324, RISK-BB-429.
- Does not claim AABB compliance.

### Tests

- TEST-BB-216 (`Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm`).
- Full suite green: Domain 1447, Application 51, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
The follow-up screen and product history still leave off the crossmatch status recorded at issue.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 175 — Follow-up screen shows the crossmatch status recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank repeat the crossmatch status recorded when a unit left the blood bank on the screen where retrospective compatibility is recorded. The retrospective queue already shows that status. The follow-up screen left it off.

### Implemented

- The retrospective follow-up screen repeats the crossmatch status recorded at issue.
- Recording the crossmatch is unchanged.

### Requirements / risk

- URS-BB-328, FRS-BB-363, SRS-BB-325, RISK-BB-430.
- Does not claim AABB compliance.

### Tests

- TEST-BB-217 (`CrossmatchStatusNoticeTests.CrossmatchStatusNotice_NamesTheStatus`).
- Full suite green: Domain 1447, Application 51, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next commercial gap versus WellSky, SafeTrace, and SoftBank, one coherent slice.
Product history still leaves off the crossmatch status recorded at issue.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).

## Iteration 176 — Product history shows the crossmatch status recorded at issue (2026-10-02)

WellSky, SafeTrace, and SoftBank show the crossmatch status recorded when a unit left the blood bank on the patient's product history, separate from the latest crossmatch result. The issued queues, Receive, Return, Document, the retrospective queue, and the follow-up screen already show that status. Issued, returned, and transfused history rows left it off.

### Implemented

- Issued, returned, and transfused product-history rows show the crossmatch status recorded at issue.
- Other rows show none.
- The issue gate is unchanged.

### Requirements / risk

- URS-BB-329, FRS-BB-364, SRS-BB-326, RISK-BB-431.
- Does not claim AABB compliance.

### Tests

- TEST-BB-218 (`Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped`).
- Full suite green: Domain 1447, Application 51, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

The crossmatch status recorded at issue is now shown on the issued queues, Receive, Return, Document, the retrospective queue, the follow-up screen, and product history.
Re-rank the next residual by safety and operational impact. Do not add another display-only field for a fact that is already on those surfaces.
SME items remain: OCD-001 (undetectable antibody vs eXM), OCD-004 (licensed extract not loaded).
