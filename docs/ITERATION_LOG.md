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

## Iteration 177 — Keep electronic crossmatch blocked by any antibody history (2026-10-02)

The crossmatch-status display chain is complete. The next ranked residual was OCD-001: whether a historically identified, currently undetectable antibody permanently precludes electronic crossmatch. The decision is to keep the existing hard stop. No eligibility code changed.

### Implemented

- OCD-001 is closed. Any antibody-history row, including a deactivated row, still blocks electronic crossmatch (`XM-EC-HISTORY`).
- Antigen-negative requirements still evaluate all history rows.
- Other electronic-crossmatch eligibility criteria are unchanged.

### Requirements / risk

- URS-BB-330, FRS-BB-365, SRS-BB-327, RISK-BB-432.
- Does not claim AABB compliance.

### Tests

- TEST-BB-219 (`ElectronicXmHistoryRegressionTests.DeactivatedAntibody_BlocksElectronicXm_WithExplainableReason`).
- Full suite green: Domain 1447, Application 51, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not add another display-only field for a fact already shown on the issue surfaces.
OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 178 — Dashboard shows the bench queues (2026-10-03)

The crossmatch-status display chain is complete, and OCD-001 is closed. The next operational gap is the start of a shift: the dashboard counted patients and inventory but did not show the queues a technologist still has to clear.

### Implemented

- The dashboard shows retrospective crossmatch due, outstanding issued units, pending testing, and reaction investigations that are open or under review.
- Each queue appears only when the signed-in user already has the matching permission.
- A closed investigation is not counted.
- A queue that cannot be loaded shows an em dash instead of zero.
- Issue, result, and investigation gates are unchanged.

### Requirements / risk

- URS-BB-331, FRS-BB-366, SRS-BB-328, RISK-BB-433.
- Does not claim AABB compliance.

### Tests

- TEST-BB-220 (`OpenReactionInvestigationRuleTests.Count_IncludesOpenAndUnderReview_AndOmitsClosed`).
- Full suite green: Domain 1447, Application 52, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not add another display-only field for a fact already shown on the issue surfaces.
OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 179 — Dashboard shows units still in transit (2026-10-03)

The shift dashboard listed outstanding issued units but not units that had left the bank and were still awaiting ward receipt. Those units already have an arrival window and an overdue flag.

### Implemented

- The dashboard shows an in-transit count for users who can already issue or document transfusion.
- When any of those units are past the arrival window, the count says how many are overdue.
- A queue that cannot be loaded shows an em dash instead of zero.
- Ward receipt is unchanged.

### Requirements / risk

- URS-BB-332, FRS-BB-367, SRS-BB-329, RISK-BB-434.
- Does not claim AABB compliance.

### Tests

- TEST-BB-221 (`InTransitAttentionRuleTests.CountOverdue_IncludesOnlyFlagsAlreadyPastTheArrivalWindow`).
- Full suite green: Domain 1447, Application 53, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

The dashboard still does not say how many retrospective crossmatches are already overdue, or how many reaction investigations still need fatality follow-up. Do not add another display-only field on the issue surfaces.
OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 180 — Dashboard shows overdue retrospective crossmatch and fatality follow-up (2026-10-03)

The dashboard counted retrospective crossmatch and reaction investigations, but a count alone did not separate routine work from items already past due. Both facts are already stored: each retrospective work item carries an overdue flag, and each investigation carries the fatality follow-up cue (`RXN-FATALITY-FOLLOWUP`).

### Implemented

- Retrospective crossmatch due shows how many items are past their stored due time.
- Reaction investigations shows how many cases still need CBER notification or the written report. A closed case is included, because closing the workup does not clear that follow-up.
- When nothing is past due, the count looks the same as before.
- Retrospective completion, reaction close, and the fatality follow-up rule are unchanged.

### Requirements / risk

- URS-BB-333, FRS-BB-368, SRS-BB-330, RISK-BB-435.
- Does not claim AABB compliance and is not an FDA filing.

### Tests

- TEST-BB-222 (`DashboardDueCountTests.RetrospectiveCountOverdue_IncludesOnlyItemsPastTheStoredDueTime`).
- TEST-BB-223 (`DashboardDueCountTests.FatalityFollowUpDue_CountsClosedCasesThatStillNeedFollowUp`).
- Full suite green: Domain 1447, Application 55, HL7 63, Printing 21, ASTM 19, Integration 924.

### Next ranked residual

The dashboard queue chain is complete. Re-rank by safety and operational impact. Do not add another dashboard count or another display-only field on the issue surfaces.
OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 181 — Pending specimens can be received and accepted (2026-10-04)

The dashboard queue chain is complete. The next operational gap is the tube that has been drawn but is not yet usable for testing. Accession always wrote Accepted. Collected and Received existed for antibody-identification scope, with no receive, accept, or worklist.

### Implemented

- Accession can record collection without accepting (`AcceptImmediately=false`).
- Receive moves Collected to Received. Accept moves Received to Accepted.
- An expired tube cannot be received or accepted. Reject remains available.
- `/test-worklist` Specimens tab lists pending tubes and Receive / Accept / Reject from the row. Scan or type an accession to filter.
- The patient Specimens tab has the same actions. In-lab accession still accepts immediately unless Accept now is unchecked.
- Result entry and order linking still require Accepted.
- Demo seed `ACC-PEND-COL` and `ACC-PEND-RCV` on Patricia Demo.

### Requirements / risk

- URS-BB-334, FRS-BB-369, SRS-BB-331, RISK-BB-436.
- Does not claim AABB compliance.

### Tests

- TEST-BB-224 (`SpecimenReceiptRuleTests`; `Phase3ServicesTests.ReceiveThenAccept_MovesCollectedToAccepted_AndLeavesTheBoard`; `Phase3ServicesTests.Receive_ExpiredCollected_IsHardStopped`).
- Full suite green: Domain 1454, Application 55, HL7 63, Printing 21, ASTM 19, Integration 930.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not add another dashboard count or another display-only field on the issue surfaces.
Shift handoff is still missing. Relabeling is still thin. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 182 — Shift handoff packet with acknowledgment (2026-10-04)

Pending specimen receipt is in place. The next operational gap is the end of a shift: dashboard counts existed, but there was no signed packet the incoming technologist could acknowledge.

### Implemented

- `ShiftHandoff` stores a required note and a snapshot of retrospective XM, outstanding issued, in transit, pending tests, pending specimens, and open reactions.
- Recording and acknowledgment require `audit.read`. The recorder cannot acknowledge. Double acknowledgment HardStops.
- `/handoff` records and lists packets. The dashboard shows an open handoff.
- Audit events use `AuditEventType.ShiftHandoff`.
- Clinical gates are unchanged.

### Requirements / risk

- URS-BB-335, FRS-BB-370, SRS-BB-332, RISK-BB-437.
- Does not claim AABB compliance.

### Tests

- TEST-BB-225 (`ShiftHandoffRuleTests`; `ShiftHandoffServiceTests.Create_WritesSnapshotAndAudit`; `ShiftHandoffServiceTests.Acknowledge_SameUser_IsHardStopped`).
- Domain and Integration handoff slices green (5 + 5).

### Next ranked residual

Next non-SME residual by safety and operational impact. Relabeling is still thin. Supervisor acknowledgment queues beyond handoff remain open. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 183 — Controlled specimen barcode relabel (2026-10-04)

Shift handoff is in place. The next identification gap is specimen barcode: collection metadata edit could change the barcode without a dedicated reason.

### Implemented

- `SpecimenRelabelRule` HardStops unusable status, blank barcode, blank reason, and unchanged barcode.
- `SpecimenService.RelabelAsync` requires `specimen.edit`, uniqueness, and audits old/new barcode with reason.
- `UpdateAsync` HardStops barcode deltas; the chart Edit panel shows barcode as read-only.
- Chart Relabel panel on Collected, Received, or Accepted specimens. Accession number stays immutable.
- Clinical gates are unchanged.

### Requirements / risk

- URS-BB-336, FRS-BB-371, SRS-BB-333, RISK-BB-438.
- Does not claim AABB compliance.

### Tests

- TEST-BB-226 (`SpecimenRelabelRuleTests`; `Phase3ServicesTests.Relabel_WritesBarcode_WithReason`; `Phase3ServicesTests.Update_BarcodeChange_IsHardStopped`; `Phase3ServicesTests.Relabel_RejectedSpecimen_IsHardStopped`).

### Next ranked residual

Next non-SME residual by safety and operational impact. Supervisor acknowledgment queues beyond handoff remain open. Elution is not a first-class workup. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 184 — Supervisor worklist (2026-10-04)

Specimen relabel is in place. The next operational gap is a single supervisor desk for ABID review, result verification, and quarantine release that otherwise sit on separate specialty pages.

### Implemented

- `SupervisorQueueRule` HardStops access unless the caller has `immuno.override`, `result.verify`, or `inventory.release`.
- `SupervisorQueueService` lists only the kinds those permissions allow and deep-links to existing workflows.
- `/supervisor` and nav/dashboard entry points. Clinical gates are unchanged.

### Requirements / risk

- URS-BB-337, FRS-BB-372, SRS-BB-334, RISK-BB-439.
- Does not claim AABB compliance.

### Tests

- TEST-BB-227 (`SupervisorQueueRuleTests`; `SupervisorQueueServiceTests.List_WithInventoryRelease_IncludesQuarantineUnit`; `SupervisorQueueServiceTests.List_WithResultVerifyOnly_OmitsQuarantine`; `SupervisorQueueServiceTests.List_WithoutSupervisorPermissions_IsHardStopped`).

### Next ranked residual

Next non-SME residual by safety and operational impact. Elution is not a first-class workup. Historical aliases remain thin. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 185 — Historical patient name aliases (2026-10-04)

Supervisor queue is in place. The next identification gap is demographics name edit: the previous name was overwritten without a retained alias or required reason.

### Implemented

- `IdentityTokenType.PriorFullName` stores the former display name on name change.
- `PatientHistoricalAliasRule` HardStops a blank name-change reason.
- Patient list and chart show prior names and prior MRNs; search matches those aliases.
- MRN remains immutable from demographics edit. Clinical gates are unchanged.

### Requirements / risk

- URS-BB-338, FRS-BB-373, SRS-BB-335, RISK-BB-440.
- Does not claim AABB compliance.

### Tests

- TEST-BB-228 (`PatientHistoricalAliasRuleTests`; `PatientServiceTests.Update_WritesDemographics_AndLeavesMrnUnchanged`; `PatientServiceTests.Update_NameChangeWithoutReason_IsHardStopped`; `PatientServiceTests.ListDtos_SearchMatchesPriorNameAlias`).

### Next ranked residual

Next non-SME residual by safety and operational impact. Elution is not a first-class workup. Unmerge is not implemented. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 186 — ADT prior-name archive (2026-10-04)

Chart name changes retain `PriorFullName`. The remaining identification gap is inbound ADT, which still overwrote a stated name without archiving the previous display name.

### Implemented

- `PatientHistoricalAliasWriter` is shared by chart edit and inbound ADT.
- ADT archives the previous display name before applying a stated name change and audits with `HL7 ADT demographics name change.`
- Sparse ADT blanks do not clear or archive names. Clinical gates are unchanged.

### Requirements / risk

- URS-BB-339, FRS-BB-374, SRS-BB-336, RISK-BB-441.
- Does not claim AABB compliance.

### Tests

- TEST-BB-229 (`Phase5Hl7Tests.InboundAdt_Update_ChangesDemographicsWithoutDuplicating`; `Phase5Hl7Tests.InboundAdt_SparseA08_DoesNotArchiveWhenNameUnchanged`).

### Next ranked residual

Next non-SME residual by safety and operational impact. Elution is not a first-class workup. Unmerge is not implemented. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 187 — Reversible patient merge (2026-10-12)

A wrong merge reassigned orders, specimens, and antibodies onto the survivor and kept no list of what moved. Undoing it by guessing could attach the wrong history to a later transfusion.

### Implemented

- `PatientMerge` and `PatientMergeMove` record moved, skipped, and demoted rows, plus the survivor deceased and pregnancy values before and after the merge.
- Merge also reassigns DAT/elution studies, pickup tags, and compatibility decisions. Rule execution logs stay on the original patient.
- Unmerge requires `patient.merge` and a reason. It restores the ledger, deactivates the prior-MRN alias created for that merge, and audits old and new status.
- Unmerge HardStops when the survivor has new clinical activity since the merge, when a moved row was edited, when a snapshotted survivor field changed, or when the merge has no ledger.
- The merged-record banner collects the reason. There is no HL7 unmerge message.

### Requirements / risk

- URS-BB-340, FRS-BB-375, SRS-BB-337, RISK-BB-442.
- Does not claim AABB compliance.

### Tests

- TEST-BB-230 (`PatientUnmergeRuleTests`; `PatientMergeServiceTests` unmerge success and failure paths).
- Domain unmerge rules and Integration `PatientMergeServiceTests` green (7 + 16).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 188 — Modification antigen inheritance (2026-10-12)

Divide, pool, irradiate, and the other modifications created a new unit with ABO and Rh only. Antigen and antibody typing on the source stayed behind, so a later selection could treat the result as untyped. A pool of a positive unit and a negative unit could also hide the conflict.

### Implemented

- `UnitModificationAttributeRule` copies a positive when any source is positive, copies a negative only when every source was typed negative, and HardStops when sources disagree (`MOD-POOL-ATTR-DISCORDANT`).
- Divide, pool, and single modifications write the inherited rows onto each result unit and list the definition ids on the modify audit.
- The pool and divide panels state that typing follows the sources. Discordant pools leave the source units available.

### Requirements / risk

- URS-BB-341, FRS-BB-376, SRS-BB-338, RISK-BB-443.
- Does not claim AABB compliance.

### Tests

- TEST-BB-231 (`UnitModificationAttributeRuleTests`; `BloodProductModificationServiceTests` divide copy, discordant pool, and partial-negative pool).
- Domain attribute rules and Integration `BloodProductModificationServiceTests` green (5 + 31).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Keeping a parent unit after an aliquot is still open.

## Iteration 189 — Modification donation restriction (2026-10-12)

Divide, pool, irradiate, and the other modifications created the result as allogeneic. An autologous or directed unit could be modified and then issued to someone else, because issue only blocks that when the reservation is still on the unit.

### Implemented

- `UnitModificationRestrictionRule` copies a shared autologous or directed reservation onto each result. Allogeneic results stay allogeneic.
- A pool HardStops when the sources do not share the restriction and reserved patient (`MOD-RESTRICTION-DISCORDANT`), or when a restricted source has no recipient (`MOD-RESTRICTION-RECIPIENT`). The check runs before the source status changes.
- The modify audit records the restriction and reserved patient. The pool and divide panels state the rule.

### Requirements / risk

- URS-BB-342, FRS-BB-377, SRS-BB-339, RISK-BB-444.
- Does not claim AABB compliance.

### Tests

- TEST-BB-232 (`UnitModificationRestrictionRuleTests`; `BloodProductModificationServiceTests` autologous divide, mixed pool, and same-patient directed pool).
- Domain restriction rules and Integration `BloodProductModificationServiceTests` green (6 + 34).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Keeping a parent unit after an aliquot is still open.

## Iteration 190 — Aliquot leaves the parent (2026-10-12)

Every modification retired the source. Removing a pediatric aliquot therefore took the remaining volume out of available inventory.

### Implemented

- `ModificationType.Aliquot` and `EvaluateAliquot` require a volume greater than zero and strictly less than the parent volume.
- The parent stays `Available`. Its volume is reduced, its product code stays, and a status note records the volume change. The aliquot is a new quarantined unit.
- The aliquot inherits tested typing and an autologous or directed reservation. An ISBT parent keeps its division code; the aliquot receives the next code.
- `POST /api/inventory/units/{id}/modifications/aliquot` requires `inventory.modify`. The modify page collects the volume and refreshes the parent volume after success.

### Requirements / risk

- URS-BB-343, FRS-BB-378, SRS-BB-340, RISK-BB-445.
- Does not claim AABB compliance.

### Tests

- TEST-BB-233 (`UnitModificationEligibilityRuleTests` aliquot cases; `BloodProductModificationServiceTests.Aliquot_KeepsParentAvailable_ReducesVolume_AndCopiesTyping`; `BloodProductModificationServiceTests.Aliquot_VolumeNotLessThanParent_IsHardStopped`).
- Domain aliquot rules and the two Integration aliquot tests green (3 + 2).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 191 — Modification volume accounting (2026-10-14)

A divide could retire a 300 mL unit after recording only 250 mL of results. The missing 50 mL left inventory with no unit. A single modification could also store a result volume larger than the source.

### Implemented

- `EvaluateDivide` HardStops `MOD-VOLUME-UNACCOUNTED` when a known source volume is not fully assigned to the result units. An oversized sum still HardStops `MOD-VOLUME-EXCEEDS-SOURCE`. A source with no recorded volume stores the entered child volumes.
- `EvaluateResultVolume` HardStops a non-positive result volume (`MOD-RESULT-VOLUME`) and a result volume larger than the source. Omitting the volume still keeps the source volume.
- Both checks run before the source status changes. The modify page shows the running divide total against the source volume and names Aliquot when a remainder should stay on the parent.

### Requirements / risk

- URS-BB-344, FRS-BB-379, SRS-BB-341, RISK-BB-446.
- Does not claim AABB compliance.

### Tests

- TEST-BB-234 (`UnitModificationEligibilityRuleTests` short divide and result-volume cases; `BloodProductModificationServiceTests.Divide_VolumesShortOfSource_IsBlocked`; `BloodProductModificationServiceTests.ApplySingle_ResultVolumeExceedsSource_IsBlocked`; `BloodProductModificationServiceTests.ApplySingle_ResultVolumeWithinSource_Succeeds`).
- Domain eligibility rules and Integration `BloodProductModificationServiceTests` green (24 + 39). The aliquot ISBT case uses its own donation number so a product-change divide on another unit cannot consume its division codes.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. A pool still drops known source volumes when any pooled unit has no recorded volume.

## Iteration 192 — Pool volume accounting (2026-10-14)

Pooling a measured unit with an unmeasured unit stored a blank volume on the pool. The known volume left the record when both sources were retired.

### Implemented

- `EvaluatePoolVolumes` HardStops `MOD-POOL-VOLUME` when any source has a recorded volume and another is missing or not greater than zero. The sources stay available.
- When every source has a volume, the pool volume is the sum. When none have a volume, the pool volume stays unrecorded.
- The modify page lists each source volume and the sum. The modify audit records the source volumes from before the change and the result volumes.

### Requirements / risk

- URS-BB-345, FRS-BB-380, SRS-BB-342, RISK-BB-447.
- Does not claim AABB compliance.

### Tests

- TEST-BB-235 (`UnitModificationEligibilityRuleTests` pool-volume cases; `BloodProductModificationServiceTests.Pool_MissingVolumeAmongKnownVolumes_IsBlocked`; `BloodProductModificationServiceTests.Pool_RecordedVolumes_SumOntoTheResult_AndAuditKeepsTheSources`).
- Domain eligibility rules and Integration `BloodProductModificationServiceTests` green (28 + 41).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. A pool result is stored at the first source location even when another source is somewhere else, with no confirmation those units were brought together.

## Iteration 193 — Pool location agreement (2026-10-14)

A pool of units in different refrigerators was stored at the first unit's location. The other unit was retired without a record that it had been somewhere else.

### Implemented

- `EvaluatePoolLocations` HardStops `MOD-POOL-LOCATION` when the sources do not share one storage location, including a mix of a location and no location. The sources stay available.
- Units that share a location, or that are all unassigned, can still be pooled. The result stays at that location.
- The modify page shows each location and tells staff to transfer the units first. The modify audit records the source location ids and the result location ids.

### Requirements / risk

- URS-BB-346, FRS-BB-381, SRS-BB-343, RISK-BB-448.
- Does not claim AABB compliance.

### Tests

- TEST-BB-236 (`UnitModificationEligibilityRuleTests` pool-location cases; `BloodProductModificationServiceTests.Pool_DifferentLocations_IsBlocked`; `BloodProductModificationServiceTests.Pool_SameLocation_KeepsThatLocationOnTheResult`).
- Domain eligibility rules and Integration `BloodProductModificationServiceTests` green (32 + 43).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Modification history for a unit still does not show the storage location of the result.

## Iteration 194 — Modification history location (2026-10-14)

Modification history named the result unit and its volume. After the unit was moved, that history did not say where it had been made.

### Implemented

- History reads the creation status row for each result and shows that location name. A later transfer does not change it.
- A result with no location shows none. Source rows do not show a result location.
- The creation status row stores the modification id once the header id exists.

### Requirements / risk

- URS-BB-347, FRS-BB-382, SRS-BB-344, RISK-BB-449.
- Does not claim AABB compliance.

### Tests

- TEST-BB-237 (`BloodProductModificationServiceTests.GetHistory_ShowsWhereTheResultWasMade_AfterItIsMoved`; `BloodProductModificationServiceTests.GetHistory_WithoutALocation_ShowsNone`).
- Integration `BloodProductModificationServiceTests` green (45).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Modification history does not say when a result has since left the location where it was made.

## Iteration 195 — Result has left where it was made (2026-10-14)

History named the refrigerator where a pool was prepared. After the unit moved to the issue bench, the row still read as if it were there.

### Implemented

- A result row keeps the creation location and carries the current location.
- `ResultHasLeftCreationLocation` is true only when those location ids differ. The history cell then adds `now at` the current place.
- A unit still at the creation location, including one that was never assigned a location, does not get that note.

### Requirements / risk

- URS-BB-348, FRS-BB-383, SRS-BB-345, RISK-BB-450.
- Does not claim AABB compliance.

### Tests

- TEST-BB-238 (`BloodProductModificationServiceTests.GetHistory_ShowsWhereTheResultWasMade_AfterItIsMoved`; `BloodProductModificationServiceTests.GetHistory_StillAtTheCreationLocation_DoesNotMarkAMove`; `BloodProductModificationServiceTests.GetHistory_WithoutALocation_ShowsNone`).
- Those history tests green (4, including the moved, still-there, and unassigned cases).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Modification history names the current place after a move, but not when the unit was moved or why.

## Iteration 196 — When and why a result left (2026-10-14)

History said a result was now at the issue bench. It did not say when that move was recorded or why.

### Implemented

- When the current location differs from where the result was made, history reads the latest status row whose destination is that current location.
- The history cell adds that time and reason. A blank reason is left off.
- A current location with no such row still names the new place and does not invent a time or reason. A unit still where it was made has neither.

### Requirements / risk

- URS-BB-349, FRS-BB-384, SRS-BB-346, RISK-BB-451.
- Does not claim AABB compliance.

### Tests

- TEST-BB-239 (`BloodProductModificationServiceTests.GetHistory_ShowsWhereTheResultWasMade_AfterItIsMoved`; `BloodProductModificationServiceTests.GetHistory_MoveWithoutAStatusRow_ShowsTheNewPlaceWithoutATimeOrReason`; `BloodProductModificationServiceTests.GetHistory_StillAtTheCreationLocation_DoesNotMarkAMove`).
- History tests green (5).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Modification history names when and why a result was moved, but not who moved it.

## Iteration 197 — Who moved a result (2026-10-14)

History said when a result left and why. It did not say who recorded that move.

### Implemented

- The same status row that supplies the time and reason also supplies `ChangedBy`.
- The history cell appends `by` that name. A blank name is left off.
- A move with no status row still names the new place and does not invent a person. A unit still where it was made has no mover.

### Requirements / risk

- URS-BB-350, FRS-BB-385, SRS-BB-347, RISK-BB-452.
- Does not claim AABB compliance.

### Tests

- TEST-BB-240 (`BloodProductModificationServiceTests.GetHistory_ShowsWhereTheResultWasMade_AfterItIsMoved`; `BloodProductModificationServiceTests.GetHistory_MoveWithoutAPerson_ShowsTimeAndReasonWithoutAName`; `BloodProductModificationServiceTests.GetHistory_MoveWithoutAStatusRow_ShowsTheNewPlaceWithoutATimeOrReason`).
- History tests green (6).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The modify page still labels a selected unit and the pool sources as `Location {id}` instead of the storage location name, so staff comparing places before a pool have to recognize the number.

## Iteration 198 — Storage location names on the modify page (2026-10-14)

The selected unit and the pool list showed `Location 12`. Staff had to recognize the number before they could tell two units apart.

### Implemented

- The modify page loads storage location names and shows the name on the selected unit and on each pool source.
- A blank or unknown name stays `Location {id}`. A unit with no location still shows none.
- A location list that cannot be loaded does not invent names. Pooling still compares location ids.

### Requirements / risk

- URS-BB-351, FRS-BB-386, SRS-BB-348, RISK-BB-453.
- Does not claim AABB compliance.

### Tests

- TEST-BB-241 (`StorageLocationLabelTests.Format_NamedLocation_UsesTheName`; `StorageLocationLabelTests.Format_BlankOrUnknownName_KeepsTheLocationId`; `StorageLocationLabelTests.Format_NoLocation_DoesNotInventOne`).
- Label tests green (3).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. After a modification, the result card shows the new unit as Quarantine and offers Open unit, but it does not show where that new unit is stored or point staff to release it.

## Iteration 199 — Where a new unit is, and how to release it (2026-10-14)

A modification result started in quarantine. The result card showed the status and an Open unit link. It did not say where the unit was stored, or that it has to be released before issue.

### Implemented

- Each new unit on the result card shows its storage location name. A missing name stays the location id. No location stays none.
- A quarantined result says to release it before issue. Staff with `inventory.release` get a link to that unit's quarantine row.
- The inventory board opens the release row only when that unit is on the quarantine list. An assigned result, including one tied to a product order, does not get a release step.

### Requirements / risk

- URS-BB-352, FRS-BB-387, SRS-BB-349, RISK-BB-454.
- Does not claim AABB compliance.

### Tests

- TEST-BB-242 (`ModificationResultNextStepTests`).
- Next-step tests green (8).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. After an aliquot, the result card lists only the new unit. It does not say the parent stayed available or what volume remains on the parent, so the new unit can be read as a replacement.

## Iteration 200 — Aliquot parent stays on the result card (2026-10-14)

An aliquot leaves the parent available at a reduced volume. The result card listed only the new unit, so that new unit could be read as a replacement.

### Implemented

- After an aliquot, the result card names the parent, the status from a fresh read, and the volume remaining.
- A parent with no recorded volume says so. The card does not invent a number.
- If the parent cannot be reread, the card says the parent was left in place and does not invent a status or a remaining volume.
- Divide, pool, and other modifications do not show this parent line.

### Requirements / risk

- URS-BB-353, FRS-BB-388, SRS-BB-350, RISK-BB-455.
- Does not claim AABB compliance.

### Tests

- TEST-BB-243 (`ModificationResultNextStepTests.AliquotParentCue_ConfirmedParent_NamesStatusAndRemainingVolume`; `ModificationResultNextStepTests.AliquotParentCue_UnrecordedVolume_DoesNotInventOne`; `ModificationResultNextStepTests.AliquotParentCue_UnconfirmedParent_DoesNotInventStatusOrVolume`; `ModificationResultNextStepTests.AliquotParentCue_ReportsTheParentStatusItWasGiven`).
- Parent-cue tests green (12 in `ModificationResultNextStepTests`).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. After a divide, pool, or other modification that retires the source, the result card lists the new units and does not say the source is now Modified, so staff can keep looking for it as available.

## Iteration 201 — Retired sources on the result card (2026-10-14)

Divide, pool, and the other retiring modifications set the source to Modified. The result card listed only the new units, so staff could keep looking for the source as available.

### Implemented

- After a retiring modification, the result card names each source and the status from a fresh read.
- A failed read says that source was retired and does not claim it is Modified.
- A read that is not Modified is shown as that status. The card does not rewrite it.
- An aliquot keeps the parent line from the previous cycle and does not use this retired-source line.
- The selected unit card is updated from that same read when the primary source can be reread.

### Requirements / risk

- URS-BB-354, FRS-BB-389, SRS-BB-351, RISK-BB-456.
- Does not claim AABB compliance.

### Tests

- TEST-BB-244 (`ModificationResultNextStepTests.RetiredSourceCue_ConfirmedSources_NamesEachStatus`; `ModificationResultNextStepTests.RetiredSourceCue_UnconfirmedSource_DoesNotInventModified`; `ModificationResultNextStepTests.RetiredSourceCue_ReportsTheStatusItWasGiven`).
- Retired-source tests green (15 in `ModificationResultNextStepTests`).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. After a source is retired, the modify form can still offer another modification on that unit until the page is reloaded.

## Iteration 202 — Close the modify form after a source is retired (2026-10-14)

A divide, pool, or other retiring modification set the source to Modified. The form on that page still offered another modification until it was reloaded.

### Implemented

- The modify form offers a rule for an available unit, and for an assigned unit only while a product-order action is open.
- The eligibility list is empty for a Modified unit, and for quarantine, issued, and the other statuses that cannot be modified.
- An assigned unit still has rules listed so a product-order action can use them.
- After a retiring modification, the selected rule is cleared. If the unit cannot be reread, the previous rules are cleared too.
- An aliquot leaves the parent form in place.

### Requirements / risk

- URS-BB-355, FRS-BB-390, SRS-BB-352, RISK-BB-457.
- Does not claim AABB compliance.

### Tests

- TEST-BB-245 (`UnitModificationEligibilityRuleTests.OfferModification_Available_IsOffered`; `UnitModificationEligibilityRuleTests.OfferModification_Assigned_OnlyWhileCompletingAnOrder`; `UnitModificationEligibilityRuleTests.OfferModification_RetiredOrHeld_IsNotOffered`; `BloodProductModificationServiceTests.GetEligibleModifications_ModifiedUnit_ReturnsNone`; `BloodProductModificationServiceTests.GetEligibleModifications_AssignedUnit_StillListsRules`).
- Offer tests green (5). Eligibility tests green (4).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The quality-quarantine board, where a new modification result is released, lists the unit and the reason and does not show the storage location name.

## Iteration 203 — Storage location on quality quarantine (2026-10-14)

A new modification result is released from the quality-quarantine board. That board listed the unit and the reason and did not say where the unit was stored.

### Implemented

- Each quarantine row shows the storage location name. The release row repeats it.
- A blank or unknown name stays `Location {id}`.
- A unit with no location says none is recorded. A missing location record does not invent a name.

### Requirements / risk

- URS-BB-356, FRS-BB-391, SRS-BB-353, RISK-BB-458.
- Does not claim AABB compliance.

### Tests

- TEST-BB-246 (`InventoryServiceTests.ListQuarantine_NamesTheStorageLocation`).
- Quarantine location test green (1).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The quality-quarantine board does not show volume, so two divided units of the same product and blood type are distinguished only by unit number.

## Iteration 204 — Volume on quality quarantine (2026-10-14)

Two divided units of the same product and blood type can sit in quarantine together. The board showed the unit number and not the volume.

### Implemented

- Each quarantine row shows the recorded volume. The release row repeats it.
- A unit with no volume says it is not recorded. A zero volume is shown as recorded.

### Requirements / risk

- URS-BB-357, FRS-BB-392, SRS-BB-354, RISK-BB-459.
- Does not claim AABB compliance.

### Tests

- TEST-BB-247 (`InventoryServiceTests.ListQuarantine_ShowsRecordedVolume_AndDoesNotInventOne`).
- Quarantine volume test green (1).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The inventory discrepancy board shows a location code, not the storage location name, so a missing or damaged unit is harder to find than a quarantined one.

## Iteration 205 — Storage location name on inventory discrepancy (2026-10-14)

The discrepancy board showed a location code. A missing or damaged unit was harder to find than a quarantined one, which already shows the storage location name.

### Implemented

- Each discrepancy row shows the storage location name.
- A blank or unknown name stays `Location {id}`.
- A unit with no location says none. A missing location record does not invent a name.

### Requirements / risk

- URS-BB-358, FRS-BB-393, SRS-BB-355, RISK-BB-460.
- Does not claim AABB compliance.

### Tests

- TEST-BB-248 (`InventoryServiceTests.ListDiscrepancy_NamesTheStorageLocation`).
- Discrepancy location test green (1). Quarantine location still green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The operational-hold board does not show where the unit is stored, so a unit that cannot be issued is harder to find than a quarantined or missing one.

## Iteration 206 — Storage location name on operational hold (2026-10-14)

A unit on operational hold cannot be issued. The hold board did not say where it was stored.

### Implemented

- Each operational-hold row shows the storage location name.
- A blank or unknown name stays `Location {id}`.
- A unit with no location says none. A missing location record does not invent a name.

### Requirements / risk

- URS-BB-359, FRS-BB-394, SRS-BB-356, RISK-BB-461.
- Does not claim AABB compliance.

### Tests

- TEST-BB-249 (`InventoryServiceTests.ListOnHold_NamesTheStorageLocation`).
- Hold location tests green (2).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The main inventory list still shows a location code, not the storage location name, so a searched unit is labeled differently from quarantine, discrepancy, and hold.

## Iteration 207 — Storage location name on the inventory search list (2026-10-14)

The main inventory search list showed a location code. A searched unit was labeled differently from quarantine, discrepancy, and hold, which already show the storage location name.

### Implemented

- Each search row shows the storage location name.
- A blank or unknown name stays `Location {id}`.
- A unit with no location says none. A location code is not used when the name is missing.
- A location list that cannot be loaded does not invent names.

### Requirements / risk

- URS-BB-360, FRS-BB-395, SRS-BB-357, RISK-BB-462.
- Does not claim AABB compliance.

### Tests

- TEST-BB-250 (`StorageLocationLabelTests.ForInventoryList_NamedLocation_UsesTheNameNotTheCode`; `StorageLocationLabelTests.ForInventoryList_BlankName_DoesNotFallBackToTheCode`; `StorageLocationLabelTests.ForInventoryList_NoLocation_DoesNotInventOne`).
- Inventory-list location tests green (3).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The near-expiry board does not say where the unit is stored, so staff discarding an expiring unit still have to open it to find the refrigerator.

## Iteration 208 — Storage location name on near expiry (2026-10-14)

The near-expiry board listed the unit, product, and expiration. Staff discarding an expiring unit still had to open it to find the refrigerator.

### Implemented

- Each near-expiry row shows the storage location name.
- The discard row repeats that place.
- A blank or unknown name stays `Location {id}`.
- A unit with no location says none. A missing location record does not invent a name.

### Requirements / risk

- URS-BB-361, FRS-BB-396, SRS-BB-358, RISK-BB-463.
- Does not claim AABB compliance.

### Tests

- TEST-BB-251 (`InventoryServiceTests.ListNearExpiry_NamesTheStorageLocation`).
- Near-expiry location test green (1).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The unit chart still shows a location code, not the storage location name, so opening a unit from search or near expiry labels it differently from those boards.

## Iteration 209 — Storage location name on the unit chart (2026-10-14)

Opening a unit from search or near expiry showed a location code on the chart. The boards already show the storage location name.

### Implemented

- The unit chart header and the unit identity show the storage location name.
- A blank or unknown name stays `Location {id}`.
- A unit with no location says none. A location code is not used when the name is missing.
- A location list that cannot be loaded does not invent names.

### Requirements / risk

- URS-BB-362, FRS-BB-397, SRS-BB-359, RISK-BB-464.
- Does not claim AABB compliance.

### Tests

- TEST-BB-252 (`StorageLocationLabelTests.ForUnitChart_NamedLocation_UsesTheNameNotTheCode`; `StorageLocationLabelTests.ForUnitChart_BlankName_DoesNotFallBackToTheCode`; `StorageLocationLabelTests.ForUnitChart_NoLocation_DoesNotInventOne`).
- Unit-chart location tests green (3).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The unit status history names the status change and not the storage location, so a transfer is visible as a status line and the refrigerator is not on the timeline.

## Iteration 210 — Storage location on a unit status move (2026-10-14)

Unit status history named the status change and the reason. A transfer was visible as a status line, and the refrigerator was not on the timeline.

### Implemented

- A status row whose recorded location changed names the storage locations.
- A blank or unknown name stays `Location {id}`.
- A row that stays in the same place, including both unassigned, does not say it moved.
- A missing destination is not invented. A location list that cannot be loaded does not invent names.

### Requirements / risk

- URS-BB-363, FRS-BB-398, SRS-BB-360, RISK-BB-465.
- Does not claim AABB compliance.

### Tests

- TEST-BB-253 (`StatusHistoryLocationCueTests`; `InventoryServiceTests.UnitActivity_NamesTheStorageLocationsOnAMove`).
- Status-move location tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The unit timeline names the patient on an issue and still says patient #id on an allocation, so staff tracing who the unit was set aside for have to leave the chart.

## Iteration 211 — Patient name on a unit allocation (2026-10-14)

The unit timeline named the patient on an issue and still said patient #id on an allocation. Staff tracing who the unit was set aside for had to leave the chart.

### Implemented

- An allocation names the patient the same way an issue does.
- The medical record number is included when it is recorded.
- A missing patient record, or a blank name, stays `patient #{id}` and does not invent a name.

### Requirements / risk

- URS-BB-364, FRS-BB-399, SRS-BB-361, RISK-BB-466.
- Does not claim AABB compliance.

### Tests

- TEST-BB-254 (`PatientActivityLabelTests`; `InventoryServiceTests.UnitActivity_NamesThePatientOnAnAllocation`).
- Allocation patient-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The unit timeline names the patient on an allocation and an issue, and a crossmatch still shows only the method and result, so staff cannot see who that crossmatch was for without leaving the chart.

## Iteration 212 — Patient name on a unit crossmatch (2026-10-14)

The unit timeline named the patient on an allocation and an issue. A crossmatch still showed only the method and result, so staff could not see who that crossmatch was for without leaving the chart.

### Implemented

- A crossmatch names the patient the same way an allocation and an issue do.
- The medical record number is included when it is recorded.
- A missing patient record, or a blank name, stays `patient #{id}` and does not invent a name.

### Requirements / risk

- URS-BB-365, FRS-BB-400, SRS-BB-362, RISK-BB-467.
- Does not claim AABB compliance.

### Tests

- TEST-BB-255 (`InventoryServiceTests.UnitActivity_NamesThePatientOnACrossmatch`).
- Crossmatch patient-label test green (1).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The unit timeline names the patient on an allocation, an issue, and a crossmatch, and a transfusion still shows only the disposition, so staff cannot see who received the unit without leaving the chart.

## Iteration 213 — Patient name on a unit transfusion (2026-10-14)

The unit timeline named the patient on an allocation, an issue, and a crossmatch. A transfusion still showed only the disposition, so staff could not see who received the unit without leaving the chart.

### Implemented

- A transfusion names the patient the same way an allocation, an issue, and a crossmatch do.
- The medical record number is included when it is recorded.
- A missing patient record, or a blank name, stays `patient #{id}` and does not invent a name.

### Requirements / risk

- URS-BB-366, FRS-BB-401, SRS-BB-363, RISK-BB-468.
- Does not claim AABB compliance.

### Tests

- TEST-BB-256 (`InventoryServiceTests.UnitActivity_NamesThePatientOnATransfusion`).
- Transfusion patient-label test green (1).

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The unit timeline names the patient on an allocation, an issue, a crossmatch, and a transfusion, and a return still names only the reason, so staff cannot see who the unit was issued to without leaving the chart.

## Iteration 214 — Patient name on a unit return (2026-10-14)

The unit timeline named the patient on an allocation, an issue, a crossmatch, and a transfusion. A return still named only the reason, so staff could not see who the unit was issued to without leaving the chart.

### Implemented

- A return names the patient from the issue that return belongs to.
- The medical record number is included when it is recorded.
- A missing patient record, or a blank name, stays `patient #{id}` and does not invent a name.
- A return whose issue cannot be read does not invent a patient.

### Requirements / risk

- URS-BB-367, FRS-BB-402, SRS-BB-364, RISK-BB-469.
- Does not claim AABB compliance.

### Tests

- TEST-BB-257 (`ReturnActivityCueTests`; `InventoryServiceTests.UnitActivity_NamesThePatientOnAReturn`).
- Return patient-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The unit timeline names the patient on an allocation, an issue, a crossmatch, a transfusion, and a return, and a modification still names only the action, so staff cannot see who that product order was for without leaving the chart.

## Iteration 215 — Patient name on an order-linked modification (2026-10-14)

The unit timeline named the patient on an allocation, an issue, a crossmatch, a transfusion, and a return. A modification still named only the action, so staff could not see who that product order was for without leaving the chart.

### Implemented

- An order-linked modification names the patient from that product order.
- The medical record number is included when it is recorded.
- A missing patient record, or a blank name, stays `patient #{id}` and does not invent a name.
- A modification whose order cannot be read does not invent a patient.
- A bench modification that is not tied to an order is unchanged.

### Requirements / risk

- URS-BB-368, FRS-BB-403, SRS-BB-365, RISK-BB-470.
- Does not claim AABB compliance.

### Tests

- TEST-BB-258 (`ModificationActivityCueTests`; `InventoryServiceTests.UnitActivity_NamesThePatientOnAnOrderModification`).
- Order-modification patient-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The unit timeline now names the patient on the order-linked rows, and a bench modification that is not tied to an order still reads like an unnamed product order, so staff cannot tell that no patient order was recorded.

## Iteration 216 — Bench modification with no patient order (2026-10-14)

The unit timeline named the patient on an order-linked modification. A bench modification still named only the type, role, and reason, so it read like a product order whose patient had been left off.

### Implemented

- A bench modification that no product order points at says no patient order was recorded.
- The row keeps the modification type, the source or result role, and the recorded reason.
- A blank reason is omitted.
- A bench row that a product order does point at does not claim that no order was recorded, and does not invent a patient.

### Requirements / risk

- URS-BB-369, FRS-BB-404, SRS-BB-366, RISK-BB-471.
- Does not claim AABB compliance.

### Tests

- TEST-BB-259 (`BenchModificationActivityCueTests`; `InventoryServiceTests.UnitActivity_SaysWhenABenchModificationHasNoPatientOrder`).
- Bench-modification order-cue tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. A bench modification that a product order points at still names only the type, role, and reason, so the source or result row does not say who that order was for.

## Iteration 217 — Patient name on a linked bench modification (2026-10-14)

The unit timeline said when a bench modification had no patient order. A source or result row that a product order did point at still named only the type, role, and reason, so staff could not see who that order was for without leaving the chart.

### Implemented

- A bench modification that a product order points at names the patient from that order.
- The medical record number is included when it is recorded.
- A missing patient record, or a blank name, stays `patient #{id}` and does not invent a name.
- A missing order does not invent a patient.
- Linked orders that do not name one patient do not pick one.
- A blank reason is still omitted. A bench modification with no product order is unchanged.

### Requirements / risk

- URS-BB-370, FRS-BB-405, SRS-BB-367, RISK-BB-472.
- Does not claim AABB compliance.

### Tests

- TEST-BB-260 (`BenchModificationActivityCueTests`; `InventoryServiceTests.UnitActivity_SaysWhenABenchModificationHasNoPatientOrder`).
- Linked bench-modification patient-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Modification history still names only the type, reason, and units, so staff reading that history cannot see who an order-linked modification was for.

## Iteration 218 — Patient name on modification history (2026-10-14)

The unit timeline named the patient on an order-linked modification. Modification history still named only the type, reason, and units, so staff reading that history could not see who the modification was for.

### Implemented

- Modification history names the patient when a product order points at the modification.
- The medical record number is included when it is recorded.
- A missing patient record, or a blank name, stays `patient #{id}` and does not invent a name.
- No product order says so.
- A missing order does not invent a patient.
- Linked orders that do not name one patient do not pick one.

### Requirements / risk

- URS-BB-371, FRS-BB-406, SRS-BB-368, RISK-BB-473.
- Does not claim AABB compliance.

### Tests

- TEST-BB-261 (`ModificationHistoryPatientCueTests`; `BloodProductModificationServiceTests.GetHistory_NamesThePatientWhenAnOrderPointsAtTheModification`).
- Modification-history patient-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The status row that records a new modification still says only that it was created, so staff reading status history cannot see who an order-linked modification was for.

## Iteration 219 — Patient name on a modification status row (2026-10-15)

Modification history named the patient when a product order pointed at the modification. The status row that recorded the new unit still said only that it was created, so staff reading status history could not see who it was for.

### Implemented

- A status row linked to a modification names the patient from the product order that points at it.
- The recorded reason is not changed.
- The medical record number is included when it is recorded.
- A missing patient record, or a blank name, stays `patient #{id}` and does not invent a name.
- No product order says so.
- A missing order does not invent a patient.
- A status row with no modification id does not invent a patient.
- Linked orders that do not name one patient do not pick one.

### Requirements / risk

- URS-BB-372, FRS-BB-407, SRS-BB-369, RISK-BB-474.
- Does not claim AABB compliance.

### Tests

- TEST-BB-262 (`InventoryServiceTests.UnitActivity_NamesThePatientOnAModificationStatusRow`).
- Modification status-row patient-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. An issue on the unit timeline still shows the issued-to place exactly as it was typed, so a location code can be read as the storage location name.

## Iteration 220 — Storage location name on an issue (2026-10-15)

An issue on the unit timeline showed the issued-to place exactly as it was typed. A storage-location code could be read as the location name.

### Implemented

- An issue names the storage location when the issued-to place is exactly one location code.
- A blank location name stays `Location {id}` and does not show the code.
- A place that is not one location code stays as typed.
- A code that is also another location's name stays as typed.
- A blank place is omitted.
- The recorded place is unchanged.
- A location list that cannot be loaded does not invent a name.

### Requirements / risk

- URS-BB-373, FRS-BB-408, SRS-BB-370, RISK-BB-475.
- Does not claim AABB compliance.

### Tests

- TEST-BB-263 (`IssueDestinationCueTests`; `InventoryServiceTests.UnitActivity_NamesTheStorageLocationOnAnIssue`).
- Issue destination tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The issuing board and the patient product history still show the issued-to place exactly as it was typed, so a location code can be read as the storage location name.

## Iteration 221 — Storage location name on the issuing board (2026-10-15)

The issuing board and the patient product history showed the issued-to place exactly as it was typed. A storage-location code could be read as the location name.

### Implemented

- The retrospective, in-transit, and issued boards name the storage location when the issued-to place is exactly one location code.
- Patient product history uses that same name on an issued row and on a transfused row.
- A blank location name stays `Location {id}` and does not show the code.
- A place that is not one location code stays as typed.
- A code that is also another location's name stays as typed.
- The recorded place is unchanged.
- A missing location list does not invent a name.
- The issue form still shows the place as typed.

### Requirements / risk

- URS-BB-374, FRS-BB-409, SRS-BB-371, RISK-BB-476.
- Does not claim AABB compliance.

### Tests

- TEST-BB-264 (`Phase4IssuingTests.IssuedBoards_NameTheStorageLocationWhenThePlaceIsALocationCode`).
- Issuing-board and patient-history location tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Lookback still shows the issued-to place exactly as it was typed, so a location code can be read as the storage location name. The issue form still shows the place as typed.

## Iteration 222 — Storage location name on lookback (2026-10-15)

Lookback showed the issued-to place exactly as it was typed. A storage-location code could be read as the location name.

### Implemented

- A DIN recipient row names the storage location when the issued-to place is exactly one location code.
- A recipient trace uses that same name.
- A blank location name stays `Location {id}` and does not show the code.
- A place that is not one location code stays as typed.
- A code that is also another location's name stays as typed.
- The recorded place is unchanged.
- A missing location list does not invent a name.

### Requirements / risk

- URS-BB-375, FRS-BB-410, SRS-BB-372, RISK-BB-477.
- Does not claim AABB compliance.

### Tests

- TEST-BB-265 (`ComplianceWorkflowTests.Lookback_NamesTheStorageLocationWhenThePlaceIsALocationCode`).
- Lookback location tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. A receive, return, or transfusion notice still prefixes Location onto a place that may already name the storage location, so a blank location name can read as Location Location {id}. The issue form still shows the place as typed.

## Iteration 223 — One Location prefix on a custody notice (2026-10-15)

A receive, return, or transfusion notice prefixed Location onto a place that already said Location {id}. A blank storage location name could be read as Location Location {id}.

### Implemented

- A place that already says Location {id} stays as that place.
- A ward name or a storage location name still says Location before that place.
- A blank place is omitted.
- The same rule applies to the retrospective follow-up notice.

### Requirements / risk

- URS-BB-376, FRS-BB-411, SRS-BB-373, RISK-BB-478.
- Does not claim AABB compliance.

### Tests

- TEST-BB-266 (`IssuedToLocationNoticeTests.IssuedToLocationNotice_NamesTheWard`; `RetrospectiveFollowUpNoticeTests.RetrospectiveFollowUpNotice_NamesCustody`).
- Custody-notice location tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Ready to issue and reserved products still show a location code when the storage location name is blank, so the code can be read as the name. A blank name should stay Location {id}. Do not invent a name.

## Iteration 224 — Location id when a reserved unit's place has no name (2026-10-15)

Ready to issue and reserved products showed a location code when the storage location name was blank. The code could be read as the name.

### Implemented

- A blank storage location name stays `Location {id}` on Ready to issue.
- A compatible unit choice and a reserved product use that same label.
- A location code is not shown.
- A unit with no location stays omitted.
- A location list that cannot be loaded does not invent a name.

### Requirements / risk

- URS-BB-377, FRS-BB-412, SRS-BB-374, RISK-BB-479.
- Does not claim AABB compliance.

### Tests

- TEST-BB-267 (`StorageLocationCueTests.StorageLocationLabel_KeepsTheLocationIdWhenTheNameIsBlank`; `Phase4IssuingTests.ListReadyToIssue_KeepsTheLocationIdWhenTheNameIsBlank`; `PatientAllocationTests.CompatibleAndReserved_KeepTheLocationIdWhenTheNameIsBlank`).
- Blank storage-location label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. A unit transfer and a delivery receipt still list a storage location as code and name. A blank name on that picker can leave the code looking like the place. Keep the code available for selection, and show Location {id} when the name is blank.

## Iteration 225 — Location id on a transfer or delivery choice (2026-10-15)

A unit transfer and a delivery receipt listed a storage location as code and name. A blank name left the code looking like the place.

### Implemented

- The transfer choice keeps the location code and the storage location name.
- The delivery-receipt choice uses that same label.
- A blank name stays `Location {id}` and is not replaced by the code.
- A missing code stays the place alone.

### Requirements / risk

- URS-BB-378, FRS-BB-413, SRS-BB-375, RISK-BB-480.
- Does not claim AABB compliance.

### Tests

- TEST-BB-268 (`StorageLocationLabelTests.ForPicker_KeepsTheCodeAndTheLocationIdWhenTheNameIsBlank`).
- Transfer and delivery location-choice tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The location admin list still shows a blank storage location name as an empty name beside the code, so the code can be read as the place. Keep the code in the catalog, and show Location {id} when the name is blank.

## Iteration 226 — Location id on the inventory-location list (2026-10-15)

The inventory-location list showed a blank name as an empty name beside the code. The code could be read as the place.

### Implemented

- The list keeps the location code in the code column.
- A blank name on that list stays `Location {id}`.
- The stored name is unchanged.
- The edit form still shows the stored name.

### Requirements / risk

- URS-BB-379, FRS-BB-414, SRS-BB-376, RISK-BB-481.
- Does not claim AABB compliance.

### Tests

- TEST-BB-269 (`InventoryLocationAdminDtoTests.NameLabel_KeepsTheLocationIdWhenTheNameIsBlank`).
- Inventory-location name-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. Saving an ordering location still stores the location code as the name when the name is left blank, so the code is kept as the place. Do not invent a name from the code.

## Iteration 227 — Do not store an ordering-location code as the name (2026-10-15)

Saving an ordering location stored the location code as the name when the name was left blank. The code was kept as the place.

### Implemented

- A blank name is stored empty on an admin save and on an HL7 location that sends no name.
- The list and the visit or order choice show `Location {id}`.
- A recorded name stays that name.
- An HL7 mapping code still uses the location code when none was sent.
- A name already stored as the code is not rewritten.

### Requirements / risk

- URS-BB-380, FRS-BB-415, SRS-BB-377, RISK-BB-482.
- Does not claim AABB compliance.

### Tests

- TEST-BB-270 (`OrderingLocationAuthorizationTests.Create_BlankName_DoesNotStoreTheCodeAsTheName`; `OrderingLocationAuthorizationTests.Hl7_BlankName_DoesNotStoreTheCodeAsTheName`; `AdminCatalogTests.CreateLocation_CodeOnly_DoesNotStoreTheCodeAsTheName`).
- Ordering-location blank-name tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. A blood supplier choice still joins the code and the name, so a blank name can leave the code looking like the place. Keep the code available for selection, and do not use the code as the name.

## Iteration 228 — Supplier id on a delivery choice (2026-10-15)

A delivery supplier choice joined the code and the name. A blank name could leave the code looking like the supplier.

### Implemented

- The choice keeps the supplier code.
- A blank name stays `Supplier {id}`.
- A recorded name stays that name.
- The stored name is unchanged.
- A locked delivery shows the same label.

### Requirements / risk

- URS-BB-381, FRS-BB-416, SRS-BB-378, RISK-BB-483.
- Does not claim AABB compliance.

### Tests

- TEST-BB-271 (`BloodSupplierLabelTests.ForChoice_KeepsTheCodeAndTheSupplierIdWhenTheNameIsBlank`; `BloodSupplierChoiceTests.DisplayName_KeepsTheCodeAndTheSupplierIdWhenTheNameIsBlank`).
- Supplier-choice tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The blood-supplier catalog still shows a blank name as an empty name beside the code, so the code can be read as the supplier. Keep the code in the catalog, and show Supplier {id} when the name is blank. The stored name and the edit form stay unchanged.

## Iteration 229 — Supplier id on the blood-supplier list (2026-10-15)

The blood-supplier catalog showed a blank name as an empty name beside the code. The code could be read as the supplier.

### Implemented

- The list keeps the supplier code in the code column.
- A blank name on that list stays `Supplier {id}`.
- The stored name is unchanged.
- The edit form still shows the stored name.

### Requirements / risk

- URS-BB-382, FRS-BB-417, SRS-BB-379, RISK-BB-484.
- Does not claim AABB compliance.

### Tests

- TEST-BB-272 (`BloodSupplierLabelTests.Name_KeepsTheSupplierIdWhenTheNameIsBlank`; `BloodSupplierChoiceTests.NameLabel_KeepsTheSupplierIdWhenTheNameIsBlank`).
- Supplier-catalog name-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. An open delivery and the unit-entry page still show a blank supplier name as empty, so staff can miss which supplier sent the blood. Show Supplier {id} there. Do not write that label onto the unit. The stored name stays unchanged.

## Iteration 230 — Supplier id on an open delivery (2026-10-15)

An open delivery and unit entry showed a blank supplier name as empty. Staff could miss which supplier sent the blood.

### Implemented

- An open delivery shows `Supplier {id}` when the name is blank.
- Unit entry on a delivery and ISBT receipt show the same label.
- The name written on the unit stays the recorded supplier name.
- The stored supplier name is unchanged.

### Requirements / risk

- URS-BB-383, FRS-BB-418, SRS-BB-380, RISK-BB-485.
- Does not claim AABB compliance.

### Tests

- TEST-BB-273 (`DeliverySupplierLabelTests.SupplierNameLabel_KeepsTheSupplierIdWhenTheNameIsBlank`; `DeliveryServiceTests.BlankSupplierName_ShowsTheSupplierIdAndDoesNotWriteItOnTheUnit`).
- Open-delivery supplier-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. A unit received from a supplier with a blank name still shows that blank name on the unit. When the delivery names that supplier, show Supplier {id} on the unit. Do not rewrite the stored supplier or collection facility.

## Iteration 231 — Supplier id on the unit (2026-10-15)

A unit received from a supplier with a blank name still showed that blank name. Staff could read the unit as having no supplier.

### Implemented

- The unit chart shows `Supplier {id}` when the name is blank and the delivery names that supplier.
- A recorded name stays that name.
- The stored supplier and collection facility are unchanged.
- A unit with no delivery does not invent a supplier.

### Requirements / risk

- URS-BB-384, FRS-BB-419, SRS-BB-381, RISK-BB-486.
- Does not claim AABB compliance.

### Tests

- TEST-BB-274 (`BloodUnitSupplierLabelTests.SupplierLabel_KeepsTheSupplierIdWhenTheDeliveryNamesABlankSupplier`; `DeliveryServiceTests.UnitChart_ShowsTheSupplierIdAndDoesNotRewriteTheStoredName`).
- Unit supplier-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The expected-inbound board still shows a blank supplier name as empty. When that unit's delivery names the supplier, show Supplier {id}. Do not rewrite the stored supplier or collection facility. A unit with no delivery does not invent a supplier.

## Iteration 232 — Supplier id on expected inbound (2026-10-15)

The expected-inbound board showed a blank supplier name as empty. Staff could miss which supplier was expected.

### Implemented

- The board shows `Supplier {id}` when the name is blank and the delivery names that supplier.
- A recorded name stays that name.
- The stored supplier and collection facility are unchanged.
- A unit with no delivery does not invent a supplier.

### Requirements / risk

- URS-BB-385, FRS-BB-420, SRS-BB-382, RISK-BB-487.
- Does not claim AABB compliance.

### Tests

- TEST-BB-275 (`ExpectedInboundSupplierLabelTests.SupplierLabel_KeepsTheSupplierIdWhenTheDeliveryNamesABlankSupplier`; `DeliveryServiceTests.ExpectedInbound_ShowsTheSupplierIdAndDoesNotRewriteTheStoredName`).
- Expected-inbound supplier-label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. A product choice still joins the code and the name, so a blank name can leave the code looking like the product. Keep the code available for selection, and do not use the code as the name.

## Iteration 233 — Product choice keeps the code (2026-10-16)

A modification-rule choice and a charge-rule product choice joined the code and the name. A blank name left the code looking like the product.

### Implemented

- `ProductTypeLabel.ForChoice` keeps the code and shows `Product {id}` when the name is blank.
- Modification-rule source and target choices use that label. The display code stays the ISBT code when one is recorded, otherwise the facility product code.
- Charge-rule product choices keep the facility product code. An ISBT code is not used as the name.
- The stored product name is unchanged.

### Requirements / risk

- URS-BB-386, FRS-BB-421, SRS-BB-383, RISK-BB-488.
- Does not claim AABB compliance.

### Tests

- TEST-BB-276 (`ProductTypeLabelTests`; `ProductTypeChoiceTests`).
- Product-choice label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. The product catalog still shows a blank name as empty beside the code. Keep the code in the catalog, and show Product {id} on the list. Do not write that label onto the product. The stored name and the edit form stay unchanged.

## Iteration 234 — Product catalog shows the product id (2026-10-16)

The product catalog showed a blank name as empty beside the code. Staff could read the code as the product name.

### Implemented

- The product list uses `ProductTypeLabel.Name`. A blank name stays `Product {id}`.
- The code column stays the stored product code. An ISBT code is not used as the name.
- The stored name is unchanged. The edit form still shows that stored name.

### Requirements / risk

- URS-BB-387, FRS-BB-422, SRS-BB-384, RISK-BB-489.
- Does not claim AABB compliance.

### Tests

- TEST-BB-277 (`ProductTypeLabelTests.Name_KeepsTheProductIdWhenTheNameIsBlank`; `ProductTypeChoiceTests.NameLabel_KeepsTheProductIdWhenTheNameIsBlank`).
- Product-catalog label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables. An order still lists a product by name only, so a blank name leaves an empty choice. Keep the code available, and show Product {id} when the name is blank. Do not use the code as the name. The stored name stays unchanged.

## Iteration 235 — Order product choice keeps the code (2026-10-16)

A new or edited order listed each product by name only. A blank name left an empty choice.

### Implemented

- New and edited order product choices use `ProductTypeDto.ChoiceLabel`.
- The display code stays on the choice. A blank name stays `Product {id}` and is not replaced by the code.
- The stored product name is unchanged. The checkbox still submits the product type id.

### Requirements / risk

- URS-BB-388, FRS-BB-423, SRS-BB-385, RISK-BB-490.
- Does not claim AABB compliance.

### Tests

- TEST-BB-278 (`ProductTypeChoiceTests.OrderChoice_KeepsTheCodeAndDoesNotUseItAsTheName`).
- Order product-choice label tests green.

### Next ranked residual

Next non-SME residual by safety and operational impact. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 236 — Computer-evaluated storage time out (2026-10-16)

A caller could still be recorded as deciding whether time out of storage was acceptable. WellSky treats a storage device as governed by, or exempt from, that limit.

### Implemented

- `InventoryLocation.TimeOutGoverned` defaults to governed.
- Return uses `ReturnReissueRule.TimeOutAcceptable`. A missing location is governed. An exempt location does not fail the existing window. A caller flag cannot override the computer.
- The inventory-location screen and the issued-units board show Governed, Exempt, or Window elapsed.

### Requirements / risk

- URS-BB-389, FRS-BB-424, SRS-BB-386, RISK-BB-491.
- Does not claim AABB compliance. The 30-minute window is unchanged.

### Tests

- TEST-BB-279.

### Next ranked residual

Modification result label check. A quarantine release of a modification result still does not require a matching unit-number scan.

## Iteration 237 — Modification result label check (2026-10-16)

A modification result could leave quarantine without a check that the new label is the result unit.

### Implemented

- `ModificationLabelVerificationRule` hard-stops a blank or different unit-number scan when the quarantined unit is a modification result.
- The quarantine board and the unit chart collect that scan. Other quarantine releases do not.

### Requirements / risk

- URS-BB-390, FRS-BB-425, SRS-BB-387, RISK-BB-492.
- Does not invent ICCBBA codes. Does not claim AABB compliance.

### Tests

- TEST-BB-280.

### Next ranked residual

Reagent lot and daily QC. Patient resulting stays unblocked unless a facility policy is turned on. That policy stays off.

## Iteration 238 — Reagent lot and daily QC (2026-10-16)

Daily reagent QC was outside the record.

### Implemented

- Reagent, lot, and daily QC records. Review must be a different user.
- The worklist shows overdue, failed, and unreviewed rows.
- Patient resulting is blocked only when `Result.BlockWhenReagentQcOpen` is on. It ships off.

### Requirements / risk

- URS-BB-391, FRS-BB-426, SRS-BB-388, RISK-BB-493.
- Does not invent a CLIA hold. Does not claim AABB compliance.

### Tests

- TEST-BB-281.

## Iteration 239 — Overdue final disposition (2026-10-16)

Open issues were not aged to a final fate.

### Implemented

- A warning board of issued units past `Issue.FinalDispositionDueHours` (default 24) with no transfusion document, return, or discard.
- The board does not block issue or documentation.

### Requirements / risk

- URS-BB-392, FRS-BB-427, SRS-BB-389, RISK-BB-494.

### Tests

- TEST-BB-282.

## Iteration 240 — Supply lot on a modification (2026-10-16)

Modification history did not record the supply used.

### Implemented

- Optional supply description and lot on `UnitModification`, audited and shown on history.
- `Modification.RequireSupplyLot` can require the lot and ships off.

### Requirements / risk

- URS-BB-393, FRS-BB-428, SRS-BB-390, RISK-BB-495.

### Tests

- TEST-BB-283.

## Iteration 241 — Batch modification (2026-10-16)

One modification rule could be applied to only one unit at a time.

### Implemented

- `ApplyBatchAsync` uses one reason and one existing rule for several units.
- If any unit is ineligible, none are modified.

### Requirements / risk

- URS-BB-394, FRS-BB-429, SRS-BB-391, RISK-BB-496.

### Tests

- TEST-BB-284.

## Iteration 242 — Par-level stock board (2026-10-16)

Available stock was not compared with a minimum.

### Implemented

- A configurable minimum by product and optional ABO.
- The board shows on-hand and the shortfall. It does not place a blood-center order.

### Requirements / risk

- URS-BB-395, FRS-BB-430, SRS-BB-392, RISK-BB-497.

### Tests

- TEST-BB-285.

### Next ranked residual

The competitor-gap queue is empty. Excluded items stay excluded. Do not invent ICCBBA tables, RhIG indications, a device or FHIR contract, a multi-facility database, or a numeric antigen-match grade.

## Iteration 243 — Dashboard shows the queues you can work (2026-10-17)

The home screen counted patients and inventory. Retrospective crossmatch, in transit, outstanding issued units, pending tests, reactions, shift handoff, and the supervisor queue already existed on their own pages. A technologist had to remember which page held the next action.

### Implemented

- **Your work** lists ready to issue, second ABO/Rh, retrospective crossmatch, in transit, outstanding issued, final disposition, pending tests, pending specimens, reaction investigations, the supervisor queue, and an open shift handoff.
- Each card is shown only when the signed-in user can already open that queue, and the card opens it.
- A failed load shows an em dash. Closed investigations stay out of the reaction count.
- Retrospective items past their stored due time, in-transit units past the arrival window, and fatality follow-up including closed cases are named only when that count is above zero.
- `/issuing?desk=` opens the named board. A patient or unit prefill still opens Issue.
- Issue, ward receipt, result, and investigation gates are unchanged.

### Requirements / risk

- URS-BB-396, FRS-BB-431, SRS-BB-393, RISK-BB-498.
- Does not claim AABB compliance.

### Tests

- TEST-BB-286 (`DashboardQueueCatalogTests`).

### Next ranked residual

A new or edited order still lists a test by its stored name. A blank name leaves an empty label beside the code, and a missing catalog name is stored as the code. Keep the code available, and show a stable test id when the name is blank. Do not rewrite a name that is already stored. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 244 — Order test choice keeps the code (2026-10-17)

A new or edited order listed each test by name only. A blank name left an empty choice beside the code.

### Implemented

- New and edited order test choices use `TestDefinitionListItemDto.NameLabel`.
- The code stays on the choice. A blank name stays `Test {id}` and is not replaced by the code.
- A recorded name stays that name. The stored line name is unchanged. The checkbox still submits the test code.

### Requirements / risk

- URS-BB-397, FRS-BB-432, SRS-BB-394, RISK-BB-499.
- Does not claim AABB compliance.

### Tests

- TEST-BB-287 (`TestDefinitionChoiceTests.OrderChoice_KeepsTheCodeAndDoesNotUseItAsTheName`).

### Next ranked residual

The test catalog still shows a blank name as empty beside the code. Keep the code in the catalog, and show Test {id} when the name is blank. Do not rewrite the stored name or the edit form. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 245 — Test catalog keeps the code (2026-10-17)

The test catalog listed each test by its stored name. A blank name left an empty cell beside the code.

### Implemented

- The test catalog name column uses `TestDefinitionDto.NameLabel`.
- The code stays in the catalog. A blank name stays `Test {id}` and is not replaced by the code.
- The stored name is unchanged. The edit form still binds that stored name. The filter can match the list label.

### Requirements / risk

- URS-BB-398, FRS-BB-433, SRS-BB-395, RISK-BB-500.
- Does not claim AABB compliance.

### Tests

- TEST-BB-288 (`TestDefinitionChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName`).

### Next ranked residual

The subtest catalog still shows a blank name as empty beside the code. Keep the code in the catalog, and show Subtest {id} when the name is blank. Do not rewrite the stored name or the edit form. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 246 — Subtest catalog keeps the code (2026-10-17)

The subtest catalog listed each subtest by its stored name. A blank name left an empty cell beside the code.

### Implemented

- The subtest catalog name column uses `SubtestDefinitionDto.NameLabel`.
- The code stays in the catalog. A blank name stays `Subtest {id}` and is not replaced by the code.
- The stored name is unchanged. The edit form still binds that stored name. The filter can match the list label.

### Requirements / risk

- URS-BB-399, FRS-BB-434, SRS-BB-396, RISK-BB-501.
- Does not claim AABB compliance.

### Tests

- TEST-BB-289 (`SubtestDefinitionChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName`).

### Next ranked residual

The test-grouper catalog still shows a blank name as empty beside the code. Keep the code in the catalog, and show Grouper {id} when the name is blank. Do not rewrite the stored name or the edit form. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 247 — Test-grouper catalog keeps the code (2026-10-17)

The test-grouper catalog listed each grouper by its stored name. A blank name left an empty cell beside the code.

### Implemented

- The test-grouper catalog name column uses `TestGrouperDto.NameLabel`.
- The code stays in the catalog. A blank name stays `Grouper {id}` and is not replaced by the code.
- The stored name is unchanged. The edit form still binds that stored name. The filter can match the list label.

### Requirements / risk

- URS-BB-400, FRS-BB-435, SRS-BB-397, RISK-BB-502.
- Does not claim AABB compliance.

### Tests

- TEST-BB-290 (`TestGrouperChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName`).

### Next ranked residual

The phase catalog still shows a blank name as empty beside the code. Keep the code in the catalog, and show Phase {id} when the name is blank. Do not rewrite the stored name or the edit form. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 248 — Phase catalog keeps the code (2026-10-17)

The phase catalog listed each phase by its stored name. A blank name left an empty cell beside the code.

### Implemented

- The phase catalog name column uses `PhaseDefinitionDto.NameLabel`.
- The code stays in the catalog. A blank name stays `Phase {id}` and is not replaced by the code.
- The stored name is unchanged. The edit form still binds that stored name. The filter can match the list label.

### Requirements / risk

- URS-BB-401, FRS-BB-436, SRS-BB-398, RISK-BB-503.
- Does not claim AABB compliance.

### Tests

- TEST-BB-291 (`PhaseDefinitionChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName`).

### Next ranked residual

The blood-attribute catalog still shows a blank antigen name or a blank antibody name as empty beside the code. Keep the code in the catalog. Show Attribute {id} when the antigen name is blank, and Antibody {id} when the antibody name is blank. Do not rewrite the stored names or the edit form. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 249 — Blood-attribute catalog keeps the code (2026-10-17)

The blood-attribute catalog listed each antigen and antibody by its stored name. A blank name left an empty cell beside the code.

### Implemented

- The antigen column uses `BloodAttributeDefinitionDto.NameLabel`.
- The antibody column uses `BloodAttributeDefinitionDto.AntibodyNameLabel`.
- The code stays in the catalog. A blank antigen name stays `Attribute {id}`. A blank antibody name stays `Antibody {id}` and is not replaced by the antigen name or the code.
- The stored names are unchanged. The edit form still binds those stored names. The filter can match either list label.

### Requirements / risk

- URS-BB-402, FRS-BB-437, SRS-BB-399, RISK-BB-504.
- Does not claim AABB compliance.

### Tests

- TEST-BB-292 (`BloodAttributeChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName`).

### Next ranked residual

The special-requirement catalog still shows a blank name as empty beside the code. Keep the code in the catalog, and show Requirement {id} when the name is blank. Do not rewrite the stored name or the edit form. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 250 — Special-requirement catalog keeps the code (2026-10-17)

The special-requirement catalog listed each requirement by its stored name. A blank name left an empty cell beside the code.

### Implemented

- The special-requirement catalog name column uses `SpecialRequirementDefinitionDto.NameLabel`.
- The code stays in the catalog. A blank name stays `Requirement {id}` and is not replaced by the code or by a product-attribute code.
- The stored name is unchanged. The edit form still binds that stored name. The filter can match the list label.

### Requirements / risk

- URS-BB-403, FRS-BB-438, SRS-BB-400, RISK-BB-505.
- Does not claim AABB compliance.

### Tests

- TEST-BB-293 (`SpecialRequirementChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName`).

### Next ranked residual

The reflex-rule catalog still shows a blank name as empty beside the code. Keep the code in the catalog, and show Reflex {id} when the name is blank. Do not rewrite the stored name or the edit form. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 251 — Reflex-rule catalog keeps the code (2026-10-17)

The reflex-rule catalog listed each rule by its stored name. A blank name left an empty cell beside the code.

### Implemented

- The reflex-rule catalog name column uses `ReflexRuleDto.NameLabel`.
- The code stays in the catalog. A blank name stays `Reflex {id}` and is not replaced by the code or by a trigger or reflex test code.
- The stored name is unchanged. The edit form still binds that stored name. The filter can match the list label.

### Requirements / risk

- URS-BB-404, FRS-BB-439, SRS-BB-401, RISK-BB-506.
- Does not claim AABB compliance.

### Tests

- TEST-BB-294 (`ReflexRuleChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName`).

### Next ranked residual

The order-and-test rule catalog still shows a blank name as empty beside the code. Keep the code in the catalog, and show Rule {id} when the name is blank. Do not rewrite the stored name or the edit form. Do not post elution findings as structured antibody history. OCD-004 remains open: do not invent ICCBBA tables.

## Iteration 252 — Scheduled unit expiration, competitor gap G1 (2026-10-31)

Round 2 of the competitor comparison (SafeTrace Tx, SoftBank.web, WellSky Transfusion; transfusion side only by product-owner decision) found nine gaps. They are queued in `docs/COMPETITOR_GAP_QUEUE.md`. G1: units past expiration moved to Expired only when someone ran the sweep by hand.

### Implemented

- `ExpiredUnitSweepService` (Api hosted service) runs `InventoryService.RunScheduledExpirationSweepAsync` every `Inventory:ExpirationSweepSeconds` (default 300; 0 disables).
- The scheduled method and the manual `ExpireDueUnitsAsync` share one core sweep. The manual path keeps its `inventory.discard` check. No endpoint maps the scheduled method.
- Status history and audit are written as the system account with path `ScheduledExpireSweep`. Terminal statuses are skipped through `InventoryStatusTransition`. A second run expires nothing new.
- The issue gate is unchanged and still blocks expired units on its own.
- User manual: the inventory page has a new Expiration section.

### Requirements / risk

- URS-BB-405, FRS-BB-440, SRS-BB-402, RISK-BB-507.
- Does not claim AABB compliance.

### Tests

- TEST-BB-295 (`InventoryServiceTests.ScheduledExpirationSweep_ExpiresDueUnits_WithoutUserPermission`, `InventoryServiceTests.ScheduledExpirationSweep_SkipsTerminalStatuses`).
- Full suite: 2,807 passed, 0 failed (baseline 2,805). Application 82, Astm 19, HL7 63, Domain 1,598, Printing 21, Help 5, Integration 1,019.
- Run check: the API started with the log line "Expired-unit sweep running every 300s." and the first run expired 1 past-due unit in the development database. No server errors.

### Next ranked residual

G2: storage temperature log and excursions. Do not auto-quarantine on an excursion until an OCD row is decided.

## Iteration 253 — Storage temperature log and excursions, competitor gap G2 (2026-10-31)

Competitor equipment-QC modules log storage temperatures. Here, locations had a minimum and maximum, but nothing compared readings against them.

### Implemented

- New `StorageTemperatureReading` entity, append-only. It holds location, reading in °C, time, recorded-by, the range in force at the time, status, deviation link, and comment. Added SQL Server migration `20261031120000_StorageTemperatureReadings`, the development SQLite bootstrap table, and the SQLite-to-SQL Server copy order (after `InventoryLocations`).
- `StorageTemperatureRule`: in range, below, above, or no range. A plausibility guard rejects values below -100 or above 60 °C, and a reading time is allowed at most five minutes in the future. `StoredStatuses` defines which units are still physically at a location.
- `StorageTemperatureService`: records a reading, then opens a Major deviation for an excursion (`ContextType = StorageTemperatureReading`). The deviation names the location, the reading, the range, and the number of units stored there. Both writes are audited. It also lists locations with their last reading, 7-day history, and open excursions (until the deviation is closed).
- `/api/storage-temps` endpoints. Recording requires `inventory.receive`.
- `/storage-temps` page, a "Storage temps" nav link, a "Temperature excursions" dashboard card (for `inventory.receive` or `deviation.manage`), and a help topic (`docs/user-manual/clinical/storage-temps.md`).
- No automatic quarantine. Recorded as OCD-036, **REQUIRES REGULATORY / SME VERIFICATION**.

### Found during verification

- The full suite failed `HelpCatalogTests.Every_page_route_has_a_topic` until the help topic was added.
- In the running app, one in-range test produced two identical rows. The record handler set `_busy` but did not check it on entry, so a second click that arrived before the disabled button rendered went through. Added the guard. Re-test: two programmatic clicks in a row stored one reading and one deviation.

### Requirements / risk

- URS-BB-406, FRS-BB-441, SRS-BB-403, RISK-BB-508, OCD-036.
- Does not claim AABB compliance.

### Tests

- TEST-BB-296: `StorageTemperatureRuleTests` (15 cases), `DashboardQueueCatalogTests.Select_TemperatureExcursions_FollowsReceiveOrDeviationPermission`, and `StorageTemperatureServiceTests` (6). The service tests cover in range, excursion with deviation and no quarantine, no range, rejects, the open list clearing when the deviation closes, and the range snapshot.
- Full suite: 2,829 passed, 0 failed (previously 2,807). Application 83, Astm 19, HL7 63, Domain 1,613, Printing 21, Help 5, Integration 1,025.
- Run check (development database, DevMode): FRIDGE-1 at 4.0 °C was recorded in range. OR-FRIDGE at 9.5 °C opened DEV-2 (Major, Open, linked to reading #3), and the dashboard card showed 1. No server errors. Not checked in the UI: a user without `inventory.receive` (DevMode grants every permission). The endpoint uses the same `RequirePermission` filter as the other endpoints.
- The development database keeps three verification readings (two duplicate FRIDGE-1 rows from before the fix, plus the OR-FRIDGE excursion) and deviation DEV-2. Readings are append-only by design.

### Next ranked residual

G3: coded discard reasons and a wastage report.


## Iteration 254 — Coded discard reasons and wastage report, competitor gap G3 (2026-10-31)

Competitor products report wastage by cause. Here the discard reason was free text, and the discards report could not group it.

### Implemented

- `UnitDiscardReason` admin catalog (code, text, comment required, sort, active), cloned from the order-cancellation-reason pattern. It has an admin service with configuration history, `/api/admin/discard-reasons`, `/api/reference/discard-reasons`, the `/admin/discard-reasons` page, a nav link, an Admin Home card, and a help topic.
- Starter codes, seeded only when missing: EXPIRED, BAG, VISUAL, TEMP, RETURN, RECALL, and OTHER (comment required). These are facility-editable examples, not a regulatory list.
- `BloodProducts.DiscardReasonCode`. Migration `20261031140000_UnitDiscardReasons`, plus the development SQLite bootstrap table and column, and the SQLite-to-SQL Server copy list.
- `DiscardReasonRule`: a blank, unknown, or inactive code, or a missing required comment, is a HardStop. `DiscardAsync` now takes a `DiscardUnitCommand` (code, comment, second verifier). Using a record makes an old positional free-text call fail to compile instead of being read as a code. The unit stores the code and the text "catalog text: comment". History reads "CODE: text"; the audit carries `DiscardReasonCode`. The permission and second-verifier checks are unchanged.
- Unit page: discard has a reason dropdown and a comment field. The comment shows "(required)" for a code that needs it, and Discard stays disabled until the input is complete.
- Reports: the discards source and the `rpt_discards` view add `discard_reason_code`, `discarded_utc` (latest status-history row to Discarded), and `discarded_month`. New seeded report RPT-WASTAGE ("Wastage by product and reason") groups by month, product, and code with a unit count. The RPT-DISCARDS layout gains those columns, but only on new databases: existing system reports are not rewritten.

### Requirements / risk

- URS-BB-407, FRS-BB-442, SRS-BB-404, RISK-BB-509.
- Does not claim AABB compliance.

### Tests

- TEST-BB-297:
  - `DiscardReasonRuleTests` (7 cases)
  - `InventoryServiceTests.Discard_StoresTheCode_AndBlocksUnknownInactiveOrMissingComment`
  - `DiscardReasonWorkflowTests.Catalog_NormalizesCode_RejectsDuplicates_AndKeepsTheCommentFlag`
  - `DiscardReasonWorkflowTests.WastageReport_CountsDiscardsByMonthProductAndCode` (the visual grouping and the SQL view)
- Updated: the nine existing discard calls now pass a code. The blank-reason test now expects `DISCARD-REASON-REQUIRED`. The seeded report count changed from 8 to 9.
- Full suite: 2,839 passed, 0 failed (previously 2,829). Application 83, Astm 19, HL7 63, Domain 1,620, Printing 21, Help 5, Integration 1,028.
- Run check (development database, DevMode):
  - The admin page listed the seven codes, with OTHER marked "Required".
  - On unit W0001230001009, Discard stayed disabled with OTHER and no comment, and enabled once a comment was typed.
  - The first submit was blocked by the existing second-verifier policy (INV-DISC-2ND). With tech1 as verifier, the unit was discarded.
  - RPT-WASTAGE showed `2026-10 | Apheresis Platelets | OTHER | 1`, plus one blank-code row for a seed unit discarded before this change. No server errors.
- Development database change: unit W0001230001009 is now Discarded (OTHER, "Verification test: dropped during handling").

### Next ranked residual

G4: crossmatch-to-transfusion ratio and utilization report.


## Iteration 255 — Crossmatch-to-transfusion utilization, competitor gap G4 (2026-10-31)

Competitor products report blood utilization, for example the C:T ratio by service and physician. Here, crossmatch and transfusion rows existed only as an ungrouped activity report.

### Implemented

- `UtilizationRule` defines the arithmetic:
  - A unit counts as transfused when the transfusion is completed or stopped (wasted and returned do not count).
  - C:T ratio = crossmatched units / transfused units, rounded to 2 places, or null when nothing was transfused.
  - Percentages are rounded to 1 place, or null with a zero base.
- `UtilizationService` works over a crossmatch date range:
  - It counts each patient-and-unit pair with a compatible crossmatch (any method) once. Issue and transfusion are matched on that pair whenever they happened.
  - The ordering location and provider come from the earliest crossmatch's order (catalog provider name, otherwise free text). A crossmatch with no order is listed as "(no order)".
  - It returns rows by location and provider, plus a total.
- `GET /api/utilization` (requires `report.run`). Added the `/utilization` page (date range, default last 30 days, total row in bold), a "Utilization" nav link beside Reports, and a help topic.
- No target or benchmark is supplied. Any threshold is a facility decision.

### Requirements / risk

- URS-BB-408, FRS-BB-443, SRS-BB-405, RISK-BB-510.
- Does not claim AABB compliance.

### Tests

- TEST-BB-298: `UtilizationRuleTests` (6 cases) and `UtilizationServiceTests` (2).
  - The fixture has 3 groups and covers: a repeat crossmatch counted once, incompatible and out-of-range crossmatches excluded, a wasted unit counted as issued but not transfused, a catalog provider and a free-text provider, no order, and a zero-transfusion row showing a dash.
  - The empty-period case also checks that reversed dates are swapped.
- Full suite: 2,847 passed, 0 failed (previously 2,839). Application 83, Astm 19, HL7 63, Domain 1,626, Printing 21, Help 5, Integration 1,030.
- Run check (development database, DevMode): `/utilization` loaded and showed one compatible crossmatch with no order, 0 issued, and C:T "—", for both the last 30 days and a range from 2025-01-01. The development seed has only one compatible crossmatch. No server errors.

### Next ranked residual

G5: in-app alert inbox.


## Iteration 256 — In-app alert inbox, competitor gap G5 (2026-10-31)

Competitor products push alerts for storage, expiry, and QC. Here the dashboard showed counts only, and each worklist had to be opened to see a problem.

### Implemented

- `Alert` entity and `Alerts` table. Migration `20261031160000_Alerts`, plus the development SQLite bootstrap and the SQLite-to-SQL Server copy list.
- `AlertRule`: severity, source constants, open versus active, and a pure `Reconcile` that returns keys to raise and active alerts to resolve. An active key is not raised again, even after acknowledgement.
- `AlertService`:
  - `RaiseAsync` raises an event alert, deduplicated against active alerts.
  - `SyncAsync` reconciles one condition source.
  - Lists open alerts plus those closed in the last 7 days, and counts open ones.
  - `AcknowledgeAsync` records user, time, and note, is audited, and can happen only once.
- Producers:
  - `StorageTemperatureService` raises a Critical alert on an excursion that names the deviation.
  - The scheduled expiry sweep raises "N unit(s) expired".
  - `AlertScanner` with the hosted `AlertScanService` (every `Alerts:ScanSeconds`, default 300) syncs three condition sources: par shortfalls, near-expiry units, and reagent QC work (overdue, failed, unreviewed; failed is Critical).
- `/api/alerts` (list, count, acknowledge). Added the `/alerts` page (open and closed in 7 days, link to the worklist, optional note), an `AlertBadge` in the top bar (reloads on navigation, every minute, and after an acknowledgement), an "Alerts" nav link, and a help topic.
- In-app only: no email or SMS contract exists (recorded under Excluded). Alerts never change a unit, deviation, or QC row.

### Requirements / risk

- URS-BB-409, FRS-BB-444, SRS-BB-406, RISK-BB-511.
- Does not claim AABB compliance.

### Tests

- TEST-BB-299: `AlertRuleTests` (5) and `AlertWorkflowTests` (5). They cover:
  - deduplication after acknowledgement
  - sync raising and resolving without touching other sources
  - acknowledgement audit, single use, and not found
  - the scanner raising par, near-expiry, and QC alerts, raising nothing on a repeat scan, and resolving par once stock arrives
  - the expired-sweep event, including zero units
  - the excursion producer firing only out of range
- Full suite: 2,857 passed, 0 failed (previously 2,847). Application 83, Astm 19, HL7 63, Domain 1,631, Printing 21, Help 5, Integration 1,035.
- Run check (development database, DevMode):
  - On startup the scan created the table and raised 2 alerts: "Reagent QC overdue: lot UXLOT1" and "Below par: RBC-LR group O", with 6 on hand against a minimum of 50. Correctly, no near-expiry alert: the soonest unit expires in about 47 hours against a 24-hour window.
  - The badge showed "Alerts (2)". Acknowledging the QC alert with a note moved it to Closed with name and note, and the badge dropped to 1.
  - A 26.5 °C reading on PLT-1 opened DEV-3 and raised a Critical alert listed first, and the badge showed 2.
  - No server errors.
- Development database changes: the alerts above, deviation DEV-3, and one PLT-1 reading.

### Next ranked residual

G6: CSV export for reports and the audit trail.


## Iteration 257 — CSV export for reports and the audit trail, competitor gap G6 (2026-10-31)

Competitor products export to CSV for downstream systems. Here, reports exported only to PDF and XLSX, and the audit trail could only be read on screen.

### Implemented

- `CsvFormat` (Domain): UTF-8 with a byte-order mark, RFC 4180 quoting (comma, quote, CR/LF, or leading or trailing space), and CRLF lines. A cell starting with =, +, -, @, tab, or CR gets a leading apostrophe so a spreadsheet does not execute it. A signed number such as -18.5 stays a number.
- Report export accepts `csv` (`RPT-CODE.csv`, `text/csv; charset=utf-8`) and is audited like PDF and XLSX. The run page has an **Export CSV** button.
- `AuditCsvExport` and `GET /api/audit-events/export` (inside the `audit.read` group): the current filters, newest first, up to 50,000 rows. Columns include old and new JSON and the hash chain. Each export writes an Export audit event (entity `AuditTrail`, with filters and row count). The audit page has an **Export CSV** button.
- User manual: updated the reports, report-run, and audit pages.

### Requirements / risk

- URS-BB-410, FRS-BB-445, SRS-BB-407, RISK-BB-512.
- Does not claim AABB compliance.

### Tests

- TEST-BB-300: `CsvFormatTests` (16 cases) and `CsvExportTests` (2).
  - `CsvFormatTests` covers quoting, the formula guard, signed numbers, the byte-order mark, and CRLF.
  - `CsvExportTests` covers the report CSV (columns, a quoted product name, a guarded `=CMD()` discard reason, the audit event, and a rejected `txt` format) and the audit CSV (JSON quoting, a guarded `@` reason, and the record hash as the last column).
- Two of my own test expectations were wrong on the first run and were corrected: a tab after the apostrophe needs no quotes, and the audit test now filters to its own event because the database holds seed events.
- Full suite: 2,875 passed, 0 failed (previously 2,857). Application 83, Astm 19, HL7 63, Domain 1,647, Printing 21, Help 5, Integration 1,037.
- Run check (development database, DevMode):
  - RPT-WASTAGE CSV: 200, `text/csv; charset=utf-8`, attachment `RPT-WASTAGE.csv`, with the same rows as the screen.
  - Audit CSV filtered to Discard returned the G3 discard event with its JSON and hashes.
  - The Export audit filter showed the `AuditTrail` export events (two: the first test script attempt also exported).
  - No server errors.

### Next ranked residual

G7: supplier order from the par shortfall.


## Iteration 258 — Supplier order from par shortfall, competitor gap G7 (2026-10-31)

SafeTrace Tx generates blood orders to the supplier. Here the par board showed the shortfall, but the order was placed outside the system.

### Implemented

- `SupplierOrder` and `SupplierOrderLine` entities. Migration `20261031180000_SupplierOrders`, plus the development SQLite bootstrap and the SQLite-to-SQL Server copy list (after `BloodDeliveries`).
- `SupplierOrderRule`:
  - Lifecycle is Draft → Submitted → Received, or Cancelled from Draft or Submitted with a reason.
  - Only a draft can be edited; quantity must be 0 to 999 (an entry guard, not an ordering rule).
  - Submit needs units and a valid signature. Receive needs a Submitted order and a delivery from the same supplier.
- `SupplierOrderService`:
  - Creates a draft from the par shortfall (fails when nothing is short or the supplier is inactive). Order numbers are SO-yyyyMMdd-NNN.
  - Edits replace lines; zeroed lines are kept and hidden, never deleted.
  - Submit validates the e-signature with `ISignatureService` for the current user and action `SupplierOrderSubmit`, consumes it, and writes the signature id on the audit event.
  - Also handles receive, cancel, and audited PDF or CSV export.
- `/api/inventory/supplier-orders` endpoints (changes require `inventory.receive`).
- `/inventory/orders` page (create from par, edit, sign and submit, export, link delivery, cancel), a "Supplier orders" nav link, and a help topic.

### Requirements / risk

- URS-BB-411, FRS-BB-446, SRS-BB-408, RISK-BB-513.
- Does not claim AABB compliance.

### Tests

- TEST-BB-301: `SupplierOrderRuleTests` (11 cases) and `SupplierOrderTests` (3, with a signature double). They cover:
  - the shortfall becoming a draft line
  - edit, merge, zero, and over-limit
  - an invalid signature rejected, and a valid one consumed and written on the audit
  - no edit after submit
  - a delivery from the wrong supplier rejected and the right one linked
  - no cancel after receive
  - CSV content, PDF header, a rejected format, and audited exports
  - nothing below par, an inactive supplier, and cancel without a reason
- Full suite: 2,889 passed, 0 failed (previously 2,875). Application 83, Astm 19, HL7 63, Domain 1,658, Printing 21, Help 5, Integration 1,040.
- Run check (development database, DevMode):
  - Create draft for American Red Cross gave SO-20261031-001 with RBC-LR group O for 44 units (the par shortfall).
  - Quantity changed to 40 and a note was saved.
  - Sign and submit with a placeholder secret was refused ("Password or PIN re-authentication is required to sign"). The development account has a password, and none was guessed. Submit and receive are verified by the integration tests, not in the UI.
  - The CSV export matched the saved order.
  - The order was then cancelled ("Verification test only").
  - No server errors.
- Development database change: cancelled order SO-20261031-001.

### Next ranked residual

G8: database backup and restore check.


## Iteration 259 — Database backup and restore check, competitor gap G8 (2026-10-31)

Hosted competitor products include managed backup. Here, restore was described in `DOWNTIME_PLAN.md`, but nothing created or checked backups.

### Implemented

- `DatabaseBackup` entity and table. Migration `20261031200000_DatabaseBackups`, plus the development SQLite bootstrap and the SQLite-to-SQL Server copy list.
- `DatabaseBackupRule`: good means written and verified. It also defines last good, due, overdue, retention (the newest N good files; failed or already-removed ones ignored), and the file name.
- `DatabaseBackupService` (Infrastructure):
  - SQLite writes `VACUUM INTO`, then opens the copy read-only and runs `PRAGMA integrity_check`. It records the size and SHA-256, and the audit event count and last `RecordHash` from inside the copy as the manifest.
  - SQL Server runs `BACKUP DATABASE … WITH COPY_ONLY, CHECKSUM, INIT`, then `RESTORE VERIFYONLY … WITH CHECKSUM` (written on the database server).
  - Every attempt is logged and audited. A failure raises a critical `backup-failed` alert.
  - Retention deletes old files only inside the configured folder, and keeps the log rows.
  - A `backup-overdue` condition alert opens and resolves with the backup state.
- `DatabaseBackupScheduleService` checks hourly against the `Backup` settings (Folder, IntervalHours 24, KeepCount 14, OverdueHours 48). `/api/admin/backups` GET and POST. Added the `/admin/backups` page (status, Back up now, recent attempts with hash and manifest), the Admin nav, an Admin Home card, and a help topic.
- `DOWNTIME_PLAN.md` section 4 now gives SQLite and SQL Server restore steps and the manifest check. `**/backups/` is git-ignored.

### Requirements / risk

- URS-BB-412, FRS-BB-447, SRS-BB-409, RISK-BB-514.
- Does not claim AABB compliance.

### Tests

- TEST-BB-302: `DatabaseBackupRuleTests` (4) and `DatabaseBackupServiceTests` (4, against the in-memory SQLite database). They cover:
  - a verified copy with matching SHA-256 and manifest
  - the copy opened as a database and containing the probe audit row (a restore read)
  - status and due checks
  - retention removing the oldest of three with keep 2, while log rows stay
  - a failure logged with a critical alert
  - the overdue alert opening with no backup and resolving after a good one
- Not exercised: the SQL Server BACKUP and RESTORE VERIFYONLY path. Tests and development run on SQLite.
- Full suite: 2,897 passed, 0 failed (previously 2,889). Application 83, Astm 19, HL7 63, Domain 1,662, Printing 21, Help 5, Integration 1,044.
- Run check (development database, DevMode):
  - On startup the schedule wrote `bloodbanklis-20261031-181247.db` (3,584,000 bytes), verified with integrity_check ok and a manifest of 1,885 audit events.
  - **Back up now** wrote `bloodbanklis-20261031-181300.db`, verified with 1,888 audit events.
  - The page showed both with SHA-256 and the last hash. The files are in `src/BloodBankLIS.Api/backups` and are git-ignored.
  - No server errors.

### Next ranked residual

G9: MFA for privileged users.


## Iteration 260 — MFA for privileged users, competitor gap G9 (2026-10-31)

Hosted competitor products offer multi-factor sign-in. Here sign-in was password only (with a PIN for signatures).

### Implemented

- `Totp` (Domain): RFC 6238 (HMAC-SHA1, 30 s, 6 digits), ±1 step drift, replay blocked by last used step, constant-time compare, RFC 4648 base32, and an `otpauth://` provisioning URI.
- `MfaRule`: an enrolled user is always challenged. With the policy on, a privileged user who has not enrolled is asked to enroll. Privileged = admin.users.manage, admin.roles.manage, admin.config.edit, admin.config.activate, or override.approve.
- `Users` gains columns `TotpSecretProtected`, `TotpPendingSecretProtected`, `TotpEnrolledUtc`, and `TotpLastStep`. Migration `20261031220000_UserTotp`, plus the development SQLite columns.
- New facility policy `Security.RequirePrivilegedMfa`, default false, listed under Facility Policies.
- `ISecretProtector` with `DataProtectionSecretProtector` (ASP.NET Core Data Protection, keys in `dataprotection-keys` under the API content root, git-ignored).
- `AuthSessionService.LoginAsync` takes an optional one-time code:
  - A missing code returns `MFA_REQUIRED` (HTTP 401 with `mfaRequired`) and no failure is counted.
  - A wrong code goes through the same failed sign-in and lockout path.
  - The login audit records `SecondFactor: TOTP` or `MfaEnrollmentRequired`.
- `MfaService` handles status, begin (key shown once), confirm with a code, self-remove with a code (refused while policy requires it), and an administrator reset (not for yourself). All are audited.
- Web:
  - The login page asks for the code when the API says so.
  - A new `/account/mfa` page, linked as "Authenticator" under the user name.
  - The layout holds a user with the enrollment flag on `/account/mfa`.
  - Admin Users has a **Reset authenticator** button.
  - New help topic, plus updated signing-in and users pages.
- OCD-037 records the enforcement choice (session plus a UI hold, not refusal) and the list of privileged permissions.

### Requirements / risk

- URS-BB-413, FRS-BB-448, SRS-BB-410, RISK-BB-515, OCD-037.
- Does not claim AABB compliance.

### Tests

- TEST-BB-303:
  - `TotpTests` (20 cases): five RFC 6238 SHA-1 vectors, drift window, replay, malformed codes, five RFC 4648 base32 vectors, secret round trip and URI, the `MfaRule` table, and the privileged set.
  - `MfaTests` (4): enrolled sign-in without a code (no failure counted), a wrong code (counted), the right code (session plus TOTP in the audit), and a replayed code rejected; the policy flagging only the privileged; enrollment rejecting a wrong or early code and a second enrollment; self-remove blocked under policy, self-reset refused, admin reset clearing and audited, and self-remove allowed with policy off.
- The existing `AuthSessionServiceTests` pass unchanged.
- Full suite: 2,921 passed, 0 failed (previously 2,897). Application 83, Astm 19, HL7 63, Domain 1,682, Printing 21, Help 5, Integration 1,048.
- Run check (development database, DevMode):
  - `/account/mfa` showed the key. A code computed by an independent Python HMAC TOTP implementation was accepted, so the page interoperates with standard authenticator apps.
  - The database held a Data Protection payload (`CfDJ8…`, 176 characters) and not the plain key, with the enrolled time and last step set.
  - Remove authenticator with a fresh code worked, and the development account is back to not enrolled.
  - Facility Policies lists `Security.RequirePrivilegedMfa` = false. No server errors.
- Not checked in the UI: the sign-in code prompt, because DevMode skips the login page. It is covered by `MfaTests` at the service level and by the API's 401 `mfaRequired` mapping.
- Development changes: a Data Protection key file in `src/BloodBankLIS.Api/dataprotection-keys` (git-ignored), and audit rows for the enroll and remove.

### Next ranked residual

The round 2 competitor queue is empty. Stop the competitor-gap loop. Open SME items: OCD-036 (excursion response) and OCD-037 (MFA enforcement).


## Iteration 261 — OCD-036 and OCD-037 decisions applied (2026-10-31)

The product owner decided both open items.

- **OCD-036:** an excursion lasting more than 30 minutes quarantines the units automatically.
  - The limit is `Inventory.ExcursionQuarantineMinutes`, default 30 (0 turns it off). Duration runs from the first reading of the unbroken out-of-range run.
  - The check runs on each reading and in the 5-minute alert scan.
  - Quarantined units get the new reason `StorageTemperatureExcursion`, plus history, audit, a deviation note, and a critical alert.
  - Consecutive excursion readings now share one deviation and one alert.
- **OCD-037:** MFA is a facility option, and required users are refused sign-in.
  - `Security.MfaEnabled` is the master switch (default off). `Security.RequireAllUsersMfa` and `Security.RequirePrivilegedMfa` choose who must enroll.
  - A required user without an authenticator gets no session. Login returns the pending key, and the first valid code enrolls the user and signs in. Permissions are withheld until the session exists.
  - The earlier layout hold was removed.
- Tests:
  - TEST-BB-304: `StorageExcursionQuarantineRuleTests` and three `StorageTemperatureServiceTests`.
  - TEST-BB-305: the rewritten `MfaTests` (5) and the `MfaRule` / `IsRequired` theories.
  - The G5 alert test was updated for the per-excursion alert key.
- The full suite passed: 2,932 passed, 0 failed.
- Not checked in the running app before the commit (`e93a029`).

## Iteration 262 — Patient-blood-management ordering criteria (2026-11-01)

The product owner asked for ordering-appropriateness (PBM) criteria, previously excluded, with the clinical thresholds left to SMEs.

### Implemented

- `TransfusionIndicationCriterion` admin catalog. A criterion is either:
  - laboratory: test code, comparison, threshold, unit, and look-back hours; or
  - orderer-selected: no test.
- **No criteria are seeded.** The `/admin/transfusion-criteria` page carries a REQUIRES SME VERIFICATION banner. Changes are kept in configuration history.
- `TransfusionIndicationRule`:
  - Met when any active laboratory criterion is satisfied by the latest verified, current, numeric result within the look-back before the order, or when the orderer selects an active attestation criterion.
  - Otherwise outside criteria, with a detail line.
  - The justification rule and criterion validation are here too.
- New facility policies, both default off:
  - `Order.PbmEvaluate`: evaluate and record.
  - `Order.PbmRequireJustification`: a manual order outside criteria needs an indication or a reason.
- `OrderService.CreateCoreAsync`:
  - Evaluates product lines and stores PbmStatus, PbmIndicationCode, PbmJustification, and PbmDetail on each `OrderLine` (migration `20261101090000_TransfusionIndicationCriteria`, plus the development SQLite table and columns).
  - Refuses a manual order with `PBM-JUSTIFY` when a reason is required. Interface orders are never stopped.
- The chart order form runs `POST /api/patients/{id}/pbm-check` before accessioning a specimen, and shows an indication choice and a reason field per product outside criteria.
- `GET /api/pbm/review` feeds the new "Orders outside transfusion criteria" card on Utilization.
- Help topic added for the admin page, plus updates to the patient orders and utilization pages.
- OCD-038 is open: the criteria content, reason policy, and reviewers are **REQUIRES REGULATORY / SME VERIFICATION**.

### Requirements / risk

- URS-BB-416, FRS-BB-451, SRS-BB-413, RISK-BB-518, OCD-038.
- Also URS-BB-414–415, FRS-BB-449–450, SRS-BB-411–412, and RISK-BB-516–517 for iteration 261.
- Does not claim AABB compliance.

### Tests

- TEST-BB-306: `TransfusionIndicationRuleTests` (10 cases) and `TransfusionIndicationTests` (4). The test thresholds are fixtures. They cover:
  - evaluation off, and no criteria
  - the latest verified result deciding, with stale, future, unverified, and superseded results ignored
  - attestation options and selection
  - a manual order refused without a reason and saved with one; an interface order recorded; an attested order met
  - the review list
  - catalog validation, uppercase normalization, duplicates, and history
- Full suite: 2,946 passed, 0 failed (previously 2,932). Application 83, Astm 19, HL7 63, Domain 1,699, Printing 21, Help 5, Integration 1,056.
- Run check (development database, DevMode):
  - `/admin/transfusion-criteria` showed "No criteria" and the SME banner.
  - Facility Policies listed both PBM policies as false.
  - `pbm-check` for RBC-LR returned NotEvaluated (policy off), so ordering is unchanged.
  - The Utilization review card showed its empty state.
  - No server errors.
- Not exercised in the UI: the reason panel on the chart. It appears only with the policies on and criteria present, and it is covered by `TransfusionIndicationTests` at the service level.

### Next ranked residual

OCD-038: SME criteria content. Also verify OCD-036 and OCD-037 in the running app.

## Iteration 263 — Top-bar "scan anything" lookup (2026-11-01)

The capability matrix had no UX rows. Adding them showed that there was no way to search from everywhere. A tech holding a tube, a bag, or a wristband first had to know which page to open. The patient timeline is also missing, and is the next item.

### Implemented

- `UniversalLookupService` (`Application/Search`) classifies input after scanner sanitizing:
  - A parsed DIN (scanner `=` form or human-readable) or an exact unit number or component identity finds units. All components that share a DIN are returned.
  - An exact accession or tube barcode, order number, MRN, or active prior MRN finds specimens, orders, and patients.
  - A merged MRN resolves to the surviving chart and names the survivor.
  - Partial MRN, name, alias, and unit-number matches are offered only when nothing matched exactly, up to 20 of each kind, with a "type more" note when capped.
  - A malformed `=` scan returns a plain message and does not fall back to a name search.
- `OpenDirectly` is true only for exactly one exact match. A partial match, even a single one, is listed and never opened.
- `POST /api/lookup` (authenticated user). POST keeps MRNs and names out of URLs and request logs.
- `GlobalLookupBar` in the top bar of every signed-in page. `/` or Ctrl+K focuses it, Enter runs it, arrow keys and Enter pick a row, and Esc closes the list.
- Routes: patient chart; specimen → the chart's Specimens tab; order → the chart's Orders tab; unit → the unit chart. No new pages and no schema change.
- Not audited: the search itself. Opening a chart still records `PatientAccess`. REQUIRES REGULATORY / SME VERIFICATION if a facility audits searches.
- Help topic `finding-records`. The gap analysis now has a Cycle 263 matrix with UX rows.

### Requirements / risk

- URS-BB-417, FRS-BB-452, SRS-BB-414, RISK-BB-519 (wrong record opened from an ambiguous scan).

### Tests

- TEST-BB-307: `UniversalLookupTests` (17 cases). They cover:
  - scanned DIN and human-readable DIN open directly
  - a DIN shared by two components lists both and does not open
  - a malformed scan gives a message and no hits
  - an accession in any case, an order number, an MRN with scanner CR/LF, a prior MRN, and a merged MRN
  - a partial name that matches two patients, and a single partial match, are both listed and not opened
  - an exact match hides partial matches
  - the cap, blank input, and unknown input
- Full suite: 2,971 passed, 0 failed (previously 2,946). Application 83, Astm 19, HL7 63, Domain 1,702, Printing 21, Help 7, Integration 1,076.
- No API-host test harness exists. The endpoint uses the shared authenticated-user filter, like the other endpoints.
- Run check (development database, DevMode):
  - Partial `W000123` listed units and did not open any.
  - `acc0013` opened `/patients/5?tab=specimens`.
  - `W0001230000001` opened the unit.
  - `=A9999` showed the bad-scan message.
  - `ZZZ-NOPE` showed "No patient, unit, specimen, or order matches".
  - Esc closed the list.
  - A `/` keydown focused the box.
  - No server or console errors.

### Next ranked residual

A unified, read-only patient timeline on the chart, then a specimen timeline. SME items OCD-004 and OCD-038 stay open.

## Iteration 264 — Patient timeline (2026-11-01)

A patient's history was split across nine chart tabs. To see whether a correction, an emergency release, or a reaction came before the current work, a technologist had to open each tab and piece the order together.

### Implemented

- `PatientTimelineService` builds one newest-first list from stored records, using each record's own time:
  - visit start and end
  - order placed, with its current status and any cancellation reason
  - specimen collected (with the rejection reason when rejected) and received
  - result entered, verified, corrected (reason; the original is kept and marked replaced), and invalidated (who and why)
  - blood type recorded, antibody recorded, and special requirement added
  - reservation, crossmatch, issue (type, crossmatch at issue, override rule, reason, and authorizer), return, transfusion, and reaction reported and closed
- A change with no stored time of its own, such as a cancellation or rejection, is shown on the event it changed. It is not given an inferred time.
- Severity:
  - Critical: antibody, incompatible crossmatch, suspected reaction, reaction report.
  - Attention: correction, invalidation, cancellation, rejection, special requirement, emergency or overridden issue.
- `GET /api/patients/{id}/timeline` (Patient Workspace group, authenticated user). Read-only, with no schema change.
- New chart tab **Timeline** (`?tab=timeline`): grouped by day, with kind filters and counts, a "Warnings and critical only" filter, and each event linked to its tab or unit.
- Help topic `patient-timeline`.

### Defects fixed while checking

- **No chart tab was ever highlighted.** `PatientChartTabBar Current="_tab"` and `UnitChartTabBar Current="_tab"` passed the literal text "_tab", because Razor treats a string parameter without `@` as text. The same mistake affected the Tests tab: `StatusFilter="_testStatusFilter"` could show "Pending" in the dropdown while the list held completed tests. All three now use `@`.
  - A new source-scan test, `String_parameters_are_not_given_a_field_name_as_literal_text`, fails on that pattern. It was shown to fail with the old binding.
- Iteration 263 cited "`UniversalLookupTests` (17 cases)" in `TEST_CATALOG.md`, which breaks `TestCatalogPackagingTests.CitedClasses_HaveSourceFiles`. The count is removed. That row was added after 263's suite run, so 263's commit did not pass this test.

### Requirements / risk

- URS-BB-418, FRS-BB-453, SRS-BB-415, RISK-BB-520 (prior antibody, correction, reaction, or emergency release missed because it is on another tab).

### Tests

- TEST-BB-308: `PatientTimelineTests` (7 cases). They cover:
  - an empty timeline
  - newest-first order using each record's own time
  - Critical and Attention marking, including the override rule, reason, and authorizer
  - a correction keeping the original and showing the reason, and an invalidation showing who and why
  - a cancelled order and a rejected specimen keeping the original event time, with no invented events
  - another patient's records excluded
  - product events naming and linking the unit
- Plus the Razor string-parameter regression test.
- Full suite: 2,979 passed, 0 failed (previously 2,971). Application 83, Astm 19, HL7 63, Domain 1,702, Printing 21, Help 8, Integration 1,083.
- Run check (development database, DevMode):
  - Patient 5 Timeline showed 6 events, with the emergency issue marked Attention and its override rule, reason, and authorizer shown.
  - "Warnings and critical only" left only that issue.
  - The Timeline tab and the unit Overview tab now highlight.
  - No server errors.

### Next ranked residual

A specimen timeline: collection, receipt, acceptance or rejection, relabel, results on that specimen, and the crossmatches and allocations that used it. It should be reachable from the Specimens tab and the find box.

## Iteration 265 — Specimen history (2026-11-01)

The facts about one tube were spread across the specimen row, the audit trail, the overrides list, results, and crossmatches. No single place answered "was this tube relabeled, rejected, or extended before we crossmatched on it, and by whom?"

### Implemented

- `SpecimenTimelineService` builds the newest-first history of one specimen:
  - Collection: collector, draw location, and label identifiers.
  - Each audit row the specimen workflow wrote (accession, receipt, acceptance, rejection, relabel, edits), with user, workstation, and up to four field changes as "field: old → new".
  - The recorded receipt time, only when no audit row recorded receipt.
  - Expiry: scheduled, then marked Attention once reached.
  - Specimen overrides: rule, reason, and authorizer.
  - The orders the specimen serves, the results on it, and the crossmatches and reservations that used it.
- Rejection and relabel rows are Attention, and an incompatible crossmatch is Critical.
- The generic Create/Update/Delete rows that the database layer writes on every save are left out. They repeat the whole record and would bury the meaningful changes.
- `IAuditQuery.ListForEntityAsync` reads one record's rows through the existing `IX_AuditEvents_EntityType_EntityId` index. Before this, the only read loaded the whole audit table.
- `GET /api/specimens/{id}/timeline` returns 404 for an unknown specimen.
- Specimens tab: a **History** button per row opens a `SpecimenTimelinePanel`. `?specimen={id}` opens it directly.
- The find box now links a scanned accession to `?tab=specimens&specimen={id}`, so one scan opens that tube's history.
- The day-grouped event list moved into a shared `TimelineEventList`, and the result-event wording into `PatientTimelineService.ResultEvents`. The patient and specimen timelines now render and word results identically.
- Help: a "Specimen history" section in `patient-specimens`, and the find-box topic updated.

### Requirements / risk

- URS-BB-419, FRS-BB-454, SRS-BB-416, RISK-BB-521 (a relabel, rejection, or expiry override not noticed before the tube is used).

### Tests

- TEST-BB-309: `SpecimenTimelineTests` (14 cases, 7 of them `Changes` theory rows). They drive the real `SpecimenService` receive, relabel, and reject so the audit rows are genuine. They cover:
  - user and workstation on each row; `Barcode: BC-OLD-1 → BC-NEW-2`
  - no generic row dumps
  - rejection marked Attention
  - the recorded receipt time used when no audit row exists
  - expiry scheduled, then expired
  - orders, results, crossmatch, and reservation on this tube only
  - an override showing rule, reason, and authorizer
  - an unknown specimen returning null
  - change formatting: blank values, unchanged values, unreadable JSON, and the cap
- `UniversalLookupTests` was updated for the new accession link.
- Full suite: 2,993 passed, 0 failed (previously 2,979). Application 83, Astm 19, HL7 63, Domain 1,702, Printing 21, Help 8, Integration 1,097.
- Run check (development database, DevMode):
  - Scanning `ACC0013` in the find box opened `/patients/5?tab=specimens&specimen=5` with the history expanded: collection, receipt (from the recorded time, because seed data has no audit rows), reservation of W0001230001004, and scheduled expiry.
  - No server or app console errors.
- Not exercised in the UI: relabel and reject. Doing so would change the development data. Both are covered by the service-level tests above.

### Next ranked residual

The UX matrix now has two Partial rows: role-based home and keyboard. Role-based home ranks first. The dashboard filters cards by permission but does not lead with the signed-in role's own work, such as the issue desk's ready-to-issue and in-transit queues, the bench's pending specimens and tests, or the supervisor's review queue.

## Iteration 266 — Role-focused home (2026-11-01)

The dashboard showed up to 12 queue cards in a fixed order. An empty queue looked as important as one with an overdue emergency crossmatch, and a temperature excursion sat in the same grid as routine pending tests. The capability matrix rated role-based home as Partial.

### Implemented

- `DashboardHome` (Application, no I/O) arranges the permitted cards.
  - **Focus**: Bench, Issue desk, Supervisor, or Everything. It is inferred from permissions: a user who holds exactly one desk's permissions starts there; anyone else, including an administrator, starts on Everything.
  - **Start here**: the most urgent card, otherwise the first focus queue with work.
  - **Needs attention**: any card with an overdue or follow-up note, plus temperature excursions, reactions, or an open handoff above zero.
    - Shown in **every** focus, in urgency order: excursions, reactions, retrospective crossmatch, in transit, handoff, disposition, supervisor.
  - **Queues with work**: focus queues above zero, in workflow order.
  - **Could not load**: a failed count. It is never treated as clear, and the page says so.
  - **Clear**: focus queues at zero, shown as compact links.
  - **Other desks**: folded, with an item count.
  - Every permitted card appears exactly once, so focus changes the order and grouping only.
- `Home.razor`:
  - Focus buttons with `aria-pressed`, and the Start here link.
  - The grouped sections, with Needs-attention cards marked by a red edge.
  - Focus is remembered per user in browser `localStorage` (`bb.home.focus.{user}`). Storage failures are ignored and fall back to the inferred focus.
- No API, permission, or schema change. Counts still come from the existing worklist endpoints.
- Help: a "Focus and what comes first" section in `dashboard`, with the desk table.

### Requirements / risk

- URS-BB-420, FRS-BB-455, SRS-BB-417, RISK-BB-522 (urgent work waits behind routine queues, or a failed count reads as clear).

### Tests

- TEST-BB-310: `DashboardHomeTests` (8 cases). They cover:
  - focus inference for a single desk, several desks, all permissions, and none
  - urgent items leading in urgency order even outside the focus
  - Start here falling back to the first focus queue
  - a failed load is unavailable and never clear, in every focus
  - all clear gives no Start here
  - every card appears exactly once in every focus
  - Everything has no other desks
  - a routine non-zero queue without a note is not attention, except the urgent queues
- Full suite: 3,001 passed, 0 failed (previously 2,993). Application 91, Astm 19, HL7 63, Domain 1,702, Printing 21, Help 8, Integration 1,097.
- Run check (development database, DevMode):
  - The admin started on Everything.
  - Start here was "Reaction investigations: 1".
  - Needs attention listed reactions, retrospective crossmatch ("1 past due"), and in transit ("1 past the arrival window").
  - Bench showed bench queues, with "Other desks (5 items in 5 queues)" folded.
  - After a reload, Bench was still selected. I reset the focus to Everything afterwards.
  - No server errors.
- Known: on a reload with a saved focus, the page first renders the inferred focus for a moment, then switches. This happens because browser storage is read only after the first render.

### Next ranked residual

The remaining UX Partial row is keyboard. There is no shortcut map, and the issue desk's scan-verify-issue path needs the mouse between steps.

## Iteration 267 — Patient identifiers read, not copied, at issue and bedside (2026-11-01)

The ranked item was keyboard flow on the issue desk. Inspecting it turned up a higher-priority identification defect (P1), which was fixed first.

### Finding

The issue gate hard-stops unless two independent patient identifiers match the patient record (`PatientIdentityMatchRule`). Four forms copied the record's MRN and date of birth straight into those fields, so the check always compared the record with itself:

- choosing a Ready-to-issue row
- choosing a patient on the issue form
- bedside transfusion documentation
- the chart's issue form

This had been in place since September. The product owner decided it in conversation as OCD-039: pre-fill is a facility policy, off by default.

### Implemented

- Facility policy `Issue.PrefillPatientIdentifiers` (Boolean, default false, category Issue). It is editable on Facility Policies with configuration history, and readable at the bench through `GET /api/reference/policies/{key}`.
  - That endpoint used to fall back to **true** for any unset policy. It now uses the catalog default (`FacilityPolicyCatalog.BoolDefault`), so a default-off policy is never reported as on. The existing second-ABO policy keeps its default of true.
- `PatientIdentifierEntryRule.Prefill` decides the starting values at all four sites. A failed policy read is treated as off.
- `IdentifierMatchHint` shows "✓ Matches the patient record" or "✗ Does not match this patient" under each field as it is typed or scanned.
  - It uses the issue gate's own matcher, so it cannot disagree with the hard stop.
  - It never shows the expected value.
  - The server check is unchanged.
- Keyboard: Enter in the MRN field moves focus to the date-of-birth field, because wristband scanners end with Enter. The fields bind on input, so the hint updates while typing.
- Ready-to-issue intro text, and the issuing and chart help topics, now say to read the identifiers from the wristband or requisition.

### Requirements / risk

- URS-BB-421, FRS-BB-456, SRS-BB-418, RISK-BB-523 (wrong-patient issue or transfusion because the two-identifier check passed without reading the wristband). OCD-039 recorded as decided.

### Tests

- TEST-BB-311: `PatientIdentifierEntryRuleTests` (11 cases). They cover:
  - pre-fill off leaves both fields empty; on fills them from the record
  - the policy ships off as an Issue Boolean
  - `BoolDefault` keeps the other policies' defaults, with unknown keys defaulting to true
  - the hint agrees with `PatientIdentityMatchRule` for empty, matching, wrong, and unreadable values
- `RazorFormBindingTests.Patient_identifier_fields_are_not_filled_from_the_record_directly` fails if any page assigns the MRN or DOB to an identifier field outside the rule. Against the previous commit's pages it finds 8 such lines (6 on the issue page, 2 on the chart).
- Full suite: 3,013 passed, 0 failed (previously 3,001). Application 91, Astm 19, HL7 63, Domain 1,713, Printing 21, Help 9, Integration 1,097.
- Run check (development database, DevMode, policy unset):
  - Issue on a Ready row opened the form with both identifier fields empty.
  - `MRN9999` showed "Does not match this patient".
  - `MRN0001` showed "Matches the patient record", and Enter moved focus to the date-of-birth field.
  - Nothing was issued, and there were no server errors.
- Not exercised in the UI: the bedside form and the chart issue form. Both use the same rule and component, and the source-scan test covers them.
- Observed, not changed: after choosing a Ready row, the Unit number picker on the issue form showed "1" instead of the unit number. Not investigated in this iteration.

### Next ranked residual

1. The Unit number picker display on the issue form, observed above.
2. Keyboard: a shortcut map, and Enter-driven flow through unit scan verification to Issue.

## Iteration 268 — Choices show real identifiers and never swap silently (2026-11-01)

Iteration 267 noticed that the issue form showed "1" in the Unit number field after Issue on a Ready-to-issue row.

### Cause

The shared searchable choice, `FilterableComboBox`, used on 15 pages, resolved its text only when its value **changed**. The issue form sets the unit before the unit picker's list has loaded, so the text fell back to the internal record id and was never updated when the list arrived.

Worse, when focus left the field, the leftover text was committed as a typed search. Unit search includes the record id, so "1" could select a different unit whose number contained that digit, with a single match.

### Implemented

- `ComboBoxDisplay` (Web, no I/O) resolves a value's text and says whether it is resolved.
  - `FilterableComboBox` resolves again on every render until the item appears.
  - A new `UnresolvedText` parameter supplies the text for a value whose item is not in the list.
  - Unresolved text is never committed as a search when focus leaves.
  - Typing, picking, or clearing marks the text as the user's own again.
- `UnitPickerComboBox` shows "Loading unit..." until its list arrives. Once loaded, a unit that is not in the list shows "Unit record N is not in this list. Clear it and scan the unit." A bare id is never shown.
- `BloodBankLIS.Help.Tests` now references the Web project, so Web helpers can be unit-tested.
- No API or schema change.

### Requirements / risk

- URS-BB-422, FRS-BB-457, SRS-BB-419, RISK-BB-524.

### Tests

- TEST-BB-312: `ComboBoxDisplayTests` (10 cases). They cover:
  - an item in the list
  - a value before the list loads, with and without unresolved text
  - a null value
  - a custom string value
  - `ShouldResolve` truth table
  - a load sequence that resolves once the item arrives
- Full suite: 3,023 passed, 0 failed (previously 3,013). Application 91, Astm 19, HL7 63, Domain 1,713, Printing 21, Help 19, Integration 1,097.
- Run check (development database, DevMode):
  - Issue on a Ready row now shows `W0001230000001 · O+ · Available · Unit expires 2026-12-01 16:46` in the Unit number field, where it showed "1" before.
  - Focusing the field and tabbing out kept the same unit.
  - Nothing was issued, and there were no server errors.
- Not covered: a rendered-component test of `FilterableComboBox` itself, because there is no bUnit harness. The decision logic is in the tested helper.

### Next ranked residual

Keyboard: a shortcut map, and Enter-driven flow from the second identifier through unit scan verification to Issue.

## Iteration 269 — Keyboard and scanner flow at issue, ward receipt, and bedside (2026-11-01)

The keyboard row was the last Partial UX row. On the issue desk, a scan into patient ID 1 or a unit-label field ended with the scanner's Enter and went nowhere, so staff had to reach for the mouse between every scan.

### Implemented

- An attribute-driven Enter flow in `wwwroot/ui.js`. Inside a `data-enter-flow` container, Enter on a `data-enter-step` input moves to the next visible, enabled step and selects its text.
  - After the last step, focus goes to the **form container**, which shows a short green outline, and **never to a button**. The next scan's Enter therefore cannot issue, receive, or document.
  - The action stays a deliberate click, or Tab then Enter.
  - Moving focus blurs the field, which fires its change event, so change-bound unit lookups still run.
- Flows are applied to:
  - the issue desk: patient ID 1 and 2, then the five unit-label fields
  - ward receipt: unit number and received-by, then the label fields
  - bedside documentation: unit number and patient IDs, then the label fields
  - the chart's issue dialog: patient IDs, then the label fields
- The per-field C# Enter handlers added in Cycle 267 are removed in favor of the shared behavior.
- `?` outside a text field opens a keyboard shortcut list; Esc or `?` closes it.
- Help: "Scanning without the mouse" in `issuing`, and the `?` key in `finding-records`.

### Requirements / risk

- URS-BB-423, FRS-BB-458, SRS-BB-420, RISK-BB-525 (a scanner's trailing Enter presses Issue, Receive, or Document).

### Tests

- TEST-BB-313: `EnterFlowTests` (5 cases, source scans). They check that:
  - only text inputs are steps
  - the issue page has three flows and the chart dialog one, both with identifier steps, and the old C# handlers are gone
  - every unit-label scan field is a step
  - the flow script focuses the container and never clicks, submits, or targets a button
- The behavior itself is JavaScript with no JS test harness, so it was checked in the browser (below).
- Full suite: 3,028 passed, 0 failed (previously 3,023). Help 24, Integration 1,097, Domain 1,713, Application 91, HL7 63, Printing 21, Astm 19.
- Run check (development database, DevMode):
  - Issue on a Ready row, then MRN0001, Enter, a date of birth, Enter, and Enter through each label field.
  - Focus visited ID 2, DIN, product code, ABO/RhD, expiration, and extended division, then the form, which showed the outline.
  - A further Enter on the form did nothing. No issue was written: the API log shows no `INSERT INTO "Issues"`, and there were no errors.
  - A `?` key event opened the shortcut list (6 entries, focus on Close), and Esc closed it.
  - The browser tool cannot send a shifted `?`, so the panel was opened with a dispatched key event. A physical keyboard sends `?`.

### Next ranked residual

The UX matrix rows are now Present. Return to the clinical matrix. Its remaining Partial rows are SME- or license-blocked:
- the ICCBBA tables (OCD-004)
- PBM criteria content (OCD-038)
- eluate findings into antibody history
- FHIR

The next unblocked candidate is an end-to-end workflow check of the bedside form under `Issue.PrefillPatientIdentifiers`, both on and off, followed by an inventory of other screens that copy identifiers from the record.

## Iteration 270 — Attestations made by a person: return inspection and override authorizer (2026-11-01)

After OCD-039, I audited the web app for other verification fields filled with the value they are meant to check. Two defects turned up, and the product owner decided both in conversation.

### Findings

1. **Return inspection (P0).**
   - Temperature, seal, and visual inspection started checked on both return forms, and `ReturnUnitRequest` defaulted them to true.
   - A return given only a reason therefore passed `ReturnReissueRule` and sent the unit back to **Available** with no inspection recorded.
   - Time out of storage was already computed by the system and was safe.
   - Appearance at issue, ward receipt, and delivery receipt also starts "Acceptable". That part is not yet changed.
2. **Override "Authorized by".**
   - Seven forms pre-filled it with the signed-in user.
   - Four submits plus the result-entry panel replaced a blank value with the signed-in user, so an emergency release recorded the technologist as its authorizer unless someone typed over it.
   - The server checks the signer's own override permission and e-signature separately, so this was mis-attribution, not a permission bypass.

### Decisions

- OCD-040: inspections become a facility policy, default off.
- OCD-041: "Authorized by" starts empty, with a "Me" button.

### Implemented

- `Inspection.DefaultAcceptable` facility policy (Boolean, default false), readable at the bench.
- `ReturnInspectionRule`: an unanswered inspection is a hard stop (`RET-INSPECT`, naming what is missing) when the unit could go back to Available, whether chosen explicitly or computed.
  - An explicit Quarantine or Disposed return may leave inspections unanswered, and is recorded as unanswered.
  - With the policy on, unanswered counts as acceptable.
  - The return record adds `InspectionUnanswered` and `InspectionFromFacilityDefault`.
- `ReturnUnitRequest` inspection fields are now `bool?`.
- `InspectionAnswer` (Acceptable or Not acceptable, starting Not answered) replaces the checkboxes on the issue desk return form and the unit chart's issued-unit status change.
  - Return stays disabled while an Available return is unanswered.
  - Fields start acceptable only when the policy is on.
- `AuthorizerField`: an empty input with a "Me" button, used on all 11 "Authorized by" inputs. The defaults and the silent fallbacks to `Session.UserName` are removed.
- `OverrideFieldsRule` adds warning `OVR-FIELDS` naming the missing reason or authorizer once an override has been started with one of them. It is a warning, never a hard stop, so screens keep showing the override form. With both blank nothing is added.
  - Wired into issue, result verify, compatibility, and two allocation paths.
  - The order-edit path returns a bool and has no message slot. It still refuses, without the message.
- Help: "Return inspection" and "Who authorized an override" in `issuing`.

### Requirements / risk

- URS-BB-424, FRS-BB-459, SRS-BB-421, RISK-BB-526. OCD-040 (return part done, appearance open) and OCD-041 recorded.

### Tests

- TEST-BB-314:
  - `ReturnInspectionRuleTests` (11 cases): rule outcomes, the policy default, and `OverrideFieldsRule` messages and append behavior.
  - Four new integration tests:
    - an unanswered return is refused and the unit stays Issued
    - the facility default allows it and is recorded
    - explicit Quarantine without answers is allowed and recorded as unanswered
    - explicit Available without answers is refused
  - `AttestationDefaultTests` (3 source scans). Against the previous commit they flag 13 authorizer defaults or fallbacks and 12 inspection defaults.
- Ten existing return tests now state their inspections explicitly. They test worklists, time windows, and authorization, not inspection.
- Found while testing: `Phase4IssuingTests` shares one database across its tests. The facility-default test now turns the policy back off in `finally`; without that, an explicit-Available test passed or failed depending on order.
- Full suite: 3,046 passed, 0 failed (previously 3,028). Domain 1,724, Integration 1,101, Help 27, Application 91, HL7 63, Printing 21, Astm 19.
- Run check (development database, DevMode):
  - Return on an issued unit showed all three inspections as "Not answered", with Return unit disabled.
  - The seeded unit was past its return window, so only Quarantine and Disposed were offered and no inspection was required.
  - Answering Seal marked it alone.
  - On the issue form, ticking Override showed an empty "Authorized by", and Me filled `DEV_ADMIN`.
  - Nothing was returned or issued, and there were no server errors.
  - Not exercised in the UI: an Available return, which needs a freshly issued unit. It is covered by the integration tests.

### Next ranked residual

OCD-040, second part: appearance at issue, ward receipt, delivery receipt, and unit receive. This covers five request types plus server enforcement, with the same policy.

## Iteration 271 — Appearance recorded by a person at issue, ward receipt, and receipt (2026-11-01)

This completes OCD-040. Iteration 270 fixed return inspection; this one fixes unit appearance. Every appearance field and request defaulted to **Acceptable**, so a clotted, hemolyzed, or leaking unit could be received, issued, or acknowledged on the ward without anyone recording what the bag looked like.

### Implemented

- `Appearance` is now `UnitAppearance?` (default null) on six requests: issue, ward receipt, receive, receive-expected, ISBT scan session, and ISBT manual entry.
- `AppearanceAttestationRule` resolves it:
  - a recorded value is kept
  - an unrecorded value is a hard stop, `APPEAR-REQUIRED` ("Look at the bag and record its appearance before issuing the unit / acknowledging receipt / receiving the unit")
  - with `Inspection.DefaultAcceptable` on, unrecorded resolves to Acceptable and is reported as defaulted
  - where receipt inspection is already optional (`RequireReceiveVisualInspection` off), unrecorded receipt keeps the earlier behavior
- Issue and ward receipt check this before saving. Their audit rows now include the appearance and `AppearanceFromFacilityDefault`.
- All three receive paths share `EvaluateReceiveAppearanceAsync`, which now returns the resolved appearance. They store that value, not the request's.
- `AppearanceSelect` replaces the seven appearance dropdowns: issue, ward receipt, chart issue, inventory arrival, unit chart arrival, ISBT receive, and delivery receive.
  - It starts at "Choose appearance..." with "Not recorded. Look at the bag."
  - It reads the facility policy itself and fills Acceptable only when the policy is on.
  - A failed policy read leaves it unrecorded.
- Issue, ward receipt, and ISBT receive clear appearance after each successful save, so the next unit does not inherit the previous answer. The other forms already reset.
- An unrecorded appearance gives no suggested supplier-return reason (as Acceptable does), and a typed reason is still used.
- Help: an "Appearance" section in `issuing`.

### Requirements / risk

- URS-BB-425, FRS-BB-460, SRS-BB-422, RISK-BB-527. OCD-040 is now fully implemented.

### Tests

- TEST-BB-315:
  - `AppearanceAttestationRuleTests` (5 cases)
  - Four integration tests:
    - issue without appearance is refused and nothing is issued
    - the facility default issues as Acceptable and audits `AppearanceFromFacilityDefault: true`
    - ward receipt without appearance is refused
    - receive without appearance is refused and no unit is created
  - Two source scans in `AttestationDefaultTests`. Against the previous commit's pages they flag 11 appearance defaults and 7 plain appearance dropdowns.
- Existing tests that issue or receive units now state `Appearance: UnitAppearance.Acceptable`: 37 constructions plus 5 request helpers. They test other behavior.
- Full suite: 3,057 passed, 0 failed (previously 3,046). Domain 1,729, Integration 1,105, Help 29, Application 91, HL7 63, Printing 21, Astm 19.
- Run check (development database, DevMode):
  - Issue on a Ready row showed "Choose appearance..." with "Not recorded. Look at the bag."
  - Choosing Acceptable removed the prompt and the note.
  - Nothing was issued, and there were no server errors.

### Working-tree note

While this iteration was in progress, someone stashed the working tree. The stash mixed this work with an unrelated "Stock par → Minimum stock" rename by another writer. After the stash was restored, the product owner said to finish this iteration.

This commit contains only the appearance work:
- `Inventory.razor` was staged without the rename's one-comment change.
- For the requirement, risk, traceability, and test-catalog documents, the staged version is the previous commit plus only this iteration's rows.

The rename stays uncommitted in the working tree, with its staged files restored to their earlier index state. The suite above ran on the combined tree, so it covers the rename too. Afterwards the commit (`d2b2947`) was checked out alone in a temporary worktree, without the rename, and built and tested: 3,057 passed, 0 failed.

### Next ranked residual

Return to the clinical matrix. Its remaining Partial rows are SME- or license-blocked (OCD-004, OCD-038, eluate history, FHIR). The next unblocked candidate is the order-edit override path, which refuses a blank authorizer without saying so (iteration 270).

## Iteration 272 — The last two override paths say which field is missing (2026-11-01)

Iteration 270 added the `OVR-FIELDS` message (OCD-041) to the issue, result, compatibility, and allocation override paths. Two more paths refused an incomplete override without naming the missing field.

The iteration 270 log described the order-edit path as returning a bool. In fact, the bool is only the "record the override" check. The refusal path returns a blocked result.

1. **Order edit (complex crossmatch downgrade).**
   - Before: with only a reason or only an authorizer, the refusal re-showed the antibody-history crossmatch warning and nothing else.
   - Now: it adds the `OVR-FIELDS` warning. The override form stays available.
2. **Specimen expiry override.**
   - Before: an incomplete override returned only the expiry warning text.
   - Now: it appends the missing-field sentence, for example "…To override, name who authorized it (the physician or supervisor, or yourself)."

### Tests

- TEST-BB-316 adds two integration tests. In each, a reason without an authorizer is refused with "name who authorized it", and no override row is written.
- Full suite: 3,059 passed, 0 failed (previously 3,057). Domain 1,729, Integration 1,107, Help 29, Application 91, HL7 63, Printing 21, Astm 19.
- The Help tests were run with build output in an ignored `obj` folder. Two `BloodBankLIS.Api` and `BloodBankLIS.Web` processes, not started by this session, held the normal output folders. They were left running.
- No UI change, so there was no run check.

### Working tree

The other writer's uncommitted "Stock par → Minimum stock" rename is still in the working tree, and its staged files are unchanged. This commit contains only this iteration's files. The test catalog was committed as the previous commit plus this row.

### Next ranked residual

The remaining clinical Partial rows are SME- or license-blocked:
- OCD-004: ICCBBA tables
- OCD-038: PBM criteria
- eluate findings into antibody history
- FHIR

The UX rows are all Present. The next loop should rescan for unblocked work. I checked the patient chart's second-verifier field as a candidate: it already uses the directory list, so it is not a gap.

### Process errors this iteration

- While writing the test-catalog row, a script opened the working `TEST_CATALOG.md` for writing before computing its content. When the computation failed, the file was left empty, wiping the other writer's uncommitted one-line rename change in it.
  - It was rebuilt exactly as the previous commit plus that line, which came from the rename's dropped stash commit `4f19203`.
  - The other five shared documents were checked and are intact.
  - The scripts now compute all content before opening a file for writing.
- In iteration 271, a failed cleanup step let a plain `git commit` include the other writer's six staged files under a log message. That local commit was undone with `git reset --soft` and the index verified unchanged.
  - Commits now go through an explicitly checked staged list.

## Iteration 273 — Eluate alloantibodies post to antibody history on verification (2026-11-01)

The product owner asked to work the pending Partial rows and decided four open items in conversation:
- eluate findings post verified alloantibodies (OCD-042)
- an ISBT licensed-code readiness report (OCD-004)
- an inactive draft set of PBM criteria (OCD-038)
- a read-only FHIR R4 API (OCD-043)

This iteration does the eluate item. The other three are next.

### Found

The patient chart's **DAT / Elution** tab had a button and a help page but **no screen**. Nothing rendered for it, and the workup was reachable only through the API. Eluate results were free text, so an alloantibody seen only in an eluate never reached antibody history and never protected later unit selection.

### Implemented

- `DatElutionEluateFinding` (kind, catalog antibody, specificity, IsActive, posted history id), plus `VerifiedBy` / `VerifiedUtc` on the workup. This comes with migration `20261102090000_EluateFindings`, the development SQLite bootstrap (nullable columns are added in place, so there is no recreate), and the SQLite-to-SQL-Server copy order. The migrator coverage test caught the missing copy-order entry.
- `EluateVerificationRule`:
  - verification needs a completed workup and a person other than the one who completed it, and happens only once
  - an alloantibody must match the antibody catalog when recorded
  - only alloantibodies post
  - after verification, findings are locked and the workup cannot be voided
- `DatElutionWorkupService`:
  - `RecordEluateFindingsAsync` replaces findings by retiring the old rows, never deleting them, with a before/after audit row
  - `VerifyAsync` (permission `immuno.override`) posts each alloantibody not already active for the patient as Identified, with a comment naming the workup and verifier, an audit row with source Eluate, and the finding linked to the new history row
- Endpoints: `POST /api/dat-elution/{id}/eluate-findings` and `/verify`.
- `PatientChartDatElutionTab`, the first screen for this tab:
  - create, edit, complete, and void with a reason
  - an eluate-findings editor with a catalog antibody picker
  - Verify for a second person, with a plain statement of what will be added
  - per-finding Posted / Already on file / Not posted markers
- The help page now describes the workflow.

### Requirements / risk

- URS-BB-426, FRS-BB-461, SRS-BB-423, RISK-BB-528. OCD-042 decided; OCD-043 (FHIR) recorded.

### Tests

- TEST-BB-317:
  - `EluateVerificationRuleTests` (7 cases)
  - `EluateFindingTests` (7 integration tests):
    - posts alloantibody only, with source and audit
    - the completer cannot verify
    - an open workup cannot be verified
    - an unmatched alloantibody is refused and earlier findings stay
    - replaced findings are retired, not deleted
    - an antibody already on file is not posted twice
    - a verified workup cannot be voided or have its findings changed
- Full suite: 3,074 passed, 0 failed (previously 3,059). Domain 1,736, Integration 1,115, Help 29, Application 91, HL7 63, Printing 21, Astm 19. Built with output in an ignored `obj` folder: the normal output folders are held by `BloodBankLIS.Api` and `BloodBankLIS.Web` processes that this session did not start.
- **Not checked in the running app.** The dev servers on the configured ports belong to another writer and run an older build without this tab. They were left alone. The tab is covered by the service tests, not by a browser check.

### Next ranked residual

OCD-004 readiness report, then OCD-038 inactive draft criteria, then OCD-043 read-only FHIR R4 API.

## Iteration 274 — ISBT licensed-code readiness report (2026-11-01)

OCD-004 decision (product owner, 2026-11-01): with no licensed extract available yet, build a readiness report so the gap is visible and measurable. The software still invents no codes.

### Implemented

- `IsbtReadinessRule` classifies a code as Licensed, Placeholder, Unknown, or None, using the `IsPlaceholder` flag each catalog row already carries. It also sets the readiness test (a licensed version is loaded, and nothing in use is on placeholder or unknown codes) and writes the summary sentence.
- `IsbtReadinessService` (read-only):
  - catalog counts for product, ABO/RhD, and collection-type codes, and the licensed versions loaded
  - active products whose ISBT product code is missing, placeholder, or unknown
  - on-hand units whose product code or ABO/RhD code is placeholder or unknown
  - the first 200 problems are listed; the totals are exact
  - units without ISBT codes are counted separately and do not block readiness
- `GET /api/admin/isbt-product-codes/readiness` (`admin.config.view`). A "Licensed-code readiness" card sits at the top of the ISBT product-codes admin page, and each problem row links to its product or unit.
- Help: a "Licensed-code readiness" section in the admin `isbt-product-codes` topic.

### Requirements / risk

- URS-BB-427, FRS-BB-462, SRS-BB-424, RISK-BB-529. OCD-004 stays open until a licensed extract is loaded.

### Tests

- TEST-BB-318:
  - `IsbtReadinessRuleTests` (12 cases)
  - `IsbtReadinessTests` (4 cases):
    - a placeholder-only catalog is not ready and names what is in use
    - a licensed catalog with everything mapped is ready
    - unknown codes and unmapped products block readiness
    - units without ISBT codes, and units not on hand, are not counted
- Full suite: 3,090 passed, 0 failed (previously 3,074). Domain 1,748, Integration 1,119, Help 29, Application 91, HL7 63, Printing 21, Astm 19.
- Run check (development database, DevMode). The dev ports were free this time:
  - The card showed "Not ready: no licensed ICCBBA extract has been imported; 24 codes in use rely on placeholder rows; 1 code is not in the catalog at all."
  - Catalog counts: 51 product codes, 2 ABO/RhD codes, and 1 collection type, all placeholder.
  - 18 active products and 40 on-hand units were checked; 34 units have no ISBT codes.
  - No server errors.
- The same run confirmed the Cycle 273 DAT / Elution tab now renders, showing its empty state and New workup. Creating a workup was not exercised, to avoid writing to the development data.

### Correction

The Cycle 273 summary to the product owner gave its commit as `7a4f1b5`. The actual commit is `ffe59e5`.

### Next ranked residual

OCD-038: an inactive draft set of PBM criteria. Then OCD-043: a read-only FHIR R4 API.

## Iteration 275 — Inactive, unverified draft PBM criteria (2026-11-01)

OCD-038 decision (product owner, 2026-11-01): seed a small set of commonly published restrictive thresholds, **inactive** and labeled unverified, for the transfusion committee to review, edit, and activate.

### Design

The drafts are not seeded at startup. An explicit **Load draft criteria** action adds them, so the facility decides when they appear, and the action is recorded in configuration history.

A draft cannot go live just by being switched on. Validation refuses any active criterion whose code starts with `DRAFT-` or whose name starts with DRAFT (`PBM-DRAFT`). Activation, which did not run validation before, now does.

### Implemented

- `PbmDraftCriteria`, nine drafts. Lab test codes HGB, PLT, INR, and FIB are placeholders to map to the facility's own test codes.
  - Red cells: Hb < 7 g/dL; Hb < 8 g/dL in cardiac or orthopedic surgery or cardiovascular disease; active bleeding (orderer attests).
  - Platelets: < 10 ×10^9/L for prophylaxis; < 50 ×10^9/L before a major invasive procedure; active bleeding (attests).
  - Plasma: INR > 1.5 with bleeding or before an invasive procedure; massive transfusion protocol (attests).
  - Cryoprecipitate: fibrinogen < 150 mg/dL with bleeding.
  - **REQUIRES REGULATORY / SME VERIFICATION.** These are not a recommendation of this software.
- `TransfusionIndicationAdminService.LoadDraftsAsync` adds the drafts inactive and skips existing codes without overwriting them. Each row gets a configuration-history entry with reason "Unverified draft loaded inactive for transfusion committee review (OCD-038)."
- `POST /api/admin/transfusion-criteria/load-drafts` (`admin.config.edit`) and a **Load draft criteria** button on the admin page. The result message says to review, map the test code, and remove DRAFT before activating.
- The help page describes the draft set and the activation rule.

### Requirements / risk

- URS-BB-428, FRS-BB-463, SRS-BB-425, RISK-BB-530. OCD-038 content still requires SME verification.

### Tests

- TEST-BB-319:
  - `PbmDraftCriteriaTests` (7 cases):
    - every draft is valid while inactive and marked as a draft
    - every draft is refused when active
    - draft detection checks both code and name
    - a renamed draft can be active
  - Three integration tests in `TransfusionIndicationTests`:
    - loading adds all drafts inactive, with history, and an order still sees "no criteria"
    - a second load skips everything and leaves an edited threshold alone
    - a draft cannot be activated until it is renamed, then it can
- Full suite: 3,100 passed, 0 failed (previously 3,090). Domain 1,755, Integration 1,122, Help 29, Application 91, HL7 63, Printing 21, Astm 19.
- Run check (development database, DevMode):
  - `/admin/transfusion-criteria` shows **Load draft criteria** next to **New criterion**, over the empty list.
  - The button was not pressed. It writes configuration into the development database, so loading is left to the product owner.
  - Afterwards, at the product owner's request, the drafts were loaded through the button. The page confirmed "Added 9 unverified draft criteria, inactive", and all nine rows list as Inactive with DRAFT codes and names.
  - Activating a draft was not tried in the app; it was outside the request. The refusal is covered by `Draft_CannotBeActivated_UntilRenamed`.
  - No server errors.

### Next ranked residual

OCD-043: a read-only FHIR R4 API.

## Iteration 276 — Read-only FHIR R4 API (2026-11-01)

OCD-043 decision (product owner, 2026-11-01): a read-only FHIR R4 API.

### Design

- No new package. R4 JSON is built with System.Text.Json in `FhirMapper`.
- No invented terminology. Coded fields use local systems (`urn:greyhound-lis:...`) with readable text. LOINC or SNOMED CT mapping requires interface / SME verification.
- GET only, signed-in users only. Errors are OperationOutcome resources.
- Safety: only the current ABO/Rh and active antibodies are served, by search and by direct read, so a superseded type can never reach the EHR as "final". The first live check found that a direct read of `Observation/abo-1` returned a superseded type; direct reads were then restricted to match search.
- Traceability: every read that returns patient data writes a PatientAccess audit event (channel FHIR), like opening the chart. Searches that find nothing write no event.

### Implemented

- `FhirMapper`: Patient (MRN identifier; a merged record has `link` replaced-by the survivor), Observation (ABO/Rh, antibody), Flag (special requirements; ended ones inactive with a period end), ServiceRequest (status and priority mapped), BiologicallyDerivedProduct (unit number and DIN identifiers, category, status, expiry), Bundle, OperationOutcome, CapabilityStatement.
- `FhirReadService`: reads and searches. Token search accepts a bare value or `system|value`; a foreign system matches nothing.
- `FhirEndpoints`: `/fhir/metadata`, `/fhir/Patient[/{id}]`, `/fhir/Observation[/{id}]`, `/fhir/Flag[/{id}]`, `/fhir/ServiceRequest[/{id}]`, `/fhir/BiologicallyDerivedProduct[/{id}]`, content type `application/fhir+json`.
- The help page for HL7 interface setup describes the API.

### Requirements / risk

- URS-BB-429, FRS-BB-464, SRS-BB-426, RISK-BB-531. OCD-043 updated. Gap analysis: FHIR implemented.

### Tests

- TEST-BB-320, `FhirReadServiceTests` (24 cases):
  - patient mapping and audit; unknown patient is null with no audit; merged patient links to the survivor
  - MRN search with bare, local-system, empty-system, foreign-system, and unknown identifiers
  - observations are the current type and active antibodies only; direct reads of a superseded type or retired antibody return nothing
  - no LOINC or SNOMED codes and no null values in any output
  - flags include ended requirements as inactive; service request status and priority
  - product by DIN, and a wrong system finds nothing; patient reference parsing; capability is read-only R4
- Full suite before the direct-read fix: 3,123 passed, 0 failed (previously 3,100). The FHIR class was rerun after the fix: 24 passed.
- Run check (development database, DevMode, API on port 5177):
  - metadata, Patient/1, MRN search, Observation, Flag, ServiceRequest, and BiologicallyDerivedProduct return `application/fhir+json`
  - a missing search parameter returns 400 and an unknown patient returns 404, both as OperationOutcome
  - POST returns 405
  - `Observation/abo-1` (superseded) returns 404 and `abo-2` (current) returns 200
  - the audit trail shows PatientAccess events with `{"Channel":"FHIR"}`
  - no server errors

### Next ranked residual

All four decided Partial items (OCD-042, OCD-004 readiness, OCD-038 drafts, OCD-043) are now addressed. Next: rescan the capability matrix for unblocked work. The leading candidate is the RhIG workflow (gap 11), which still needs SME indications, so the scan should first look for UX rows still rated Partial.

## Iteration 277 — Emergency release cannot override an established ABO conflict (2026-11-01)

### Capability matrix rescan

All rows are Present except three Partial rows that wait on a license or SME sign-off: licensed ICCBBA tables (OCD-004), PBM content (OCD-038), and FHIR terminology mapping (OCD-043). With no unblocked Partial row, this cycle audited a safety-critical workflow instead: emergency and MTP issue.

### Finding

`IssueGate` converted **every** ABO compatibility hard stop to an overridable warning when the issue type was emergency or MTP. A user with `issue.emergency-release`, a reason, and a signature could therefore release group A red cells to a patient whose group O type was established. OCD-003 covered only the missing crossmatch and the choice of group O; it never decided this. No test covered it.

### Decision

OCD-044 (product owner, 2026-11-01): red cells conflicting with an established ABO group are a hard stop even at emergency issue.

### Design

- New result `ISS-EMERG-ABO-CONFLICT` from `EmergencyUncrossmatchedAboRule.EvaluateEstablishedConflict`. The gate adds it next to the existing emergency rules. The existing conversion is left alone, so every other converted result behaves as before.
- Red-cell direction only: the unit carries an A or B antigen the patient lacks. Whole blood is judged on its red cells, so low-titer group O whole blood for a group A patient (plasma direction) stays an overridable warning.
- Only an established type counts. With no type, the group O warning still applies.
- Group O red cells never conflict, so this can never block an emergency transfusion. It only stops the wrong group.
- Audit: a refused issue already writes an EmergencyRelease audit row with "Issue blocked by safety gate." and the hard-stop codes. No override row is created.

### Questions asked (seven UX questions, emergency issue)

- Safety risk: yes, an acute hemolytic reaction from a deliberate but mistaken override. Now closed.
- Override control: the override stays for crossmatch, Rh, and plasma direction, with permission, reason, authorizer, and signature. It no longer reaches an ABO red-cell conflict.
- Next action: the message names the patient's group and says to choose group O or another compatible unit.

### Requirements / risk

- URS-BB-430, FRS-BB-465 (narrows FRS-BB-040), SRS-BB-427, RISK-BB-532. RISK-BB-001 updated. OCD-003 and the new OCD-044 updated.
- Help: issuing and safety pages.

### Tests

- TEST-BB-321:
  - `IssueGateTests`, replacing `Emergency_AboMismatch_IsWarning`, which asserted the old behavior:
    - four conflicting pairs are a single hard stop with a readable message
    - four compatible pairs, including AB recipients, are not stopped
    - group O whole blood for group A stays overridable; group A whole blood for group O is stopped
    - plasma ABO mismatch and Rh-only mismatch stay overridable
    - an unknown patient type with a group A unit is still only the group O warning
    - non-emergency issue still uses the ordinary ABO hard stop
  - `Phase4IssuingTests`:
    - a conflicting emergency release with reason and authorizer is refused; the unit stays Received; no issue or override row is written; the blocked attempt is audited with the code
    - group O red cells for an established group A patient still issue with an override
  - `IssueGateSafetyRegressionTests.Emergency_GroupARedCells_ForEstablishedGroupO_IsHardStopped`
- Full suite: 3,139 passed, 0 failed (previously 3,123). Domain 1,768, Integration 1,148, Help 29, Application 91, HL7 63, Printing 21, Astm 19. The safety-regression case was added afterwards; the safety suite then ran 44 passed.
- Run check (development API, DevMode): patient 1 is established O Pos and unit 13 is group A red cells.
  - With an override reason, `POST /api/issues` asks for an electronic signature first.
  - Signing needs password re-entry, which was not done in this check, so the hard stop itself was not reached live. It is proven by the integration test above, which runs the same service path with an override and an authorizer.
  - No server errors.
- Seen in passing: a request body with an enum written as text (`"issueType":"EmergencyRelease"`) returns 500 instead of 400. It is not a safety issue but is worth fixing.

### Next ranked residual

Malformed request bodies return 500 instead of a readable 400 across the API: an error-handling and human-readable-errors gap. Then continue auditing safety-critical workflows: MTP packs, then return and reissue.

## Iteration 278 — Readable 400 for requests the API cannot read (2026-11-01)

### Capability matrix rescan

No change since Cycle 277. The remaining Partial rows still wait on a license or SME sign-off. This cycle takes the ranked residual from Cycle 277.

### Finding

- Development: a body the API could not read, such as an enum sent as text, a missing body, broken JSON, or an address or query value in the wrong format, went to the development exception handler. That handler answered 500 "An unexpected error occurred." and included the serializer message, which names internal types.
- Production: minimal APIs answered an empty 400. Nothing told the user or the interface team which value was wrong.
- Production had no exception handler at all.

### Design

- `RouteHandlerOptions.ThrowOnBadRequest = true` in every environment, so these failures reach one exception handler.
- The handler now runs in every environment:
  - `BadHttpRequestException` returns its status (400, or 415 for a wrong content type) as problem+json, titled "The request could not be read." The detail comes from `RequestReadError`:
    - "The request body is not valid JSON." for a JSON syntax error (detected from the reader exception, not from the path)
    - "The value for 'scan.count' … is the wrong type, or is not one of the allowed values." for a field
    - "The value for 'fromUtc' in the address or query is not in the expected format." for a bind failure
    - "The request body is missing or is not JSON."
  - The submitted value, .NET type names, and stack are never returned.
  - Production: any other exception returns a bare 500 with no detail.
  - Development keeps its database-unavailable 503 and detailed 500.
- The Web client already shows `detail`, so screens show the new text unchanged.
- Enums stay numeric on the wire. Accepting text names would also change every response the Web client reads, so that is not done here.

### Tests

- TEST-BB-322, `RequestReadErrorTests` (13 cases):
  - an enum sent as text names the field and does not echo the value or the type name
  - a nested field uses its dotted path
  - three kinds of broken or truncated JSON say the body is not valid JSON
  - JSON path rendering, including array indexes
  - route and query bind failures name the parameter and not the value, including a nullable DateTime
  - a missing body
  - an unknown failure gets a generic message
- Full suite before the syntax-error refinement: 3,152 passed, 0 failed (previously 3,139). Application 103, Domain 1,768, Integration 1,149, Help 29, HL7 63, Printing 21, Astm 19. After the refinement, Application ran 104 passed. Only `RequestReadError` and its tests changed.
- Run check (development API, DevMode):
  - an enum as text on `POST /api/issues` returns 400 naming 'issueType'
  - truncated JSON on `/api/lookup` returns 400 "The request body is not valid JSON."
  - no body returns 400 "missing or is not JSON"
  - `fromUtc=yesterday` on the audit query returns 400 naming 'fromUtc'
  - a number where text is expected returns 400 naming 'query'
  - a valid lookup returns 200, and FHIR still returns 200
  - nothing reached the database for the bad requests

### Next ranked residual

FHIR endpoints should answer an unreadable search the FHIR way (OperationOutcome) rather than problem+json; this is minor because they take no body. Next safety audit: MTP packs, then return and reissue.

## Iteration 279 — Units come back to stock through retype; mislabeled units never leave (2026-11-01)

### Capability matrix rescan

No change since Cycle 278. This cycle continues the safety audit ranked there: massive transfusion, then return and reissue.

### Findings

1. **Unretyped units stranded as "Available".**
   - An emergency or MTP release may issue a unit that is still Received, before its ABO/Rh retype.
   - When that unit came back unused and passed inspection, the return sent it to Available.
   - Retype can be recorded only while a unit is Received, and the retype worklist lists only Received units. The unit could therefore never be retyped. It counted as available stock, yet allocation and routine issue refused it. The only way out was another emergency release, still unretyped.
   - Quarantine release and operational-hold release did the same, for example after an automatic temperature quarantine of a Received unit.
2. **Known mislabeled unit could be emergency-released.**
   - A verified retype that disagrees with the label quarantines the unit. After a quarantine release it was Available.
   - The issue gate saw only "retype not confirmed", which emergency release downgrades to an overridable warning. A unit proven to carry the wrong ABO/Rh label could therefore leave uncrossmatched with an override.

### Design

- `RetypeStockStatus.ForStock`: a retype-required unit with no verified matching retype goes back to Received; otherwise it goes to Available. Applied at return (only when the destination would be Available), quarantine release, and hold release.
- Status transitions Returned, Quarantine, and OnHold to Received are now allowed. Issued to Received is still refused.
- The status history reason adds "ABO/Rh retype not yet confirmed; back to Received for retype". The return evaluation and the audit record `RetypePending`. The release and hold audits do too.
- The Return, quarantine release, and hold release screens now say the unit went back to Received and that the retype must be recorded before allocation or issue.
- `ISS-RETYPE-MISMATCH`: when the latest verified retype disagrees with the label, it is a hard stop on every issue type. It sits outside the emergency conversion: "not yet retyped" may be overridden, "retyped and wrong" may not.
- No clinical rule was invented. Both changes enforce the existing retype requirement. No schema change.

### Seven questions (return and reissue)

- Information to remember: the user had to know that a returned emergency unit still needed a retype. Now the system infers it from the retype record and says so.
- Safety risk: both findings could lead to a wrong-type unit leaving the blood bank. Both are closed.
- Override control: the mismatch is not overridable; "not yet retyped" stays overridable only for emergency, as before.

### Requirements / risk

- URS-BB-432, FRS-BB-467, SRS-BB-429, RISK-BB-534. Help: issuing and inventory pages.

### Tests

- TEST-BB-323:
  - `RetypeStockStatusTests`: the decision table and the new transitions; issued to received is still refused
  - `IssueGateTests`: a mismatch is a hard stop for routine and emergency issue; no mismatch passes
  - `Phase4IssuingTests`:
    - an unretyped emergency unit returned with acceptable inspection goes to Received and is back on the retype worklist; the history reason, the return evaluation, the audit, and `ReturnDto.RetypePending` all say so
    - a retyped unit still returns to Available
    - emergency release of a unit with a verified mismatch is a hard stop and the unit stays put
  - `InventoryServiceTests`: quarantine release and hold release go to Received without a retype and to Available with one
- Full suite: 3,171 passed, 0 failed (previously 3,152). Domain 1,779, Integration 1,156, Application 104, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development API and Web, DevMode):
  - the Inventory and Issue & Transfuse pages render with the changed code and no server errors
  - no unit was returned or released, to leave the development data unchanged, so the new messages were not seen on screen
  - the behavior is covered by the integration tests above

### Next ranked residual

Massive transfusion protocol: check MTP pack issue for ratio guidance, cooler tracking, and that every unit in a pack passes the same gate. Then reaction investigation.

## Iteration 280 — Massive transfusion protocol activation and board (2026-11-01)

### Capability matrix rescan

No change since Cycle 279 apart from this row. The cycle continues the ranked safety audit: massive transfusion.

### Finding (seven questions, MTP)

- MTP was only an issue type picked unit by unit. Nothing recorded who called the protocol, why, or when it ended.
- Nothing tied a protocol's units together, so the desk had to remember how many units of each component had gone. Each unit was entered as if it were the first.
- Safety: untraceable uncrossmatched release and over-issue or wastage. Benchmarked products treat MTP as an activation.

### Design

- **Activation record** (`MtpActivation`):
  - patient, the requesting clinician, the reason, an optional location, and who activated it and when
  - closed with a reason and the user; rows are never deleted
  - one active per patient; refused on a merged chart
  - activate and close need `issue.emergency-release` (OCD-012); both are audited (`MassiveTransfusionProtocol`)
- **Issue link:**
  - a massive-transfusion issue is refused without an active activation (`MTP-NOT-ACTIVE`, with a message pointing to activation or to emergency release for a single unit), and otherwise links to it
  - the issue audit records the activation id
  - every unit still passes the full issue gate and needs its own override and signature
  - emergency release of a single unit is unchanged
- **Board:**
  - every Issue & Transfuse desk shows each active protocol: patient, minutes active, clinician, reason, and units issued, returned, and transfused per component class
  - **Issue under this MTP** opens the issue form for the patient with the MTP type and override on
  - **Close…** asks for the reason
- **Issue form:** with MTP selected and no activation, the form asks for the clinician, reason, and location and offers **Activate MTP**. With one, it names the activation and offers **Use as override reason**, an explicit click, never a silent default (OCD-041 spirit). The authorizer stays empty.
- **Not invented:** pack composition and component ratios. These are facility policy and require SME sign-off (OCD-012 note), so the board shows counts only.
- **Schema:** table `MtpActivations` and nullable `Issues.MtpActivationId`, through migration `20261103090000_MtpActivations`, the dev SQLite bootstrap, and the SQL Server copy order.

### Requirements / risk

- URS-BB-433, FRS-BB-468, SRS-BB-430, RISK-BB-535. OCD-012 note. Help: issuing page.

### Tests

- TEST-BB-324:
  - `MtpActivationRuleTests` (14 cases): activation needs a clinician and a reason, one active per patient, not on a merged chart; close needs a reason and an active protocol; an MTP issue needs an activation, other issue types do not
  - `Phase4IssuingTests`:
    - an MTP issue without an activation is refused and nothing is written
    - with an activation it links, appears on the board with a red-cell count of 1 and its unit number, and the issue audit carries the activation id
    - emergency release does not link
    - lifecycle: a second activation is refused; closing needs a reason; close is audited; a closed protocol is not on the board and cannot be closed again; a new activation after close works
    - activation without the emergency permission is refused and nothing is written
  - The existing MTP test now activates first.
- Full suite: 3,190 passed, 0 failed (previously 3,171). Domain 1,793, Integration 1,161, Application 104, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development API and Web, DevMode):
  - the dev database gained the table on startup
  - `GET /api/mtp/active` and `GET /api/patients/1/mtp` return `[]`
  - an activation with a blank reason returns 422 with the readable hard stop
  - on Issue & Transfuse, choosing MassiveTransfusion for patient 1 shows the activation panel; **Activate MTP** with empty fields shows "Name the clinician who called the massive transfusion protocol." and writes nothing
  - no server errors
  - no activation was created, to leave the development data unchanged, so the red board was not seen live

### Next ranked residual

Reaction investigation workflow audit. Pack composition for MTP waits on SME input (OCD-012).

## Iteration 281 — Facility MTP packs, shipped empty (2026-11-01)

Decision (product owner, 2026-11-01): "Yes, empty packs". MTP packs are configurable and none are shipped. Recorded under OCD-012.

### Design

- **Catalog:** Admin › MTP Packs (`MtpPackDefinition`).
  - Each pack has a code, a name, and units per round for red cells, plasma, platelets, cryo, and whole blood. Each is 0 to 50, with at least one above 0.
  - Each pack also has a sort order and an active flag.
  - Changes go to configuration history. The new-pack form starts every component at 0, so the form never suggests contents.
- **Activation:** may name an active pack. An inactive or unknown pack is refused (`MTP-PACK`). The audit records the pack id.
- **Round guidance (`MtpPackRule.Progress`):**
  - completed rounds come from the slowest pack component
  - each component shows units sent in the current round against its per-round amount, and can run ahead
  - "Sent" means issued and not returned
  - the banner shows the round, the completed rounds, and for example "Red cells 2/4 · Plasma 0/4"
- **Guidance only:** a pack never blocks, adds, or chooses a unit, and every unit still passes the full issue gate.
- **Known limit:** an activation follows its pack as currently defined. Edits are traceable in configuration history, but the activation does not keep a snapshot of the pack.
- **Schema:** table `MtpPackDefinitions` and nullable `MtpActivations.PackDefinitionId`, through migration `20261103100000_MtpPacks`, the dev SQLite bootstrap, and the SQL Server copy order.
- **Screens:** an admin page, a nav entry and Admin home card, a help topic, a pack choice at activation, and round progress on the banner.

### Requirements / risk

- URS-BB-434, FRS-BB-469, SRS-BB-431, RISK-BB-536. OCD-012 updated.

### Tests

- TEST-BB-325:
  - `MtpPackRuleTests` (14 cases): a valid pack; all components at zero; out of range; missing or long code or name; duplicate code; progress with nothing sent; completed rounds set by the slowest component; components outside the pack ignored
  - `Phase4IssuingTests`:
    - a pack is created (code upper-cased, summary text, configuration history), chosen at activation, and the board shows red cells 1 and plasma 0 in round 1 after one issue; after the unit is returned it no longer counts
    - an inactive or unknown pack is refused at activation and nothing is written
    - an all-zero pack is refused
- Full suite: 3,204 passed, 0 failed (previously 3,190). Domain 1,804, Integration 1,164, Application 104, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development API and Web, DevMode):
  - `/api/admin/mtp-packs`, `/api/mtp/packs`, and `/api/mtp/active` return `[]`, so none are shipped
  - `/admin/mtp-packs` renders
  - **New pack** then **Save** with nothing entered shows "Enter a code for the pack." and "A pack needs at least one unit of some component." and writes nothing
  - no server errors
  - no pack was created, to keep the development data empty as decided

### Next ranked residual

Reaction investigation workflow audit.

## Iteration 282 — MTP pack records, transport containers, and the pack report (2026-11-01)

Requested by the product owner (2026-11-01):
- a pack report for the MTP workflow listing the units, pack name and code, date and time packed, the tech who packed it, and for each unit the product type, code, ABO/Rh, and expiration date and time
- the temperature of the pack, recorded
- disclaimers
- spaces for recipients to record the date and time they check, with initials
- space for the ordering provider's name and date
- pack setup in Admin with selectable coolers or other transport vessels

### Design

- **Transport containers** (Admin › Transport Containers, `TransportContainer`):
  - code, name, type (cooler, insulated bag, box, other), description, and optional lowest and highest °C and validated hours
  - none shipped; ranges and hours are the facility's own validation and the form starts them blank
  - changes go to configuration history
- **Pack record** (`MtpPackRecord`), made from **Pack a cooler…** on the protocol banner:
  - container, optional "which one" label, temperature, comment, and the issued units ticked into it
  - nothing is pre-ticked and the temperature starts blank, so the tech records what is actually in the container
  - the system sets the pack number, the packed time (UTC), and the packer (the signed-in user)
  - `Issues.MtpPackRecordId` links each unit
- **Packing rules** (`TransportContainerRule.EvaluatePacking`):
  - needs an active protocol, an active container, a temperature from -40 to 60 °C (catching typing errors, not a specification), and at least one unit
  - every unit must have been issued under this protocol, still be issued, and be in no other pack
  - a reading outside the container's facility range is a warning: recorded, flagged on the banner and the sheet, not blocked
  - packing needs `issue.create` and is audited (`MassiveTransfusionProtocol`, action Packed, units listed)
- **Pack report** (`/mtp/pack-records/{id}`, printable landscape; times UTC):
  - patient, MRN, date of birth; protocol number, when activated, who called it and why
  - pack definition name and code, pack number, packed time and packer, container and label, temperature and range
  - per unit: number, product type, facility and ISBT product code, ABO/Rh, expiration, and the crossmatch status the issue recorded
  - blank "Checked (date/time)" and "Initials" columns; blank "Received by / date/time / initials"; blank "Ordering provider (print name) / Date / Signature"
  - the statements; printing is audited (`Print`)
- **Statements** (`MtpPackReportText`): they describe how the units were released and give general handling reminders, with no clinical or time-limit claim of their own. A time limit is printed only from the container's validated hours. Wording is recorded for SME review as OCD-045.
- **Found in the live check:** the first version labelled plasma "Crossmatched" because it inferred from "not uncrossmatched". The column now prints the status the issue recorded ("Uncrossmatched", "Compatible", "Not crossmatched", ...).
- **Demo data:**
  - patient MRN0021 with an **active** protocol: two O Neg red cells and two AB Pos plasma issued under it, each its own new demo unit
  - one demo cooler with no range or hours, labelled not validated, and one recorded pack
  - this lets the banner, packing form, and report be seen; close the protocol to clear the banner
- **Schema:** tables `TransportContainers` and `MtpPackRecords`, and `Issues.MtpPackRecordId`, through migration `20261103110000_MtpPackRecords`, the dev bootstrap, and the SQL Server copy order.

### Requirements / risk

- URS-BB-435, FRS-BB-470, SRS-BB-432, RISK-BB-537, OCD-045. Help: new topics `transport-containers` and `mtp-pack-report`; issuing page updated.

### Tests

- TEST-BB-326:
  - `TransportContainerRuleTests`: container validation, inclusive range limits, the packing refusals, and an out-of-range reading is a warning
  - `MtpPackReportTextTests`: base statements carry no time limit; uncrossmatched, out-of-range, and validated-hours statements are added only when they apply
  - `MtpPackCrossmatchTextTests`: crossmatch wording from the recorded status
  - `Phase4IssuingTests`:
    - a pack is recorded, linked, and audited, and the report carries every field; printing is audited
    - an out-of-range reading is recorded and flagged
    - refusals write nothing: no temperature, a foreign issue, no permission; a unit already packed; a closed protocol
  - `SeederTests`: the demo protocol, its four units, and its pack; the demo cooler has no range
- Full suite: 3,232 passed, 0 failed before the crossmatch-label fix (previously 3,204).
- After the fix, the full suite found one test-catalog citation the packaging check could not parse; that was corrected. The final counts are 3,237 passed: Domain 1,829, Integration 1,172, Application 104, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development API and Web, DevMode, with the demo data seeded on startup):
  - the banner shows MTP #1 with "Red cells 2 issued · Plasma 2 issued" and "Pack 1 · Demo cooler (not validated) Cooler 1 · 4 units · 4.0 °C"
  - **Pack a cooler…** opens the form with the container list; it says every unit is already packed, and **Record pack** stays disabled
  - the report shows every field, the blanks, and the statements; after the fix the plasma rows read "Not crossmatched"
  - Admin › Transport Containers lists DEMO-COOLER
  - no server errors
  - **Print** was not clicked, to avoid leaving a print dialog open in the preview; the print audit is covered by the integration test

### Next ranked residual

Reaction investigation workflow audit. OCD-045 wording and local-time printing await SME and product-owner review.

## Iteration 283 — Signed reaction investigations are locked; reopen needs a reason (2026-11-01)

### Capability matrix rescan

No change since Cycle 282. This cycle is the ranked audit of the reaction investigation workflow.

### Findings

1. **A closed, signed investigation could still be edited.**
   - `UpdateAsync` never checked the status.
   - The screen deliberately resent edits to a closed case "keeping its original close signature", so conclusions could change after signing while the record still showed the old signature.
   - Setting the status back to Open needed no reason.
2. **Written conclusions were not in the audit before-and-after.** The update snapshot listed status, type, severity, repeat types, DAT, elution, and flags, but not Findings, Conclusions, Follow-up, Disposition, clerical notes, or visual acceptability.
3. **Considered and found already covered:** the repeat ABO/Rh comparison against the type of record and the unit label (`RepeatPatientAboRhDiscrepancy`).

### Design

- `ReactionInvestigationLockRule`:
  - a closed case refuses any change to a workup or conclusion field, and any status away from Closed (`RXN-CLOSED`)
  - resending identical values is accepted, so an unchanged save from the screen does not fail
  - blank and null text count as the same value
- **Reopen** (`ReopenAsync`, `POST /api/reaction-investigations/{id}/reopen`):
  - needs the investigate permission and a reason (`RXN-REOPEN`), and works only on a closed case
  - moves the case to Under review, clears the close fields, and records `ReopenedBy`, `ReopenedUtc`, and `ReopenReason` (the latest; earlier ones are in the audit)
  - the audit carries the previous close signature, closer, and time, with the reason
  - closing again needs a new signature
- CBER notification and the written report stay available on a closed fatality, because they are reporting, not findings.
- The audit snapshot now includes the written fields.
- **Screen:**
  - a closed case shows "Closed and signed by … on … UTC", the fields are read-only, Save is hidden, and **Reopen…** asks for a reason
  - a reopened case shows who reopened it and why
- **Schema:** three nullable columns, through migration `20261103120000_ReactionReopen` and the dev bootstrap.

### Requirements / risk

- URS-BB-436, FRS-BB-471, SRS-BB-433, RISK-BB-538. Help: reactions page.

### Tests

- TEST-BB-327:
  - `ReactionInvestigationLockRuleTests` (11 cases)
  - `ReactionInvestigationServiceTests`:
    - a closed case refuses a conclusion change and a move back to Open, but accepts an unchanged resave; the stored conclusion and signature are unchanged
    - reopen: no reason is refused; with a reason the case moves to Under review with the reopen recorded and the old signature in the audit; a second reopen is refused; an edit's audit shows the old and new conclusion; closing without a signature is refused, and with a new one succeeds
    - a closed fatality still records CBER notification
- Full suite: 3,251 passed, 0 failed (previously 3,237). Domain 1,840, Integration 1,175, Application 104, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development API and Web, DevMode):
  - the only dev investigation is under review, and reopening it returns 400 "Only a closed investigation can be reopened."
  - `/reactions?id=1` renders it editable with Save
  - no server errors
  - closing needs a password-backed signature, so the locked view was not produced live; the integration tests cover it

### Next ranked residual

Continue the workflow audits: transfusion administration documentation (bedside start, stop, vitals, and volume), then shift handoff.

## Iteration 284 — MTP pack report in local time (2026-11-01)

Decision (product owner, 2026-11-01): "Yes, local time". The pack report travels to the bedside, so its times print in the facility's local time. Recorded under OCD-045.

### Design

- No facility time zone existed. The ISBT expiration parser still marks it as a policy item.
- New `IFacilityTimeZone`, resolved from configuration `Facility:TimeZone` (a Windows or IANA id, empty by default):
  - blank uses the server's zone, and the label says "(server time zone)"
  - an unknown id also falls back to the server's zone, and the label names the id that was not found, so a typo is visible on paper instead of silently printing the wrong zone
- The API converts, so the result does not depend on the machine that displays the page. Stored times stay UTC.
- The report DTO adds `TimeZoneLabel`, `ActivatedLocal`, `PackedLocal`, and per unit `ExpiresLocal`. The header reads "All times: (UTC-05:00) Eastern Time (US & Canada) …".
- Only the pack report changes; the rest of the system still shows UTC.

### Tests

- TEST-BB-328:
  - `FacilityTimeZoneTests`: conversion across daylight saving (January UTC-6, July UTC-5 for America/Chicago), unspecified kind treated as UTC, blank and unknown zones fall back with a visible label, and the configuration key is read
  - `Phase4IssuingTests.MtpPackReport_PrintsLocalTime_AndNamesTheZone`
- Full suite: 3,258 passed, 0 failed (previously 3,251). Domain 1,840, Integration 1,182, Application 104, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development, no zone configured, server on Eastern Time):
  - the demo pack report reads "All times: (UTC-05:00) Eastern Time (US & Canada) (server time zone)"
  - packed 2026-11-02 01:47 UTC prints as 2026-11-01 20:47, which is correct after daylight saving ended on 1 November
  - unit expirations are converted the same way
  - no server errors

### Next ranked residual

Transfusion administration documentation audit (bedside start, stop, vitals, volume), then shift handoff. Whether the rest of the screens should also show local time is a separate product decision.

## Iteration 285 — No transfusion without bedside checks; consistent times and volume (2026-11-01)

### Capability matrix rescan

No change since Cycle 284. This cycle audits transfusion documentation (bedside start, stop, vitals, volume).

### Findings

1. **Critical: "mark transfused" skipped the bedside.** A one-click `MarkTransfusedAsync`, behind the "Transfuse" button on the chart Products tab, the Product History tab, and the unit page, recorded a completed transfusion:
   - with no patient identifiers checked
   - with no bedside unit scan, even for ISBT 128 units
   - with **no expiration check**
   - with no second-verifier policy
   - with **ward receipt filled in as received by the clicking user**
   - with the clicking user as transfusionist, and the start time equal to the stop time
   It recorded bedside verification that never happened.
2. The full form accepted inconsistent data: a stop before the start, times in the future, zero or negative volume, or more volume than the bag held.

### Design

- The shortcut is removed: service method, `POST /api/issues/{id}/mark-transfused` (now 404), client call, and the three buttons.
- The buttons became **Document transfusion** links to `/issuing?desk=transfuse&issueId={id}`. The Issuing page opens bedside documentation prefilled with that issue (unit number and issue cues), so it is still one click to reach and every check applies.
- An issue that is no longer waiting gets "Issue #n is not waiting for transfusion documentation. It may already be documented or returned."
- `TransfusionDocumentationRule` (`TXN-DOCUMENTATION`, hard stops; data checks, not clinical limits):
  - a start or stop time more than 5 minutes ahead of the server clock (the allowance covers clock drift)
  - a stop before the start
  - a volume of 0 or less
  - a volume above the unit's recorded volume, when that is known
- The chart workflow ribbon now reads "Issued — document the transfusion at the bedside".

### Requirements / risk

- URS-BB-437, FRS-BB-473, SRS-BB-435, RISK-BB-539. Help: issuing and patient product history pages.

### Tests

- TEST-BB-329:
  - `TransfusionDocumentationRuleTests` (10 cases)
  - `Phase4IssuingTests.DocumentTransfusion_InconsistentTimesOrVolume_AreRefused`: a stop before the start, more than the bag held, and a future start are each refused; the unit stays Issued with no transfusion event; consistent times and volume then document
- `MarkTransfused_CompletesIssuedUnit` was removed with the shortcut.
- Full suite: 3,268 passed, 0 failed (previously 3,258). Domain 1,850, Integration 1,182, Application 104, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development API and Web, DevMode):
  - `POST /api/issues/1/mark-transfused` returns 404
  - `/issuing?desk=transfuse&issueId=5` opens Document transfusion with unit W0001230009101 filled in
  - an unknown issue id shows the "not waiting" message
  - the MRN0001 Product History tab shows **Document transfusion** linking to issue 1
  - no server errors

### Next ranked residual

Shift handoff audit. Bedside vitals are stored as JSON with no structure on the documentation form; what vitals to require is a facility decision (do not invent).

## Iteration 286 — Shift handoff accountability (2026-11-01)

### Capability matrix rescan

No change since Cycle 285. This cycle audits shift handoff.

### Findings

1. **Wrong permission.**
   - Recording and acknowledging a handoff were gated by `audit.read`, a read permission. The seeded **ReadOnly**, **Auditor**, and **Interface Analyst** roles could therefore accept responsibility for the bench's open work.
   - A facility that removed audit access from bench staff would have blocked handoffs altogether.
2. **The recipient was not checked on the server.** The page offers a directory picker, but the API took any text, so a typo addressed the handoff to nobody.
3. **The packet did not count active massive transfusion protocols** (added in Cycle 280), the most time-critical open work at shift change.

### Design

- New permission `shift.handoff`:
  - in `PermissionCodes.All` and seeded to Technologist, so Supervisor inherits it; Administrator has all
  - upgraded databases grant it to Administrator, Dev Admin, and Supervisor through the existing permission sync, and to Technologist through the bench-permission step that already handled `specimen.edit`
  - the API group, menu entry, dashboard card, and page use it; the Audit Trail and Downtime Snapshot links still use `audit.read`
- `ShiftHandoffRule.EvaluateRecipient` (`SHIFT-HANDOFF-TO`): a named recipient must be an active, unlocked, non-service user other than the sender; blank still means anyone on the next shift.
- `ShiftHandoffs.ActiveMtp` counts active protocols at handoff; it is in the audit snapshot and leads the summary as "MASSIVE TRANSFUSION ACTIVE: n".

### Requirements / risk

- URS-BB-438, FRS-BB-474, SRS-BB-436, RISK-BB-540. Safety rules table updated. Help: shift handoff page.

### Tests

- TEST-BB-330:
  - `ShiftHandoffRuleTests`: recipient cases
  - `ShiftHandoffServiceTests`:
    - an audit-read-only account cannot record or acknowledge
    - a misspelled recipient and a self-addressed handoff are refused and nothing is written; an active user is accepted case-insensitively
    - active MTP protocols are counted and closed ones are not
  - `DashboardQueueCatalogTests`: audit read alone no longer shows the handoff card
  - Existing handoff tests now use `shift.handoff`.
- Full suite: 3,277 passed, 0 failed (previously 3,268). Domain 1,855, Integration 1,185, Application 105, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (upgraded development database, DevMode):
  - roles holding `shift.handoff`: Administrator, Dev Admin, Supervisor, and Technologist; not Auditor, ReadOnly, Interface Analyst, Inventory Manager, Billing Analyst, or Service Account
  - a handoff to "no-such-tech" returns 400 "no-such-tech is not an active user …"
  - `/handoff` renders
  - no server errors

### Next ranked residual

Downtime workflow audit (snapshot, recovery, back-entry of paper records). That is also where a deliberate, clearly labelled path for transfusions documented on paper during downtime would belong (Cycle 285 removed the unverified shortcut).

## Iteration 287 — Late-entry issue time (2026-11-01)

### Capability matrix rescan

No change since Cycle 286. This cycle audits downtime recovery and back-entry.

### Findings

1. **The issue time was unrestricted.**
   - The chart issue form has an editable "Date/time of issue", and the API stored any `IssuedUtc`.
   - An issue could be dated hours or days earlier, or in the future, with no reason. The gate still ran at entry time, so the record said checks happened at a time they did not.
   - Retrospective-crossmatch and in-transit due times were computed from the stated time, so a backdated issue was born overdue and a future-dated one hid its due time.
2. **Back-entry of downtime transfusions is blocked in places** (recorded, not changed this cycle):
   - a unit or specimen that has expired since downtime fails the gate, which runs at entry time
   - bedside documentation requires a fresh unit scan, which is impossible once the bag is gone
   - how to allow this safely is a product and SME decision (who may do it, whether a second person reviews it, and what happens to compatibility findings as of the event time); the question is put to the product owner

### Design

- `IssueTimeRule` (`ISS-TIME`):
  - a stated time more than 5 minutes ahead of the server is refused
  - more than 5 minutes earlier is a late entry that needs a reason
- `Issues.LateEntryReason`:
  - the stated time stays `IssuedUtc` and the entry time is `CreatedUtc`; the DTO adds `LateEntryReason` and `EnteredUtc`
  - the issue audit records `IssuedUtc`, `EnteredUtc`, and `LateEntryReason`
  - the gate still runs at entry time
- **Chart issue form:**
  - the time is sent only if the user edits it, so a form left open for a while does not become a "late entry"
  - **Late entry reason** appears only for an edited, earlier time
- **Schema:** migration `20261103140000_IssueLateEntry` and the dev bootstrap.

### Requirements / risk

- URS-BB-439, FRS-BB-475, SRS-BB-437, RISK-BB-541. Help: downtime page, step 3.

### Tests

- TEST-BB-331:
  - `IssueTimeRuleTests` (10 cases)
  - `Phase4IssuingTests.Issue_LateEntry_NeedsReason_AndKeepsBothTimes`: a future time and a reasonless backdated time are refused with nothing written; with a reason the issue keeps the stated time, the reason, and `EnteredUtc`, and the audit carries them
  - `Phase4IssuingTests.Issue_WithoutStatedTime_IsNotALateEntry`
  - Five existing return and in-transit tests that simulate elapsed time with a backdated issue now pass a late-entry reason.
- Full suite: 3,289 passed, 0 failed (previously 3,277). Domain 1,865, Integration 1,187, Application 105, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development API and Web, DevMode):
  - on a ready-to-issue unit, a 3-hour-earlier issue with no reason returns 422 `ISS-TIME` "…late entry. Give the reason…", and a 3-hour-later issue returns 422 "…in the future…"; nothing was issued
  - the chart issue form opens without the reason field and shows **Late entry reason** after the time is edited 3 hours earlier; the form was not submitted
  - no server errors

### Next ranked residual

Downtime back-entry of transfusions (awaiting product decision), then the supervisor queue audit.

## Iteration 288 — Second-person override review (2026-11-01)

### Capability matrix rescan

No change since Cycle 287. This cycle audits the supervisor queue.

### Findings

- **No override review existed.** The queue held antibody reviews, result verifications, and quarantine releases only.
  - `Override` rows (emergency release, issue warnings, results, specimens, orders, allocations) had no reviewer fields.
  - The catalog permission `override.approve` was defined, and counted as privileged for MFA, but **no code used it**.
  - Nobody ever looked at an override again.
- **Inconsistent demo data:** the trauma demo stored its emergency-release override with the issue id as context, while `IssuingService` uses the unit id.

### Design

- **Override review fields:** `ReviewOutcome` (Acceptable, Needs follow-up), `ReviewedBy`, `ReviewedUtc`, `ReviewNote`.
- `OverrideReviewRule` (`OVR-REVIEW`):
  - one review only
  - the reviewer must not be the performer (`CreatedBy`) or the authorizer
  - Needs follow-up requires a note
  - review is a record, not a gate; it never undoes the override
- `OverrideReviewService` (`override.approve`) lists unreviewed overrides oldest first and records each review, audited.
- **Supervisor queue:**
  - a new kind, **Override review**, for holders of `override.approve`
  - each row shows the action, context, rule, reason, authorizer, performer, and time, with **Open** (the unit, for issue overrides) and inline **Review** → **Acceptable** or **Needs follow-up**
  - the menu, dashboard card, and access rule include `override.approve`
- **Not decided here:**
  - who holds `override.approve`; it stays Administrator-only by default, and facilities grant it to their medical director or supervisor role (it makes holders MFA-privileged)
  - how soon overrides must be reviewed
- **Demo data:** the trauma override's context is now the unit, matching real issues. Existing development databases keep their row.
- **Schema:** migration `20261103150000_OverrideReview`, the dev bootstrap, and an index on `ReviewedUtc`.

### Requirements / risk

- URS-BB-440, FRS-BB-476, SRS-BB-438, RISK-BB-542. Help: supervisor queue page.

### Tests

- TEST-BB-332:
  - `OverrideReviewRuleTests` (7 cases)
  - `OverrideReviewTests`:
    - a second-person review is recorded, audited, and leaves the pending list, and cannot be repeated
    - review by the performer, by the authorizer (case-insensitive), without the permission, or with follow-up and no note is refused, and nothing is written
    - the supervisor queue lists unreviewed overrides only for `override.approve`
- `SeederTests` updated for the unit-context convention.
- Full suite: 3,299 passed, 0 failed (previously 3,289). Domain 1,872, Integration 1,190, Application 105, Help 29, HL7 63, Printing 21, Astm 19. Integration was rerun after the seeder fix: 1,190 passed.
- Run check (development API and Web, DevMode):
  - `/api/overrides/pending-review` lists the trauma demo's emergency release (authorized by Dr. Amy Lee, performed by DEV_ADMIN)
  - the supervisor queue shows **Override review (1)**
  - reviewing it as DEV_ADMIN, the performer, is refused on screen: "Someone other than the person who performed or authorized the override must review it."
  - a follow-up review with a blank note returns 422
  - no review was recorded and there were no server errors

### Next ranked residual

Downtime back-entry of transfusions (awaiting product decision). Otherwise continue workflow audits: specimen receipt and rejection.

## Iteration 289 — Invalid results never stay behind the type of record (2026-11-02)

### Capability matrix rescan

No change since Cycle 288. This cycle audits specimen receipt and rejection.

### Findings (critical)

1. **Invalidating a verified ABO/Rh result left its blood type current.**
   - A current type was only ever retired when a newer one was verified.
   - The issue gate, allocation, and electronic crossmatch read the current history row, so a wrong-blood-in-tube type kept driving compatibility after its result was invalidated.
2. **Retired types counted as evidence.** The two-determination rule and the ABO/Rh discrepancy checks read every history row, so an invalid type could satisfy "second concordant determination", or raise a discrepancy that never cleared.
3. **Rejecting a specimen changed only its status.** Its results, any type recorded from them, and reservations made against it stayed in use.

### Design

- **On invalidation** (`ResultService.FinishInvalidateAsync`):
  - every current history row recorded from the invalidated result (`SourceResultId`) gets `IsCurrent` false, `InvalidatedUtc`, and a reason, and is audited
  - the warning `RES-TYPE-RETIRED` says to repeat the ABO/Rh before red cells are issued
  - no earlier type is reinstated; that is the conservative choice, and the patient's type must be re-established
- **Retired rows are never evidence:**
  - `SecondAboDeterminationRule.FromHistory` skips rows with `InvalidatedUtc`; it replaces six hand-built determination lists (issue, ready-to-issue and worklists, allocation, electronic crossmatch)
  - both discrepancy checks at issue skip them too
  - the chart header skips them through `BloodTypeDto.Invalidated`
- **Specimen rejection** (`SPEC-REJECT-RESULTS`):
  - refused while the specimen has active results (not invalidated, not superseded), naming the tests
  - invalidate them first, so their consequences go through the path above
  - afterwards, units still reserved against the specimen are listed (`SPEC-REJECT-RESERVED`); reservations are not released automatically
- **Schema:** `PatientBloodTypeHistory.InvalidatedUtc` (migration `20261103160000_BloodTypeInvalidated`, dev bootstrap).
- **Known limit:** a correction that is not yet verified leaves the previous type current until the correction is verified (existing behavior; not changed).

### Requirements / risk

- URS-BB-441, FRS-BB-477, SRS-BB-439, RISK-BB-543. Help: patient specimens and patient tests pages.

### Tests

- TEST-BB-333:
  - `DerivedFromSourceRuleTests`
  - `ResultLifecycleTests`:
    - invalidating a verified ABO/Rh retires its type, leaves no current type, marks and audits the row, and warns
    - an invalidated type followed by a new matching type is not a second concordant determination and not a discrepancy
    - rejecting a specimen with a verified ABO/Rh is refused and names ABORH; after invalidation the rejection succeeds
- Full suite: 3,305 passed, 0 failed (previously 3,299). Domain 1,875, Integration 1,193, Application 105, Help 29, HL7 63, Printing 21, Astm 19.
- Run check (development API, DevMode):
  - rejecting ACC0001 returns 400 "SPEC-REJECT-RESULTS: This specimen still has results (ABORH, ABSC). Invalidate them first, then reject the specimen…"
  - the specimen stays Accepted
  - no server errors

### Next ranked residual

Coded specimen rejection reasons (rejection reasons are free text, so rejection rates cannot be reported by cause). Pending product decisions: downtime back-entry, and who holds `override.approve`.

## Iteration 290 — Coded specimen rejection reasons (2026-11-02)

### Capability matrix rescan

No change since Cycle 289 other than the specimen rejection row.

### Finding

Specimen rejection took a free-text reason. Rejections could not be counted by cause, so a ward that keeps sending mislabeled tubes or a run of suspected wrong blood in tube had no reliable signal.

### Design

- **Catalog:** `SpecimenRejectionReason` (Code, Text, RequiresComment, SortOrder, IsActive), maintained at Admin → Specimen Rejection Reasons with configuration history, cloned from Discard Reasons.
- **Starter list**, seeded as an editable example, not a regulatory list: hemolyzed, clotted, quantity not sufficient, unlabeled, label problem, suspected wrong blood in tube (comment required), wrong tube type, too old to test, Other (comment required).
- **Rejection** (`SpecimenService.RejectAsync`):
  - requires an active code (`SPEC-REJECT-REASON-REQUIRED`, `SPEC-REJECT-REASON-UNKNOWN`)
  - requires a comment when the code says so (`SPEC-REJECT-COMMENT-REQUIRED`)
  - stores `RejectionReasonCode` and the text plus comment; the audit records the code
  - the earlier refusals (active results, `SPEC-REJECT-RESULTS`) are unchanged
- **UI:** a shared picker (reason list plus comment) on the chart Specimens tab and the test worklist; Confirm is disabled until a reason is chosen, and the comment box says when a comment is required.
- **Schema:** table `SpecimenRejectionReasons` and `Specimens.RejectionReasonCode` (migration `20261103170000_SpecimenRejectionReasons`, dev bootstrap, migrator order).

### Requirements / risk

- URS-BB-442, FRS-BB-478, SRS-BB-440, RISK-BB-544. Help: new Specimen rejection reasons page; patient specimens page updated.

### Tests

- TEST-BB-334: rule tests (blank, unknown, inactive, comment required, pass, stored text); integration tests (coded rejection stores code, text, and audit; comment-required refusal then success; unknown, inactive, or blank code refused with the specimen unchanged).
- Full suite: 3,316 passed, 0 failed (previously 3,305).
- Run check (development API and web, DevMode):
  - the reference endpoint lists the nine starter codes
  - rejecting with OTHER and no comment returns 400 `SPEC-REJECT-COMMENT-REQUIRED`
  - the admin page lists the codes
  - the chart picker shows the reasons, keeps Confirm disabled until one is chosen, and asks for a required comment for Other
  - no server errors

### Next ranked residual

A rejection-by-cause report (by reason, ward and collector) on the new codes. Pending product decisions: downtime back-entry, and who holds `override.approve`.

## Iteration 291 — Specimen rejection reports by cause (2026-11-02)

### Capability matrix rescan

No change since Cycle 290 other than the specimen rejection row.

### Finding

Cycle 290 coded rejection reasons, but no report used them. The time and user of a rejection were only in the audit trail, so a rejection could not be dated or attributed in a report.

### Design

- **Specimen:**
  - `RejectedUtc` and `RejectedBy` are set at rejection and included in the audit
  - specimens rejected before this change report their last modification time instead
- **Report source** `specimen_rejections`:
  - SQL view `rpt_specimen_rejections` for SQL reports, plus an EF loader for visual reports, with the same columns
  - rejected specimens only
  - a missing code shows as `UNCODED`, so free-text rejections made before Cycle 290 are counted rather than dropped
- **Seeded reports:**
  - `RPT-SPECIMEN-REJECTIONS`: a list
  - `RPT-REJECTIONS-BY-CAUSE`: counts by month, code, draw location, and collector
  - both prompt for a date range; existing databases get them from the seeder
- **Schema:** migration `20261103180000_SpecimenRejectionReport` (columns and views), plus the dev bootstrap columns.
- **Not done:** a draw location picked from the ordering-location catalog. Draw location stays as recorded at collection.

### Requirements / risk

- URS-BB-443, FRS-BB-479, SRS-BB-441, RISK-BB-545. Help: the specimen rejection reasons page names the two reports.

### Tests

- TEST-BB-335:
  - grouped report: two WBIT rejections from one ward and collector count as 2, and uncoded rejections show as UNCODED
  - an accepted specimen is excluded
  - the list report returns only rejected specimens
  - the seeded-report count is now 11
  - rejection records its time and user
- Full suite: 3,317 passed, 0 failed (previously 3,316).
- Run check (development API):
  - both reports are seeded and run (no rejected specimens in dev data, so no rows)
  - a SQL preview against `rpt_specimen_rejections` runs on SQLite
  - no server errors
- SQL Server view syntax is not exercised locally.

### Next ranked residual

Product decisions are still pending: who holds `override.approve`, and downtime back-entry. The remaining Partial matrix rows wait on a licensed ICCBBA extract (OCD-004) or the downtime back-entry decision. The next cycle re-audits a high-risk workflow (emergency release and massive transfusion) against the seven workflow questions.

## Iteration 292 — Incompatible crossmatch after emergency release raises a critical alert (2026-11-02)

### Capability matrix rescan

No change since Cycle 291. This cycle re-audits emergency release and massive transfusion against the seven workflow questions.

### Audit (emergency release and MTP)

- **Controls already in place:**
  - permission `issue.emergency-release`
  - an MTP issue needs an active activation
  - reason and authorizer required, and second-person review in the supervisor queue
  - ABO conflict is a hard stop (OCD-044)
  - fresh unit scan and two identity tokens
  - late-entry reason
  - retrospective crossmatch queue with a due time
- **Finding (critical, question 6):** an incompatible crossmatch recorded after release only changed `Issue.CrossmatchStatus`.
  - Nobody was alerted.
  - The person recording it got no warning.
  - On the board the row sorted by due time, and its status was labeled "At issue: Incompatible", which is wrong: it was not incompatible at issue.
  - A unit being transfused, or one already given, could go unnoticed.
- **Lesser findings, not changed this cycle:**
  - the emergency authorizer is free text, not checked against the provider directory
  - an MTP issue's override is recorded as a warning override rather than as an emergency release

### Design

- `RetrospectiveCrossmatchIncompatibleRule`:
  - states where the unit stands (issued / transfusion started / transfused) in plain words
  - builds the warning `ISS-RETRO-XM-INCOMPATIBLE` and a Critical alert (source `retro-xm-incompatible`, key per issue, link to the retrospective board)
- `CompatibilityService`:
  - on an incompatible result against an open follow-up, keeps the follow-up open (unchanged), returns the warning, and raises the alert
  - the existing alert de-duplication keeps a repeat result from raising a second alert
  - acknowledging the alert records who, when, and a note
- Retrospective board:
  - incompatible rows first, with a red "Incompatible after release" flag
  - the in-transit and issued boards use the same label instead of "At issue"
- No automatic stop, notification, deviation, or reaction investigation. That is facility procedure: OCD-046 asks the SME / medical director.

### Requirements / risk

- URS-BB-444, FRS-BB-480, RISK-BB-546, OCD-046. No schema change. Help: issuing and compatibility pages.

### Tests

- TEST-BB-336:
  - rule tests (unit state wording, warning, alert key, severity, and patient naming)
  - integration:
    - an incompatible crossmatch after emergency release returns the warning, raises exactly one critical alert even when repeated, and lists the row before an older pending row
    - a compatible crossmatch raises no alert
- Full suite: 3,325 passed, 0 failed (previously 3,317).
- Run check (development API and web, DevMode):
  - the crossmatch service resolves with the alert dependency (compatibility evaluate returns 200)
  - the retrospective board renders its three demo rows with the new guidance
  - no server errors
  - no incompatible crossmatch was written to development data

### Next ranked residual

Check the emergency-release authorizer against the provider directory (lesser finding above), unless OCD-046 or the pending product decisions (`override.approve` holders, downtime back-entry) come back first.

## Iteration 293 — Override authorizer checked against the directory (2026-11-02)

### Capability matrix rescan

No change since Cycle 292 other than the supervisor-queue row.

### Finding

"Authorized by" on every override, including emergency release, was free text: misspellings, nicknames, or names of people who are not clinicians could not be told apart. Iteration 292 listed this as a lesser finding. The field can name a physician or a supervisor (OCD-041), so checking providers alone would be wrong.

### Design

- **Matching** (`AuthorizerDirectoryRule`):
  - against active ordering providers and active non-service LIS users
  - matches the id, the name, or the "Name (id)" suggestion text, ignoring case and extra spaces
  - a single provider match wins over a user with the same name
  - two entries in one list are ambiguous
- **Recording:** `BloodBankDbContext` stamps every new override with `AuthorizerMatch` and `AuthorizerReference` before audit capture.
  - This covers all six services that record overrides.
  - The stamp is fixed at override time; later edits do not recompute it.
  - Older rows show "not checked".
- **Never blocks:** an unknown name is accepted and recorded as typed. Whether a name outside the directory should be refused is a policy choice; it is not made here.
- **UI:**
  - the authorizer field suggests directory entries
  - it hints when a name is not found or is ambiguous
  - the supervisor queue shows the match in plain words
- **Schema:** migration `20261103190000_OverrideAuthorizerMatch`, plus dev bootstrap.

### Requirements / risk

- URS-BB-445, FRS-BB-481, SRS-BB-442, RISK-BB-547. Help: issuing page.

### Tests

- TEST-BB-337:
  - rule tests: provider by id, name, and suggestion; provider wins over a same-name user; user match; ambiguous providers; unknown and blank; suggestion round-trip; wording
  - integration:
    - provider, user, and unknown names are stamped on save
    - an inactive provider is not in the directory
    - a preset match is kept, and later edits do not restamp
- Full suite: 3,341 passed, 0 failed (previously 3,325).
- Run check (development API and web, DevMode):
  - the bootstrap added the columns
  - the supervisor queue shows "not checked against the directory" for the older demo override
  - on the issue desk, the field offered 12 directory suggestions, hinted on "Dr Nobody", and cleared for "Dr. Amy Lee (PROV-LEE)"
  - nothing was submitted
  - no server errors

### Next ranked residual

The MTP issue's override is recorded as a warning override rather than an emergency release (iteration 292 lesser finding). Pending product decisions: `override.approve` holders, downtime back-entry, and OCD-046.

## Iteration 294 — MTP overrides recorded as massive-transfusion releases (2026-11-02)

### Capability matrix rescan

No change since Cycle 293 other than the supervisor-queue row.

### Finding

An override made while issuing under a massive transfusion protocol was recorded as `WarningOverride`; only an emergency-release issue got `EmergencyRelease`. Second-person review showed MTP releases as ordinary warning overrides, and the overrides report could not count them. Iteration 292 listed this as a lesser finding.

### Design

- `OverrideAction.MassiveTransfusion` (2).
- `OverrideActionRule.ForIssue` maps the issue type to the action. `OverrideActionRule.Describe` gives the review wording, which the supervisor queue now uses.
- Migration `20261103200000_OverrideActionMassiveTransfusion` re-installs the report views so `rpt_overrides` names the new value. The dev seeder already refreshes views at startup.
- Historical rows are not rewritten: changing a recorded action would alter history.

### Requirements / risk

- URS-BB-446, FRS-BB-482, SRS-BB-443, RISK-BB-548. Help: supervisor page.

### Tests

- TEST-BB-338:
  - the issue-type mapping and wording
  - a real MTP issue records a `MassiveTransfusion` override
- Full suite: 3,348 passed, 0 failed (previously 3,341).
- Run check (development API):
  - the API started and rebuilt the views
  - the overrides report ran
  - a SQL preview grouping `rpt_overrides` by kind returned EmergencyRelease 2 and MassiveTransfusion 4 (the MTP rows are issue rows in the demo data)
  - no server errors

### Next ranked residual

The iteration 292 audit findings are now closed. Pending product decisions: `override.approve` holders, downtime back-entry, OCD-046, and whether an authorizer outside the directory should be refused. Next engineering candidate: re-audit return and reissue against the seven workflow questions.

## Iteration 295 — Supervisors review overrides (2026-11-02)

### Decision

The product owner decided that Supervisors hold `override.approve` (OCD-047). Until now only Administrator and Dev Admin had it on a new install, so in practice the supervisor queue's override review was reachable only by administrators.

### Change

- `override.approve` is added to the Supervisor role for new installs.
- `EnsureOverrideApproveSupervisorGrantAsync` grants it once on existing databases.
  - It records the marker setting `Seed.OverrideApproveSupervisorGrant`, so an administrator who later removes the permission from the role is not overridden at the next start.
  - The marker is not a facility policy and does not appear on the policy page.
- `override.approve` is MFA-privileged. Nothing changes at sign-in while both MFA settings stay at their default (off). If the facility turns on "Require MFA for privileged users", Supervisors must enroll an authenticator.
- Self-review stays refused.

### Tests

- TEST-BB-339: fresh install grants it and records the marker; an older database without the marker is granted once; an administrator removal after the grant is not undone.
- Full suite: 3,351 passed, 0 failed (previously 3,348).
- Run check (development API): after startup the dev database shows Supervisor holding `override.approve`, with the marker set.

### Next ranked residual

Re-audit return and reissue against the seven workflow questions. Pending product decisions: downtime back-entry, OCD-046, and whether an authorizer outside the directory should be refused.

## Iteration 296 — A return never disposes of a unit (2026-11-02)

### Capability matrix rescan

No change since Cycle 295 other than the return/discard row. This cycle audits return and reissue against the seven workflow questions.

### Audit (return and reissue)

- **Controls already in place:**
  - `issue.return` permission and a required reason
  - no return after transfusion started
  - three inspections, never assumed (OCD-040)
  - the return-to-available window, governed by the storage device
  - expiry
  - an unretyped emergency unit goes back to Received for retype
  - each step recorded in status history
- **Finding (question 7: override control):** a return could choose Disposed, and a failed seal or appearance was discarded automatically. That skipped what the discard step enforces:
  - `inventory.discard`
  - the coded discard reason (the wastage report showed no code)
  - the facility's second verifier for discards
  - A user holding only `issue.return` could dispose of a unit.

### Design

- `ReturnUnitAsync` never sets Discarded:
  - a disposal request is quarantined with reason `ReturnedForDisposal`
  - a failed seal or visual inspection is quarantined with `FailedReturnInspection`
- `ReturnDisposalRule.Notice` returns warning `RET-DISPOSE-VIA-DISCARD`, telling the user to discard on the unit page with a reason.
- The requested destination is kept in the return evaluation JSON and the audit.
- The return forms on the issue desk and the unit page no longer offer Disposed; the help line explains the path.
- **Trade-off:** disposing of a returned unit now takes one more step (discard from quarantine), and that step carries the controls. The unit is not selectable while it is quarantined.

### Requirements / risk

- URS-BB-447, FRS-BB-483, RISK-BB-549. No schema change; quarantine reason value 12 added. Help: issuing page.

### Tests

- TEST-BB-340:
  - rule tests: quarantine reason, notice wording, and that a quarantined unit can still be discarded
  - integration (updated):
    - a disposal request quarantines with `ReturnedForDisposal`, with no discard code and no Discarded status row
    - a failed seal or appearance quarantines with `FailedReturnInspection`, with the warning
- Full suite: 3,358 passed, 0 failed (previously 3,351).
- Run check (development API and web):
  - the return desk offers Available and Quarantine only, with the new guidance
  - no server errors
  - nothing was submitted

### Next ranked residual

Reissue after return: whether a returned unit keeps its reservation to the same patient. Check how allocation status changes at issue and return. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 297 — Reserve a returned unit again in one step (2026-11-02)

### Capability matrix rescan

No change since Cycle 296 other than the return row.

### Finding (questions 2 and 5)

At issue the patient's reservation becomes Consumed. A unit returned to Available is in general stock. Nothing told the user that, and re-reserving it for the same patient meant going to Compatibility and typing the unit and patient again. Reissue itself was safe: the issue gate reruns every check.

### Design

- **No automatic re-reservation.** Reservation runs compatibility, antigen-negative override, and other checks that a return must not bypass.
- `ReturnDto` gains:
  - `BackInStock`, read from the destination already recorded in the return evaluation
  - `PatientId`, from the issue
  - `ReserveAgainHref`
- After a return to stock, the issue desk and the unit page say the unit is no longer reserved and offer **Reserve again for this patient** (for users with `compatibility.allocate`).
  - The link opens Compatibility with the unit, patient, and current specimen filled in: one click to reserve.
- History keeps both reservations: the consumed one stays, and a new row is added.

### Requirements / risk

- URS-BB-448, FRS-BB-484, RISK-BB-550. No schema change. Help: issuing page.

### Tests

- TEST-BB-341:
  - return to stock: the DTO is back in stock with patient and link, no reservation is left; then the normal reservation and a reissue succeed, and two consumed reservations are recorded
  - return to quarantine offers no link
- Full suite: 3,360 passed, 0 failed (previously 3,358).
- Run check (development API and web):
  - the link target opens Compatibility with the unit (W0001230000001), the patient (MRN0001), and accession ACC0001 filled in, and Reserve unit enabled
  - nothing was reserved
  - no server errors

### Next ranked residual

Re-audit transfusion documentation and reaction reporting at the bedside against the seven workflow questions. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 298 — Transfusion judged against the unit at its start (2026-11-02)

### Capability matrix rescan

No change since Cycle 297 other than the transfusion row. This cycle audits bedside transfusion documentation and reaction reporting against the seven workflow questions.

### Audit

- **Controls already in place:**
  - `transfusion.document` permission
  - two patient identifiers matched to the chart
  - a fresh bedside unit scan for ISBT units
  - ward receipt, when the facility requires it
  - second verifier or electronic identification, with a directory check
  - consistent times and volume (Cycle 285)
  - a suspected reaction opens an investigation
  - status history and audit
- **Finding 1 (question 6):** unit expiry was compared with the time the record was entered. Bedside documentation is often charted after the fact, so a transfusion that started while the unit was in date could not be documented once the unit had expired. The record could not be completed.
- **Finding 2:** a start time before the unit was issued was accepted.
- **Separate start step (not changed):** the transfusion is recorded once, start and stop together, so a unit is never shown as "transfusion started" in real time. The incompatible retrospective crossmatch alert (Cycle 292) therefore says "issued" for a unit being transfused. Recorded as a residual.

### Design

- `TransfusionDocumentationRule.EvaluateAgainstUnit` checks the start time against the unit's life:
  - a start more than the 5-minute clock allowance before the issue is refused (`TXN-DOCUMENTATION`)
  - a start at or after the expiry is refused (`COMPONENT_EXPIRED`, naming the expiry)
  - documentation no longer compares the expiry with the time of entry
- This is a data-consistency check, not a new clinical limit. It keeps the existing "expired unit" stop and judges it at the moment the unit was started.

### Requirements / risk

- URS-BB-449, FRS-BB-485, RISK-BB-551. No schema change. Help: issuing page.

### Tests

- TEST-BB-342:
  - rule tests: started in date and charted after expiry; started after expiry; start before issue, both beyond and within the allowance
  - integration:
    - a transfusion started in date and charted after expiry is documented
    - a start after expiry is refused and the unit stays Issued
    - a start before issue is refused and no transfusion is recorded
  - an existing consistency test documented a start two hours before its own issue; it now issues earlier
- Full suite: 3,366 passed, 0 failed (previously 3,360).
- Run check (development API):
  - documenting issue 5 with a start in 2020 returned a hard stop naming the issue time (2026-11-02 03:05 UTC)
  - nothing was written

### Next ranked residual

A real-time "transfusion started" step at the bedside (start recorded when spiked, stop and outcome later), so in-progress transfusions show on boards and alerts. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 299 — Start a transfusion at the bedside, complete it later (2026-11-02)

### Capability matrix rescan

No change since Cycle 298 other than the transfusion row.

### Finding (questions 5 and 6)

A transfusion was recorded once, after it ended, with start and stop together. While it ran, the unit looked merely issued:
- the issued-units board could not show it as being transfused
- the incompatible retrospective crossmatch alert (Cycle 292) could not say the unit was in the patient
- a unit being transfused could still be returned

The `transfusion.start` and `transfusion.complete` permissions existed but nothing used them.

### Design

- **Start** (`transfusion.start` or `transfusion.document`) runs the bedside checks shared with one-step documentation, now in one place (`EvaluateBedsideAsync`):
  - two identifiers
  - a fresh unit scan for ISBT units
  - the unit in date and already issued at the start time
  - ward receipt when the facility requires it
  - second verifier or electronic identification, with a directory check
- Start records a transfusion with a start and no stop, and moves the unit to TransfusionStarted.
- **Complete** (`transfusion.complete` or `transfusion.document`) adds the stop, volume, disposition, and suspected reaction to the same record. It finishes the unit and the issue, opens a reaction investigation when suspected, and captures billing. The patient is not identified again.
- **Started** means a record with no stop on an issue still Issued. Start refuses an issue that already has any transfusion record.
- While started, one-step documentation and return are refused.
- **UI:** the issued-units board shows "Transfusing since HH:mm" and offers Complete; the desk offers Start, Document start and finish, or Complete; the desk tab is now "Transfusion".
- **Permissions:** accepting either code keeps today's technologist and supervisor access (`transfusion.document`) while allowing a bedside role with only start and complete.
- **Demo data:** issue 1 in the demo data has a finished transfusion record while the issue and unit are still Issued (`SeedDemoClinicalDataAsync`). The stricter rule above does not treat it as started; the seed inconsistency is recorded as a residual.

### Requirements / risk

- URS-BB-450, FRS-BB-486, RISK-BB-552. No schema change. Help: issuing page.

### Tests

- TEST-BB-343:
  - start then complete: the unit shows as transfusing, the board shows the start, and a second start, one-step documentation, and return are all refused; completion fills in the same record, finishes the issue, and opens the reaction investigation
  - start without identifiers is refused and records nothing
  - complete without a start, or with a stop before the start, is refused
  - a finished record on an issued issue is not shown as started and cannot be started again
  - start-only permission can start but not complete
- Full suite: 3,371 passed, 0 failed (previously 3,366).
- Run check (development API and web):
  - the desk shows "Start or document transfusion" with Start transfusion and Document start and finish
  - after the fix, no demo issue is shown as started
  - no server errors
  - nothing was started

### Next ranked residual

Fix the demo seed so issue 1 is consistent: either a transfused issue, or an issued one without a finished record. The incompatible retrospective crossmatch alert can now say "has started" for a running transfusion. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 300 — Demo transfusion made consistent (2026-11-02)

### Finding

The base demo (`SeedDemoClinicalDataAsync`, patient MRN0001, unit W0001230000099) recorded a finished transfusion (stop time, 250 mL, Completed), while the issue and unit stayed Issued. Iteration 299 surfaced it when the board briefly showed the issue as "transfusing". Demo data that contradicts itself trains users on states the system never produces and hides defects in tests.

### Change

- The demo is now a transfusion running at the bedside:
  - started 15 minutes ago by RN Jones on 4W Med/Surg, with no stop, volume, or outcome yet
  - unit status TransfusionStarted, with a status-history row (Issued to TransfusionStarted, "Transfusion started at the bedside")
  - the issue stays Issued
- It shows the new "Transfusing since" display, and the Complete flow can be tried on it.
- New databases get this. Existing development databases keep their earlier demo rows (the demo seeds once per MRN); nothing was rewritten.

### Tests

- TEST-BB-344: after seeding, every transfusion record on an Issued issue has no stop and no volume, and its unit is TransfusionStarted. The demo unit is among them.
- The lookback scenario test now counts a running transfusion as reaching the patient (Issued, TransfusionStarted, or Transfused); recipient tracing is unchanged.
- Full suite: 3,372 passed, 0 failed (previously 3,371).

### Next ranked residual

The incompatible retrospective crossmatch alert (Cycle 292) already distinguishes "has started" from "issued". Run a demo-data consistency sweep across the other seeded scenarios with the same invariant style (issued units versus allocations, returns, and discards). Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 301 — Demo data held to the system's own rules (2026-11-02)

### Sweep

Following iteration 300, one test now holds all seeded demo data to states the system itself produces (TEST-BB-345):
- issued or transfusing units against open issues
- open, transfused, and returned issues against their unit, transfusion, and return records
- coded discards and coded specimen rejections
- reservations against unit status

### Finding and fix

One violation: demo unit W0001230001027 (`SeedExtendedInventoryAsync`) was Discarded with free text and no discard reason code. A real discard always carries a code, and the wastage report groups by it. The seed now records `EXPIRED`, "Expired before use". Existing development databases keep their earlier row; nothing was rewritten.

### Tests

- TEST-BB-345 lists every violation in one message, so a future seed that breaks a rule says exactly what and where.
- Full suite: 3,373 passed, 0 failed (previously 3,372).

### Also found

`dotnet list package --vulnerable --include-transitive` reports high-severity advisories:
- `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (GHSA-2m69-gcr7-jv3q) and `System.Security.Cryptography.Pkcs` 9.0.4 (GHSA-846f-f764-76mx), in Api, Infrastructure, and Integration.Tests
- `Microsoft.OpenApi` 2.0.0 (GHSA-v5pm-xwqc-g5wc), in Api

### Next ranked residual

Update those packages to patched versions and verify with the full suite and a run check. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 302 — Vulnerable packages updated (2026-11-02)

### Request

The product owner approved updating the packages flagged in iteration 301.

### Change

| Package | Before | After | How |
|---|---|---|---|
| `Microsoft.OpenApi` (GHSA-v5pm-xwqc-g5wc) | 2.0.0 | patched | `Microsoft.AspNetCore.OpenApi` 10.0.4 to 10.0.14 (Api) |
| `SQLitePCLRaw.lib.e_sqlite3` (GHSA-2m69-gcr7-jv3q) | 2.1.11 | patched | `Microsoft.EntityFrameworkCore.Sqlite`, `.SqlServer`, and `.Design` 10.0.8 to 10.0.14 (Api, Infrastructure, Integration.Tests) |
| `System.Security.Cryptography.Pkcs` (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7) | 9.0.4 (9.0.11 after the EF bump) | 10.0.14 | direct reference in Infrastructure, above the version `Microsoft.Data.SqlClient` brings in |

- Patch releases only. Other outdated packages, including major versions of the test SDK, xunit runner, coverlet, and Markdig, were not touched.

### Also fixed

- The TEST-BB-345 catalog row from iteration 301 had prose with semicolons, which the catalog parser reads as class citations. `TestCatalogPackagingTests.CitedClasses_HaveSourceFiles` failed after that commit, because the suite ran before the docs were written. The row now cites only the test method.

### Verification

- `dotnet list package --vulnerable --include-transitive`: no project has vulnerable packages.
- Build clean. Full suite: 3,373 passed, 0 failed.
- Run check (development API and web):
  - the API started on the upgraded EF Core and SQLite, ran its bootstrap and seeder, and rebuilt the report views
  - `/openapi/v1.json`, the issue, supervisor-queue, and reference endpoints returned 200
  - a SQL preview against `rpt_specimen_rejections` ran
  - the web issuing page returned 200
  - no server errors
- SQL Server was not exercised locally.

### Next ranked residual

Add the vulnerable-package check to CI so new advisories are caught when they are published. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 303 — Vulnerable-package check in CI (2026-11-02)

### Request

The product owner approved adding the vulnerable-package check to CI.

### Change

- The repository had no CI workflow. Added `.github/workflows/vulnerable-packages.yml` (GitHub Actions, Ubuntu; every project targets `net10.0`). It runs:
  - on every push to `main`
  - on every pull request
  - weekly on Monday at 06:00 UTC, to catch advisories published when nothing changed
  - on demand
- It installs the SDK from `global.json` and runs `scripts/check-vulnerable-packages.sh`.
- The script restores the solution and runs `dotnet list package --vulnerable --include-transitive`. It fails if any project reports a vulnerable package. It also fails if the package source cannot be reached, so it never passes without having checked. It runs the same way locally (README, Build and test).
- The workflow has read-only permissions and does not build or run tests; that can be added as a separate job.

### Verification

- The current tree passes ("No vulnerable NuGet packages.").
- With the `System.Security.Cryptography.Pkcs` pin temporarily removed, the script reported Pkcs 9.0.11 (GHSA-846f-f764-76mx) and exited 1. The project file was then restored from git.
- The workflow has not run on GitHub yet; it runs on the next push to `main` or the next pull request.

### Next ranked residual

A CI job that builds and runs the test suite on pull requests. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 304 — Reminder for transfusions not completed (2026-11-02)

### Capability matrix rescan

No change since Cycle 303 other than the transfusion row. PR #13 is open; its vulnerable-package check was pending when this cycle started.

### Finding

With start and complete as separate steps (Cycle 299), a transfusion started and never completed leaves the unit "Transfusion started" indefinitely. The record also lacks stop, volume, outcome, and any reaction. Nothing flagged it.

### Design

- Facility policy `Transfusion.CompletionReminderHours` (Transfusion category; default 6; 0 to 24; 0 turns it off).
  - Its description says it is a documentation reminder, not an infusion time limit; the facility sets its own.
- `TransfusionCompletionRule` decides when a start is overdue and builds the Warning alert. The alert carries the unit, MRN, and start time, and links to the transfusion desk for that issue.
- `AlertScanner` syncs these alerts on its schedule. An alert resolves on the first scan after the transfusion is completed.
  - The scanner's new sources are optional constructor parameters, so existing callers are unchanged.
- The issued-units board shows the "Transfusing since" badge in red with "completion overdue".

### Requirements / risk

- URS-BB-451, FRS-BB-487, RISK-BB-553. No schema change. Help: issuing page.

### Tests

- TEST-BB-346:
  - rule tests: overdue at and after the hours, not before; never when off; no start; alert key, link, and wording
  - integration:
    - a 7-hour-old start raises one warning and a 1-hour-old start raises nothing
    - completing it resolves the alert on the next scan
    - hours set to 0 raise nothing
- Full suite: 3,381 passed, 0 failed (previously 3,373).
- Run check (development API):
  - the facility-policy list shows `Transfusion.CompletionReminderHours` = 6
  - the alert scanner started (every 300 s) and its startup scan ran
  - no server errors
  - no alert was expected, since the development data has no running transfusion

### Next ranked residual

Show the in-progress transfusion on the patient chart's Products tab and timeline, so the clinical team sees it without the issue desk. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 305 — Chart and timeline show a running transfusion as running (2026-11-02)

### Capability matrix rescan

No change since Cycle 304 other than the transfusion row.

### Finding (question 6)

The patient chart's product history showed a running transfusion as **Transfused** with disposition **Completed**, because a started record keeps the field's default value until it is completed. The patient timeline said "Unit … transfused". A clinician reading the chart during a transfusion would believe it had finished.

### Design

- **Product history:** a transfusion record with no stop on an issue still Issued is reported as `TransfusionStarted` (new event type 10), with disposition "In progress", highlighted as open. A **Complete transfusion** action opens the transfusion desk (for `transfusion.complete` or `transfusion.document`).
- **Timeline:** "Unit … transfusion started", "In progress, not yet completed", at Warning severity.
- Completed and one-step records are unchanged. A record with a stop time is always a finished transfusion.

### Requirements / risk

- URS-BB-452, FRS-BB-488, RISK-BB-554. No schema change; event type value 10 added. Help: product history and timeline pages.

### Tests

- TEST-BB-347, on seeded data (the running demo transfusion from Cycle 300):
  - product history shows it as TransfusionStarted, "In progress", open, and never as Transfused
  - the timeline shows "transfusion started", "In progress, not yet completed", at Warning
- Full suite: 3,382 passed, 0 failed (previously 3,381).
- Run check (development API):
  - MRN0001 product history and timeline both returned 200
  - the older development row, which has a stop time, still reads as Transfused
  - no server errors

### Next ranked residual

PR #13 holds Cycles 263 to 303; Cycles 304 and 305 are local commits. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 306 — No reader reports a running transfusion as finished (2026-11-02)

### Capability matrix rescan

No change since Cycle 305 other than the transfusion row.

### Sweep

After Cycle 305 fixed the chart and timeline, every other reader of transfusion records was checked. A running record keeps its disposition field's default (Completed) until it is completed. These readers reported that default as if it were real:
- the issues and transfusions report, both the SQL view and the visual reader (disposition "Completed")
- the crossmatch and transfusion activity report (detail "Completed")
- the lookback recipient trace (disposition Completed)
- unit activity ("Completed for …")
- utilization (counted as transfused)

The final-disposition board and billing were already correct: the board reads issues still Issued, and billing is captured only at completion.

### Design

- `TransfusionRecordState`: a record is running when it has no stop and its issue is still Issued, and then it has no outcome. `SqlDisposition` gives the same rule for report views.
- **Reports:** `InProgress`.
- **Lookback trace:** no disposition.
- **Unit activity:** "Started for …, not yet completed".
- **Utilization:** not counted as transfused.
- Migration `20261103210000_RunningTransfusionReportViews` re-installs the views on existing databases.
- **Test fixtures:** two tests (unit activity naming and utilization grouping) seeded a recorded outcome on an issue still Issued, a state the system never produces. Their issues are now Transfused; what they test is unchanged.

### Requirements / risk

- URS-BB-453, FRS-BB-489, RISK-BB-555.

### Tests

- TEST-BB-348:
  - rule tests: running state, outcome, and the SQL fragment
  - on the seeded running transfusion (W0001230000099):
    - the lookback trace has a transfusion record but no disposition
    - both report readers say InProgress
    - the SQL view `rpt_issue_transfusion` says InProgress
- Full suite: 3,388 passed, 0 failed (previously 3,382).
- Run check (development API):
  - both rebuilt views queried through the report preview on SQLite
  - no server errors

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy. Next engineering candidate: re-audit specimen collection and receipt (identification at draw, collector, and draw time) against the seven workflow questions.

## Iteration 307 — Tube-label identifiers and collector at accession (2026-11-02)

### Capability matrix rescan

No change since Cycle 306 other than the specimen row. This cycle audits specimen collection and receipt against the seven workflow questions.

### Audit

- **Controls already in place:**
  - `specimen.accession` permission
  - active specimen type
  - no future collection time
  - merged-patient block
  - unique accession number
  - two-identifier match
  - expiry calculated from policy and type
  - relabel needs a reason
  - receive and accept steps
  - coded rejection
- **Finding 1 (question 6, critical):** blank identifiers were filled from the patient record (MRN and date of birth) and then matched against the same record. The check could not fail, and a mislabeled tube (wrong blood in tube) would pass. Both chart accession forms said "Defaults to patient MRN".
- **Finding 2:** neither chart accession form sent Collected by or Draw location, so specimens accessioned in the app never recorded who drew them or where. The rejection-by-collector report (Cycle 291) could not group them.

### Design

- `SpecimenLabelIdentityRule`: both identifiers must come from the tube label (`SPEC-LABEL-IDS`), unless the facility pre-fills identifiers.
  - The policy is `Issue.PrefillPatientIdentifiers`, OCD-039, default off. It already governs issue and bedside.
  - The existing match against the record is unchanged.
  - Without a policy service (unit tests) the earlier behavior remains, so other tests are unaffected.
- **Chart forms:**
  - Specimens and Orders: fields labelled "on the tube", starting empty, with the live match hint (no expected value shown)
  - new Collected by and Draw location fields
- Collector and draw location are stored and added to the accession audit.
- Interface (HL7) specimens are not affected; they do not use this path.

### Requirements / risk

- URS-BB-454, FRS-BB-490, RISK-BB-556. OCD-039 note extended. Help: patient specimens page.

### Tests

- TEST-BB-349:
  - rule: missing identifier refused unless pre-fill is on
  - integration:
    - no identifiers is refused and nothing is accessioned
    - mismatched identifiers are refused
    - matching identifiers are stored, with collector, draw location, and the audit
    - with pre-fill on, the record is used
- Full suite: 3,396 passed, 0 failed (previously 3,388).
- Run check (development API and web):
  - posting an accession without identifiers returned "SPEC-LABEL-IDS: Enter both patient identifiers as they appear on the tube label…" and nothing was created (lookup 404)
  - the chart Specimens form shows the "on the tube" fields, a match hint for MRN0001, and Collected by and Draw location
  - nothing was submitted
  - no server errors

### Next ranked residual

Receipt: check the tube in hand against the accession at receive, by scanning the label. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 308 — Receive a specimen by scanning the tube (2026-11-02)

### Capability matrix rescan

No change since Cycle 307 other than the specimen row.

### Finding (questions 1, 4, and 6)

Receipt was a click on a row of the pending list. The scan field above the list only filtered it. Nothing tied the tube in hand to the row received, and a busy bench could receive the wrong row.

### Design

- `SpecimenReceiptScanRule`:
  - normalizes the scan (scanner sanitizer, trim, upper case)
  - matches the accession number or label barcode
  - gives plain refusals for no match, more than one match, or an empty scan (`SPEC-RECEIPT-SCAN`)
- `SpecimenService.ReceiveByScanAsync` receives the one matching specimen through the same receipt gate as the row button (Collected and not expired). A scan refusal names the accession; the row button keeps its message.
- The audit records the receipt method: "Scanned tube" or "Selected from the worklist".
- `POST /api/specimens/receive-scan` (`specimen.accession`).
- **Test worklist, Specimens tab:** the field is now "Scan tube to receive". Enter (a scanner's suffix) receives the tube and clears the field for the next one. Typing still filters, and the row Receive button still works.
- Requiring a scan for every receipt would be a facility policy decision. Not made here.

### Requirements / risk

- URS-BB-455, FRS-BB-491, RISK-BB-557. Help: test worklist page.

### Tests

- TEST-BB-350:
  - rule: accession or barcode match in any case, no match, empty scan, normalization, wording
  - integration:
    - a lower-case scan with scanner whitespace receives the tube and records the method
    - a barcode scan receives
    - an unknown tube is refused and nothing changes
    - a second scan of the same tube is refused, naming the accession
- Full suite: 3,407 passed, 0 failed (previously 3,396).
- Run check (development API and web):
  - scanning ACC-NOT-A-TUBE returned "SPEC-RECEIPT-SCAN: No specimen has accession or label ACC-NOT-A-TUBE…"
  - scanning ACC0001 (already Accepted) returned "ACC0001: A specimen with status Accepted cannot be received…"
  - the worklist Specimens tab shows "Scan tube to receive"
  - no server errors
  - nothing was received

### Next ranked residual

Accept the received tube with a scan as well, and show the specimen's draw-to-receipt time on the pending list. Pending decisions: downtime back-entry, OCD-046, and the authorizer directory policy.

## Iteration 309 — Accept by scan; draw-to-receipt time (2026-11-02)

### Capability matrix rescan

No change since Cycle 308 other than the specimen row.

### Change

- **Accept by scan:**
  - `AcceptByScanAsync` and `POST /api/specimens/accept-scan` use the scan matching from Cycle 308 (now shared in `FindByScanAsync`) and the existing accept gate
  - refusals name the accession, and the audit records `AcceptMethod`
- **Explicit mode:** a Receive/Accept choice next to the scan field, default Receive. Accepting declares the tube fit for testing, so it is never the side effect of scanning the same tube twice.
- **Draw-to-receipt time:** each pending row shows "received … after draw" or "waiting … since draw" (`SpecimenTransitRule`). This is display only and sets no limit; the specimen's expiry remains the limit that applies.
- An already-accepted specimen is now refused with "This specimen is already accepted for testing", instead of "Receive it first".
- **Fix found by the tests:** the scan lookup returned an untracked copy of a specimen that the same unit of work already tracked. It now loads the matched specimen by id. Each API request has its own context, so the running app was not affected.

### Requirements

- URS-BB-456, FRS-BB-492. Help: test worklist page.

### Tests

- TEST-BB-351:
  - transit wording
  - accept by scan after receipt, with the method recorded
  - accept by scan before receipt is refused, naming the tube
  - the pending list shows "waiting 20 min since draw" and then "received 20 min after draw"
- Full suite: 3,415 passed, 0 failed (previously 3,407).
- Run check (development API and web):
  - accept-scan of ACC0001 was refused, naming the tube
  - the worklist shows Receive/Accept, and the label follows the choice
  - rows show "received 1 h 40 min after draw" and "waiting 11 h 00 min since draw"
  - no server errors
  - nothing was received or accepted

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether every receipt must be by scan (facility policy). Next engineering candidate: audit result entry and verification (manual entry, instrument results, delta checks) against the seven workflow questions.

## Iteration 310 — ABO/Rh correction retires the derived type at once (2026-11-02)

### Capability matrix rescan

No change since Cycle 309 other than the results row. This cycle audits result entry and verification against the seven workflow questions.

### Audit

- **Controls already in place:**
  - permissions for enter, verify, correct, and invalidate
  - verify by a second person (ABO self-verify only by facility policy)
  - corrected and invalidated results keep their history (versioned, superseded links)
  - invalidation retires the derived type (Cycle 289)
  - the second-determination rule and discrepancy checks
  - instrument values verified without re-keying
- **Finding (question 6, critical):** the known limit recorded in Cycle 289.
  - Correcting a verified ABO/Rh left the type from the corrected result current until the correction was verified.
  - The person correcting had declared that type wrong, yet it kept driving compatibility at issue, allocation, and electronic crossmatch until someone verified.

### Design

- `CorrectResultAsync` retires every current type recorded from the corrected result:
  - not current, and `InvalidatedUtc` set, so it is never evidence again
  - reason "Source result corrected: …"
  - audited as Correct
- It returns `RES-TYPE-RETIRED` with wording saying the patient has no current ABO/Rh until the correction is verified.
- Verifying the correction records the corrected type, as before.
- Withdrawing the correction reinstates nothing. This follows the Cycle 289 conservative choice: the type is repeated or the correction verified.
- The retirement is now one helper shared with invalidation (`RetireDerivedTypesAsync`).

### Requirements / risk

- URS-BB-457, FRS-BB-493, RISK-BB-558. Help: patient tests page.

### Tests

- TEST-BB-352:
  - rule wording
  - integration: correcting a verified A Positive leaves no current type, marks the row invalidated with the correction reason, writes a Correct audit row, warns, and the retired type is not a second determination
- Full suite: 3,417 passed, 0 failed (previously 3,415). No existing test relied on the corrected type staying current.
- Run check: not done in the running app, because it would mean correcting a verified ABO/Rh in development data. The change is server logic covered by the tests above.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: delta checks between the new ABO/Rh and the historical type at entry (before verification), shown to the verifier.

## Iteration 311 — Second ABO/Rh determination from a separate specimen (2026-11-02)

### Capability matrix rescan

No change since Cycle 310 other than the results row.

### Ruled out

The planned next item, a delta check between a new ABO/Rh and the historical type, already exists:
- `AboRhDeltaRule` runs at verification, with an override controlled through the exception catalog
- `TestWorklistService` already shows the mismatch on worklist rows

Nothing was rebuilt.

### Finding (question 6, critical)

`SecondAboDeterminationRule.HasSecondConcordant` accepted any earlier concordant type as the second determination, including one tested on the same specimen. A repeat ABO/Rh on a wrong-blood-in-tube specimen would confirm itself and unlock routine red-cell issue and electronic crossmatch. The facility policy already describes the requirement as "historical or second-sample type"; the implementation did not enforce the separate sample.

### Design

- `PatientBloodTypeHistory.SpecimenId`: set when a type is recorded from a verified result. Older rows are backfilled from their source result (migration `20261103220000_BloodTypeSpecimen`, plus the dev bootstrap).
- `HasSecondConcordant` counts a historical type only when it is not from the same specimen as the current one. Rows with no known specimen (manual or historical types) still count.
- The issue hard stop and the chart notice now say "separate specimens".
- Every caller goes through `FromHistory`, so issue, ready to issue, worklists, allocation, and electronic crossmatch all follow.

### Found by the run check

The first version ran the backfill before the bootstrap added the new column. The development API failed to start with "no such column: SpecimenId". Tests use EnsureCreated and could not see this. The backfill now runs after the additive columns, and the API starts.

### Requirements / risk

- URS-BB-458, FRS-BB-494, SRS-BB-444, RISK-BB-559.

### Tests

- TEST-BB-353:
  - rule: the same specimen does not count, a different specimen counts, an unknown specimen counts, and the issue message wording
  - integration:
    - two verified B Positive results on one tube are not two determinations, and a second tube makes it two
    - each row records its specimen
    - the backfill SQL fills a row on SQLite
- The chart-notice wording test was updated to the new message.
- Full suite: 3,423 passed, 0 failed (previously 3,417).
- Run check (development API):
  - the API started after the fix
  - the dev database has no blood-type rows from test results, so the backfill filled nothing and there are no same-specimen pairs
  - no server errors
- SQL Server: the backfill SQL is standard (correlated UPDATE) but was not run there.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan.

## Iteration 312 — Handoff counts running transfusions and incompatible retrospective crossmatches (2026-11-02)

### Capability matrix rescan

No change since Cycle 311 other than the handoff row.

### Audit of manual blood type entry (no change)

Recording a patient ABO/Rh by hand is a controlled dangerous action: `immuno.override`, a required reason, audited as an override. It becomes the current type and counts as a determination, so with a concordant earlier type it can satisfy the second-determination rule. Whether a typed-in type should count is clinical policy, recorded as OCD-048 for the SME or medical director. Behavior is unchanged.

### Finding (operations)

The shift handoff packet counted retrospective crossmatches, issued and in-transit units, pending work, reactions, and active MTPs. It had no count for the states added in Cycles 292, 299, and 304: transfusions running at the bedside, those overdue for completion, and emergency releases whose retrospective crossmatch came back incompatible. These are what an incoming shift most needs to be told.

### Design

- Three counts on the snapshot, from the same queues as the issue desk:
  - `TransfusionsRunning`
  - `TransfusionsCompletionOverdue`
  - `RetrospectiveIncompatible`
- Stored on the handoff (migration `20261103230000_ShiftHandoffTransfusionCounts`, dev bootstrap) and included in the handoff audit snapshot.
- **Packet wording:** "INCOMPATIBLE AFTER RELEASE: N" right after an active MTP, then "transfusing N, M completion overdue".

### Requirements

- URS-BB-459, FRS-BB-495, SRS-BB-445, OCD-048. Help: shift handoff page.

### Tests

- TEST-BB-354: with a transfusion started 7 hours ago and an emergency release with an incompatible retrospective crossmatch, the next handoff counts one more running, one more overdue, and one more incompatible, and the DTO carries them.
- Full suite: 3,424 passed, 0 failed (previously 3,423).
- Run check (development API):
  - the API started with the new columns
  - the handoff list loaded (no handoffs in development data)
  - no server errors
  - no handoff was recorded

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, OCD-048, the authorizer directory policy, and whether receipt must be by scan.

## Iteration 313 — Manual ABO/Rh counts only when documented (OCD-048) (2026-11-02)

### Decision

The product owner decided OCD-048: a hand-recorded ABO/Rh counts as a determination only if documented from a previous blood bank application or a documented outside source.

### Change

- **Manual entry:**
  - records `ManualDocumentation`: Not documented, Previous blood bank system, or Documented outside source
  - records `ManualDocumentationReference`, the record or report, which is required for the two documented kinds (`BT-MANUAL-SOURCE`)
  - both are stored and included in the override audit
- `SecondAboDeterminationRule.FromHistory` marks an undocumented manual entry as not a determination. `HasSecondConcordant` then requires both the current and the earlier type to count.
  - Older manual rows have no documentation recorded, so they no longer count. That is the decision applied to history; their type and audit are unchanged.
  - Imported history (`HistoricalImport`, from a previous system) and tested types are unchanged.
- **Chart overview form:**
  - a Documented source choice, with the reference field shown when documented
  - a line explaining that only documented types count
- **Schema:** migration `20261104000000_ManualBloodTypeDocumentation`, dev bootstrap.
- **Tidy-up:** the service's new parameters come before the cancellation token, following the code's convention; the endpoint passes them by position.

### Requirements / risk

- URS-BB-460, FRS-BB-496, SRS-BB-446, RISK-BB-560. OCD-048 closed. Help: patient chart page.

### Tests

- TEST-BB-355:
  - rule:
    - an undocumented manual type does not count as either the earlier or the current determination
    - documented types count
    - imported history counts
    - a reference is required when documented
  - integration:
    - a documented type without a reference is refused and nothing is recorded
    - a documented type stores the source and reference, and they are audited
    - an undocumented type stores no reference
- Full suite: 3,433 passed, 0 failed (previously 3,424).
- Run check (development API and web):
  - the API started with the new columns
  - posting a documented type without a reference returned the `BT-MANUAL-SOURCE` message
  - the chart form shows the three choices, the required reference field, and the note
  - nothing was recorded
  - no server errors

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan.

## Iteration 314 — Blood type history shows source, retirement, and what counts (2026-11-02)

### Capability matrix rescan

No change since Cycle 313 other than the results row.

### Finding (question 2: what the user must remember)

The chart's blood type history showed type, subgroup, the raw source enum, and the date. Several recent rules decide which rows matter, and none of it was visible:
- retired types (Cycles 289 and 310) looked like valid ones
- the retirement reason was hidden
- an undocumented manual entry looked like any other
- the user could not tell which rows the two-determination rule could use

### Design

- `BloodTypeHistoryLabel`:
  - plain source wording: Tested result, Interface result, Imported from a previous blood bank system, or Manual with its documented source and record (or "not documented")
  - `CountsAsDetermination`: not retired, and not an undocumented manual entry; the same test the rule uses
- `BloodTypeDto` carries `SourceLabel` and `CountsAsDetermination`.
- **Chart history table:**
  - a retired type is struck through, with a Retired badge and the reason
  - an undocumented manual entry reads "Does not count as an ABO/Rh determination"
  - who recorded each row is shown
- Display only. No rule changed.

### Requirements

- URS-BB-461, FRS-BB-497. Help: patient chart page.

### Tests

- TEST-BB-356:
  - wording for each source
  - counting for retired, undocumented, documented, and tested types
  - DTO mapping for a retired tested type and a documented manual type
- Full suite: 3,441 passed, 0 failed (previously 3,433).
- Run check (development web):
  - MRN0001's history shows "Imported from a previous blood bank system" and "Tested result (Current)", each with who recorded it
  - development data has no retired or manual types
  - no server errors

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan.

## Iteration 315 — Outbound results say final, corrected, or wrong (2026-11-02)

### Capability matrix rescan

No change since Cycle 314 other than the HL7 row.

### Finding (interoperability, question 6)

Outbound ORU results were always sent with OBX-11 = F, and only results with status Verified could be sent.
- After a verified ABO/Rh was corrected (Cycle 310) or invalidated (Cycle 289), the EHR could not be told. A corrected value went as another "final", and an invalidated value could not be sent at all, so the EHR kept the wrong type as final.
- The builder's documentation and `hl7-design.md` said results were sent when verified. In fact they are queued only on request through `POST /api/hl7/outbound/results/{id}`, with no UI and, until now, no tests.

### Design

- `ResultReleaseRule.ObxStatus`, using HL7 v2 table 0085 (an interface standard, not a clinical rule):
  - F for a verified first version
  - C for a verified later version (a correction)
  - W for a result that had been verified and was then invalidated
  - nothing else is sent
- `Hl7OruBuilder` writes that status.
- `Hl7OutboundService` accepts an invalidated, previously verified result (sent as W) and still refuses unverified results.
- The builder comment and `hl7-design.md` 2.3 now describe sending on request and the F/C/W statuses.

### Requirements / risk

- URS-BB-462, FRS-BB-498, RISK-BB-561. The residual risk stays Medium because nothing sends results automatically yet.

### Tests

- TEST-BB-357:
  - rule: F, C, W, and not sent
  - builder: OBX-11 for a first verified result, a verified correction, and an invalidated verified result
  - integration (the first service test for this path): an invalidated verified ABO/Rh is queued with W, and an unverified result is refused
- Full suite: 3,451 passed, 0 failed (previously 3,441).
- Run check (development API):
  - queueing a missing result returned "Result 999999 not found."
  - no server errors
  - nothing was queued, so no outbound message was written to development data

### Next ranked residual

When a result is corrected or invalidated after being sent, queue the C or W message automatically, so the EHR is updated without anyone remembering to. This needs a record of which results were sent; it may need a product decision on automatic sending. Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan.

## Iteration 316 — Corrections and withdrawals reach the EHR automatically (OCD-049) (2026-11-02)

### Decision

The product owner decided OCD-049: send result corrections and invalidations to the EHR automatically.

### Design

- `IResultReleasePublisher` in Application, implemented by `Hl7OutboundService`, following the billing publisher pattern. `ResultService` stays free of HL7 types.
- **When:**
  - after a correction is verified, an ORU with OBX-11 C is queued
  - after a verified result is invalidated, one with OBX-11 W is queued
- **Only with somewhere to go:** a message is queued only when an enabled outbound Results interface endpoint exists, so none is left with no destination.
- First final results stay on request. The decision covered corrections and invalidations, and some facilities release results to the EHR another way.
- **Failure never undoes the clinical change:** if queueing throws, the verification or invalidation already saved stands, and the user gets `RES-EHR-NOT-SENT` asking them to send by hand.
- The send itself still goes through the existing outbound sender (MLLP) and its retry and error handling.

### Requirements / risk

- URS-BB-463, FRS-BB-499, RISK-BB-562, OCD-049. `hl7-design.md` 2.3 updated.

### Tests

- TEST-BB-358:
  - rule: only C and W are sent automatically
  - integration, in their own database:
    - invalidating a verified ABO/Rh with an enabled results interface queues exactly one ORU with W
    - correcting does not queue; verifying the correction queues one with C
    - without an enabled interface nothing is queued
    - a failing publisher leaves the invalidation saved and returns the warning
- Full suite: 3,459 passed, 0 failed (previously 3,451).
- Run check (development API):
  - the API resolves `ResultService` with the publisher (specimen results 200)
  - development data has no interface endpoints, so nothing is sent there until one is configured
  - no server errors

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan.

## Iteration 317 — Expiring units end their reservations (2026-11-02)

### Capability matrix rescan

No change since Cycle 316 other than the reservation row.

### Finding (question 6)

The expiration sweep moved a unit to Expired but left any reservation on it Reserved. The patient's chart and reservation lists still showed blood held that could not be issued, and staff would find out only when issue refused the unit. This is the same state the demo-data invariant (TEST-BB-345) treats as inconsistent, and the sweep itself created it. `AllocationStatus.Expired` existed but nothing set it.

### Design

- **The sweep (scheduled and manual):** for each unit it expires, every Reserved allocation becomes Expired, with release reason "Unit … expired at …", audited as Assignment.
- `InventoryActionResult.ReservationsEnded` carries the count to the hosted sweep.
- **The units-expired alert** adds "N of them were reserved for patients; those reservations have ended. Reserve replacement units."
- `InventoryService` takes the allocation repository as an optional dependency, resolved by dependency injection in the app.
- Issue still refuses an expired unit; this change makes the earlier screens tell the truth.

### Requirements / risk

- URS-BB-464, FRS-BB-500, RISK-BB-563. Help: alerts and inventory pages.

### Tests

- TEST-BB-359:
  - a reserved, crossmatched unit 30 minutes past expiry ends its reservation, with the reason and the audit; a reservation on an in-date unit is untouched
  - the alert wording for reserved units
  - one existing alert test was reordered, because the new assertion created a second units-expired alert
- Full suite: 3,460 passed, 0 failed (previously 3,459).
- Run check (development API):
  - the expired-unit sweep service started (every 300 s) with no errors
  - development data has no reservation left on an expired unit

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the same check for reservations whose crossmatch or specimen expires before the unit does.

## Iteration 318 — An expired crossmatch no longer shows "Ready for issue" (2026-11-02)

### Capability matrix rescan

No change since Cycle 317 other than the reservation row.

### Finding (questions 5 and 6)

The residual from Cycle 317 asked what happens to a reservation whose crossmatch or specimen expires before the unit does.

- **Specimen:** the expired-specimen sweep already unlinks pending crossmatch orders, and the issue desk already flags an expired specimen. No change needed.
- **Crossmatch:** the display status used only the latest crossmatch result, not its expiration. After a compatible crossmatch expired:
  - the chart Products tab still showed a green "Ready for issue"
  - the issue desk showed a green badge next to a blocking crossmatch notice
  - issue then hard-stopped

### Design

- New display state `CrossmatchExpired` ("Crossmatch expired", amber).
- `ProductAllocationDisplayStatusRule.Evaluate` gains an optional "has a current compatible crossmatch" input, computed with the same predicate as issue (`CrossmatchValidityRule.IsCurrentCompatible`).
- **Chart:** applied only to Reserved allocations, so units already issued keep their status. **Issue desk:** passes the existing `hasValidCrossmatch`.
- Display only. No stored state changes and nothing is overwritten; the issue gate is unchanged.
- The reservation is not ended automatically. Repeating the crossmatch is the usual next step, and the clinical procedure is not invented here.

### Requirements / risk

- URS-BB-465, FRS-BB-501, RISK-BB-564. Help: issuing page.

### Tests

- TEST-BB-360:
  - rule: Compatible with none in date gives Crossmatch expired; in date gives Ready; incompatible, missing, and non-crossmatch products unchanged
  - integration: a reserved unit whose only compatible crossmatch expired a minute ago shows Crossmatch expired on the issue desk, blocks routine issue, and has no crossmatch expiration time
- Full suite: 3,466 passed, 0 failed (previously 3,460).
- Run check (development API): `GET /api/issues/ready-to-issue` returns 200 with unchanged statuses for the in-date demo reservations; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: a work-queue count of reservations with an expired crossmatch on the dashboard, so they are repeated before the ward calls.

## Iteration 319 — Dashboard names reservations that need a new crossmatch (2026-11-02)

### Capability matrix rescan

No change since Cycle 318 other than the operations row (worklists).

### Finding (question 5)

Cycle 318 made an expired crossmatch visible on the chart and the issue desk, but only to someone who opened them. The dashboard Ready to issue card counted all reservations alike, so the repeat crossmatch was often found only when the ward called.

### Design

- `ReadyToIssueAttentionRule.CountCrossmatchExpired` counts ready-to-issue rows with display status `CrossmatchExpired`.
- The card note uses the existing `DashboardQueueCatalog.AttentionNote`, the same form as the retrospective ("past due") and in-transit cards: "1 needs a new crossmatch" or "N need a new crossmatch".
- It uses the list the card already loads, so there is no new API call. Read only, so nothing to audit.

### Requirements / risk

- URS-BB-466, FRS-BB-502, RISK-BB-565. Help: dashboard page.

### Tests

- TEST-BB-361: counts only Crossmatch expired rows, singular/plural wording, no note when none.
- Full suite: 3,467 passed, 0 failed (previously 3,466).
- Run check (development API and web): the dashboard shows Ready to issue 3 with no note (no demo reservation has an expired crossmatch); no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: on the issue desk Ready to issue board, put rows that cannot be issued routinely (crossmatch expired, specimen expired, outdated unit) after those that can, or let the user filter to them.

## Iteration 320 — Ready to issue lists issuable units first (2026-11-02)

### Capability matrix rescan

No change since Cycle 319 other than the operations row (worklists).

### Finding (questions 1 and 5)

The Ready to issue board listed reservations in reservation order. A unit that could not be issued routinely (expired crossmatch, expired specimen, outdated unit, special requirement, ABO/Rh discrepancy, second ABO/Rh) sat between units that could. When blood was needed, the tech had to scan each row's marks to find one with an Issue button.

### Design

- `ReadyToIssueAttentionRule.CanIssueRoutinely` encodes the same conditions the board already uses to show the Issue button, so the order and the button agree.
- The API returns issuable rows first; each group keeps reservation order.
- A status line above the table: "All N can be issued now." or "N can be issued now. M need attention first and are listed after them."
- Display only. The issue gate, the marks on each row, and the emergency button are unchanged.

### Requirements / risk

- URS-BB-467, FRS-BB-503, RISK-BB-566. Help: issuing page.

### Tests

- TEST-BB-362:
  - ordering puts issuable rows first and keeps order within each group
  - summary wording (all issuable, mixed, empty)
  - each blocking mark alone stops routine issue; non-blocking marks (open antibody workup, near expiry, antigen notice) do not
- Full suite: 3,469 passed, 0 failed (previously 3,467).
- Run check (development API and web): the board shows "1 can be issued now. 2 need attention first and are listed after them.", with the crossmatched demo unit first and the two units without a crossmatch after it; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the patient chart's Products tab says what to do next for a Crossmatch expired row (order a crossmatch on a current specimen) instead of only showing the status.

## Iteration 321 — No crossmatch on an expired specimen; repeat offered on the chart (2026-11-02)

### Capability matrix rescan

No change since Cycle 320 other than the safety rules row (sample expiration).

### Finding (questions 5 and 6)

The planned change was next-step guidance on the chart for a Crossmatch expired row. Checking how the repeat crossmatch would be recorded found a larger gap:

- `CompatibilityService.RecordCrossmatchAsync` (the direct API) and the automatic electronic crossmatch did not check the specimen's expiration.
- A crossmatch on an expired specimen was saved with an expiration already in the past, so it never counted for issue, and the user was not told.
- Result entry already refused expired specimens for every test, but it reported a blocked crossmatch only as "Crossmatch could not be recorded."
- On the chart, a reserved unit with an expired compatible crossmatch still offered Issue, which the gate refuses, and did not offer Crossmatch.

### Design

- `CrossmatchSpecimenRule`: a specimen at or past its expiration is a hard stop `XM-SPEC-EXPIRED` naming the specimen and the next step ("Collect a new specimen and crossmatch on it.").
  - Applied in `RecordCrossmatchCoreAsync` before anything is saved, for direct and electronic crossmatch.
  - An undated specimen keeps the existing default crossmatch validity.
- Retrospective crossmatches are refused too. Result entry already refused them, and allowing an exception is a clinical decision, recorded as OCD-050 rather than invented.
- Result entry passes the hard-stop message through.
- `CrossmatchEntryNeed` gains "compatible crossmatch expired". The chart offers Crossmatch on that row (serologic only; issued units excluded) and shows "Crossmatch expired. Repeat it on a current specimen before issue."
- No schema change. The refusal saves nothing, so there is no clinical record to audit.

### Requirements / risk

- URS-BB-468, FRS-BB-504, RISK-BB-567, OCD-050. `safety-rules.md` gains `XM-SPEC-EXPIRED`. Help: compatibility page.

### Tests

- TEST-BB-363:
  - rule: hard stop past and at expiration, with the specimen named; pass in date or undated
  - entry need: expired compatible needs repeating; not when electronic, issued, or incompatible
  - integration: a direct serologic crossmatch on a specimen expired five minutes ago is refused with `XM-SPEC-EXPIRED`, and no crossmatch row is saved
- Full suite: 3,474 passed, 0 failed (previously 3,469). No existing test relied on crossmatching an expired specimen.
- Run check (development API and web): the demo patient's Products tab renders the in-date reservation as Ready for issue with Issue offered; no server errors. No crossmatch was attempted against development data.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, OCD-050, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: check whether antibody screen and ABO/Rh entry through the instrument interface apply the same specimen-expiration refusal as manual result entry.

## Iteration 322 — Retrospective crossmatch on an expired pre-transfusion specimen, as an exception (OCD-050) (2026-11-02)

### Decision

The product owner decided OCD-050: a retrospective crossmatch after emergency release may be performed on the pre-transfusion sample after it expires, with an exception, because this is common in many labs.

### Design

- **What qualifies:**
  - the unit has an open retrospective crossmatch for the patient (emergency or MTP issue with tests incomplete, not returned, not yet completed)
  - the specimen was collected at or before the issue
  - anything else on an expired specimen stays hard stop `XM-SPEC-EXPIRED`
- **The exception uses the existing override path:**
  - warning `XM-SPEC-EXPIRED-RETRO`, overridden with a reason and the authorizer (`OverrideFieldsRule`)
  - access checked against an `ExceptionDefinition` (seeded at security level 0 so the tech on shift can record it; editable in the exception catalog)
  - an `Override` row with context Crossmatch, matched to the provider and user directory on save like other overrides
  - an Override audit event naming the specimen, the reason, and the authorizer
  - the override is unreviewed, so it appears in supervisor review
- The crossmatch then closes the retrospective worklist item exactly as before; an incompatible result still raises its alert.
- **Compatibility page:** when the warning comes back, the crossmatch form shows the exception reason and authorizer fields with a plain explanation.
- No schema change: two optional request fields and one seeded exception definition.

### Requirements / risk

- URS-BB-469, FRS-BB-505, RISK-BB-568, OCD-050 decided. `safety-rules.md` gains `XM-SPEC-EXPIRED-RETRO`. Help: compatibility page.

### Tests

- TEST-BB-364:
  - rule: warning on the pre-transfusion specimen; pass when in date; collected at or before issue counts as pre-transfusion
  - integration:
    - first attempt returns only the overridable warning and saves nothing
    - a reason without an authorizer is asked for again
    - with both, the crossmatch is saved, the override is recorded against it and unreviewed, the issue's retrospective crossmatch is set, and the audit event is written
    - a specimen collected after issue still hard-stops
    - without an active exception definition, the exception is refused
- Full suite: 3,479 passed, 0 failed (previously 3,474).
- Run check (development API): the seeder adds the `XM-SPEC-EXPIRED-RETRO` exception definition (active, level 0); no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: check whether ABO/Rh and antibody screen results arriving from instruments are refused on an expired specimen, as manual entry is.

## Iteration 323 — Instrument and interface results check the specimen on every branch (2026-11-02)

### Capability matrix rescan

No change since Cycle 322 other than the interoperability row (instrument interfaces).

### Finding (question 6)

The residual from Cycle 322 asked whether instrument results are refused on an expired specimen as manual entry is.

- Manual entry (`SaveTestResultAsync`) checks the specimen before every branch.
- `EnterFromExternalAsync`, used by the ASTM instrument processor and the HL7 inbound processor, checked it only when creating a new result.
- Two branches skipped the check:
  - an instrument re-sending a result that is still pending (update in place)
  - a result arriving after the earlier one was invalidated (replacement)
- Either could post a value to an expired, rejected, or cancelled specimen, or one belonging to a merged-away patient, which manual entry refuses.

### Design

- `EnterFromExternalAsync` runs `ValidateSpecimenForEntryAsync` before choosing a branch, exactly as manual entry does; the new-result branch no longer repeats it.
- A refusal returns the same message as manual entry. The inbound processors already record a failed filing on the message log, and the stored result is unchanged.
- No schema, API, or screen change.

### Requirements / risk

- URS-BB-470, FRS-BB-506, RISK-BB-569. Help: instruments page.

### Tests

- TEST-BB-365: an instrument result files as pending; the specimen then expires; the instrument re-sends a different value. It is refused ("has expired") and the stored value is still the first one. The test fails against the previous code.
- Full suite: 3,480 passed, 0 failed (previously 3,479).
- Run check: not run. Result service only; development data has no live instrument feed.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: check that a verified result can still be corrected after its specimen expires (corrections should remain possible, as they fix the record rather than test the sample).

## Iteration 324 — Corrections can be completed after the specimen expires (2026-11-02)

### Capability matrix rescan

No change since Cycle 323 other than the testing row (corrected and invalidated results).

### Finding (question 6)

The residual from Cycle 323 asked whether a verified result can still be corrected after its specimen expires.

- `CorrectResultAsync` did not check the specimen, so the correction was saved.
- Verifying it ran the full specimen gate and was refused ("Specimen … has expired", or "is Expired; only Accepted specimens allow result entry" once the sweep marked it), so the correction stayed pending for good.
- Correcting an ABO/Rh retires the type derived from the original at once (Cycle 312), so the patient was also left without that type until a verification that could never happen.
- The worklist save path refused the correction outright, because it gates the specimen before knowing the request is a correction.

### Design

- `ValidateSpecimenForEntryAsync(allowExpired)` skips only the expiration date and accepts status Expired. Rejected and cancelled specimens and merged-away patients are still refused.
- **Verify:** `allowExpired` only for a result with status Corrected.
- **Worklist save:**
  - with a correction reason, the save may pass the gate with `allowExpired`
  - it then proceeds only when the current result is Verified (new correction) or Corrected (editing a pending correction)
  - otherwise it returns the original expiration message
- Nothing else changes. First results, re-entry after invalidation, and interface results keep the full check. Corrections keep their reason, signature, second-person verification, and audit.
- Recorded as OCD-051 for SME confirmation, including whether a correction that changes interpretation should need a retest.

### Requirements / risk

- URS-BB-471, FRS-BB-507, RISK-BB-570, OCD-051. Help: test worklist page.

### Tests

- TEST-BB-366 (own database):
  - a verified ABO/Rh is corrected after the specimen expires and is marked Expired, and the correction is verified; this fails against the previous code
  - a pending first result is still not verified on an expired specimen
  - a correction on a specimen later rejected is still refused at verification
- The worklist save branch is covered by the code path shared with verification, not by its own test.
- Full suite: 3,483 passed, 0 failed (previously 3,480).
- Run check: not run. Result service only, and development data has no verified result on an expired specimen to correct.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. OCD-051 was confirmed by the product owner the same day ("keep it"). Next engineering candidate: an integration test for the worklist correction path on an expired specimen, then review invalidation on an expired specimen the same way.

## Iteration 325 — Worklist correction and invalidation after specimen expiry, covered by tests (2026-11-02)

### Capability matrix rescan

No change since Cycle 324.

### Finding

The residual from Cycle 324 asked for two things:

- a test of the worklist correction path on an expired specimen
- a review of invalidation the same way

Findings:

- `InvalidateResultAsync` does not run the specimen gate, so a verified result can be invalidated after its specimen expires. That is correct, because invalidation withdraws a wrong value; no change needed.
- The worklist correction path had no test of its own. Cycle 324 changed it so a correction reason opens the gate only when the save is a correction, and that guard needed a failure-path test.

### Design

Tests only; no code change.

### Tests

- TEST-BB-367:
  - a worklist correction of a verified result after the specimen expired (and was marked Expired) creates the Corrected version
  - a correction reason sent with a first result on an expired specimen does not let it post; nothing is saved
  - a verified ABO/Rh can be invalidated after its specimen expires
- Full suite: 3,486 passed, 0 failed (previously 3,483).

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: a result entered while the specimen was in date but not verified before it expired can no longer be verified (TEST-BB-366 pins this). Show pending-verification results whose specimen expires soon at the top of the worklist, so they are verified in time.

## Iteration 326 — Verify before the specimen expires (2026-11-02)

### Capability matrix rescan

No change since Cycle 325 other than the operations row (unreviewed results).

### Finding (questions 2 and 5)

The residual from Cycle 325:

- A result entered while the specimen was in date, but not verified before it expired, can no longer be verified.
- The worklist ordered rows by priority and order time only, and the supervisor queue by title, so nothing told the verifier that time was running out. The tech had to remember the specimen's expiration.

Also found: the worklist's own entry gate still blocked a pending correction on an expired specimen ("has expired"), although since Cycle 324 the server verifies it. The screen and the server disagreed.

### Design

- `VerificationDeadlineRule.Notice`:
  - applies to Entered or Pending verification results whose specimen expires within the facility near-expiry window (`NearExpiryWarningHours`, default 24; an existing setting, no new one)
  - text: "Verify before … UTC, when the specimen expires. After that this result cannot be verified."
- **Worklist:**
  - rush priorities first, then deadline rows (earliest expiration first), then the rest as before
  - a "Verify before HH:mm" badge under the specimen expiration
- **Supervisor queue:** the notice is added to the result-verify detail, and those items come first within result verification.
- `VerificationDeadlineRule.AllowsExpiredSpecimen` (Corrected only) is used by the worklist entry gate, matching `ResultService` (OCD-051).
- Display only. Nothing is stored, so nothing to audit; the server verification gate is unchanged.

### Requirements / risk

- URS-BB-472, FRS-BB-508, RISK-BB-571. Help: test worklist page.

### Tests

- TEST-BB-368:
  - rule: deadline inside the window for Entered and Pending verification; none outside it, after expiry, or without an expiration; none for corrections or finished results; only Corrected allows an expired specimen
  - integration: a pending result whose specimen expires in two hours is listed before a routine row without a deadline and carries the notice; a pending correction on an expired, Expired-status specimen can be worked from the worklist
- Full suite: 3,495 passed, 0 failed (previously 3,486).
- Run check (development API and web): the test worklist (7 rows) and supervisor queue (6 items) render with no server errors. No demo result is pending on a specimen expiring within 24 hours, so no badge shows there.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the dashboard's pending-tests card could say how many results must be verified before their specimen expires, as the Ready to issue card does for expired crossmatches.

## Iteration 327 — Dashboard names results to verify before their specimen expires (2026-11-02)

### Capability matrix rescan

No change since Cycle 326 other than the operations row (unreviewed results).

### Finding (question 5)

Cycle 326 put the verification deadline on the worklist and supervisor queue, but the dashboard Pending tests card counted all pending rows alike.

### Design

- `PendingTestAttentionRule.CountVerifyBeforeExpiry` counts rows that carry a `VerificationDeadlineNotice`.
- The card uses the existing `AttentionNote`, the same form as the Ready to issue and retrospective cards.
- It reuses the list the card already loads. Read only.

### Requirements / risk

- URS-BB-473, FRS-BB-509, RISK-BB-572. Help: dashboard page.

### Tests

- TEST-BB-369: counts only rows with a deadline; singular and plural wording; zero when none.
- Full suite: 3,496 passed, 0 failed (previously 3,495).
- Run check (development API and web): the dashboard shows Pending tests 7 with no note (no demo result is near its specimen deadline); no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the shift handoff packet could carry the same count of results to verify before specimen expiry, so the next shift sees them.

## Iteration 328 — Shift handoff counts results to verify before specimen expiry (2026-11-02)

### Capability matrix rescan

No change since Cycle 327 other than the operations row (shift handoff).

### Finding (question 5)

The handoff packet counts pending tests but not those whose specimen expires soon. A result left for the next shift could expire unverified, and the outgoing tech had to remember to write it in the note.

### Design

- The new `ShiftHandoff.PendingTestsVerifyBeforeExpiry` column is filled at record time with `PendingTestAttentionRule.CountVerifyBeforeExpiry`, over the same pending worklist the packet already loads.
- It is a snapshot like the other counts: in the audit snapshot, the DTO, and the handoff list ("tests N, M to verify before specimen expiry").
- Schema:
  - migration `20261105000000_ShiftHandoffVerifyBeforeExpiry`
  - development SQLite bootstrap column
  - older packets read 0

### Requirements / risk

- URS-BB-474, FRS-BB-510, SRS-BB-447, RISK-BB-573. Help: shift handoff page.

### Tests

- TEST-BB-370: a handoff recorded while a pending result's specimen expires in two hours stores a count of at least one, no more than the pending tests, and carries it on the DTO.
- Full suite: 3,497 passed, 0 failed (previously 3,496).
- Run check (development API and web):
  - the API started, and the bootstrap added the column
  - `GET /api/shift-handoffs/` returns 200 (no packets in development data)
  - the Shift handoff page loads; no server errors
  - no handoff was recorded against development data
- SQL Server migration not exercised locally.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: on the patient chart's Tests tab, show the same verify-before deadline on pending results.

## Iteration 329 — Verify-before deadline on the chart Tests tab (2026-11-02)

### Capability matrix rescan

No change since Cycle 328.

### Finding (question 5)

The chart Tests tab uses the same worklist rows as the test worklist, so the deadline was already in its data, but the tab did not show it. A tech working from the chart did not see it.

### Design

- The badge from Cycle 326 is now a shared component, `VerificationDeadlineBadge`, used on both the test worklist and the chart Tests tab, so the two cannot drift.
- Display only; no API or schema change.

### Requirements / risk

- URS-BB-475, FRS-BB-511 (under RISK-BB-571). Help: patient chart page.

### Tests

- TEST-BB-371: the patient Tests list carries the deadline when the specimen expires in five hours, and none at three days.
- No component test project exists, so the badge markup itself is covered by the run check.
- Full suite: 3,498 passed, 0 failed (previously 3,497).
- Run check (development API and web): the demo patient's Tests tab (2 rows) and the test worklist (7 rows) render with no server errors. No demo result is near its specimen deadline, so no badge shows.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. The specimen-expiration thread (Cycles 317–329) is complete. Next: rescan the capability matrix for the next highest-impact gap outside specimen and reservation expiry.

## Iteration 330 — The chart's "Second ABO on file" matches issue (2026-11-02)

### Capability matrix rescan

- Every row is Present except three Partial rows, each blocked on a decision or a license:
  - ISBT identifiers (OCD-004)
  - product modifications (eluate history)
  - HL7 (downtime back-entry)
- Rather than add a feature, this cycle checked that the rule changes of Cycles 311–314 reached every place that applies them.

### Finding (question 6)

- Every server path builds determinations with `SecondAboDeterminationRule.FromHistory`: issue, ready to issue, the Second ABO/Rh desk, electronic crossmatch eligibility, and allocation.
- The patient chart header built its own list from the blood type DTOs and dropped two things:
  - the specimen (two types from one specimen are one determination, Cycle 311)
  - whether the row counts (an undocumented manual entry does not, Cycle 313)
- So the chart said "Second ABO on file", and hid the missing-second-ABO notice, in exactly the cases issue refuses red cells. The tech could conclude no second specimen was needed.

### Design

- `BloodTypeDto` carries `SpecimenId`.
- `BloodTypeDto.Determinations` gives one mapping from DTO rows, which mirrors `FromHistory`.
- The chart header uses it for both the cue and the notice.
- The server is unchanged and stays authoritative.

### Requirements / risk

- URS-BB-476, FRS-BB-512, RISK-BB-574.

### Tests

- TEST-BB-372 is a parity test. For each case the server answer is asserted, and the chart mapping must give the same answer.
  - Cases: two specimens, same specimen twice, undocumented manual, documented outside source, second type invalidated, discordant, only one type.
  - The previous header mapping gave "on file" for the same-specimen and undocumented-manual cases.
- The DTO carries the specimen.
- Full suite: 3,506 passed, 0 failed (previously 3,498).
- Run check (development API and web):
  - `GET /api/patients/1/blood-type/history` returns `specimenId`
  - the demo patient (historical import plus test result, both counting) still shows "Second ABO on file", matching issue; no server errors

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the same parity check for the chart's ABO/Rh discrepancy banner (it maps types itself rather than through the server's rule input).

## Iteration 331 — The chart's discrepancy banner pinned to the issue gate (2026-11-02)

### Capability matrix rescan

No change since Cycle 330.

### Finding

The residual from Cycle 330 asked whether the chart's ABO/Rh discrepancy banner also builds its own input.

- It does, but today the inputs agree. The chart and both issue paths leave out invalidated types and compare the current type with the others.
- No live defect, but the second-ABO cue drifted exactly this way, so the duplication is a risk.
- An undocumented manual entry that disagrees with the current type does raise the discrepancy on both sides. That is the cautious reading: a disagreeing type on record is held for resolution even when it cannot count as a determination. It is unchanged.

### Design

- `IssueGate.DiscrepancyInput` (from stored history) replaces the two inline mappings in `IssuingService`.
- `BloodTypeDto.DiscrepancyInput` replaces the chart header's inline mapping.
- No behavior change.

### Tests

- TEST-BB-373 is a parity test over five cases: agreeing, disagreeing, disagreeing type invalidated, undocumented manual entry disagreeing, no current type. The server, the chart, and the banner text agree.
- Full suite: 3,511 passed, 0 failed (previously 3,506).
- Run check: not run, because behavior is unchanged; the chart header was checked live in Cycle 330.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: look for other Web components that re-derive a server rule from DTOs (for example ready-to-issue form cues and electronic crossmatch display), and give each a parity test or a server-computed field.

## Iteration 332 — The unit chart's return choice follows the server (2026-11-02)

### Capability matrix rescan

No change since Cycle 331.

### Finding (questions 3 and 6)

The residual from Cycle 331 asked for other screens that re-derive a server rule.

- Most web calls into domain rules are display helpers, or counts over rows the server computed.
- One was a decision: the unit chart's Change status choice for an issued unit offered Available using `ReturnReissueRule.DefaultReturnToAvailableMinutes` (30) and the current time. It ignored two things:
  - the facility `ReturnToAvailableMinutes`
  - storage locations exempt from the window
- The outstanding-issue row the page already loads carries the server's decision (`StorageTimeOutAcceptable`), and the page ignored it.
- Effects:
  - with a longer facility window, or an exempt location, the page offered only Quarantine, so units that could return to stock were quarantined
  - with a shorter window, it offered Available and the return was then refused
- The server return gate was correct throughout.

### Design

- The page uses the row's `StorageTimeOutAcceptable`. Without an outstanding row it offers Quarantine only (fail safe).
- The unused issued-time field was removed. No API or schema change.

### Requirements / risk

- URS-BB-477, FRS-BB-514, RISK-BB-575. Help: unit detail page.

### Tests

- TEST-BB-374: a unit issued 40 minutes ago to a governed location is not acceptable under the default 30-minute window, and becomes acceptable when the facility window is 60 minutes. The test removes its setting afterwards; the class shares one database.
- Existing tests already cover the exempt location and the elapsed window on the same row.
- Full suite: 3,512 passed, 0 failed (previously 3,511).
- Run check (development API and web):
  - every outstanding demo issue is governed with the window elapsed
  - unit W0001230009101's Actions tab offers Quarantine only, matching the server
  - no server errors
  - no return was made

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: continue the screen-versus-server audit with `SpecimenExpirationOverrideRule.Evaluate` and `UnitModificationEligibilityRule` uses in the web project.

## Iteration 333 — Chart specimen edit uses the server's policy expiration (2026-11-02)

### Capability matrix rescan

No change since Cycle 332.

### Finding (questions 3, 6 and 7)

Continuing the screen-versus-server audit.

- **Unit modification eligibility:** the web uses `OfferModification` and the pool checks only to show or hide choices, and the server re-evaluates every modification in full. No finding.
- **Specimen expiration override:**
  - The server's policy expiration is `ComputeExpiresUtcAsync`. It shortens the window for a patient at alloimmunization risk (transfused or pregnant within the lookback) to `SpecimenAlloHours`, default 72.
  - The chart's edit form computed "policy" from the specimen-type catalog only.
  - For an at-risk patient, changing the collected time refilled the expiration with the longer catalog value, and the chart asked for an override.
  - A supervisor completing that override extended the specimen beyond the risk window. A new alloantibody could then be missed at crossmatch.
  - Conversely, a routine edit could demand an override the server did not need.
- **Authorizer default:** the override's Authorized by defaulted to the signed-in user when left blank, contrary to OCD-041.

### Design

- `SpecimenService.PreviewExpirationAsync` and an authenticated endpoint return the policy expiration from the same `ComputeExpiresUtcAsync`, and say whether the allo-risk window shortened it.
- The chart uses it when the edit form opens and when the collected time changes. The catalog preview is only a fallback if the call fails.
- The expiration override now requires the authorizer to be named. The page asks for it, and the Me button fills it on request.
- The server override gate is unchanged: reason, authorizer, security level, and audit.

### Requirements / risk

- URS-BB-478, FRS-BB-515, RISK-BB-576. Help: patient specimens page.

### Tests

- TEST-BB-375:
  - for a patient with a recent pregnancy, the preview is collection + 72 h and flagged as shortened
  - saving that expiration needs no override, while saving the catalog window does
  - without risk, the preview is the catalog window
  - an unknown patient fails
- Full suite: 3,514 passed, 0 failed (previously 3,512).
- Run check (development API and web):
  - the preview for the demo patient (transfused) returns collection + 72 h, shortened
  - opening the edit form on ACC0001 loads without server errors (nothing saved)
  - the form asks for an override because the demo seed stored expiration = collection + 74 h, two hours past policy; the server agrees
  - the seed quirk is pre-existing and left as is (development data not rewritten)

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidates:

1. The accession form's expiration preview has the same catalog-only gap. It is display only, because the server computes at accession, but it is misleading for at-risk patients.
2. The demo seeder should store specimen expiration from its own collection time, so the demo does not invite overrides.

## Iteration 334 — Accession expiration preview matches accession (2026-11-02)

### Capability matrix rescan

No change since Cycle 333.

### Finding (question 2)

The residual from Cycle 333: the accession form's "Expires (preview)" used the specimen-type catalog window. For a patient transfused or pregnant recently, it showed a later expiration than accession would save. The tech saw one date on screen and another on the label and specimen record. Display only (the server computes the stored value), but misleading.

### Design

- The preview comes from the Cycle 333 server preview, which now also takes the optional validity hours the accession will use. Those hours are still capped by the risk window.
- It refreshes when the specimen type, collection time, or validity hours change, and only calls when one of them changed.
- When the window was shortened, the form says "Shorter window: patient transfused or pregnant recently."
- The catalog value shows only while the call loads or if it fails.

### Requirements / risk

- FRS-BB-516 (under URS-BB-478 and RISK-BB-576). Help: patient specimens page.

### Tests

- TEST-BB-376: for an at-risk patient, the preview with 168 validity hours equals the expiration accession then stores, and is flagged as shortened.
- Full suite: 3,515 passed, 0 failed (previously 3,514).
- Run check (development API and web): on the demo patient (transfused), the accession form shows collection + 72 h with the shorter-window note. The form was cancelled and nothing was accessioned; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the demo seeder should store specimen expiration from its own collection time and the patient's risk, so the demo does not invite unnecessary overrides.

## Iteration 335 — Demo specimens seeded with the policy expiration (2026-11-02)

### Capability matrix rescan

No change since Cycle 334.

### Finding

The residual from Cycle 333:

- The demo patient's three specimens (ACC0001, ACC-PEND-COL, ACC-PEND-RCV) were seeded with expiration = seed time + 3 days, but collected 1–2 hours before that.
- The demo patient is transfused, so policy is collection + 72 h. The stored value was 1–2 hours past policy.
- Opening any of them for an edit therefore asked for an expiration override. That is correct behavior for the data, but it trains demo users to override.

### Design

- The seeder computes those three expirations with `SpecimenValidityPolicy.ComputeExpiresUtc(collected, alloimmunizationRisk: true)`, as another demo scenario already does.
- Seed data only. An existing development database keeps its rows (the seeder does not rewrite them); a freshly seeded database gets the corrected values.

### Tests

- TEST-BB-377: after seeding, each of the three specimens' expiration equals `SpecimenService.PreviewExpirationAsync` for that patient. The test fails against the previous seeder.
- Full suite: 3,516 passed, 0 failed (previously 3,515).
- Run check: not run. The existing development database is not reseeded, so nothing would change there.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: rescan for other overrides whose authorizer is defaulted to the signed-in user (OCD-041), as the specimen expiration override was until Cycle 333.

## Iteration 336 — Override authorizers audited across every path (OCD-041) (2026-11-03)

### Capability matrix rescan

No change since Cycle 335.

### Finding (question 7)

The residual from Cycle 335: are any other overrides defaulting Authorized by to the signed-in user, as the chart specimen expiration override did until Cycle 333?

- **Web:**
  - no other page fills the authorizer
  - `AuthorizerField`'s Me button fills it only when pressed
  - every second-verifier list excludes the signed-in user
- **Server:**
  - every override path requires both reason and authorizer: issue, allocation, crossmatch exception, order, result, specimen expiration
  - none substitutes the current user
  - directory matching normalizes whitespace
- **One inconsistency:** the issue override (emergency release, MTP, warning overrides) stored the reason and authorizer exactly as typed, padding included, while every other override trims them.

### Design

- The issue override trims both fields on write, like the others.
- No schema or screen change. Older rows are read through DTOs that already trim.

### Tests

- TEST-BB-378: an emergency release with a blank authorizer is refused with `OVR-FIELDS`. With padded fields it is issued, and the override stores "Hemorrhage" and "dr-authorizer".
- Full suite: 3,517 passed, 0 failed (previously 3,516).
- Run check: not run (server write-path normalization only).

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next: rescan the capability matrix rows for operations and administration, which have had the least attention in recent cycles.

## Iteration 337 — Near-expiry units reserved for a patient say so (2026-11-03)

### Capability matrix rescan

- Operations and administration rows: Present; no change since Cycle 336.
- The application was renamed HemoCore in commit c99c68e, outside this loop. No effect on this work.

### Finding (questions 2 and 5)

- The Near expiry board and the near-expiry alert showed unit, type, location, and expiry, but not whether a patient's reservation was on the unit.
- A reserved unit about to expire looked like any other. Staff learned of the reservation only after the sweep expired it and ended the reservation (Cycle 317), when the replacement was already late.

### Design

- `ListNearExpiryAsync` adds the patient on each unit's Reserved allocation (the latest, if several) as id, MRN, and name, with a "Name (MRN)" label.
- **Board:** "Reserved for …" under the unit number, linked to the patient's Products tab, plus a Reserved badge with the next step: "Issue it first or reserve a replacement."
- **Alert:** for a reserved unit it is a Warning (Info otherwise), and the message names the patient and the next step.
- Read only, so nothing to audit. No schema change.

### Requirements / risk

- URS-BB-479, FRS-BB-518, RISK-BB-577. Help: inventory and alerts pages.

### Tests

- TEST-BB-379:
  - a crossmatched unit expiring in 3 hours with a Reserved allocation names "Nearby, Rosa (MRN-NEAR-RES)"
  - a unit whose allocation was released is not marked reserved
  - the scan raises a Warning near-expiry alert whose message names the patient and the next step
- Full suite: 3,519 passed, 0 failed (previously 3,517).
- Run check (development API and web): the near-expiry endpoint returns 200 and an empty list (no development unit is inside the window), so the board shows nothing to mark; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the dashboard's near-expiry or inventory card could count reserved units near expiry, as the Ready to issue card counts expired crossmatches.

## Iteration 338 — The dashboard counts reserved units expiring soon (2026-11-03)

### Capability matrix rescan

No change since Cycle 337.

### Finding (question 5)

- Cycle 337 named the patient on a near-expiry reserved unit on the Near expiry board and in the alert.
- The dashboard has no near-expiry card, but reserved units are exactly what the Ready to issue card counts, and each of its rows already carries `ExpiresSoon`.
- Its note said only how many need a new crossmatch.

### Design

- `ReadyToIssueAttentionRule.CardNote` joins two notes with " · ":
  - "N need(s) a new crossmatch"
  - "N expire(s) soon": reserved units inside the near-expiry window and not already outdated
- It uses the existing `DashboardQueueCatalog.AttentionNote`.
- Read only; no new call.

### Requirements / risk

- FRS-BB-519 (under URS-BB-479 and RISK-BB-577). Help: dashboard page.

### Tests

- TEST-BB-380: no note when neither applies; "1 expires soon"; "1 needs a new crossmatch · 2 expire soon"; an outdated unit is not counted as expiring soon.
- Full suite: 3,520 passed, 0 failed (previously 3,519).
- Run check (development API and web): the dashboard shows Ready to issue 3 with no note (no demo reservation expires soon or has an expired crossmatch); no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next: rescan administration (configuration screens, roles, audit settings) for gaps, which recent cycles have not touched.

## Iteration 339 — Override exception changes record why (2026-11-03)

### Capability matrix rescan

The administration row was rescanned:

- Every admin configuration service records before-and-after configuration history through `ConfigAdminServiceBase.RecordChange`.
- Facility policy and compatibility rule edits require a reason.

### Finding (question 7)

The exception catalog decides who may override each safety rule (minimum security level) and whether it can be overridden at all. Its edits and activation changes were recorded with no reason. An administrator could lower the level for antigen-negative overrides, or switch an exception off, with nothing saying why. The admin help page already told users "The reason you type is the change-control note", but the page never asked for one.

### Design

- `ExceptionCatalogChangeReasonRule`: a reason of at least 8 characters, as for facility policy; hard stop `EXC.REASON.REQUIRED`.
- **Required** on update and on activate/deactivate, checked before anything changes. **Optional** on create (recorded when given).
- The reason is stored with the configuration history and audit event.
- The activate and deactivate endpoints take `{ "reason": … }`.
- **Admin page:** a "Reason for the change" field on the edit form, and an inline "Why activate/deactivate …?" with the confirm button disabled until a reason is typed.

### Requirements / risk

- URS-BB-480, FRS-BB-520, RISK-BB-578. Help: admin exceptions page.

### Tests

- TEST-BB-381:
  - rule: missing, blank, or under 8 characters after trimming is refused; 8 passes
  - service: update without a reason is refused and leaves the level unchanged; with a reason it succeeds and the audit event carries it; deactivation with a short reason is refused and the exception stays active; the authorization test now gives a reason
- Full suite: 3,526 passed, 0 failed (previously 3,520).
- Run check (development API and web): on Admin › Exceptions (9 rows), Deactivate opens "Why deactivate ALLOC-XM-AB-HISTORY?" with Deactivate disabled until a reason is entered. Cancelled; nothing changed; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: check the other safety-relevant admin catalogs (special requirements, antigen and antibody configuration, crossmatch settings) for the same missing-reason gap.

## Iteration 340 — Changes to the active compatibility table record why (2026-11-03)

### Capability matrix rescan

The other safety-relevant admin catalogs were rescanned:

- Crossmatch settings require a reason.
- Special requirements and blood attributes require one when the definition is active.
- Compatibility table versions require one to activate or retire.

### Finding (question 7)

- Rules inside a compatibility table version could be added, edited (for example severity Hard stop to Warning), or switched off with no reason, including in the active, approved table.
- The domain marks that table MEDICAL_DIRECTOR_APPROVAL configuration.
- The rows are reference configuration, not evaluated at issue, but the approved table could still change with nothing saying why.
- The admin help page said "The issue gate reads the active version". It does not: the gate reads each product's ABO/Rh matrix and the built-in rules. The help page is corrected, and whether the table should drive the gate is recorded as OCD-052.
- There is no "copy version" function, so freezing the active table would force re-entering every row. A reason requirement follows the pattern the special requirement and blood attribute catalogs already use.

### Design

- `CompatibilityTableChangeReasonRule`: when the rule's version is active, a reason of at least 8 characters is required; draft versions need none. Hard stop `COMPAT.REASON.REQUIRED`.
- Applied to rule create, update, activate, and deactivate, before anything changes. The reason is stored with the configuration history.
- The rule activate and deactivate endpoints take `{ "reason": … }`.
- **Admin page:** a "Reason for changing the active table" field appears above the rules when the active version is selected.

### Requirements / risk

- URS-BB-481, FRS-BB-521, RISK-BB-579, OCD-052. Help: compatibility tables page (reason, and the corrected statement of what the issue gate reads).

### Tests

- TEST-BB-382:
  - rule: in an active table, a missing, blank, or 7-character reason is refused; 8 passes; a draft table needs none
  - service:
    - a rule created in a draft table needs no reason
    - after the table is activated, softening the rule without a reason is refused and the severity stays Hard stop
    - deactivating it or adding another rule without a reason is refused
    - with a reason the change succeeds and the history row carries it
- Full suite: 3,531 passed, 0 failed (previously 3,526).
- Run check (development API and web): Admin › Compatibility Tables opens on the active table (PLACEHOLDER-1, 16 rules) and shows the reason field above its rules; nothing changed; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, OCD-052, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: users and roles. Check that granting a role, changing a role's permissions or security level, and unlocking a user are audited with a reason, since those decide who may override.

## Iteration 341 — The issue gate reads the active compatibility table, stricter only (OCD-052) (2026-11-03)

### Decision

The product owner decided OCD-052: make the issue gate read the compatibility tables.

### Design

- **What a row means:** the table rows hold a rule code (optionally with a component suffix), a component class, a severity, and an active flag. At issue, a failing gate result is matched to an active row of the active version for the unit's component class, by code or code plus suffix.
- **Stricter only:**
  - A matching HardStop row turns a gate Warning into a hard stop, with " The active compatibility table makes this a hard stop." appended to the message.
  - A hard stop is never softened.
  - A missing or deactivated row leaves the built-in result.
  - Softening ABO or Rh through configuration would be a new clinical rule, which this loop does not invent.
- **Escalation only above the built-in default:** the seeded table copies `CompatibilityRuleCatalog.Defaults`, and some defaults read HardStop for rules the gate lets a supervisor override (antigen-negative, antibody-history crossmatch). Escalating on those would remove established override workflows at go-live. So a row escalates only where it is stricter than its default, and the seeded table changes nothing.
- **Admin save:** a row softer than its built-in default is refused (`COMPAT.SEVERITY.SOFTER`), so the table never says something the gate will not do. The existing validator test that used ABO as a Warning now uses HardStop.
- **Scope:** applied in `IssuingService` to the issue gate. Allocation and ready-to-issue cues are unchanged and still use the built-in rules.
- **Escalation reaches emergency issue too.** A row that makes emergency uncrossmatched-ABO a hard stop blocks non-O emergency release. Changing an active row needs a reason (Cycle 340).

### Requirements / risk

- URS-BB-482, FRS-BB-522, RISK-BB-580, OCD-052 decided. Help: compatibility tables page rewritten for the new behavior.

### Tests

- TEST-BB-383:
  - a `.PLT` HardStop row raises a platelet ABO warning
  - the full seeded table leaves an antigen-negative warning a warning
  - a Warning row does not soften the ABO hard stop
  - an inactive row, another component class, a non-matching code, a Pass result, and an empty table change nothing
  - softer-than-default rows are refused at save
  - integration: with an active table row raising `UNIT-NEAR-EXPIRY` for red cells, issuing a unit that expires in 3 hours is refused with the escalated hard stop, and the unit is not issued; a row trying to soften ABO is present and ignored
- Full-solution build and full suite (after the user closed their running HemoCore instance): 3,542 passed, 0 failed (previously 3,531), Help.Tests included.
- Run check (development API): `PUT /api/admin/compatibility-rules/2`, trying to set the active table's `ISS-ABO-COMPAT` row to Warning with a reason, returned 422 `COMPAT.SEVERITY.SOFTER` with the plain message; the row is still HardStop; no server errors. No unit was issued, to avoid writing development data.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidates:

1. Show on the admin compatibility page which rows currently raise a rule above its built-in default.
2. Users and roles audit (carried from Cycle 340).

## Iteration 342 — The admin compatibility table says what each row does at issue (2026-11-03)

### Capability matrix rescan

No change since Cycle 341.

### Finding (question 2)

After Cycle 341 the active table's rows change issue behavior only when stricter than their built-in default, and only for codes the gate reports. An administrator had to work that out from the catalog and the severity column. The table did not say which rows do anything.

### Design

`CompatibilityTablePolicy.DescribeAtIssue` makes the same decision `Apply` makes, and the admin table shows it as an **At issue** column:

| Label | When |
| --- | --- |
| Raises to hard stop | A built-in rule made stricter (red badge). |
| Hard stop if the gate reports this code | A HardStop row for a code that is not a built-in issue rule (amber badge). It applies only if a gate rule reports that code. |
| Same as built-in | The row matches the built-in default. |
| No effect at issue | A non-HardStop row for a code the catalog does not know. |
| Off – built-in rule applies | The row is deactivated. |

The first version of the column labeled the seeded placeholder row `RBC-ABO-BASE` (HardStop, a code no gate rule reports) as "Raises to hard stop". The run check caught it before commit, and that is why the "if the gate reports this code" label exists.

### Requirements / risk

- FRS-BB-523 (under URS-BB-482 and RISK-BB-580). Help: compatibility tables page.

### Tests

- TEST-BB-384: one case per label, including the placeholder code and the seeded antigen-negative row (Same as built-in).
- Full suite: 3,549 passed, 0 failed (previously 3,542).
- Run check (development API and web): the active table shows 15 "Same as built-in" and 1 "Hard stop if the gate reports this code" (`RBC-ABO-BASE`). No row of the seeded development table changes issue behavior; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: users and roles audit. Check that granting a role, changing a role's permissions or security level, and unlocking a user are audited with a reason.

## Iteration 343 — Access changes record why (2026-11-03)

### Capability matrix rescan

The administration row was rescanned for users, roles, and permissions.

### Finding (question 7)

- `UserAdminService` required a reason only when a new user was given an administrative role, or a user was newly granted one. Everything else recorded a reason only if one happened to be typed:
  - changing a role's permission codes or security level, which decides who may override safety rules (Cycle 339)
  - giving a user a non-administrative role such as Supervisor, or removing one
  - activating or deactivating an account
  - unlocking an account
- Activation and lock changes recorded only the state after, not before.
- Two help pages were stale. users.md said a role change takes effect only when the person signs in again, and roles.md said the same for role edits. The code ends those sessions immediately.

### Design

- `AccessChangeReasonRule`: a reason of at least 8 characters, `ACCESS.REASON.REQUIRED`, checked before anything changes.
- Required for:
  - any change to a user's role set (resending the same set needs none)
  - any change to a role's permissions or security level (a description-only edit needs none)
  - activating or deactivating an account
  - unlocking an account
- Locking stays reason-optional, because it protects.
- Activation and lock changes now record the before state.
- The roles page label now says when the reason is required. Both help pages were corrected.

### Requirements / risk

- URS-BB-483, FRS-BB-524, RISK-BB-581. Help: users and roles pages.

### Tests

- TEST-BB-385:
  - roles: a change needs a reason, and the same set again does not
  - deactivation: a blank reason is refused and the user stays active; with a reason, the history row has the before state
  - locking: allowed without a reason; unlocking with a 2-character reason is refused; with a real reason it succeeds
  - roles: a description-only edit needs no reason; a security-level change without one is refused and leaves the level; with one it succeeds
- Full suite: 3,551 passed, 0 failed (previously 3,549).
- Run check (development API): `PUT /api/admin/roles/4` raising ReadOnly's security level with no reason returned 400 "Give a reason of at least 8 characters for this access change."; the level stayed 0; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: other help pages may describe behavior that has since changed. Check each admin help page's specific behavioral claims against the code, as Cycles 339–343 found three that were wrong.

## Iteration 344 — Admin help claims checked against the code (2026-11-03)

### Capability matrix rescan

No change since Cycle 343.

### Why

Cycles 339–343 found four admin help statements that did not match the code:

- the exception reason was promised but never asked for
- the issue gate was said to read the compatibility table when it did not
- role changes were said to apply only at next sign-in, in both the users and roles help

This cycle extracted the behavioral claims from all 50 admin help pages ("cannot be", "requires a reason", "is refused", "is audited", and similar) and checked each one with safety or inventory consequences against the code path that should enforce it.

### Result

All checked claims hold; no code or help change was needed.

| Claim | Enforced by |
| --- | --- |
| A location or storage device that holds units cannot be deactivated | `InventoryLocationAdminService` (both admin pages use it) |
| No discard without an active reason code | `DiscardReasonRule` in `InventoryService.DiscardAsync` |
| No specimen rejection without an active code | `SpecimenRejectionReasonRule` whenever the catalog repository is configured, as in the app |
| No order cancellation without an active code | `OrderService`; expiry auto-cancel resolves catalog text |
| A withdrawn panel lot cannot be used on a new workup or completed | `AntibodyPanelLotValidityRule` at create, attach, and complete |
| A DRAFT transfusion criterion cannot be activated | `TransfusionIndicationRule.DraftCode` |
| FHIR reads are audited as patient access | `FhirReadService` |
| Facility policy edits need a reason | `FacilityPolicyValidator` |

Not checked this cycle (lower consequence, or display-only):

- actions "an action with no rule cannot be completed"
- tests "a test that is not here cannot be ordered"
- products and blood-attributes catalog-only statements

### Tests

No code change; the full suite stands at 3,551 passed (Cycle 343).

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the same claim-versus-code check for the clinical help chapter, starting with issuing, compatibility, and patient-specimens, where the claims are safety-critical.

## Iteration 345 — Clinical help claims checked: issuing, compatibility, patient specimens (2026-11-03)

### Capability matrix rescan

No change since Cycle 344.

### Method

The behavioral claims in the three most safety-critical clinical help pages (issuing, compatibility, patient specimens) were extracted. Those touching rules changed in recent cycles, or emergency behavior, were checked against the code.

### Results

- **Two statements made stale by OCD-051 (Cycle 324):**
  - patient-specimens.md said "Results cannot be verified … on a specimen past its testing window"
  - test-worklist.md said "Verify is refused when the specimen is rejected or expired"
  - Both are now corrected: a correction of an already verified result can be verified after the specimen expires.
- **Held:**
  - an established-group ABO conflict is refused even at emergency release (OCD-044)
  - a unit whose transfusion has started cannot be returned
  - a governed device's return window ships at 30 minutes and follows the facility setting (Cycle 332)
  - crossmatch on an expired specimen is refused (Cycles 321–322)
- **Found, recorded as OCD-053:**
  - the issue gate hard-stops every issue type, emergency and MTP included, when there is no current specimen or it has expired
  - a trauma patient who needs uncrossmatched group O red cells before a sample is drawn cannot be issued blood
  - the help describes this accurately, so it is a policy question, not a documentation error
  - behavior is unchanged pending a decision

### Tests

Documentation only; the full suite stands at 3,551 passed.

### Next ranked residual

Pending decisions: OCD-053 (new), downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: if OCD-053 is decided to allow pre-specimen emergency release, implement it with the controls chosen. Otherwise, continue the help claim check with the transfusion, returns, and alerts pages.

## Iteration 346 — Emergency release before a specimen is drawn, with controls (OCD-053) (2026-11-03)

### Decision

The product owner decided OCD-053: allow emergency or massive-transfusion release before a specimen is drawn, with controls.

### Design

- **When it applies:** an emergency or MTP issue where the patient has no current specimen, or only an expired one (`EmergencyPreSpecimenRule.Applies`). The gate's specimen checks (`ISS-SPEC-EXISTS`, `ISS-SPEC-PATIENT`, `SPEC-EXPIRED`, `SPEC-NEAR-EXPIRY`) are replaced by `EmergencyPreSpecimenRule`.
- **What it allows:**
  - group O red cells or whole blood, and group AB or A plasma: overridable warning `ISS-PRE-SPECIMEN`, whose message ends "Collect a pre-transfusion specimen now."
  - any other group, and any other component (platelets and cryoprecipitate included): hard stop with a plain message
- **Controls:**
  - The warning is accepted only through the existing emergency override: emergency-release permission, reason, and authorizer, recorded on the Override with the rule code and sent to supervisor review.
  - The other emergency rules still apply: unknown patient type is a warning; uncrossmatched group and Rh rules; an established ABO conflict is a hard stop (OCD-044).
- **Unchanged:**
  - A specimen that belongs to another patient is a wrong-patient signal and stays a hard stop.
  - Routine issue without a specimen is unchanged (hard stop).
- **Follow-up:** the retrospective crossmatch board shows "No specimen on file: collect one" (or "Specimen expired: collect a new one") with an Accession link. The retrospective crossmatch still has to be filed on a specimen.
- Help updated: issuing (including the stale "not offered for … emergency issue" line) and `safety-rules.md`.

### Requirements / risk

- URS-BB-484, FRS-BB-525, RISK-BB-582, OCD-053 decided.

### Tests

- TEST-BB-386:
  - rule: each component and group, and when it applies
  - gate:
    - emergency group O red cells without a specimen and with an unknown patient type has no hard stops and gives the overridable warning with the collect-now prompt
    - group A red cells is a hard stop
    - routine issue is still a hard stop
    - another patient's specimen is still a hard stop
    - an expired specimen uses the pre-specimen rule
  - integration: with the patient's only specimen expired, routine issue is refused with `SPEC-EXPIRED`. Emergency release without an override is blocked with only warnings. With a reason and authorizer it issues, and the Override names `ISS-PRE-SPECIMEN`.
- Full suite: 3,566 passed, 0 failed (previously 3,551), safety regression tests included.
- Run check (development API and web): the issue desk Retrospective board renders its 3 demo items with no collect cue, since each patient has an in-date specimen; no server errors. No unit was issued.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: continue the help claim check with the transfusion, returns, and alerts pages.

## Iteration 347 — The Ready to issue board offers pre-specimen emergency release (2026-11-03)

### Capability matrix rescan

No change since Cycle 346.

### Finding (questions 1 and 5)

Cycle 346 let the gate accept emergency release without a usable specimen, but the Ready to issue board decided first:

- A specimen-blocked row offered only **New specimen**, never Emergency.
- So the release that OCD-053 allows could not be started from the board.
- The row text gave no hint that it was possible.

### Design

- The list sets `PreSpecimenEmergencyAllowed` when the specimen is missing or expired and the unit qualifies under the same `EmergencyPreSpecimenRule` the gate uses.
- **Row text:**
  - a qualifying row adds "Emergency release of this unit is possible with an authorized override (OCD-053)."
  - any other row adds "Before a specimen is drawn only group O red cells or group AB or A plasma can be released."
- **Button:** a qualifying row shows **Emergency** beside **New specimen** for users with emergency-release permission. It opens the same emergency issue form, and the gate and override still decide.

### Requirements / risk

- FRS-BB-526 (under URS-BB-484 and RISK-BB-582). Help: issuing page.

### Tests

- TEST-BB-387: with both patients' specimens expired, the group O red cell row is blocked, offers emergency, and names OCD-053. The group A row is blocked, does not offer emergency, and says only group O red cells can be released.
- Full suite: 3,567 passed, 0 failed (previously 3,566).
- Run check (development API and web): the Ready to issue board renders its 3 rows ("1 can be issued now. 2 need attention first …"); no demo specimen is missing or expired, so no row offers pre-specimen emergency; no server errors.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: continue the help claim check with the transfusion, returns, and alerts pages.

## Iteration 348 — Help claims checked: alerts and reactions (2026-11-03)

### Capability matrix rescan

No change since Cycle 347.

### Results

- **alerts.md was incomplete.** It said "Alerts come from five places", but `AlertRule` defines nine. Four were missing:

  | Alert | Severity | How it is raised | Closes |
  | --- | --- | --- | --- |
  | Incompatible after release (`retro-xm-incompatible`) | Critical | Raised once | When acknowledged |
  | Transfusion not completed | Warning | Synced each scan | On its own when cleared |
  | Backup failed | Critical | Raised once | When acknowledged |
  | Backup overdue | Critical | Synced | On its own when cleared |

  The page now lists all nine, what each means and what to do, and which close on their own (every synced source) versus at acknowledgment.
- **Dead code:** `AlertRule.ConditionSources` was never read, and had already gone stale (it left out transfusion not completed). Removed.
- **reactions.md:** the claims hold. The remaining product is quarantined (`TryQuarantineRemainderAsync`), and closing is refused while clerical, visual, or DAT items are unfinished (`ReactionWorkupCompletenessRule`).

### Tests

- Documentation, plus removal of an unused constant.
- Full suite: 3,567 passed, 0 failed.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: a test that keeps the alerts help in step with `AlertRule`, so a new alert source fails a test until it is documented.

## Iteration 349 — The alerts help cannot fall behind the alert sources (2026-11-03)

### Capability matrix rescan

No change since Cycle 348.

### Finding

Cycle 348 found the alerts help four sources behind the code. Nothing would have caught it, and nothing would catch the next one.

### Design

- `AlertRule.HelpName` maps each alert source to the name the help uses.
- TEST-BB-388 enumerates every public string constant on `AlertRule` by reflection and fails when:
  - a source has no help name, or
  - alerts.md does not describe that name in bold, or
  - the page's "Alerts come from N places" count is wrong
- Adding an alert source now fails the tests until it is documented.

### Tests

- TEST-BB-388 (2 tests). It was confirmed to fail when one bold name was removed from the page, and the page was restored.
- Full suite: 3,569 passed, 0 failed (previously 3,567).

### Process note

The first commit of this cycle (ef9205c) emptied `docs/validation/TEST_CATALOG.md`. A documentation script opened the file for writing before computing its new content, the insert then failed, and the empty file was staged.

- `TestCatalogPackagingTests` caught it.
- The catalog was restored from the previous commit and the row added in a follow-up commit.
- Scripts now compute every new file before writing any.

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the same kind of coverage test for the exception catalog (every overridable rule code the issue gate can emit has an exception definition seeded), since an override with no definition is refused at the bedside.

## Iteration 350 — Every checked override has a seeded exception definition (2026-11-03)

### Capability matrix rescan

No change since Cycle 349.

### Finding

An override that checks the exception catalog (`ExceptionOverridePolicy.EvaluateAccess`) is refused with `EXC-DEF-MISSING` when no active definition exists, so the workflow stops at the bench. Eight calls in `src` check five codes:

- antigen-negative (`ISS-ANTIGEN-NEG`): issue, allocation, and the compatibility screen
- antibody-history crossmatch: orders and allocation
- ABO/Rh delta
- specimen expiration override
- retrospective crossmatch on an expired pre-transfusion specimen (Cycle 322)

All five are seeded active and overridable today, so there is no live gap. Nothing would catch the next override path added without a seeded definition.

### Design

TEST-BB-389:

- Scans every `.cs` file in `src` for `ExceptionOverridePolicy.EvaluateAccess(…, …, Type.Constant)`.
- Resolves each constant through the Domain assembly by reflection.
- Seeds a fresh database and asserts each code has an active, overridable definition.
- A sanity test asserts the scan finds the known paths, so a regex that stopped matching cannot pass silently.

### Tests

- TEST-BB-389 (2 tests). It was confirmed to fail with the antigen-negative definition temporarily seeded as not overridable, and the seeder was restored with no diff.
- Full suite: 3,571 passed, 0 failed (previously 3,569).

### Next ranked residual

Pending decisions: downtime back-entry, OCD-046, the authorizer directory policy, and whether receipt must be by scan. Next engineering candidate: the same seeded-coverage check for facility policy keys (every `FacilityPolicyKeys` constant read by a service is listed in the facility policy admin catalog with its default), so no setting is invisible to administrators.

## Iteration 351 — An incompatible retrospective crossmatch opens a deviation (2026-11-04)

### Decisions received (product owner, 2026-11-04)

- OCD-046: an incompatible retrospective crossmatch requires a deviation.
- OCD-054: downtime back-entry of since-expired units is controlled by role.
- OCD-055: an authorizer outside the directory is not refused; they can be added to the directory as an outside provider.
- OCD-056: receipt by scan is a facility setting, off by default.

### Capability matrix rescan

No change since Cycle 350 apart from this cycle's row.

### Finding (questions 6 and 7)

An incompatible retrospective crossmatch raised a critical alert, but nothing made sure the event entered the quality system. Whether a deviation was opened depended on someone remembering to open one by hand on **Deviations**.

### Design

- When the incompatible result is recorded against an open retrospective follow-up, the system opens a Critical deviation:
  - context `Issue` and the issue id, so there is one per issue
  - the description names the issue, unit, patient, crossmatch, and where the unit stands
  - audited as a Deviation event with the crossmatch id
- A repeat incompatible result on the same issue reuses the deviation. A compatible result opens none.
- The warning on Compatibility and the critical alert name the deviation number.
- Severity is Critical, matching the alert. Notification and clinical follow-up stay facility procedure and are recorded in the acknowledgement and the corrective action.

### Requirements / risk

- FRS-BB-527 (under URS-BB-444 and RISK-BB-546). Help: issuing, alerts, and compatibility pages.

### Tests

- TEST-BB-390:
  - the deviation is Critical, and its number is named in the warning and the alert
  - an incompatible result opens exactly one deviation linked to the issue, audited, even after a repeat
  - a compatible result opens none
- Full suite: 3,593 passed, 0 failed.

### Next ranked residual

Implement the other three decisions, one per cycle:
1. OCD-055: add an outside provider to the directory from the override form.
2. OCD-054: a permission for back-entry of downtime transfusions of since-expired units.
3. OCD-056: a facility setting that requires specimen receipt by scan.

Next: OCD-055, because it touches every override.

## Iteration 352 — Add an override authorizer as an outside provider (2026-11-04)

### Capability matrix rescan

No change since Cycle 351 apart from this cycle's row.

### Finding (questions 2, 3, and 7)

OCD-055 decided that an authorizer outside the directory is not refused and can be added as an outside provider. Before this cycle:

- A transferring or visiting physician was always recorded as "not in the provider or user directory".
- Reviewers could not tell a real outside authorizer from a misspelling.
- Only an administrator with admin configuration rights could add a provider, and only from the admin catalog, away from the override.

### Design

- **Permission:** new `provider.outside.add`, seeded to Technologist, Supervisor, and Administrator. Technologist gets it on upgraded databases too.
- **Field:** when the typed authorizer is not in the directory, the authorizer field offers **Add as outside provider** to users with the permission. The form takes an optional identifier (NPI or local id) and a required reason.
- **Rule (`OutsideProviderRule`):**
  - the name is required
  - the reason must be at least 8 characters
  - a name already in the directory is refused, so the same person is not added twice
  - an identifier already in use is refused
  - without an identifier, `OUT-0001`, `OUT-0002`, … is assigned
- **Storage:** an `OrderingProvider` row with source `Outside`. No schema change.
- **Audit:** the addition is audited and recorded in configuration history with the reason.
- **Directory matching:** `AuthorizerDirectoryMatch.OutsideProvider` (4) is recorded on overrides that name them. Review describes them as "outside provider <id>".
- **Orders:** outside providers are left out of the ordering-provider list unless `includeOutside=true`, which only the authorizer field asks for.

### Requirements / risk

- URS-BB-485, FRS-BB-528, RISK-BB-583. Help: issuing (authorizer) and providers pages.

### Tests

- TEST-BB-391:
  - rule validation, permission, generated id, and directory match
  - added with an assigned id and an explicit id; audited with the reason; recorded in configuration history
  - refused without the permission, without a reason, without a name, for a name already listed, and for a duplicate id, with nothing written
  - a later override naming the provider is stamped OutsideProvider
- Full suite: 3,601 passed, 0 failed.
- Run check (development API and web):
  - the ordering-provider list leaves outside providers out by default and marks `outside` when asked
  - on Issue, an unknown authorizer shows **Add as outside provider**
  - the form opens, and saving with no reason shows the reason message and adds nothing
  - no server errors

### Next ranked residual

- OCD-054: a permission for back-entry of downtime transfusions of since-expired units.
- Then OCD-056: a facility setting that requires specimen receipt by scan.

## Iteration 353 — Downtime back-entry of paper issues, controlled by role (2026-11-04)

### Capability matrix rescan

No change since Cycle 352 apart from this cycle's row (interoperability and downtime).

### Finding (questions 6 and 7)

Cycle 287 recorded that a paper issue from downtime could not be entered once its unit or specimen had expired:

- the gate ran at entry time
- the expiry sweep had marked the unit Expired and ended its reservation
- the issue wanted a fresh scan of a bag that was gone

OCD-054 decided this is allowed, controlled by role.

### Design

- **Permission:** `issue.downtime-backentry`, seeded to Supervisor and Administrator.
- **Request:** `DowntimeBackEntry`, the paper time (`IssuedUtc`, earlier than now) with a late-entry reason, and `BackEntryDin`.
- **`DowntimeBackEntryRule`:**
  - `ISS-DOWNTIME-PERM`: without the permission, a hard stop
  - `ISS-DOWNTIME-TIME`: the stated time must be earlier than now
  - `ISS-DOWNTIME-DIN`: the typed DIN must match the unit, ignoring spaces and case
- **The gate as of the paper time:**
  - unit and specimen expiry, crossmatch validity, special requirements, and age are judged as of the paper time
  - the specimen is the latest accepted one collected by then
  - only crossmatches performed by then count
  - a reservation the sweep ended counts if it was made by then
  - a unit marked Expired counts as issuable only if it was in date then
- **Unchanged:** identity, ABO/Rh, antigen, discrepancy, location, and every warning. Warnings true at the paper time, such as near expiry, still need an override.
- **Record:** `Issues.DowntimeBackEntry` (migration `20261106000000_IssueDowntimeBackEntry`, plus the bootstrap); the issue audit carries `DowntimeBackEntry` and `BackEntryDin`; the status history reads "Downtime back-entry: issued … on paper".
- **Issue desk:** for permitted users, a **Downtime back-entry from a paper issue record** toggle shows:
  - the paper time, the DIN on the record, and the reason
  - a unit picker that includes Expired units
  - no scan panel

### Requirements / risk

- URS-BB-486, FRS-BB-529, SRS-BB-448, RISK-BB-584. Help: downtime page and the downtime plan.

### Tests

- TEST-BB-392 (8 domain tests and 1 integration scenario). The integration scenario covers:
  - an ordinary late entry of the since-expired unit is refused
  - without the permission, with no paper time, with the wrong DIN, and with a paper time after the unit expired, the back-entry is refused, with nothing written
  - with near-expiry warnings unacknowledged, an override is required
  - with an override, the back-entry is issued, flagged, audited, and named in the status history
- One regression found and fixed while testing: the allocation is now loaded by id, so the issue still marks it Consumed.
- Full suite: 3,610 passed, 0 failed.
- Run check (development API and web): on the Issue desk, the toggle shows the paper time, DIN, and reason fields and hides the scan panel; no server errors. No issue was submitted.

### Next ranked residual

- OCD-056: a facility setting, off by default, that requires specimen receipt by scan.
- Then the bedside half of downtime back-entry: documenting a transfusion of a back-entered unit without a fresh bedside scan.

## Iteration 354 — A facility setting to require specimen receipt by scan (2026-11-04)

### Capability matrix rescan

No change since Cycle 353 apart from this cycle's row (orders and specimens).

### Finding (questions 4 and 6)

Since Cycle 308 a tube can be received by scanning it, but **Receive** on a worklist or chart row still works. Clicking the wrong row receives a tube that is not in hand. OCD-056 decided this is a facility setting, off by default.

### Design

- **Setting:** `Specimen.RequireReceiptByScan` in the facility policy catalog ("Receive specimens by scan only", Specimen, default false, OCD-056). Bench pages can read it.
- **Server:** with the setting on, `SpecimenService.ReceiveAsync` (receipt from a row) is refused with `SPEC-RECEIPT-SCAN-REQUIRED` before anything is loaded or changed. Receipt by scan is unchanged.
- **Duplicate labels:** while scanning is required, a label matching more than one specimen says to check the label and have a supervisor correct the duplicate, instead of "receive it from its row", which would be refused.
- **Accept:** unchanged. Only a received tube can be accepted, and with the setting on that tube was scanned.
- **UI:**
  - the specimen worklist rows show "Scan to receive" in place of **Receive**
  - the chart Specimens tab shows a **Scan to receive** link to the specimen worklist
  - both read the setting

### Requirements

- URS-BB-487, FRS-BB-530. Help: test worklist page. The setting describes itself on Facility Policies.

### Tests

- TEST-BB-393:
  - the rule refuses list receipt only when the setting is on, and the catalog default is off
  - with the setting on, receipt from a row is refused and nothing changes; the scan receives the tube; accept from the row then works
  - by default, receipt from a row works and is recorded as "Selected from the worklist"
  - a duplicate label under the setting does not point to the row
- The existing scan-receipt tests now run with facility policy present. Their setup accessions without it, so the label identifiers are not asked for.
- Full suite: 3,615 passed, 0 failed.

### Next ranked residual

All four decisions from 2026-11-04 are now implemented. Next: the bedside half of downtime back-entry, documenting the transfusion of a back-entered unit without a fresh bedside scan (the DIN typed from the paper record, the same role).

## Iteration 355 — Ward receipt and transfusion of a back-entered issue (2026-11-04)

### Capability matrix rescan

No change since Cycle 354 apart from this cycle's row (downtime).

### Finding (questions 4 and 6)

Cycle 353 let a paper issue be back-entered, but the rest of its record still could not be completed:

- ward receipt asked for a fresh scan of an ISBT unit
- so did transfusion start and one-step documentation
- the bag is gone, so the transfusion stayed on paper

### Design

- The three requests accept `BackEntryDin`.
- **`DowntimeBackEntryRule.EvaluateFollowOn`** allows it only when:
  - the issue is a downtime back-entry (`ISS-DOWNTIME-NOT-BACKENTRY` otherwise, so an ordinary issue can never skip its scan)
  - the user holds `issue.downtime-backentry`
  - the DIN matches the unit
- **Identification:** the paper DIN is not electronic identification, so a required second verifier must be a directory user.
- **Record:** the transfusion's unit identification is `PaperRecordDin`, with no bedside scan stored. Without a scan or a paper DIN, the scan is still required.
- **UI:** the shared `PaperRecordDinField`, a collapsed "Downtime back-entry: no bag to scan" panel, appears under the scan panel on Ward receipt and the transfusion desk, for permitted users only.

### Requirements / risk

- FRS-BB-531 (under URS-BB-486 and RISK-BB-584). Help: downtime page.

### Tests

- TEST-BB-394. Domain: the follow-on rule. Integration, with ISBT units:
  - an ordinary issue is refused the paper DIN
  - the back-entered issue is refused without the permission and with the wrong DIN
  - with neither a scan nor the paper DIN, it is refused and nothing is written
  - ward receipt by paper DIN works
  - documentation works with a directory second verifier, and the free-text verifier is refused
  - the transfusion records `PaperRecordDin`
- Full suite: 3,617 passed, 0 failed.

### Next ranked residual

- A back-entered issue's ward receipt is stamped with the entry time, not the time on the paper record. The same applies to its in-transit due time, which is computed from the paper issue time and so is already past. Check whether back-entered issues raise in-transit or outstanding-transfusion alerts they should not, and let ward receipt take the paper time for a back-entry.

## Iteration 356 — A back-entered ward receipt keeps the paper time (2026-11-04)

### Capability matrix rescan

No change since Cycle 355 apart from this cycle's row (downtime).

### Finding (questions 2, 5, and 6)

A back-entered issue:

- showed **Overdue** on In transit straight away, because its due time came from the paper issue time
- had its ward receipt stamped with the time it was typed in, not when the ward received it
- had that receipt flagged late in the audit

So the board showed an alarm that was not real, and the record gave a wrong custody time.

The other alerts were checked and left alone:

- **Retrospective crossmatch due** for a back-entered emergency issue is a real obligation.
- **Transfusion not completed** only follows a started transfusion.

### Design

- **`DowntimeBackEntryRule.EvaluateReceiptTime`** (`ISS-DOWNTIME-RECEIPT-TIME`): a ward receipt identified from the paper record needs the receipt time from that record. It cannot be before the issue or in the future.
- **The receipt:**
  - `WardReceivedUtc` is the paper time
  - lateness is judged against it
  - the audit adds `EnteredUtc` and `FromPaperRecord`
- **In transit:** `InTransitWorkItemDto.DowntimeBackEntry` is set and `IsOverdue` is false for a back-entered issue. The row shows a **Back-enter receipt** badge.
- **UI:** `PaperRecordDinField` takes an optional time. Ward receipt asks "Received at (from paper record)".

### Requirements / risk

- FRS-BB-532 (under URS-BB-486 and RISK-BB-584). Help: downtime page.

### Tests

- TEST-BB-395:
  - domain: required, not before the issue, not in the future, and the boundaries
  - integration: In transit shows the row as back-entry and not overdue; a missing, too-early, or future receipt time is refused; the receipt is stamped at the paper time; the audit carries `FromPaperRecord` and `Late: false`
- Full suite: 3,618 passed, 0 failed.

### Next ranked residual

The downtime back-entry chain is complete: issue, receipt, and transfusion. Next: rescan the capability matrix for the highest remaining operational gap. Candidates:
- the facility policy key coverage check named in Cycle 350
- the downtime snapshot counting back-entered issues still missing a receipt or transfusion record

## Iteration 357 — The downtime snapshot counts back-entries still to complete (2026-11-04)

### Capability matrix rescan

No change since Cycle 356 apart from this cycle's row (downtime).

### Finding (question 5)

The downtime snapshot counted every open issue together. After recovery, the supervisor could not see which paper issues had been back-entered but still lacked a transfusion record or ward receipt. That is the follow-up the downtime procedure needs closed.

### Design

- **`DowntimeReconciliationSnapshot`** adds two counts:
  - `BackEntriesAwaitingTransfusion`: back-entered issues still Issued
  - `BackEntriesAwaitingWardReceipt`: those of them with no ward receipt
- **Downtime page:** a row "Paper issues back-entered, transfusion not yet entered", with the receipt count beneath and links to **Issued units** and **In transit**.
- No schema change. The page still only counts.

### Requirements

- FRS-BB-533 (under URS-BB-486). Help: downtime page.

### Tests

- TEST-BB-396: three back-entered issues (open without receipt, open with receipt, transfused) count as two awaiting transfusion and one awaiting receipt; the open-issue total rises by three.
- Full suite: 3,618 passed, 0 failed.

### Next ranked residual

Rescan for the highest remaining gap. Candidate: the facility policy key coverage check, a test that every `FacilityPolicyKeys` constant is in `FacilityPolicyCatalog` with a default, so no setting is invisible to administrators.

## Iteration 358 — Facility policy defaults match what administrators see (2026-11-04)

### Capability matrix rescan

No change since Cycle 357 apart from this cycle's row (administration).

### Finding (questions 6 and 7)

A coverage check of facility policy keys against the Facility Policies catalog found:

- Every key is listed, except `Inventory.BlockRetypeSelfVerify`, which is retired and read by nothing.
- One default disagreed. `FacilityPolicyService.GetRequireSecondVerifierAsync` fell back to **false** when the setting was unset, but the catalog showed **true**, and fresh databases seed it true.
- The settings seeder only runs when the settings table is empty. A database upgraded from before the setting existed therefore has no row.
- On such a database, issue and bedside documentation ran without a second verifier while Facility Policies showed it as on.

### Design

- The service default for `Transfusion.RequireSecondVerifier` is now true. That is what the catalog shows and what a fresh database seeds. OCD-008 still lets a facility turn it off, with a reason.
- **TEST-BB-397**, an Application-layer test with no database:
  - every `FacilityPolicyKeys` constant is in the catalog unless listed as retired
  - every `FacilityPolicyService` getter is called against an empty settings store; which key it read is recovered from its query, and its fallback must equal the catalog default
- **Behavior change:** an existing database with no `Transfusion.RequireSecondVerifier` row will now require a second verifier at issue and bedside, as its Facility Policies page already said. A facility that does not want this saves the setting off.
- **Integration fixture:** it now records the setting off explicitly, instead of relying on the missing row. The one test that needs it on toggles that row and restores it.

### Requirements / risk

- URS-BB-488, FRS-BB-534, RISK-BB-585. Help: Facility Policies page.

### Tests

- TEST-BB-397 (2 tests), which found the mismatch. Full suite: 3,620 passed, 0 failed.

### Next ranked residual

The same drift exists for settings the seeder never adds to an existing database. Consider a startup step that adds any catalog key missing from SystemSettings with its catalog default, recorded in configuration history, so the stored value is always visible and auditable.

## Iteration 359 — Missing facility policies are stored at startup (2026-11-04)

### Capability matrix rescan

No change since Cycle 358 apart from this cycle's row (administration).

### Finding (question 7)

The settings seed runs only when the settings table is empty, so an upgraded database lacks every setting added since it was created. Cycle 358 made each code default equal its catalog default, but the stored value still did not exist:

- nothing showed when it took effect
- no configuration history recorded it
- only a few settings had their own one-off "ensure" step

### Design

- **`DatabaseSeeder.EnsureCatalogPoliciesStoredAsync`** runs after the existing per-setting steps. For every `FacilityPolicyCatalog` entry with no row, it adds one at the catalog default, with the catalog's category and description.
- **History:** each added row gets a Create entry in `ConfigurationChangeHistory`, by `system`, with the reason "Facility policy was not stored; added at its Facility Policies default, which the system was already using".
- **Safety:** values already stored are never changed, and the step is idempotent. It changes no behavior, because code defaults equal catalog defaults (TEST-BB-397).

### Requirements

- FRS-BB-535 (under URS-BB-488 and RISK-BB-585). Help: Facility Policies page.

### Tests

- TEST-BB-398:
  - on a database whose settings table already has facility choices, seeding stores every catalog key
  - existing values stay (second verifier off, in-transit hours 6)
  - a newly stored key has the catalog default and a system Create history row; a kept key gets none
  - a second seed adds no settings and no history
- Full suite: 3,622 passed, 0 failed.

### Next ranked residual

Rescan the capability matrix. The remaining Partial rows are:
- ISBT identifiers: waits on a licensed ICCBBA extract (OCD-004)
- HL7 and interfaces

In the interfaces area, check whether instrument and HL7 error queues offer a next action for every error type, with a reason recorded on resolve.

## Iteration 360 — Interface errors keep why they were resolved (2026-11-04)

### Capability matrix rescan

No change since Cycle 359 apart from this cycle's row (interoperability).

### Finding (question 7)

- **Manual resolve** required a reason, but it was stored only in the audit trail. The row showed only who closed it and when.
- **Replay that applied** closed the row with no audit entry and no note.
- **Acknowledged outbound resend** did the same.
- **The HL7 page** listed open errors only, so nobody could review how errors were closed.

### Design

- **`InterfaceErrorQueueItem.ResolutionNote`** (nvarchar(500)), set on all three paths:
  - manual: the reason
  - replay: "Resolved by a successful replay of the stored message."
  - resend: "Resolved: the message was delivered and acknowledged on a later send."
- **Audit:** replay and resend closes now write an Interface audit event per row, with the note as the reason. `Hl7OutboundSender` gains an optional `IAuditWriter`.
- **Endpoint:** `GET /api/hl7/errors/resolved?take=` lists the newest closed rows (default 50, at most 200) with who, when, why, and the message type.
- **HL7 page:** a collapsed **Recently resolved** list under the error queue. Older rows closed before notes existed say "Not recorded".
- **Schema:** migration `20261107000000_InterfaceErrorResolutionNote`, plus the bootstrap.

### Requirements

- URS-BB-489, FRS-BB-536, SRS-BB-449. Help: HL7 page.

### Tests

- TEST-BB-399, by extending existing HL7 tests:
  - manual resolve stores its reason
  - a replay that applies stores the replay note and an audit event
  - an acknowledged resend stores the delivery note and an audit event
- Full suite: 3,622 passed, 0 failed.
- Run check (development API and web): `/api/hl7/errors/resolved` returns `[]` on the development database, which has no closed errors; the HL7 page loads and hides the empty section; no server errors.

### Next ranked residual

The instrument interface has its own error handling. Check that instrument result errors also offer a next action and keep why they were resolved, the same as HL7.

## Iteration 361 — Failed instrument messages are followed up (2026-11-04)

### Capability matrix rescan

No change since Cycle 360 apart from this cycle's row (interoperability).

### Finding (questions 5, 6, and 7)

- A failed instrument (ASTM) message stayed **Failed** forever:
  - a replay that worked created a new log and left the original looking unhandled
  - nobody could record that it was handled another way
- **Any** message could be replayed, including one that had already processed. Replaying that would file its results again.

### Design

- **`InstrumentMessageFollowUpRule`:**
  - only an open failed message can be replayed (`INST-REPLAY`); a processed, replayed, or resolved one is refused with the reason
  - resolve needs an open failure and a reason (`INST-RESOLVE`)
- **`InstrumentMessages`** gains `ResolvedUtc`, `ResolvedBy`, and `ResolutionNote` (migration `20261108000000_InstrumentMessageResolution`, plus the bootstrap).
- **`AstmInboundProcessor`:**
  - `ReplayAsync` refuses as above and, when the replay applies, closes the original with the replay note
  - `ResolveAsync` closes it with the given reason
  - each close is an Interface audit event, with the replay log id when there is one
- **API:** `POST /api/instruments/messages/{id}/resolve`. A refused replay returns 422 with the reason.
- **Instruments page:**
  - "N failed, open" badge on Messages
  - status shows "· resolved"
  - Replay only for open failures
  - the detail shows **Resolve without replay** or the resolution

### Also fixed

The Cycle 360 TEST-BB-399 catalog row cited the test class with a description instead of method names, so `TestCatalogPackagingTests` failed. Iteration 360 only ran the Help tests after its doc edits. The row now names the three methods.

### Requirements / risk

- FRS-BB-537 (under URS-BB-489), SRS-BB-450, RISK-BB-586. Help: instruments page.

### Tests

- TEST-BB-400:
  - domain: replay only for an open failure; resolve needs an open failure and a reason
  - integration: a result arriving before its order fails and stays open; after the order is placed the replay applies and closes the original (note and audit); replaying the closed failure, or the processed replay, is refused and writes no new log
  - integration: blank reason refused; resolved with a reason by the current user and audited; a second resolve and a replay after it are refused
- Full suite: 3,629 passed, 0 failed.

### Next ranked residual

Count open instrument failures and open HL7 errors together on the dashboard's interface card and on the downtime snapshot, so a failed instrument result is not missed after an outage.

## Iteration 362 — Interface failures on the dashboard and downtime snapshot (2026-11-04)

### Capability matrix rescan

No change since Cycle 361 apart from this cycle's row (interoperability and operations).

### Finding (question 5)

- The dashboard had no interface card. An instrument result that failed to file, or an HL7 order that failed, was seen only by someone who opened the HL7 or Instruments page.
- The downtime snapshot counted HL7 errors but not failed instrument messages, which pile up during an outage.

### Design

- **Dashboard cards:**
  - **Instrument failures** (`instrument.manage`, opens Instruments)
  - **HL7 errors** (`hl7.manage`, opens HL7)
- **Placement:** both are in the urgent-when-non-zero set, after temperature excursions, reactions, retrospective crossmatch, and in transit. Instrument failures is on the Bench desk; both are on the Supervisor desk.
- **Endpoint:** `GET /api/instruments/messages/failed` lists open failures oldest first.
- **Downtime snapshot:** gains `OpenInstrumentFailures`, read through an optional instrument-message repository, with a row linking to Instruments.

### Also fixed

The dashboard help said the Shift handoff card needed audit-read permission. The code shows it to holders of `shift.handoff`, and the help now says so.

### Requirements

- FRS-BB-538 (under URS-BB-489). Help: dashboard and downtime pages.

### Tests

- TEST-BB-401:
  - the cards follow the interface permissions and are absent for a bench-only user
  - non-zero failures are Needs attention in urgency order; zero is clear
  - the snapshot counts only the open failure (not a resolved failure or a processed message)
- Full suite: 3,632 passed, 0 failed.
- Run check (development API and web): the failed-messages endpoint returns `[]`; the dashboard lists Instrument failures and HL7 errors under Clear; no server errors.

### Next ranked residual

Rescan the capability matrix for the next operational gap outside interfaces. Candidate: the reaction investigation workflow, checked against the seven workflow questions, since it has not been re-audited since the fatality follow-up cycle.

## Iteration 363 — Reactions noticed after the transfusion (2026-11-04)

### Capability matrix rescan

No change since Cycle 362 apart from this cycle's row (patient-product workflows).

### Finding (seven-question audit of reaction investigation; questions 5 and 6)

An investigation opened only when the transfusion itself was documented with "reaction suspected", at bedside start or completion, or by interface. A reaction noticed later had no path:

- delayed hemolytic reactions, days later
- fever or breathing problems after the unit finished

Without a path, nothing was investigated, any remainder was not held, and the patient's next issue showed no prior reaction, because that notice reads the transfusion's flag.

### Design

- **`LateReactionReportRule` (`RXN-LATE-REPORT`).** It needs `transfusion.document` or `reaction.investigate`. It refuses:
  - an issue with no documented transfusion
  - a transfusion that already has an investigation (naming it)
  - a missing description
  - an onset before the transfusion started, or in the future
- **`ReactionInvestigationService.ReportLateAsync`:**
  - opens the investigation with `ReportedAfterTransfusion`, `OnsetUtc`, the optional reaction type, and findings beginning "Reported after the transfusion:"
  - quarantines the remainder as a bedside report does
  - sets the transfusion's `ReactionSuspected`, with an audit event keeping the earlier value, so the chart, lookback, timeline, and the next issue's reaction notice all see it
- **API:** `POST /api/reaction-investigations/report`, outside the investigator-only group, guarded by either permission.
- **Schema:** migration `20261109000000_LateReactionReport`, plus the bootstrap.
- **Chart:** Product history shows **Report reaction** on transfused rows that have no reaction. It opens a description and onset form and reloads the history on success.

### Found during the run check

The issue's Issued row and its Transfused row share an issue id, so the form first opened under both. It now tracks the row itself.

### Requirements / risk

- URS-BB-490, FRS-BB-539, SRS-BB-451, RISK-BB-587. Help: reactions and product history pages.

### Tests

- TEST-BB-402. Domain: a valid report, and each refusal with its reason. Integration:
  - before the transfusion is documented, refused
  - a completed transfusion without a reaction opens nothing
  - refused without permission, with a blank description, with an onset before the start, and with a future onset, with nothing written
  - a valid report opens the investigation (late flag, onset, findings)
  - a second report is refused, naming the investigation
  - the transfusion is marked, and the audit keeps `ReactionSuspected: false` as the old value
- Full suite: 3,635 passed, 0 failed.
- Run check (development API and web): patient 9's transfused row shows **Report reaction**, and the form opens under that row only. It was not submitted.

### Next ranked residual

Continue the reaction audit. Check that closing an investigation records what follows for the patient, for example a new special requirement such as washed products after a severe allergic reaction, and that this reaches selection. The medical decision itself is the facility's, so record it as an OCD if no linkage exists.

## Iteration 364 — Special requirements linked to the reaction that led to them (2026-11-04)

### Capability matrix rescan

No change since Cycle 363 apart from this cycle's row (patient-product workflows, antibody/antigen history).

### Finding (questions 2, 5, and 6)

Nothing connected a reaction investigation to the patient's special requirements:

- A requirement decided after a reaction (for example washed products after a severe allergic reaction) had to be typed on the chart by hand.
- The investigation it came from had to be remembered and copied into the reason.
- Nothing showed later why the requirement existed.

Which reactions call for which requirements is a medical decision, so it is not invented. It is recorded as **OCD-057** (REQUIRES SME / MEDICAL DIRECTOR DECISION).

### Design

- **Link:** `SpecialTransfusionRequirements.ReactionInvestigationId` (migration `20261110000000_SpecialRequirementReactionLink`, plus the bootstrap).
- **Add request:** takes the id. It must be one of the same patient's investigations; otherwise the add is refused with nothing written. The audit and the requirement DTO carry it.
- **Reactions page:** **Add special requirement** (`immuno.record`) opens `/patients/{id}?reaction={investigation}`. The chart switches to Overview, starts the reason with "After reaction investigation N:", and links the requirement on save.
- **Chart:** the requirement list shows "From reaction investigation N".
- **Not done (OCD-057):** the software suggests no requirement and does not require a decision at close.

### Requirements

- FRS-BB-540 (under URS-BB-490), SRS-BB-452, OCD-057. Help: reactions and patient chart pages.

### Tests

- TEST-BB-403:
  - a requirement added from the patient's own investigation is linked, listed with the link, and audited with it
  - linking another patient's investigation, or one that does not exist, is refused with nothing written
- Full suite: 3,637 passed, 0 failed.

### Next ranked residual

Pending decision: OCD-057. Next engineering candidate: rescan the capability matrix for the operations area (worklists and supervisor queues), checking each queue against question 5 (is the next action surfaced on the row?).

## Iteration 365 — The reaction board says what to do next (2026-11-04)

### Capability matrix rescan

No change since Cycle 364 apart from this cycle's row (operations: worklists).

### Finding (questions 1, 2, and 5; worklist rescan)

- **Already fine:** the supervisor queue gives every row a next action and a link.
- **Reactions board:**
  - each row spreads its state over up to six badges across ten columns (workup, remainder, discrepancies, fatality), so the reader had to work out what to do next
  - rows were in list order, so a fatality report that was due could sit below a routine case

### Design

**`ReactionNextStepRule`** gives one step per investigation, in this order:
1. fatality reporting due, even after closing, since the report stays due
2. hold the remainder, when it is not held and there is no note saying why it cannot be
3. review the ABO/Rh discrepancy, if the repeat patient or unit type disagrees or the unit was ABO/Rh incompatible
4. the first missing workup item, from the completeness rule
5. ready to close: review and sign

It names only work the investigation already requires; it decides no clinical outcome.

- **DTO:** `ReactionInvestigationDto.NextStep` computes it from the facts the DTO already carries.
- **Reactions board:** a **Next step** column, with steps 1–3 in red, and rows ordered by step, then oldest report.

### Requirements

- FRS-BB-541 (under URS-BB-490). Help: reactions page.

### Tests

- TEST-BB-404 (3 domain tests):
  - fatality reporting leads, even when closed
  - the urgency order holds
  - a remainder that cannot be held is not asked to be held
- Full suite: 3,640 passed, 0 failed.
- Run check (development API and web): the API returns `nextStep` (the demo case is "Ready to close: review and sign."); the board shows the column; no server errors.

### Next ranked residual

Pending: OCD-057. Continue the worklist rescan with question 5 on the specimen and test worklists: does each row name its blocking reason and next action (accept, result, verify)?

## Iteration 366 — Expired waiting tubes say what to do (2026-11-04)

### Capability matrix rescan

No change since Cycle 365 apart from this cycle's row (orders and specimens, operations).

### Finding (question 5, worklist rescan continued)

- **Test worklist:** already names each row's bench action and blocking reason.
- **Pending-specimen list:** an expired tube showed a greyed-out **Receive** or **Accept** with no reason; only the paragraph at the top of the page said to reject it.
- **Dashboard:** the Pending specimens card counted expired tubes as ordinary waiting work, so nothing flagged that they needed rejecting and recollecting.

### Design

- **Row:** an expired row shows "Expired: reject it" (red, with a tooltip saying to request a new specimen) in place of the dead button, and **Reject** is emphasized.
- **Dashboard:** `PendingSpecimenAttentionRule.CountExpired` feeds the Pending specimens card note "N expired, to reject". A note puts the card under Needs attention.
- **Server:** unchanged; it still refuses receipt or acceptance of an expired tube.

### Requirements

- FRS-BB-542 (under URS-BB-487). Help: test worklist and dashboard pages.

### Tests

- TEST-BB-405:
  - expired tubes (collected or received) are counted, and the note puts the card under Needs attention
  - with none expired the note is absent
- Full suite: 3,642 passed, 0 failed.

### Next ranked residual

Pending: OCD-057. The worklist rescan (question 5) is complete for issue, reactions, supervisor, test, and specimen boards. Next: rescan inventory boards (near-expiry, quarantine, modifications) for the same question.

## Iteration 367 — Quarantine release follows why the unit is held (2026-11-04)

### Capability matrix rescan

No change since Cycle 366 apart from this cycle's row (blood products: status history and quarantine).

### Finding (questions 5 and 6, inventory board rescan)

The quarantine board offered **Release** on every row, and the server checked only the second verifier and the label scan. It never looked at why the unit was held, so any of these could go back to Available:

- the remainder of a unit whose reaction investigation was still open, which is evidence for that investigation
- a unit whose verified ABO/Rh retype disagreed with its label (the issue gate already refuses it)
- an expired unit
- a unit returned with disposal requested

The board also never said what to do with each unit.

### Design

- **`QuarantineReleaseRule` (`QREL-REASON`)** refuses release for the four cases above, each with what to do instead:
  - discard
  - discard or return to the supplier
  - release after the investigation closes
  - discard

  Every other hold releases as before. Each stop follows a gate or request that already exists; no new clinical policy.
- **Server:** `ReleaseFromQuarantineAsync` applies the rule before the label check.
  - open investigations are read through a new optional reaction-investigation repository
  - verified mismatches come through `ProductRetypeConfirmation`, the same check the issue gate uses
- **Board:** `QuarantineWorkItemDto` gains `NextStep` and `CanRelease`. A **Next step** column shows refusals in red, and Release appears only when allowed.

### Requirements / risk

- URS-BB-491, FRS-BB-543, RISK-BB-588. Help: inventory page.

### Tests

- TEST-BB-406:
  - domain: ordinary holds release; a reaction remainder waits for its investigation; expired, mismatch, and disposal are refused, with what to do
  - integration: the board marks the remainder, expired, and disposal units as not releasable and a visual-defect unit as releasable; each refused release leaves the unit in Quarantine; the routine one releases; closing the investigation lets the remainder release
- Full suite: 3,646 passed, 0 failed. One test assertion was fixed for letter case during the run.

### Next ranked residual

Pending: OCD-057. Continue the inventory rescan: the near-expiry board (Cycle 337 gave reserved units a next step) and the discrepancy and hold boards. Check that each row says what to do.

## Iteration 368 — Hold release says why, and keeps why it was held (2026-11-04)

### Capability matrix rescan

No change since Cycle 367 apart from this cycle's row (blood products: status history).

### Finding (questions 6 and 7, inventory board rescan continued)

- **Discrepancy board:** Locate and Inspect move missing or damaged units into quality quarantine, which now has its own release checks (Cycle 367). Its rows already name the action.
- **Operational hold, release:** one click with no reason. The unit's hold reason was cleared, and the audit's old value did not include it, so the record lost why the unit had been held: important history silently overwritten.
- **Operational hold, expired units:** an expired held unit could be released to stock.
- **Help:** the help said hold release "works the same way" as quarantine release, which needs a second verifier. Hold release never did.

### Design

- **`HoldReleaseRule` (`HOLD-RELEASE`):** a reason is required, and an expired unit is refused (discard it).
- **Service:** `ReleaseFromHoldAsync(unitId, reason)`:
  - the audit's old value includes the hold reason, and its new value includes the release reason
  - the audit reason and the status history read "Released from operational hold: <reason>"
- **API:** `POST /api/inventory/units/{id}/release-hold` takes `{ reason }`.
- **Hold board:** **Release from hold** opens a "How was the hold resolved?" field. An expired row shows "Expired: discard it." instead.
- **Unit page:** asks for the reason next to its Release button.
- **Help:** corrected to say hold release takes a reason and no second verifier.

### Requirements

- FRS-BB-544 (under URS-BB-491). Help: inventory page.

### Tests

- TEST-BB-407:
  - domain: reason and expiry
  - integration: the audit keeps "Pending paperwork" as the old reason and "Paperwork received" as the new one; a blank reason is refused; an expired held unit is refused and stays OnHold
- The seven existing hold-release test calls now pass a reason. One existing assertion of the exact audit text was updated.
- Full suite: 3,647 passed, 0 failed.

### Next ranked residual

Pending: OCD-057. Remaining inventory question: Locate (missing) and Inspect (damaged) are one-click and record neither where the unit was found nor what the inspection showed. Ask for those facts and keep them on the quarantine record.

## Iteration 369 — Locate and inspect record what was found (2026-11-04)

### Capability matrix rescan

No change since Cycle 368 apart from this cycle's row (blood products: status history, chain of custody).

### Finding (questions 2 and 7)

On the discrepancy board and the unit page, **Locate** (missing) and **Inspect** (damaged) were one click:

- Nothing recorded where a missing unit turned up or what the damage inspection showed.
- Those are the facts the later quarantine review decides on, and the chain-of-custody record for the time the unit was unaccounted for.

### Design

- **Service:** `LocateMissingAsync(unitId, foundNote)` and `InspectDamagedAsync(unitId, findings)` require the note. A blank note is refused with nothing changed.
- **Record:** the note is kept in the quarantine reason with the earlier reason ("Located after missing (<why missing>): <found>"), in the status history, and in the audit (`Found` or `Findings` alongside `PriorReason`).
- **API:** the endpoints take `{ reason }`.
- **Discrepancy board:** Locate or Inspect opens the question with an example, then "Record found, to quarantine" or "Record inspection, to quarantine".
- **Unit page:** asks the same question beside its button.

### Requirements

- FRS-BB-545 (under URS-BB-491). Help: unit detail page.

### Tests

- TEST-BB-408:
  - a blank note is refused and the unit stays Missing or Damaged
  - the quarantine reason keeps both the earlier reason and the note
  - the audit reason carries the note
- The ten existing calls to these actions now pass a note. At first I wrongly thought there were none; my search pattern missed them. Two exact-text audit assertions were updated.
- Full suite: 3,647 passed, 0 failed.

### Next ranked residual

Pending: OCD-057. The inventory board rescan is complete (near expiry, quarantine, discrepancy, hold). Next: rescan the administration area for question 7. Check that every admin catalog change records a reason. Cycles 339–343 covered exceptions, compatibility, and users and roles; check the rest (products, tests, locations, special requirement definitions).

## Iteration 370 — Reaction and treatment lists for the provider's conclusion (OCD-057) (2026-11-04)

### Decision received (product owner, 2026-11-04)

OCD-057:
- The treatments are an admin list the provider selects from, always with a "No treatment" option.
- The reaction the provider concludes is chosen from a similar list, always with "No reaction".

### Capability matrix rescan

No change since Cycle 369 apart from this cycle's row (patient-product workflows: reaction investigation; administration).

### Design

- **Admin lists:** one catalog, `ReactionCatalogEntry`, with two kinds:
  - ReactionType: the reaction concluded
  - Treatment: the treatment for future transfusions
  - each entry has a code (unique per list), text, sort order, and active flag; changes are recorded in configuration history
- **Fixed none entries:** NONE (No reaction, No treatment) is seeded on both lists. It cannot be deactivated or recoded, NONE is reserved, and its text can be reworded.
- **Starter content:**
  - the reaction list follows the CDC NHSN Hemovigilance adverse reaction categories, as an example to edit
  - no treatments are seeded; that list is the medical director's
- **`ReactionConclusionRule` (`RXN-CONCLUSION`)** applies when closing:
  - a reaction and at least one treatment are required
  - codes must be active entries on the right list
  - No treatment stands alone
- **Interpretation, adjustable:**
  - more than one treatment is allowed, since a patient may need more than one product modification
  - both choices are required to close
- **Recorded on the investigation:** `ConcludedReactionCode/Text` and `TreatmentCodes/Texts`, keeping the list text as it read, in the audit snapshot, and protected by the closed-case lock.
- **Screens and API:**
  - **Admin › Reactions and Treatments** (`/admin/reaction-lists`) has tabs for the two lists, with the none rows locked; `/api/admin/reaction-catalog`
  - the reference endpoint `/api/reference/reaction-catalog` feeds the Reactions page, which has a **Provider conclusion** section: a reaction drop-down, and treatment check boxes where No treatment clears the others
- **Next step:** the board's next step asks "Choose the reaction concluded and the treatment, then sign to close" when the workup is complete but the conclusion is not.
- **Special requirements:** a treatment does not create one by itself. Cycle 364's linked **Add special requirement** still does that, so the requirement is enforced at selection and issue.
- **Schema:** migration `20261111000000_ReactionCatalog`, the bootstrap, and the SQLite-to-SQL Server table list.

### Requirements

- URS-BB-492, FRS-BB-546, SRS-BB-453. OCD-057 moved to decided. Help: new admin topic "Reactions and treatments"; reactions page.

### Tests

- TEST-BB-409. Domain:
  - close needs both, and NONE counts for each
  - codes must be on the active list of the right kind
  - No treatment stands alone
  - the none entries are locked
  - the next step asks for the conclusion
- TEST-BB-409. Integration:
  - seeding gives NONE on both lists and the starter reactions
  - the none rows cannot be deactivated or recoded; NONE is reserved; duplicates are refused
  - a new treatment is added with history
  - closing without a reaction, without a treatment, with an unknown code, or with No treatment plus another is refused and the case stays open
  - a valid close records codes and texts, which later list renames do not change, and is audited
  - No reaction with No treatment closes a case
- The eight existing close requests in tests now pass No reaction and No treatment.
- Full suite: 3,654 passed, 0 failed.
- Run check (development API and web):
  - the reference list returns No reaction and the NHSN starter reactions
  - the admin page shows "Always offered"
  - the investigation form shows Provider conclusion with No treatment
  - no server errors

### Next ranked residual

Administration rescan for question 7 (deferred from Cycle 369): check that product, test, location, and special requirement definition catalogs record a reason with each change.

## Iteration 371 — Storage location changes record why (2026-11-04)

### Capability matrix rescan

No change since Cycle 370 apart from this cycle's row (administration).

### Finding (question 7, administration rescan)

A count of `RecordChange` calls with no reason across the admin services found the gaps:

- **Billing catalogs:** charge codes and rules, billing maps. Low clinical risk.
- **Coded-reason lists:** discard, rejection, cancellation, and reaction lists. Their changes are recorded with who and when.
- **Printers.**
- **Inventory and storage locations:** a location's settings are enforced at transfer and issue:
  - storage range and allowed components
  - remote and electronic issue
  - second verifier
  - whether it is time-out governed

  Yet an edit, activation, or deactivation recorded no reason. That is the safety-relevant gap.

### Design

- **`StorageLocationChangeReasonRule` (`LOC.REASON.REQUIRED`):** at least 8 characters, the same minimum as the exception, compatibility, and access reasons.
- **Service:**
  - `InventoryLocationAdminService.UpdateAsync` needs `SaveInventoryLocationRequest.Reason`
  - `SetActiveAsync` needs a reason
  - without one, the change is refused with nothing changed
  - the reason goes to the audit and configuration history
- **What doesn't need a reason:** creating a location. A new location affects nothing until units are moved into it.
- **API:** activate and deactivate take `{ reason }` (`InventoryLocationActiveRequest`).
- **Admin › Inventory locations:** a "Reason for the change" field at the top. Edit Save, Activate, and Deactivate stay disabled until it is filled.
- **Admin › Refrigerators & Freezers:** Edit asks for the reason; Deactivate and Activate ask in their confirm panel.

### Requirements

- FRS-BB-547 (under URS-BB-483). Help: inventory locations and storage devices topics.

### Tests

- TEST-BB-410:
  - domain: the reason length
  - integration: an edit without a reason is refused; with one it saves and configuration history keeps the reason
  - the existing deactivate tests now pass a reason; the permission and stored-units refusals are unchanged
- Full suite: 3,658 passed, 0 failed.

### Next ranked residual

Same check for transport containers (validated temperature and hours) and MTP pack definitions, which also lack reasons. Then the remaining low-risk lists, if worth it.

## Iteration 372 — Transport container and MTP pack changes record why (2026-11-04)

### Capability matrix rescan

No change since Cycle 371 apart from this cycle's row (administration).

### Finding (question 7, administration rescan continued)

Two catalogs were edited, activated, or deactivated with no reason recorded:

- **Transport containers:** they carry the facility's validated temperature range and hours.
- **MTP pack definitions:** they record medical-director decisions on pack contents.

Their endpoints already enforce the admin permissions.

### Design

- **`ValidatedConfigChangeReasonRule` (`CFG.REASON.REQUIRED`):** at least 8 characters, and the message names what is being changed.
- **Services:** `UpdateAsync` needs the request's `Reason`, and `SetActiveAsync` needs a reason. Without one, the change is refused with nothing changed; with one, it goes to the audit and configuration history.
- **Creating an entry:** does not need a reason.
- **API:** activate and deactivate take `{ reason }` (`ConfigActiveRequest`).
- **Both admin pages:** a "Reason for the change" field. Edit Save and Activate or Deactivate stay disabled until it is filled.

### Requirements

- FRS-BB-548 (under URS-BB-483). Help: transport containers and MTP packs topics.

### Tests

- TEST-BB-411:
  - domain: the length rule, and the message names what is changed
  - integration: a container edit without a reason is refused and the range is unchanged; with a reason it saves and history keeps it; deactivation needs a reason
  - integration: a pack deactivation without a reason is refused; with one it succeeds, and history keeps the reason; the existing inactive-pack test is unchanged otherwise
- Full suite: 3,662 passed, 0 failed.

### Next ranked residual

The remaining no-reason catalogs are billing (charge codes and rules, billing maps), coded-reason lists, and printers. They are low clinical risk and are recorded with who and when. Next: rescan the capability matrix for the highest operational gap rather than finish those lists.

## Iteration 373 — Special requirements by unit category (2026-11-04)

### Request (product owner, 2026-11-04)

Special requirements on the patient chart that enforce product constraints, for example only group O red cells, or only irradiated and CMV-negative red cells:

- They are set up in admin tables: unit typings, unit modifications, informational.
- Typings and modifications are specific to a unit category.
- Informational ones show as a banner.
- They are enforced unless informational.
- Each has a start and end date, and stops applying on and after the end date.

Decisions in the same session:

- An unmet typing or modification is a hard stop, overridable only on emergency or MTP release through the existing emergency override.
- Warmer, extended crossmatch, A2 typing, and Other stay as a fourth table, Issue procedures.
- CMV- and HbS-negative are met by the product code or a negative test on the unit.
- Each entry has one unit category; existing entries become All categories.

### Capability matrix rescan

Compatibility and special requirements: the row changes this cycle. No other change since Cycle 372.

### Finding

The catalog had no unit category, so an irradiation requirement applied to every component. It had no way to require:

- a unit ABO group
- RhD-negative units
- HbS-negative units

A negative CMV test recorded on a unit (`BloodComponentSpecialTest`) was never read. Issue gave one generic message and had no emergency path. The chart end date was UTC midnight. The banner was one line, with informational notes mixed into the enforced ones.

### Design

- **Catalog:** `SpecialRequirementDefinition` gains:
  - Category (the four tables)
  - ComponentClass (null = All categories)
  - AllowedAboGroups, AntigenCode (fixed antigen), UnitTestCode
  - new kinds: RequireUnitAbo, RequireRhNegative, RequireUnitTestNegative, Informational
- **Admin:** four tables on Admin › Special requirements, each with only its own fields. The validator checks the table against the kind and asks for what each kind needs. New typings and modifications must name a unit category.
- **One rule for every caller:** `SpecialTransfusionRequirementRule` takes the unit's typing (category, ABO/Rh, negative tests). A typing or modification applies only to its unit category, and informational entries never block. `SpecialRequirementLoader` replaces the two copies of the requirement and attribute loaders in issue and allocation, and adds the unit's negative tests.
- **Selection and allocation:** selection leaves out units that fail; reserving one is refused.
- **Issue:** `IssueGate.EvaluateSpecialRequirements` names each unmet typing or modification. That is a hard stop, or on emergency or MTP release a warning that goes through the existing override (authorizer, reason, override record). Issue procedures stay a hard stop.
- **Chart:**
  - the list is grouped by table, with "Name – unit category"
  - start date (default today) and end date are sent as facility dates and stored as facility midnight via `IFacilityTimeZone`
  - the end must follow the start
  - the table shows type and unit category
- **Banners:** informational requirements show as their own blue banner on the chart header and the issue form. Enforced ones keep the warning line, with the unit category.
- **Schema:** migration `20261112000000_SpecialRequirementCategories` and the bootstrap place earlier rows with one shared backfill:
  - product attribute → modification
  - antigen negative → typing
  - CMVNEG → typing met by the product attribute or a negative CMV test
  - the rest → procedures
  - unit category is left empty, so current patients keep the same protection
- **Seeding:** adds editable examples, one unit category each: Group O red cells only, CMV-negative red cells, HbS-negative red cells, Irradiated red cells, Irradiated platelets, and an informational "Call the transfusion service physician".

### Requirements

- URS-BB-493, FRS-BB-549, FRS-BB-550, SRS-BB-454, RISK-BB-589.
- Help: special requirements (admin), patient chart, issuing.

### Tests

- TEST-BB-412. Domain:
  - unit-category scoping, and All categories applies to every unit
  - allowed ABO groups and RhD negative
  - CMV/HbS met by the product code or a unit test, and not otherwise
  - informational never blocks
  - start inclusive, end exclusive
  - the emergency warning versus the procedure stop
  - validator per table
  - choice labels
- TEST-BB-412. Integration:
  - a group A red cell is refused for a group-O-only patient while group A plasma issues
  - an unmet modification issues on emergency release only with the override, which is recorded
  - a negative CMV test on the unit meets a CMV typing
  - selection omits the group A red cell, keeps group A plasma, and offers it again after the end date
  - admin create per table with the refusals
  - the backfill keeps All categories
  - chart dates are facility midnight, and an end on the start date is refused
- Full suite: 3,677 passed, 0 failed.
- Run check (development API and web):
  - the bootstrap placed the earlier catalog rows (CMVNEG now a typing with test CMV) and seeding added the examples
  - Admin › Special requirements shows four tables, and the typing form shows unit category, kind, and ABO check boxes
  - on demo patient 1, "Irradiated platelets" removed the platelets from the compatible-unit list and kept the plasma
  - the informational entry showed as a blue banner above the enforced warning line
  - an end date before the start was refused
  - the start was stored at facility midnight
  - both run-check requirements were then ended, and the platelets were offered again
  - no server errors

### Next ranked residual

Unit tests (CMV, HbS) can only arrive with the unit today; there is no screen to record one. Next: a Unit tests entry on the unit page (test code, result, audited), so a unit tested after receipt can meet a typing.

## Iteration 374 — Unit tests (CMV, HbS) recorded on the unit (2026-11-04)

### Capability matrix rescan

Blood products: the unit's tests are now recorded and corrected on its page. Safety rules: special requirements read the latest result. No other change since Cycle 373.

### Finding (residual from Cycle 373)

A CMV- or HbS-negative unit typing could be met by a negative test on the unit (`BloodComponentSpecialTest`), but no screen or API recorded one; only seeded data had them. The loader counted any Negative row, so a later Positive could not take a unit back out (question 6).

### Design

- **`UnitSpecialTestRule` (`UNIT-TEST`):**
  - the test code is upper-cased, letters, digits, or dashes, up to 20
  - the result must be Negative or Positive (Neg and Pos accepted)
  - the same result again is refused
  - a different result is a correction and needs a reason of at least 8 characters
- **`UnitSpecialTestService`:**
  - each recording is a new row and never overwrites; `Current` is the latest per code
  - audited as Create, or as Correct with the earlier result and the reason
  - needs `inventory.receive`, like unit attributes
  - not limited by unit status, so a late Positive on an issued unit is still recorded
- **API:** `GET`/`POST /api/inventory/units/{id}/special-tests`.
- **Loader:** `SpecialRequirementLoader.NegativeTestCodesByUnitAsync` reads only the latest result per test.
- **Unit page:**
  - the Attributes tab has a **Unit tests** card listing results, with earlier ones marked Corrected
  - the entry form has a test code and a Negative/Positive choice
  - the reason field appears when the entry would change a current result

### Requirements

- FRS-BB-551 (under URS-BB-493). RISK-BB-589 residual updated. Help: unit detail and special requirements topics.

### Tests

- TEST-BB-413. Domain: the code and result checks; the same result is refused; a change needs a reason.
- TEST-BB-413. Integration:
  - a result is recorded normalized and audited, and the same result again is refused
  - an unknown unit is refused
  - a correction without a reason is refused; with one it keeps both rows, marks the latest current, and audits the reason and earlier result
  - after a later Positive the loader no longer counts the unit CMV-negative
  - recording without the permission is refused
- Full suite: 3,682 passed, 0 failed.
- Run check (development API and web):
  - the seeded CMV Negative on unit 43 lists as Current
  - through the API, the same result was refused, a Positive without a reason was refused, and "Maybe" was refused
  - on the unit page, choosing Positive for CMV showed "Reason for correcting CMV (recorded Negative)"
  - nothing was saved
  - no server errors

### Next ranked residual

Rescan the capability matrix for the highest remaining operational gap.

## Iteration 375 — Reserved units checked against a later special requirement (2026-11-04)

### Capability matrix rescan

The ranked gap list is closed or waiting on SME decisions:
- RhIG indications
- the licensed ICCBBA extract (OCD-004)
- the administration-device protocol

The rescan of the special-requirement workflow from Cycles 373–374 found the next operational gap, with questions 5 and 6 applying.

### Finding

A requirement added after units were reserved, for example "Irradiated red cells" ordered the day after crossmatch, showed nothing on the patient's allocated products. The tech found out only at the issue desk:
- the Ready-to-issue badge
- the issue hard stop

That is minutes before the product is needed, with a crossmatched unit that cannot go.

### Design

- `PatientAllocationService.ListActiveAsync` checks each reserved, unissued unit against the patient's active requirements, with the same rule and unit facts as selection and issue (`SpecialRequirementLoader`). It returns the first thing the unit lacks as `SpecialRequirementNotice`.
- The Products tab row shows a red **Does not meet a special requirement** badge, the message, and what to do: modify it if a modification meets the requirement, or release it and choose another unit.
- After a requirement is added on the Overview tab, the chart reloads allocations. The confirmation lists each reserved unit that does not meet it.
- No new enforcement: issue already refuses the unit outside emergency release.

### Requirements

- FRS-BB-552 (under URS-BB-493). Help: patient products.

### Tests

- TEST-BB-414. Integration:
  - two reserved red cells (A and O) show no notice
  - after a group-O-only requirement is added, the A unit says "must be group O" and the O unit says nothing
- Full suite: 3,683 passed, 0 failed.
- Run check (development API and web), demo patient 1 with a reserved O+ leukoreduced red cell:
  - "Irradiated red cells" was added
  - the row showed the red badge and "Patient requires Irradiated red cells: the unit's product does not carry IRRAD.", with Modify and Release beside it
  - ending the requirement cleared the notice
  - no server errors
- The product-attribute and antigen-negative messages now name the requirement in the same form as the others ("Patient requires …: the unit …").

### Next ranked residual

The row's allocation status still reads Ready for issue next to the unmet-requirement badge. Consider a distinct display status for a reserved unit that cannot be issued routinely, if the bench finds the pair confusing.

## Iteration 376 — Issue several units to one patient in a run (2026-11-04)

### Direction (product owner, 2026-11-04)

Take a break from safety audits. Concentrate on operations, UI, interface, and workflow gaps compared with commercial transfusion systems.

### Capability matrix rescan (workflow lens)

- Role-based home: present.
- Search and scan bar: present.
- Ready to issue board: present.
- MTP banner and coolers: present.
- Multi-unit issue to one patient: missing. Commercial issue screens let the tech scan a patient once and send several units into one cooler; here each unit was a separate pass from the board.
- Batch ward receipt: noted as a later candidate.

### Finding (questions 1, 2, 5)

Issuing two units for one order took about 12 actions per unit:
- choose the row
- re-check the prefilled patient and enter identifiers if pre-fill is off
- re-type destination, issued-to, and cooler
- pick the unit
- scan, appearance, P-tag, Issue

After an issue the form also kept the old unit and its scan, so the next attempt started with stale unit data.

### Design

- After a successful issue, routine or override, the form keeps the patient, identifiers, destination, issued-to, and cooler. It clears the unit and its label scan; appearance and P-tag were already cleared.
- **This patient's units** panel:
  - issued in this run: unit, product, destination, cooler, time
  - **Issue next** buttons for the patient's other reserved units that Ready to issue would offer for routine issue, oldest reservation first
  - a count of units that need attention first
- `IssueRun` holds the selection logic: `OffersRoutineIssue` is the board's test, and `NextForPatient` / `HeldBackForPatient` apply it per patient. It is tested without the UI.
- Issue next only selects the unit. Scanning, appearance, P-tag, acknowledgments, and the gate are unchanged, so no safety step is skipped. Choosing another patient resets the run.
- No server or schema change.

### Requirements

- URS-BB-494, FRS-BB-553. Help: issuing (new section "Several units for one patient").

### Tests

- TEST-BB-415:
  - next units are that patient's, oldest first, without the unit on the form or ones already issued
  - rows blocked by any of the six board conditions are not offered, are counted, and fail `OffersRoutineIssue`
- Full suite: 3,685 passed, 0 failed.
- Run check (development API and web):
  - Issue on the demo patient's only ready unit opened the prefilled form with no run panel (no other units)
  - the unit stayed reserved: nothing was issued, to avoid changing demo data
  - the patient with two reserved units needs a crossmatch and second ABO first, so the panel would only count them
  - no server errors
  - the full issue-then-next path is covered by the helper tests, not exercised live

### Next ranked residual (workflow lens)

Ward receipt and transfusion documentation for a cooler of several units: one scan of the cooler or patient to receive all units that went out together.

## Iteration 377 — Receive a cooler at the ward unit after unit (2026-11-04)

### Capability matrix rescan (workflow lens)

Patient-product workflows: the issue side handles several units in a run (Cycle 376); ward receipt still went one unit at a time from the In transit board. No other change.

### Finding (questions 1, 2, 5)

A four-unit MTP cooler arriving at the trauma bay meant four trips:
- back to In transit
- find the next row of the same cooler
- Receive

The nurse had to remember which units of that cooler were already done. The development data has exactly that case: Cooler 1, four units for one patient.

### Design

- After a ward receipt is recorded, the form keeps **Received by**, as before. It now also shows what has been received from the cooler, or for that patient and destination when no cooler was recorded.
- It offers **Receive next** for the units of the same shipment still in transit, earliest issued first.
- `IssueRun.NextToReceive` holds the matching:
  - same patient
  - same cooler, ignoring case and spaces; or, without a cooler, same destination and only uncooled units
  - not yet received
  - not a downtime back-entry
- Receive next only selects the unit. Scan and appearance are recorded per unit, and the receipt rules are unchanged. Another patient or cooler starts a new list.
- No server or schema change.

### Requirements

- FRS-BB-554 (under URS-BB-494, extended). Help: issuing, ward receipt.

### Tests

- TEST-BB-416:
  - the rest of the same cooler, for that patient only, earliest first, without back-entries or other coolers
  - without a cooler, the same destination (ignoring case) and only uncooled units
  - an empty list once all are received
- Full suite: 3,687 passed, 0 failed.
- Run check (development API and web):
  - In transit lists Cooler 1 with four units for one patient
  - Receive on the first opens the Ward receipt form, which names Cooler 1, with no list yet because nothing has been received
  - no receipt was recorded, to keep the demo data
  - no server errors
  - the receive-then-next path is covered by the helper tests

### Next ranked residual (workflow lens)

Transfusion documentation for several units of one patient (MTP), the same way: keep the transfusionist and identifiers, and offer the next unit received at the bedside.

## Iteration 378 — Tell the EHR when a unit is dispensed or returned (BPS^O29) (2026-11-04)

### Capability matrix rescan (interface lens)

Interoperability:
- **ADT, ORM/OML, ORU in:** present.
- **ORU out (with corrections and withdrawals):** present.
- **DFT out:** present.
- **BPAM in (RAS/BPS) to document transfusion:** present.
- **FHIR read:** present.

**Missing:** the transfusion service never told the EHR that a unit was dispensed or returned. Mature transfusion systems send this so the EHR's bedside blood administration can match the scanned unit to the patient's issue. The workflow residual from Cycle 377 (transfusion documentation in a run) is a smaller, similar pattern; this interface gap ranks higher.

### Design

- `IProductDispensePublisher` (Application) is implemented by `Hl7OutboundService.PublishDispenseAsync`. It queues only while an outbound BPAM endpoint is enabled, like result corrections (OCD-049 pattern).
- `Hl7BpsBuilder` builds HL7 v2.5 BPS^O29: MSH, PID, PV1 (destination), ORC (SC; placer and filler order numbers, or the issue id), BPO (product), BPX.
- BPX carries:
  - dispense status: table 0510, DS on issue, RA on return
  - F, and the status time
  - DIN, product code, ABO/Rh, unit number
  - on dispense: destination, receiver, and issuer
- The status code goes through the site's value translations (`BloodProduct.DispenseStatus`), since EHRs vary.
- `IssuingService` calls it after the issue or return is saved. A message that cannot be queued adds the warning `HL7-BPS-NOT-SENT` and does not undo anything.
- The existing MLLP sender delivers it and records the ACK. No schema change.

### Requirements

- URS-BB-495, FRS-BB-555. Help: HL7 page (new section).
- The table 0510 codes are from the HL7 v2.5 standard. The interface team confirms them with the EHR at go-live, and the translation covers differences.

### Tests

- TEST-BB-417. Builder:
  - dispense message fields: MSH-9, PID, PV1, ORC, BPO, BPX-2/3/4/5/6/11/18/20
  - return gives RA with no destination, and ORC-3 falls back to the issue id without an order
  - a site translation replaces DS
- TEST-BB-417. Integration:
  - with no BPAM endpoint, an issue queues nothing
  - with an enabled outbound BPAM endpoint, the issue queues DS and the return queues RA, both outbound on that endpoint
- Full suite: 3,691 passed, 0 failed.
- Live check limited to startup: the development API started, and the issuing endpoints resolved `IssuingService` with the publisher from dependency injection (in transit and outstanding returned 200, no server errors).
- No message was sent live: the development data has no outbound BPAM endpoint, and enabling one would change interface configuration. The integration test covers the path through the real outbound service.

### Next ranked residual

- Interface lens: an outbound BPAM endpoint preset (message types BPS) in the interface admin, so it can be set up without typing the message type.
- Workflow lens: transfusion documentation in a run.

## Iteration 379 — Several units for one patient at the bedside (2026-11-04)

### Capability matrix rescan (workflow lens)

- Issue (Cycle 376) and ward receipt (Cycle 377) handle several units for one patient in a row.
- Transfusion documentation still went back to Issued units for each unit, and re-filled or blanked the identifiers by the pre-fill policy each time.
- Outbound BPAM (Cycle 378) needs no preset: BPS is already among the BPAM message types and the sender routes by endpoint.

### Finding (questions 1, 2, 5)

An MTP patient receiving four units had four bedside passes:
- re-select the row
- re-key both identifiers (pre-fill off) and the transfusionist
- remember which unit was still running and needed completing

The scan also stayed from the previous unit.

### Design

- After Start, Complete, or Document succeeds, **This patient at the bedside** lists the actions (unit, product, Started / Completed or Documented with disposition, time).
- It offers the patient's other units received on the ward and not finished (`IssueRun.NextAtBedside`): units already transfusing first (**Complete**), then received ones by issue time (**Start next**).
- **What stays, for the same patient:** the transfusionist and the two identifiers already entered.
- **What is cleared after each action:** the bedside scan, second verifier, volume, and the reaction flag. A reaction on one unit is never carried to the next; the reaction investigation lookup uses the flag as it was submitted.
- Finished units are not offered again. Another patient starts a new list. No server change; every check runs per unit.

### Requirements

- FRS-BB-556 (under URS-BB-494). Help: issuing, document the transfusion.

### Tests

- TEST-BB-418: transfusing first, then received by issue time; not received, other patients, the unit on the form, and finished units are left out.
- Full suite: 3,692 passed.
  - One integration test failed once in the first full run, then passed in two further full integration runs. It was not identified, and the change is UI and helper only.
- Run check (development API and web):
  - the transfusion desk renders, with no server errors
  - the demo data has no unit received on the ward, so the bedside list was not exercised live; the helper test covers it

### Next ranked residual (workflow lens)

Demo data: add a received, ready-to-transfuse pair of units for one patient, so these run panels can be shown and checked live without writing new records each time.

## Iteration 380 — Demo data for multi-unit runs, and the rest of a cooler shown on opening (2026-11-04)

### Capability matrix rescan (workflow lens)

The runs from Cycles 376, 377, and 379 could not be shown or checked live:
- the demo patient with two reserved units needed a crossmatch and a second ABO first
- no unit was received on the ward

The receipt and bedside lists also appeared only after the first action. A nurse opening the first unit of a cooler could not see what else was in it (question 5).

### Design

- **Demo patient MRN0022 (Demo-Multi, Riley), O+, ICU 4:**
  - type and screen verified; historical and current type, so second ABO is on file
  - a red-cell order
  - six leukoreduced red cells with verified label retypes, each reserved and crossmatched compatible on today's specimen
  - units 1–2: reserved (Ready to issue)
  - units 3–4: issued in "Cooler M2", in transit
  - units 5–6: issued in "Cooler M1", received on the ward
  - its own units 9201–9206; seeded once by MRN, like the other scenarios
- **Showing what comes next earlier:**
  - opening a unit on Ward receipt shows "Also in Cooler M2" with Receive next before anything is received
  - opening a unit on the transfusion desk shows the patient's other units at the bedside before the first start
- **Wording:** cooler labels that already say "Cooler" are not doubled ("Also in Cooler M2", not "cooler Cooler M2").
- Moving to the next bedside unit keeps the identifiers when the next unit belongs to the patient on the form, also before the first action.

### Tests

- The seeder tests count 12 demo patients, 13 encounters, 18 orders, and 63 units. They assert the scenario: 2 reserved, 2 in transit in Cooler M2, 2 received and still issued.
- Full suite: 3,692 passed, 0 failed.
- Run check (development API and web; scenario seeded on start):
  - Ready to issue → Issue on MRN0022 shows "Issue next: W0001230009202"
  - In transit → Receive on W…9203 shows "Also in Cooler M2", "Receive next: W…9204"
  - Issued units → Start / document on W…9205 shows "Start next: W…9206"
  - clicking it moved the form to W…9206, kept the typed MRN, and offered W…9205
  - nothing was issued, received, or transfused
  - no server errors

### Next ranked residual (workflow lens)

Scan-to-select on these desks: scanning a unit's DIN on Issue, Ward receipt, or Transfusion, without a selected unit, picks it from the board and fills the form (question 4).

## Iteration 381 — Scan and Enter selects; a scanned unit brings its patient (2026-11-04)

### Capability matrix rescan (workflow lens, question 4)

- Ward receipt and Transfusion already took a typed or scanned unit number and selected the issue.
- Ready to issue rows filled the Issue form.
- On the Issue form, picking a unit left the patient blank, though the reservation names it.
- Live, a scanned DIN and its Enter did not select anything: the pick list committed only when focus left the field, and Enter landed on the "—" clear row.

### Design

- **`FilterableComboBox`, shared by every pick list:** Enter or NumpadEnter commits the typed text the same way leaving the field does, an exact match or the only partial match, and closes the list. A scanner's trailing Enter now selects the unit or patient. The enter-to-next-field script is untouched; it applies only to `data-enter-step` inputs.
- **Issue form unit picked or scanned:** `IssueRun.ForPickedUnit` checks Ready to issue:
  - reserved for the patient on the form, or the form has no patient: fill the patient, as the board's Issue does. Identifiers already typed for that same patient stay.
  - reserved for another patient: leave the form and say "Unit … is reserved for Name (MRN), not the patient on this form." The gate still refuses it.
  - not reserved: nothing changes, for example emergency release.
- **Patient changed after the unit:** the notice is re-checked.
- Downtime back-entry is left alone.

### Requirements

- FRS-BB-557 (under URS-BB-494). Help: issuing.

### Tests

- TEST-BB-419: fills for no patient and the same patient; names the other patient; nothing for an unreserved or empty unit.
- Full suite: 3,693 passed, 0 failed.
- Run check (development API and web, demo patient MRN0022):
  - on the empty Issue form, typing W0001230009201 and pressing Enter selected the unit, filled "Demo-Multi, Riley (MRN0022)", and offered the next ready unit
  - choosing MRN0001 by typing and Enter showed "Unit W0001230009201 is reserved for Demo-Multi, Riley (MRN0022), not the patient on this form."
  - nothing was issued
  - no server errors

### Next ranked residual (workflow lens)

Search everywhere: the header search takes a scanned DIN or MRN; check that a DIN lands on the unit page and a reserved unit offers Issue from there.

## Iteration 382 — The unit page shows its reservation and issues from there (2026-11-04)

### Capability matrix rescan (workflow lens: search everywhere, next step)

- A DIN scanned in the header search lands on the unit page (confirmed live).
- That page showed "Crossmatched" but not for whom, and offered no way to issue it. The tech had to go to Issue & Transfuse, find the row, and re-select it (questions 1, 2, 5).
- The issue desk already accepted `?bloodUnitId=` but left the patient empty.

### Design

- **Unit page:** a banner under the header when the unit is on Ready to issue: "Reserved for Name (MRN) · crossmatch", with Open chart · Products.
  - When routine issue is possible and the user may issue: **Issue** (`/issuing?desk=issue&bloodUnitId=`).
  - Otherwise: a warning naming what stops routine issue, with a link to Ready to issue.
- **`IssueRun.RoutineIssueBlocks`** gives the blocks in the board's words: specimen, outdated unit, special requirement, ABO/Rh discrepancy, second ABO, crossmatch. `OffersRoutineIssue` is now defined as no blocks, so the board, the runs, and the unit page cannot disagree.
- **Issue desk opened with a unit and no patient** uses the reservation to fill the patient, the same path as a scan on the form (Cycle 381).
- No server change. The reservation comes from the Ready to issue list the user can already see (issue or allocate permission).

### Requirements

- FRS-BB-558 (under URS-BB-494). Help: unit detail.

### Tests

- TEST-BB-420: no blocks for a ready row; second ABO and crossmatch named in order; consistent with `OffersRoutineIssue`.
- Full suite: 3,694 passed, 0 failed.
- Run check (development API and web):
  - header search W0001230009201 opened the unit page with "Reserved for Demo-Multi, Riley (MRN0022) · Crossmatch compatible · Crossmatch expires …", Open chart, and Issue
  - Issue opened the desk with the unit and patient filled and the next unit offered
  - W000123UAG0001 showed a warning: "Before routine issue: … second concordant ABO/Rh … A compatible, unexpired crossmatch is required …", with Ready to issue
  - the first version named the unit expiration line instead of the block; this was found live and corrected with `RoutineIssueBlocks`
  - nothing was issued
  - no server errors

### Next ranked residual (workflow lens)

Return the issued unit from its page the same way: an issued unit's page shows the issue (patient, destination, time) and offers Return, Ward receipt, or Transfusion as its next step.

## Iteration 383 — An issued unit's page shows where it went and its next step (2026-11-04)

### Capability matrix rescan (workflow lens: search everywhere, next step)

- A unit out of the bank, scanned in the header search, showed its status ("Issued") but not:
  - who it went to
  - whether the ward had it
  - what to do next
- The page had a Document transfusion link buried under Actions.
- The issue desk opened only the transfusion form from a link; there was no link to receipt or return.

### Finding (also question 6)

The live check found a demo transfusion started without a recorded ward receipt (Patricia, W0001230000099). Issued units offered only Receive for it, never Complete: the board offered Start/Complete only after a receipt. A running transfusion could not be completed from the board.

### Design

- `IssueRun.NextStepForIssued` gives the order:
  - a running transfusion → Complete, even without a recorded receipt
  - else not received → Receive on ward
  - else → Start / document
- `CanReturn` is true until the transfusion starts.
- **Unit page:** a banner when the unit is issued or transfusing: issued to Name (MRN), destination, time and issuer, cooler. Then where it stands (in transit / received at a time by someone / transfusing since a time, with completion overdue flagged), Open chart, the next step, and Return when allowed.
- **Issue & Transfuse:** `?desk=receipt&issueId=` and `?desk=return&issueId=` open that issue's form, as `desk=transfuse` already did. A message is shown if the issue is no longer in that state.
- **Issued units board:** uses the same rule. Receive shows only before anything has started; Start/Complete shows otherwise.
- No server change. Completing a transfusion has no receipt check.

### Requirements

- FRS-BB-559 (under URS-BB-494). Help: unit detail.

### Tests

- TEST-BB-421:
  - in transit → Receive; received → Start; running → Complete
  - started without a receipt → Complete
  - Return is offered until the transfusion starts
- Full suite: 3,695 passed, 0 failed.
- Run check (development API and web):
  - W…9203 (Cooler M2): "In transit…", Receive on ward and Return
  - W…9205: "Received on the ward 20:28 by RN Demo; transfusion not started", Start / document and Return
  - W…0099 (Patricia): "Transfusing since 18:15", Complete transfusion. The first version offered Receive; this was found live and fixed.
  - the receipt link opened W…9203 with "Also in Cooler M2"
  - the return link opened W…9205
  - the transfuse link opened the running transfusion with Complete
  - nothing was saved
  - no server errors

### Next ranked residual (workflow lens)

Patient timeline: check that the chart timeline shows each unit's issue, receipt, start, and completion in order, and links to the unit page.

## Iteration 384 — Timelines show the crossmatch result and the ward receipt (2026-11-04)

### Capability matrix rescan (workflow lens: timelines)

The MRN0022 timeline, checked live, lists reservation, crossmatch, issue, and order events with links to each unit.

### Finding (question 6)

- Every crossmatch read "NotPerformed", though stored and shown elsewhere as Compatible:
  - the timelines read `Crossmatch.ClinicalStatus`
  - `RecordCrossmatchAsync` never set it (only a modification copy did), so every crossmatch recorded through the application, compatible or incompatible, showed NotPerformed on the patient and specimen timelines
  - an incompatible crossmatch was not marked critical there
  - issue, ready to issue, and selection read `Result`, so no unit was issued on the wrong basis; the gap was in the record a reviewer reads (RISK-BB-590)
- Ward receipt had no timeline event: issue appeared, then transfusion, with nothing between.

### Design

- `CrossmatchValidityRule.ClinicalStatusFor(result)` and `Shown(result, clinicalStatus)`: a recorded Compatible or Incompatible result is shown; otherwise the clinical status (NotPerformed, emergency uncrossmatched).
- **Timelines:** both use `Shown`, so existing rows display correctly without a data change. Incompatible is critical.
- **`RecordCrossmatchAsync`:** sets `ClinicalStatus` from the result from now on.
- **Patient timeline:** a "Unit … received on the ward" event at the receipt time, with destination, who received it, and the appearance when not acceptable, linked to the unit.

### Requirements

- FRS-BB-560 (under URS-BB-494), RISK-BB-590. Help: patient timeline.

### Tests

- TEST-BB-422:
  - domain: result wins; otherwise the clinical status
  - timeline: a result-only Compatible and Incompatible show as such, with Incompatible critical, and the ward receipt event has its time, actor, and destination
  - recording a compatible crossmatch sets the clinical status Compatible
- Full suite: 3,697 passed, 0 failed.
- Run check (development API and web, MRN0022 timeline):
  - before: "Crossmatch W…9201: NotPerformed", and no receipt rows
  - after: "Crossmatch W…9201: Compatible", and "Unit W…9205 received on the ward · At ICU 4 · by RN Demo" at 20:28 (and W…9206 at 20:29)
  - no server errors

### Next ranked residual

Other readers of `Crossmatch.ClinicalStatus`: none besides the timelines and the modification copy, which now copies a set value. A one-time backfill of the stored column is optional, since display no longer depends on it.

## Iteration 385 — Near-expiry units on the home screen (2026-11-04)

### Capability matrix rescan (operations lens)

Operations lists near-expiry products. The home screen showed every other operational queue (ready, second ABO, retrospective, in transit, issued, disposition, tests, specimens, reactions, supervisor, temperature, interfaces, handoff) but not near expiry. Units close to outdating were seen only by someone who opened Inventory and chose the Near expiry board (questions 2, 5). A reserved unit about to expire, which needs issuing or a replacement, was flagged only on Ready to issue.

### Design

- **`DashboardQueueCatalog.NearExpiry`:** the card is shown to users who can receive or discard inventory, issue, or allocate. It counts the Near expiry board.
- **`NearExpiryNote`:** "N reserved for a patient/patients" when some near-expiry units are reserved. That makes the card Needs attention, ranked after shift handoff and before final disposition. With no reserved units it is an ordinary queue.
- **Focus:** Issue desk and Supervisor.
- **Inventory:** the page opens any of its boards from `?board=` (before: only retype and quarantine), so the card lands on Near expiry.
- No server change: the count comes from the existing near-expiry endpoint.

### Requirements

- URS-BB-496, FRS-BB-561. Help: dashboard.

### Tests

- TEST-BB-423:
  - the card is shown for each of the four permissions and not for result entry alone
  - its link opens the board
  - the note is silent at zero and names a count
  - it is a queue on the Issue desk focus and in the other desks on Bench
  - a reserved note puts it first under Needs attention ahead of final disposition
- Full suite: 3,699 passed, 0 failed.
- Run check (development API and web):
  - the dashboard shows "Near expiry 1", matching the API (1 unit, none reserved)
  - `/inventory?board=near` opens with Near expiry selected
  - no server errors
  - the reserved note was not exercised live (no reserved unit is near expiry in the demo data); the unit test covers it

### Next ranked residual (operations lens)

The Near expiry board could offer the next step for a reserved unit (Issue, from Cycle 382's reservation banner) and, for an unreserved one, a compatible patient with an open order (find a use before it outdates).

## Iteration 386 — Near-expiry rows offer their next step (2026-11-05)

### Capability matrix rescan (operations lens)

The Near expiry board (now on the home screen, Cycle 385) listed units with Discard and Open. A reserved unit said "issue it first", but the tech still had to go to Issue & Transfuse and find it. An unreserved unit gave no hint whether any open order could use it before it outdated (questions 1, 5).

### Design

- `IssueRun.ForNearExpiry(reserved, readyRow, openOrdersForProduct)`:
  - **Issue** for a reserved unit that Ready to issue would offer for routine issue
  - **Allocate** for an unreserved unit whose product has open orders
  - otherwise nothing
- **Board:**
  - Issue (users who can issue) opens `/issuing?desk=issue&bloodUnitId=`, which fills the reserved patient (Cycle 382)
  - "N open orders for this product" (users who can allocate) opens the unit's Actions tab, which lists the open orders and allocates through the compatibility gate. The count is by product, and the link says compatibility is checked when allocating.
- **Data:** Ready to issue is loaded once when a reserved unit is on the board. Open orders are loaded once per product among unreserved rows. No server change.

### Requirements

- FRS-BB-562 (under URS-BB-496). Help: inventory.

### Tests

- TEST-BB-424:
  - a reserved, ready unit → Issue
  - a reserved unit that is blocked (second ABO) or not on Ready to issue → nothing
  - an unreserved unit with open orders → Allocate; without → nothing
- Full suite: 3,700 passed, 0 failed.
- Run check (development API and web):
  - the demo's one near-expiry unit (washed red cells, unreserved) has no open orders for its product (API: 0), so the row shows Discard and Open only, as designed
  - Issue and Allocate were not exercised live (no such unit in the demo data); the helper test covers them
  - no server errors

### Next ranked residual (operations lens)

Rescan for the next gap: assigned products (units allocated to patients with no recent activity), or minimum stock against open orders.

## Iteration 387 — Reservations past their end time, released from Ready to issue (2026-11-05)

### Capability matrix rescan (operations lens: assigned products)

Every reservation has an end time (`Allocation.ExpiresUtc`, set at allocation, for example 24 hours). Nothing used it:
- Reservations ended only when the unit itself expired.
- Ready to issue did not show when a reservation was meant to end.
- No list showed units held past it.

Release existed only on the patient chart, one patient at a time. Mature systems give the bench a list of units held past their reservation to put back in stock (questions 2, 5).

### Design

- **Ready to issue rows:** they carry `ReservationExpiresUtc`. A row at or past it shows **Reservation ended** with the time. For users who can allocate it offers **Release**: an inline reason, then the existing `ReleaseAllocationAsync`, which is audited and returns the unit to Available. The board says what was released.
- **Nothing automatic:** release stays a person's decision with a reason. Automatic release would be facility policy, not decided here.
- **Dashboard:** the Ready to issue card adds "N past their reservation, to issue or release", which puts it under Needs attention.
- **Cleanup:** `ReadyToIssueAttentionRule.CanIssueRoutinely` now calls `IssueRun.OffersRoutineIssue`, so there is one definition of routine issue.

### Requirements

- URS-BB-497, FRS-BB-563. Help: issuing, dashboard.

### Tests

- TEST-BB-425:
  - rule: past and exactly-at count; future and none do not; the card note names the count; the existing routine-issue checks are unchanged
  - integration: a lapsed reservation is still on Ready to issue with its end time; release without a reason is refused; with one the unit is Available and leaves the list
- Full suite: 3,702 passed, 0 failed.
- Run check (development API and web):
  - the five demo reservations end tonight (23:30 and 02:22 UTC), so none showed Reservation ended or Release, and the Ready card read 5 with no note, as expected
  - the badge and release path were not exercised live; the integration test covers them
  - no server errors

### Next ranked residual (operations lens)

Minimum stock against open orders: compare on-hand units by product and type with minimum stock and today's open product orders.

## Iteration 388 — Open product orders beside minimum stock (2026-11-05)

### Capability matrix rescan (operations lens)

Minimum stock compared available units with a minimum, but did not show demand. A product could be at its minimum while more patients' orders waited for it than there were units on the shelf. The tech had to remember, or count, open orders on Compatibility (question 2).

### Design

- **`MinimumStockService`** counts open product order lines per product:
  - the line is active, a product line, Ordered or partly filled
  - its order is not completed, cancelled, or discontinued
- **Rows** carry `OpenOrders`. Orders have no quantity, so each line counts as one unit, at least.
- **`MinimumStockRule.OpenOrdersExceedOnHand`:** more open orders than units on hand. The page shows an Open orders column and a "More orders than on hand" badge.
- **The count is by product:** an ABO-specific row compares that group's units with orders of any ABO, and the help says so. It is a count, not a projection. Nothing is ordered automatically.
- **Demo data:** four starter minimums (group O and A leukoreduced red cells, plasma, apheresis platelets) are seeded when none exist. The page had no rows before. As a result, the existing below-minimum alert fires for group A red cells in the demo.

### Requirements

- URS-BB-498, FRS-BB-564. Help: minimum stock.

### Tests

- TEST-BB-426:
  - rule: more orders than units only
  - integration: two open lines count, while a filled line, a cancelled line, a line on a completed order, another product, and a test line do not; with 1 on hand the row is flagged, with no shortfall against a minimum of 1
- Full suite: 3,704 passed.
  - The first run of the new integration test failed on a foreign key (orders need an encounter and an ordering location). That was the test's setup, fixed.
- Run check (development API and web):
  - seeding added four rows
  - red cells show 4 open orders (ORD0019 and ORD0023 lines and others)
  - group A (2 on hand, minimum 4) shows short 2 and "More orders than on hand"
  - group O (6 on hand) is not flagged
  - plasma and platelets have none
  - the Alerts count rose by one: the below-minimum alert for group A red cells
  - no server errors

### Next ranked residual (operations lens)

Supplier orders could start from the shortfall, including the open-order count, so the order quantity is suggested rather than remembered.

## Iteration 389 — Reserve several units for one order (2026-11-05)

### Capability matrix rescan (workflow lens)

- Supplier orders already start from the minimum stock shortfall with suggested quantities. Folding open orders into those quantities would be an ordering policy, so it was not done.
- Reserving units for one product order (for example two red cells) took a full pass per unit: after each reservation the Allocate panel closed. The tech reopened it and chose the visit, order, and specimen again (questions 1, 2). Issue, receipt, and bedside already have runs (Cycles 376–379); allocation did not.

### Design

After a successful reservation the panel stays open:
- **Kept:** the order, visit, and specimen.
- **Cleared:** the unit and any antigen override reason and authorizer.
- **Refreshed:** the compatible units, so the reserved one is gone.
- **Shown:** "Reserved now for this order" with the unit numbers, a **Reserve next unit** button, and **Done**.
- **Reset:** changing the order or closing the panel starts a new list.

Every unit goes through `AllocateAsync` and its checks as before. The auto-thaw question still appears after a reservation when it applies. No server change.

### Requirements

- FRS-BB-565 (under URS-BB-494). Help: patient products.

### Tests

- No automated test: the change is page state in the chart, with no new rule. Full suite: 3,704 passed, 0 failed.
- Run check (development API and web, MRN0022):
  - Allocate unit opens the panel with ORD0023 and "Reserve unit", with no list yet
  - no server errors
  - not exercised live: a successful reservation and the "Reserved now" list. MRN0022's red-cell order has no linked type-and-screen specimen, so allocation would be refused, and other paths would change demo stock.

### Next ranked residual (workflow lens)

Give the MRN0022 product order a linked type-and-screen specimen in the demo data, so allocation runs can be shown, or add a UI test harness for chart state.

## Iteration 390 — Demo data for reserving several units, and the Cycle 389 run checked live (2026-11-05)

### Finding

Cycle 389's reserve run could not be shown:
- MRN0022's red-cell order (ORD0023) had no linked type-and-screen specimen, so the server refused allocation.
- The available leukoreduced red cells in the demo stock were not retyped, which that product requires. They were correctly left off the compatible list, and they are the Retype board's demonstration units.

### Design (demo data only)

- **New databases:** ORD0023 is linked to the ACC0022 tube.
- **Two of the scenario's own O+ leukoreduced red cells** (W0001230009207, …9208) are added, Available, retyped, in the main refrigerator.
- **Existing databases:** `EnsureMultiUnitOrderSpecimenAsync` adds the link and the two units once. It is idempotent by order link and unit number.
- The shared stock is unchanged, so the Retype board keeps its units.

### Tests

- The seeder tests count 65 units and assert that ORD0023 has a specimen link.
- Full suite: 3,704 passed, 0 failed.
- Run check (development API and web, MRN0022, Products → Allocate unit):
  - ORD0023 (ACC0022) chosen, W0001230009207 typed and selected with Enter, Reserve unit
  - "Unit allocated; XM attached to the product order."
  - the panel stayed open on ORD0023 with "Reserved now for this order: W0001230009207", Reserve next unit, and Done
  - W0001230009208 typed, Reserve next unit → "Reserved now for this order: W0001230009207, W0001230009208"
  - both reservations were then released through the release endpoint with a reason, and both units are Available and offered again. The XM actions added to ORD0023 remain, as recorded work.
  - no server errors
- Cycle 389's requirement and traceability rows now note the live check.

### Next ranked residual (workflow lens)

Rescan the capability matrix for the next workflow gap (testing desk: result entry and verification clicks).

## Iteration 391 — Entered results say Verify on the bench (2026-11-05)

### Capability matrix rescan (testing desk, live)

On the test worklist, a DAT entered manually by tech1 and waiting since the evening (Febrile, Marcus) read "Entered · Negative · Source: Manual", with an **Enter** button. Untested lines read Enter too, while an interface value read Verify. The second technologist doing verification could not tell the rows waiting for them from the untouched ones without opening each (questions 1, 2, 5). Inside, the verify prompt called the manual value "the interface or instrument value".

### Design

- **`TestWorkItemDto.AwaitsVerification`:** a result on the line that is Entered, PendingVerification, or Corrected.
- **`BenchActionLabel`:**
  - Verify for those, from any source
  - Enter for none
  - Blocked when entry is blocked
- **`BenchActionLabelFor(user)`:** Review for the enterer of an ABO/Rh they may not verify. The worklist and the chart Tests tab use it.
- **`AwaitingValueOrigin`:** "The value entered by tech1" / "The instrument value" / "The interface value". The panel button reads Verify entered result or Verify posted result.
- Verification rules (self-verify, holds, expiry, deltas) are unchanged.

### Requirements

- URS-BB-499, FRS-BB-566. Help: test worklist.

### Tests

- TEST-BB-427:
  - Enter for an empty line; Verify for a manual Entered or an interface PendingVerification result
  - Enter for a Verified result; Blocked when blocked
  - Review for the ABO/Rh enterer, Verify for others; the origin text
- The existing interface-value and Blocked worklist tests are unchanged and pass.
- Full suite: 3,706 passed, 0 failed.
- Run check (development API and web):
  - the MRN0006 DAT row now reads Verify
  - opened, it shows "Verify entered result" and "The value entered by tech1 is already on the order."
  - not pressed, so nothing changed
  - no server errors

### Next ranked residual (testing desk)

A Verify filter or sort on the worklist ("waiting for me to verify"), and bulk verify of plain negative screens by a second technologist, if the medical director allows it (an OCD question before building).

## Iteration 392 — To verify filter on the test worklist (2026-11-05)

### Capability matrix rescan (testing desk)

With Cycle 391 the bench says Verify on waiting results, but the verifying technologist still scanned every line of a mixed list (pending entry, blocked, waiting, their own ABO/Rh) to find their work (questions 1, 5).

### Design

- **`TestWorklistView.ToVerify(items, user)`:** lines with a result waiting for verification, entry not blocked, and not an ABO/Rh this user entered, in the list's order.
- **Patient tests, for users who can verify:** **All (n)** and **To verify (n)** buttons.
  - an empty To verify list says so, and that the user's own results are verified by someone else
  - `/test-worklist?show=verify` opens on To verify
- **aria-pressed:** rendered as "true"/"false" strings. The first build used booleans, which Blazor rendered as an empty or missing attribute; found live and fixed.
- No server change. The worklist content is unchanged.

### Requirements

- FRS-BB-567 (under URS-BB-499). Help: test worklist.

### Tests

- TEST-BB-428: for tech2, a colleague's DAT, tech1's ABO/Rh, and an instrument crossmatch are kept in order. An empty line, a blocked line, and tech2's own ABO/Rh are left out.
- Full suite: 3,707 passed, 0 failed.
- Run check (development API and web):
  - `?show=verify` opens with "To verify (2)" pressed, out of "All (8)"
  - the rows are MRN0009 (interface antibody screen) and MRN0006 (manual DAT), both Verify
  - All restores 8 rows, and aria-pressed flips true/false
  - nothing was verified
  - no server errors

### Next ranked residual (testing desk)

Bulk verification of plain negative results by a second technologist needs a medical director decision first. Next is to rescan the other desks.

## Iteration 393 — The shift handoff carries the new queues (2026-11-05)

### Capability matrix rescan (operations: shift handoff)

Cycles 385–392 made three operational states visible on the desks. The shift handoff, the packet the next shift acknowledges, counted none of them:
- results entered and waiting for a second person to verify
- reservations past their end time
- units near expiry, and how many are reserved

The incoming shift would learn about them only by opening each desk (question 2).

### Design

- **`ShiftHandoff`:** gains `ResultsAwaitingVerification`, `ReservationsLapsed`, `NearExpiryUnits`, `NearExpiryReserved`. They come from:
  - the pending test list (`AwaitsVerification`)
  - Ready to issue (`ReservationLapsed`)
  - the Near expiry board
  They are recorded in the audit snapshot.
- **`ShiftHandoffService`:** takes `InventoryService` (optional) for the near-expiry count.
- **Schema:** migration `20261113000000_ShiftHandoffVerifyReserveExpiry` and the bootstrap columns.
- **`ShiftHandoffDto.QueueSummary`:** the one-line summary moved from the page so it can be tested. It adds "N to verify", "N past reservation", and "near expiry N, M reserved", each only when above zero.

### Requirements

- URS-BB-500, FRS-BB-568, SRS-BB-455. Help: shift handoff.

### Tests

- TEST-BB-429:
  - the summary is silent at zero and names each count
  - a handoff with an entered result counts it as waiting for verification, and the line says "to verify"
  - a handoff recorded while a reservation is past its end counts it
- Full suite: 3,708 passed, 0 failed.
- Run check (development API and web):
  - the bootstrap added the four columns (the handoff query reads them)
  - `api/shift-handoffs` and the Shift handoff page load with the new dependency
  - no server errors
  - no handoff was recorded, to keep the demo free of an unacknowledged handoff; the counts are covered by the integration tests

### Next ranked residual

Rescan: downtime snapshot parity with the handoff (results to verify, lapsed reservations), or the specimen desk.

## Iteration 394 — Printable downtime patient summary (2026-11-05)

### Capability matrix rescan (downtime)

The planned next step was snapshot parity with the handoff. The rescan found a larger gap. Downtime support covered only recovery:
- the snapshot counts queues after the outage
- back-entry from paper issue records

There was nothing to work *from* during the outage. Mature transfusion systems print a downtime patient report so the bench can issue from paper with the patient's type, antibodies, and requirements in hand. Without one, during an outage the technologist must remember or phone for (question 2):
- the patient's history
- whether a second ABO/Rh is on file
- which units are already reserved for them

This is a transfusion safety risk (question 6).

### Design

- **`DowntimePatientSummaryService`:** lists every patient with a specimen not yet expired (and not rejected or cancelled), a reserved unit, or an issued unit still out. Merged-away patients are left out. For each patient:
  - current ABO/Rh, and whether a second concordant determination is on file (the rule issue uses)
  - active antibodies
  - special requirements in effect, with informational notes separate
  - current specimens with expiration
  - reserved and issued units with product, ABO/Rh, unit expiry, reservation end, and issue location
- **`/downtime/patients`:** permission *issue.create*, the people who would issue from paper. Linked from Downtime Snapshot. The sheet prints landscape and carries the time it was generated.
- **Print:** audited before the browser dialog opens (Print event with the patient ids), as with the MTP pack report.
- No schema change and no clinical rule. The sheet says it does not replace the facility's downtime procedure.

### Requirements

- URS-BB-501, FRS-BB-569, SRS-BB-456. Help: downtime (new section, route added).

### Tests

- TEST-BB-430:
  - inclusion: current specimen, reserved unit, issued unit
  - exclusion: expired or rejected specimen only, transfused unit only
  - row content: type and second determination (with and without), active antibody only, ended requirement left out, informational note, units in order with reservation end and location
  - the print is audited with the patient id
- Full suite: 3,710 passed, 0 failed.
- Run check (development API and web):
  - the Downtime Snapshot link opens /downtime/patients
  - it lists 12 demo patients, for example MRN0004 with anti-K, MRN0022 with two reserved and four issued units at ICU 4, and patients without a second determination flagged
  - Print was not pressed, to keep the audit trail free of a test print; the audited print is covered by TEST-BB-430
  - no server errors

### Next ranked residual

Downtime snapshot parity with the handoff (results to verify, lapsed reservations), or the specimen desk.

## Iteration 395 — Downtime snapshot: results to verify and lapsed reservations (2026-11-05)

### Capability matrix rescan (downtime recovery)

After an outage, results are entered from paper and wait for a second person to verify them. Reservations also run past their end while nobody can work them. The shift handoff counts both (Cycle 393); the downtime recovery checklist counted neither. The supervisor had to open each desk to know whether recovery was finished (questions 2, 5).

### Design

- **`DowntimeReconciliationSnapshot`:** gains `ResultsAwaitingVerification`, `ResultsVerifyBeforeExpiry`, and `ReservationsLapsed`. They use the same rules as the handoff.
- **`DowntimeReconciliationService`:** takes the test worklist and issuing services (optional; without them the counts are zero).
- **Downtime Snapshot page:** two rows, linking to To verify and Ready to issue.
- **Help:** the recovery steps name both.
- No schema change and no new rule.

### Requirements

- URS-BB-502, FRS-BB-570.

### Tests

- TEST-BB-431:
  - an entered result due before its specimen expires is counted in both numbers
  - without the worklist service the count stays zero
  - a reservation past its end is counted until it is released
- The existing downtime snapshot tests are unchanged and pass.
- Full suite: 3,711 passed, 0 failed.
- Run check (development API and web):
  - Downtime Snapshot shows "Results entered and waiting for verification 2", linking to /test-worklist?show=verify (the same two the To verify filter lists)
  - it shows "Reservations past their end time 0", linking to /issuing?desk=ready
  - no server errors

### Next ranked residual

The specimen desk (pending receipt and collection lists, scan to receive), or patient-summary access from the chart for a single patient.

## Iteration 396 — Scan a tube to work its tests (2026-11-05)

### Capability matrix rescan (specimen desk to bench)

Receipt and acceptance already work by scan. After acceptance the trail stopped:
- the tech switched to Patient tests and searched a mixed list of every patient's lines for the tube in hand (questions 1, 2, 4)
- opening each test took a click
- after saving, the "next" row could belong to another patient's tube

Commercial bench systems start testing from the tube's barcode.

### Design

- **`TestWorklistView`:**
  - `NormalizeTube`
  - `ForTube`: exact accession match, ignoring case and spaces
  - `FirstToWork`: not blocked, specimen linked
- **Patient tests:**
  - a tube scan field opens the tube's first workable test
  - a "Tube X: n tests" badge with Show all tubes
  - combines with To verify
  - an empty tube says where to look
  - `?accession=` link
- **Next row after saving:** follows the rows as shown, so it stays on the tube.
- **Specimens tab:** after an accept (scan or button), it says how many tests wait on the tube, with Start testing.
- No server change. Result entry and its checks are unchanged. Matching is on the accession number only; a tube's label barcode is not in the worklist row.

### Requirements

- URS-BB-503, FRS-BB-571. Help: test worklist.

### Tests

- TEST-BB-432:
  - the scanned tube keeps its lines in order; another tube with a longer accession is not matched
  - the first workable line skips a blocked one
  - blank shows all; an unknown tube is empty with nothing to open
- Full suite: 3,712 passed, 0 failed.
- Run check (development API and web):
  - scanning "acc0010" showed "Tube ACC0010: 2 tests" with ABO/Rh opened for entry
  - ACC9999 showed the empty-tube message
  - `?accession=acc0017` opened on the two ACC0017 tests
  - no server errors; nothing was saved
  - not exercised live: the Start testing prompt after accepting a tube, which would accept a demo tube

### Next ranked residual

Match a tube's label barcode as well as its accession on the bench (needs the barcode in the worklist row), or the chart's single-patient downtime print.

## Iteration 397 — The bench tube scan reads the label too (2026-11-05)

### Capability matrix rescan (bench, live)

The Cycle 396 tube scan matched only the accession number. Demo tubes carry a printed label barcode (for example SPC-ACC0017), and receipt by scan already accepts either. Scanning the label at the bench therefore found nothing, so the tech had to type the accession (question 4).

The run check also found an app-wide display bug. Bootstrap's `.btn:hover` text color outranks the app's button variants, so a hovered secondary button (**To verify**, **Refresh**, tabs) showed white text on a light background.

### Design

- **`TestWorkItemDto.SpecimenBarcode`:** filled from the specimen.
- **`TestWorklistView.ForTube`:** uses the receipt scan rule's cleaning (scanner noise removed) and match (accession or label).
- **`TubeLabel`:** names the tube by its accession after a label scan.
- **Scan field placeholder:** "Scan the label or type the accession".
- **`app.css`:** each button variant keeps its own text color when hovered, pressed, or focused.
- No schema change.

### Requirements

- FRS-BB-572 (under URS-BB-503). Help: test worklist.

### Tests

- TEST-BB-433:
  - the label scan finds both lines of the tube (case and stray carriage return ignored) and the tube is named ACC1
  - a line without a barcode is still found by accession
  - an unknown label finds nothing and names itself
  - integration: a worklist row carries the specimen's barcode and a lower-case label scan finds it
- Full suite: 3,714 passed, 0 failed.
- Run check (development API and web):
  - the API rows carry SPC-… labels
  - scanning "spc-acc0017" showed "Tube ACC0017: 2 tests" with ABO/Rh opened
  - after the CSS fix, a hovered To verify button reads dark text (rgb 28,36,51) instead of white
  - no server errors; nothing saved

### Next ranked residual

The chart's single-patient downtime print, or a role-based home for the specimen processor.

## Iteration 398 — One patient's summary from the chart (2026-11-05)

### Capability matrix rescan (chart, downtime)

The Cycle 394 downtime summary prints everyone the blood bank is working for. A single patient's blood bank summary, for example before a planned outage, a procedure, or a transfer, still meant:
- printing the whole list, carrying other patients' data off the system needlessly
- or copying from the chart by hand (questions 1, 2)

A patient with history but nothing current (an antibody, a special requirement, no specimen) was not on the sheet at all.

### Design

- **`DowntimePatientSummaryService.GetAsync(patientId)`:** that patient only, listed even with nothing current. An id that is no patient, or a merged-away record, gives an empty sheet, never everyone.
- **`RecordPrintedAsync(patientId)`:** audits the print against the patient.
- **API:** `?patientId=` on both endpoints.
- **Chart header:** **Print summary** (*issue.create*; hidden on merged-away records).
- **Page:**
  - a one-patient title and text, and **Back to chart**
  - the empty case explains itself and Print is disabled
  - the breadcrumb shows Patients / Chart
- No schema change.

### Requirements

- FRS-BB-573 (under URS-BB-501). Help: downtime.

### Tests

- TEST-BB-434:
  - a patient with an antibody and nothing current is on their own sheet, with "Not on file" type
  - another patient's sheet carries only their specimen
  - an unknown id gives an empty sheet
  - the print is audited with the patient as EntityId
- Full suite: 3,715 passed, 0 failed.
- Run check (development API and web):
  - Kellerman (MRN0004) chart → Print summary opened `/downtime/patients?patientId=4` with one row: O+, anti-K, ACC0012
  - Back to chart → /patients/4
  - patientId=99999 shows the not-found message with Print disabled
  - Print not pressed (no test audit row)
  - no server errors

### Next ranked residual

A role-based home for the specimen processor (receipt queue first), or patient-level print from the Issue desk.

## Iteration 399 — Results to verify on the home screen (2026-11-05)

### Capability matrix rescan (role-based home)

Desks already exist (Bench, Issue desk, Supervisor). The home screen counted pending tests, but not the work a verifier is waiting to do. Results entered and waiting for a second person were visible only after opening the worklist and pressing To verify (Cycle 392), so the verifying technologist had to remember to look (questions 2, 5).

### Design

- **`DashboardQueueCatalog.ToVerify`:** a **Results to verify** card for *result.verify*, opening `/test-worklist?show=verify`.
- **`ToVerifyCard(pending, user)`:** counts the To verify view for this user. Only ABO/Rh is barred from self-verification, as on the worklist. It names how many are due before their specimen expires, which brings the card under Needs attention.
- **Desks:** in Bench and Supervisor focus.
- **`Home.razor`:** the pending-test worklist is fetched once for both cards.
- No server change.

### Requirements

- URS-BB-504, FRS-BB-574. Help: dashboard.

### Tests

- TEST-BB-435:
  - the card is offered for result.verify only, and is in Bench and Supervisor focus but not Issue desk
  - tech2 counts tech1's two entries (one due before expiry) but not their own ABO/Rh
  - tech1 counts their own DAT and screen and tech2's ABO/Rh
  - an empty line gives zero and no note
- Full suite: 3,716 passed, 0 failed.
- Run check (development API and web):
  - the home screen shows "Results to verify 2" linking to /test-worklist?show=verify (the same two as the worklist's To verify), beside Pending tests 7 and Pending specimens 2
  - no server errors

### Next ranked residual

Patient-level summary print from the Issue desk, or keyboard shortcuts for the bench (next/previous test).

## Iteration 400 — Blood bank flags on the patient list (2026-11-05)

### Capability matrix rescan (patient lookup)

- **Already covered:**
  - unlinked orders already show on the bench with an Accession step
  - the Issue desk already shows informational requirements
- **Gap:** the patient directory showed only MRN, name, DOB, sex, and status. A technologist choosing between similar names, or checking a patient before accepting a call, had to open each chart to see blood type and alerts (questions 1, 2). Commercial transfusion systems show type and alert flags on lookup.

### Design

- **`PatientDto`:** gains `CurrentBloodType`, `ActiveAntibodies`, `ActiveSpecialRequirements`, filled by `PatientService.ListDtosAsync` from:
  - current, not invalidated, types
  - active antibodies (distinct, sorted)
  - special requirements clinically in effect now (`SpecialRequirementCatalog.IsClinicallyActive`)
- **Patients page:** ABO/Rh and Alerts columns: a red antibody badge, and an amber "N special requirements" badge.
- No schema change. Read only.

### Requirements

- URS-BB-505, FRS-BB-575. Help: patients.

### Tests

- TEST-BB-436:
  - the current B- type (not the earlier B+); a retired current type is not shown
  - anti-E and anti-K, without a duplicate or the inactive anti-c
  - one requirement in effect, not the ended, future, or deactivated ones
  - an unflagged patient shows none
- Full suite: 3,717 passed, 0 failed.
- Run check (development API and web):
  - the list shows Kellerman (MRN0004) O+ with anti-K, Rhesus (MRN0003) A-, Demo (MRN0001) O+, and "—" for untyped patients, matching the downtime summary
  - no demo patient has a special requirement in effect, so that badge is covered by the test only
  - no server errors

### Next ranked residual

The same flags in the top-bar lookup results, or keyboard shortcuts for the bench.

## Iteration 401 — Type and alerts on top-bar patient hits (2026-11-05)

### Capability matrix rescan (search everywhere)

Cycle 400 put ABO/Rh and alerts on the Patients list. The top-bar find box, used from every page, still listed patients by name, DOB, and sex only. Choosing between similar names there meant opening charts to tell them apart and to see their risks (questions 1, 2).

### Design

- **`PatientBloodBankFlags`:** one definition of current type, active antibodies, and requirements in effect, used by both the patient list and the lookup, so the two agree.
- **`LookupHitDto`:** gains `BloodType`, `Antibodies`, `RequirementsInEffect`, read for the chart the row opens; a merged MRN shows the survivor's.
- **`GlobalLookupBar`:** shows the type (or "No type on file"), a red antibody badge, and an amber requirement badge.
- No schema change. A partial match still lists rather than opens.

### Requirements

- FRS-BB-576 (under URS-BB-505). Help: finding records.

### Tests

- TEST-BB-437:
  - by MRN and by a merged duplicate MRN, the row is the survivor with AB-, anti-Jka (not the inactive anti-S), and one requirement in effect (not the ended one)
  - without the history repositories the row has no flags
- The patient list test (TEST-BB-436) passes on the shared helper.
- Full suite: 3,718 passed, 0 failed.
- Run check (development API and web):
  - "kel" lists Kellerman MRN0004 with "O+ anti-K"
  - "gravid" lists Gravid MRN0007 with "No type on file"
  - no server errors
  - the browser pane was not drawing, so the find box was driven with DOM input and Enter events

### Next ranked residual

Keyboard shortcuts for the bench (next or previous test), or the same flags on the Issue desk patient picker.

## Iteration 402 — Patient pickers show type and alerts (2026-11-05)

### Capability matrix rescan (patient selection at issue)

Five patient pickers listed only "Last, First (MRN)":
- Issue desk
- Compatibility crossmatch
- Compatibility allocation
- ISBT receipt
- delivery receipt

On the Issue desk, typing "nora" offered two Noras with nothing to tell them apart but the MRN. A merged-away record looked like any other (questions 2, 6). The patient list and the top-bar lookup already carry type and alerts (Cycles 400–401).

### Design

- **`PatientDto.PickerLabel`:** name and MRN, then ABO/Rh (or "no type on file"), active antibodies, and the special-requirement count. A merged-away record reads "merged into another record" instead of flags. The issue gates still refuse it.
- **Pickers:** all five use it as their display text. Searching stays on name and MRN.
- No server change; the flags come from the patient list (Cycle 400).

### Requirements

- FRS-BB-577 (under URS-BB-505). Help: patients.

### Tests

- TEST-BB-438:
  - no type on file
  - type, antibodies, and two requirements
  - one requirement (singular)
  - a merged record marked without flags
- Full suite: 3,719 passed, 0 failed.
- Run check (development API and web, Issue desk):
  - "kel" offers "Kellerman, Diane (MRN0004) · O+ · anti-K"
  - "nora" offers "Kline, Nora (MRN0010) · O+" and "Rhesus, Nora (MRN0003) · A-"
  - nothing selected or issued; no server errors

### Next ranked residual

Keyboard shortcuts on the bench (next or previous test), or the chart Products tab unit picker showing special-requirement fit.

## Iteration 403 — A demo patient with special requirements (2026-11-05)

### Capability matrix rescan (sample data)

Cycles 394–402 surface special requirements in many places:
- the downtime sheet
- the patient list
- the top-bar lookup
- the patient pickers
- the chart banners

No demo patient had a requirement in effect, so none of these could be shown or checked live (Cycles 400 and 402 noted the gap). Iteration step 7 calls for realistic sample data.

### Design

- **`SeedSpecialRequirementsScenarioAsync`:** adds MRN0023 (Demo-Special, Avery):
  - an inpatient in ONC 6, with specimen ACC0023
  - typed O+ twice, so routine red-cell issue has its second determination
  - **in effect from two weeks ago, using the starter catalog entries:**
    - Irradiated red cells (enforced)
    - CMV-negative red cells (enforced)
    - Call the transfusion service physician (informational)
  - reasons marked "Demo:"
- **Seeding:** added once by MRN, on new and existing development databases. It does nothing if the starter entries are missing.
- Example data, not clinical policy. No rule or code path changed.

### Requirements

- SRS-BB-457.

### Tests

- TEST-BB-439: the seeder test now expects 13 patients and 14 encounters, MRN0023 with the three catalog entries clinically in effect, and two O type rows. The partial-migration seeder test count was updated too.
- Full suite: 3,719 passed, 0 failed.
- Run check (development API and web; the API seeded MRN0023 into the dev database on start):
  - Patients: "MRN0023 Demo-Special, Avery … O+ 3 special requirements"
  - chart: the informational banner "Call the transfusion service physician …" and "Special transfusion requirements: Irradiated red cells (RBC), CMV-negative red cells (RBC)"
  - one-patient downtime sheet: the two enforced requirements, the note listed separately, and ACC0023
  - no server errors

### Observations

The list and picker count includes the informational note (3), while the chart and downtime sheet separate it. Whether the count should show enforced requirements only is a display choice for a later cycle.

### Next ranked residual

Count enforced requirements apart from informational notes in the list, lookup, and picker badges, or keyboard shortcuts on the bench.

## Iteration 404 — Requirements and notes counted apart (2026-11-05)

### Capability matrix rescan (alerts versus warnings)

With the Cycle 403 demo patient visible, the patient list, lookup, and pickers read "3 special requirements" for two enforced requirements and one informational note. The chart and downtime sheet keep them apart. The UX objective asks for clear separation of informational alerts from warnings. A note counted as a requirement overstates what will block a unit, and blurs what will.

### Design

- **`PatientBloodBankFlags.RequirementsInEffect`:** leaves out rows whose catalog entry is Informational.
- **`NotesInEffect`:** counts those rows.
- **`InformationalIds`:** reads the catalog once per list.
- **Unknown catalog entry:** a row whose catalog entry is unknown counts as enforced, so nothing that may constrain a unit is hidden.
- **`PatientDto.InformationalNotes` and `LookupHitDto.InformationalNotes`:** shown as a blue "N note(s)" badge after the amber requirement badge, and as a picker part ("· 1 note").
- **Patients:** the alert badges wrap with a gap. They had sat flush ("requirements1 note"), found in the run check.
- No schema change.

### Requirements

- FRS-BB-578 (under URS-BB-505). Help: patients, finding records.

### Tests

- TEST-BB-440:
  - patient list: one enforced requirement and one note in effect counted apart, and the picker label ends "· 1 special requirement · 1 note"
  - lookup: no notes for a patient without one
  - picker: "· O+ · 2 notes"
  - without the catalog repository, rows count as enforced (the earlier lookup and list paths)
- Full suite: 3,719 passed, 0 failed.
- Run check (development API and web):
  - Patients: MRN0023 shows "2 special requirements" and "1 note" as separate badges with a 4px gap
  - the find box lists "MRN0023 Demo-Special, Avery O+ 2 special requirements 1 note"
  - no server errors

### Next ranked residual

Keyboard shortcuts on the bench (next or previous test), or an informational-note banner on the Compatibility page for the chosen patient.

## Iteration 405 — Compatibility reserve checks special requirements (2026-11-05)

### Capability matrix rescan (Compatibility page)

On the Compatibility page, the reserve section showed nothing about the patient's special requirements. Its Reserve (`CompatibilityService.AllocateUnitAsync`) and the preview did not check them either. The chart's Allocate panel and issue do (Cycle 373).

A group A red cell could therefore be reserved here for a "Group O red cells only" patient, or a non-irradiated unit for an irradiated-only patient. The problem surfaced only as a hard stop at issue, after the unit had been held and crossmatched (questions 5, 6). No unit could reach the patient, since issue still refused it.

### Design

- **`CompatibilityService`:** takes the special requirement, catalog, product attribute, and unit test repositories (optional). The new `UnmetSpecialRequirementsAsync` uses `SpecialRequirementLoader` and `SpecialTransfusionRequirementRule.UnmetUnitRequirements`, the same as the chart allocation and issue.
- **Reserve and preview:** each unmet requirement is a hard stop `SPECIAL-REQ`; nothing is reserved.
- **`PatientRequirementsBanner`:** a shared component on the crossmatch and reserve sections:
  - requirements in effect as a warning naming them ("refused at reserve and at issue")
  - informational notes as information
  - cleared on a change of patient
- No schema change. Emergency issue keeps its own override path.

### Requirements

- URS-BB-506, FRS-BB-579. Help: compatibility.

### Tests

- TEST-BB-441:
  - for "group O red cells only", the preview shows the stop and Reserve refuses a group A red cell with "must be group O", leaving no reservation
  - group A plasma is reserved for the same red-cell requirement
- Full suite: 3,720 passed, 0 failed.
- Run check (development API and web, Compatibility):
  - choosing MRN0023 shows "Special requirements: Irradiated red cells, CMV-negative red cells. A unit that does not meet them is refused at reserve and at issue." and the physician note
  - choosing Kline (MRN0010) clears both
  - nothing reserved; no server errors

### Next ranked residual

Filter the Compatibility reserve unit picker to units that meet the patient's requirements (as the chart's compatible-unit list does), or keyboard shortcuts on the bench.

## Iteration 406 — Compatibility reserve offers compatible units (2026-11-05)

### Capability matrix rescan (unit selection)

After Cycle 405, Reserve on the Compatibility page refuses a unit that misses the patient's requirements. Its unit picker still listed every available unit: 33 in the demo, of which 18 suit Kline. So the tech found out by trial and refusal, or had to know the patient's type and requirements by heart (questions 2, 3, 5). The chart's Allocate panel already offers only compatible units.

### Design

- **Compatible units first:** choosing the patient loads the chart's compatible-unit list (ABO/Rh, special requirements, antigens) and limits the picker to it, with "N available units are compatible …".
- **Toggle:** **Show all available units** / **Show compatible units only**.
- **`UnitPickerComboBox.OnlyIds`:** keeps a unit already chosen, so a scanned unit is not dropped.
- **Fallback:** when the list cannot be built (no current ABO/Rh), every available unit is shown with the reason. A doubled full stop in that reason was found in the run check and fixed.
- **Refresh:** the list refreshes after a reserve or release.
- Reserve still checks the chosen unit. No server change.

### Requirements

- FRS-BB-580 (under URS-BB-506). Help: compatibility.

### Tests

- No new automated test: the change is page state; the compatible list is covered by `PatientAllocationTests`, and Reserve's own check by TEST-BB-441.
- Full suite: 3,720 passed, 0 failed.
- Run check (development API and web, Compatibility, Reserve):
  - Kline: "18 available units are compatible …", the picker lists 18, and Show all lists 33
  - MRN0023: 16 compatible (red cells limited by irradiated and CMV-negative)
  - Gravid (no type): "Showing all available units: Patient has no current ABO/Rh on file; compatible units cannot be listed. Reserve still checks the unit."
  - nothing reserved; no server errors

### Next ranked residual

The same compatible-first picker for the Compatibility crossmatch section, or keyboard shortcuts on the bench.

## Iteration 407 — Transfusion reaction workup on pre- and post-transfusion specimens (2026-11-05)

### Request

"Most transfusion reactions involve a workup with both a pre-transfusion and a post-transfusion sample. … Use a setting that maps each specimen to a Test Group. The user will then need to result the tests in that Test Group."

The plan was approved after four decisions:
- the pre specimen is picked (crossmatch tube suggested) and the post specimen is accessioned or picked
- an expired pre tube is allowed for workup orders only (OCD-058)
- every workup test must be verified to close
- starter groups TRX-PRE (ABO/Rh, DAT) and TRX-POST (ABO/Rh, DAT, antibody screen), recorded as OCD-059

### Before

The investigation had no specimens, orders, or results. Repeat ABO/Rh, DAT, and elution were typed onto the investigation; nothing was resulted, verified, or tied to a tube.

### Design

- **Setting:** `ReactionWorkupSettings` (singleton, versioned; the CrossmatchSettings pattern, since facility policy only stores numbers and booleans) maps each specimen to a Test Group.
  - Admin page `/admin/reaction-workup`
  - validator: active, released groups only
  - reason, version, history, audit
- **`ReactionInvestigation`:** gains the pre- and post-transfusion specimen and order ids.
- **`ReactionWorkupService`:** lists candidate specimens (pre: collected ≤ issue, crossmatch tube suggested; post: collected ≥ start), attaches or accessions each specimen, and orders its group as Stat `RXN-{id}-PRE` / `-POST`.
  - The order goes through `OrderService.CreateForReactionWorkupAsync`, so the group expands to its tests and the order is typed as a reaction workup before the tube is linked.
  - Each specimen is set once, and the service is kept separate from `ReactionInvestigationService` to avoid a DI cycle with issuing.
- **Expiry exception (OCD-058):** reaction workup orders may link to, result, and verify on an expired tube, in `OrderService` link, `TestWorklistService` entry gate, and `ResultService` save and verify. Every other order keeps the block.
- **Close gate (`ReactionWorkupCompletenessRule` with `WorkupState`):** clerical, visual, pre specimen, post specimen, all workup tests verified, then elution on a verified positive post DAT.
  - The repeat patient ABO/Rh comes from the verified post ABO/Rh, compared with the current type and with the pre specimen.
  - A missing read counts as unverified (fail closed).
- **Reactions page:** a **Workup specimens** section:
  - pick, accession, and "Use and order {group}"
  - per-test results and status
  - **Result on bench** opens `/test-worklist?accession=`
  - the typed repeat patient ABO/Rh and DAT inputs are removed; legacy values are shown read only
- **Demo:**
  - MRN0006: ACC0014 becomes the pre specimen (ORD0016, ABO/Rh line added)
  - post tube ACC0024 with `RXN-n-POST` (ABO/Rh, DAT, antibody screen) unresulted, added once on new and existing databases

### Requirements

- URS-BB-507, FRS-BB-581 to 584, SRS-BB-458.
- OCD-058, OCD-059.
- Help: reactions, admin reaction workup (new), test worklist.
- Safety rules §5a.

### Tests

- TEST-BB-442 to 447:
  - specimen, expiry, close-gate, and settings rules
  - settings round trip and refusals
  - pre order on the crossmatch tube with its group lines and audit
  - wrong tube, unchosen group, closed case, and second attach refused, with nothing ordered
  - expired pre tube used; post tube accessioned (too early refused, nothing accessioned)
  - expired workup line resulted while an ordinary order on an expired tube is refused
  - close waits for the last verification; the board shows the results
  - repeat ABO/Rh discrepancy from results
  - seeded demo workup
- Existing closing tests give their cases a verified workup (`ReactionWorkupTestData`).
- Full suite: all projects pass, Help.Tests included (31).
- Help.Tests found the post-transfusion accession field updating only on blur, which left its button disabled while typing or scanning. Fixed with `@bind:event="oninput"`.
- Run check (development API and web; the bootstrap added the table and columns, and the seed linked MRN0006's workup):
  - Administration › Reaction Workup shows TRX-PRE (ABORH, DAT) and TRX-POST (ABORH, DAT, ABSC)
  - the board's next step for MRN0006 reads "Result and verify the workup tests: 5 still to verify"
  - the investigation shows ACC0014 (ORD0016: DAT Negative Entered, ABO/Rh Pending) and ACC0024 (RXN-1-POST: three tests Pending), with Result on bench links
  - `/test-worklist?accession=ACC0024` opens on its 3 tests
  - nothing was resulted live; no server errors

### Follow-up (same iteration): a cancelled workup order

Review found that a cancelled or discontinued workup order left its tests pending forever, so the case could never close. Such an order now frees its role:
- the gate asks for the specimen again
- the investigation offers the pickers again
- a new tube can be attached; its order takes the next free number (`RXN-n-PRE-2`)
- the replacement is audited with the replaced specimen and order

TEST-BB-448.

### Next ranked residual

Hemolysis check and other workup tests as test definitions (OCD-059), and a reaction workup print packet.

## Iteration 408 — Printable reaction investigation report (2026-11-05)

### Capability matrix rescan (reaction investigations)

After Cycle 407 the investigation carries its workup results, but the case was spread over screens. Assembling the medical director's review packet meant copying from several places (questions 1, 2):
- the board and editor
- the Issue desk's transfusion record (start, stop, volume, what the nurse saw and did)
- the bench results

Transfusion services print a reaction report for review and the record.

### Design

- **`ReactionInvestigationService.GetReportAsync`:** returns `ReactionReportDto`, the investigation DTO (with workup specimens and results) plus product name, issue, and the transfusion record.
- **`RecordReportPrintedAsync`:** writes a Print audit event against the investigation, naming a draft while the case is open.
- **Permission:** both need *reaction.investigate*.
- **Page `/reactions/{id}/report`:**
  - opened from **Report** on the investigation
  - facts, what was seen and done, the workup checklist, each specimen's tests and results, the conclusion and close, and medical director review signature lines
  - Draft banner while open
  - Print is audited first (the MTP report pattern)
  - breadcrumb Reactions / Investigation #n / Report
- No schema change.

### Requirements

- URS-BB-508, FRS-BB-585. Help: reactions (route and section).

### Tests

- TEST-BB-449:
  - the report carries the bedside actions, transfusionist, start time, issue, and post-specimen lines
  - the print is audited as a draft
  - refused without *reaction.investigate*, and for an unknown id
- Full suite: 3,734 passed, 0 failed (Help.Tests included).
- Run check (development API and web):
  - `/reactions/1/report` for MRN0006 shows the Draft banner, unit W0001230001007 A+, issue by tech2 to 3N Medical, transfusion stopped at 12 minutes with 60 mL, the bedside actions and observations, the checklist, and both workup specimens with their tests
  - the breadcrumb reads Dashboard / Reactions / Investigation #1 / Report; the investigation's Report link opens it
  - Print not pressed; no server errors

### Next ranked residual

Workup tests beyond ABO/Rh, DAT, and screen (hemolysis check, bilirubin) need the medical director's list (OCD-059). Otherwise rescan operations.

## Iteration 409 — Reaction workup lines marked on the bench (2026-11-05)

### Capability matrix rescan (bench, after Cycle 407)

Reaction workup tests reached the bench as ordinary Stat lines. The technologist could not see that an investigation was waiting on them, or get to it from the row (questions 2, 5).

### Design

- **`TestWorkItemDto.ReactionWorkup`:** set when the line's order is a transfusion reaction workup.
- **`ReactionHref`:** the reaction board opened on the patient.
- **Display:** the test worklist and the chart Tests tab show a red **Reaction workup** badge on those rows, linking there.
- No schema change.

### Requirements

- FRS-BB-586 (under URS-BB-507). Help: test worklist.

### Tests

- TEST-BB-450: the workup line is marked with the board link; an ordinary line on the same kind of tube is not.
- Full suite: 3,734 passed, 0 failed.
- Run check (development API and web):
  - the test worklist marks MRN0006's five workup lines (ORD0016 ABO/Rh and DAT; RXN-1-POST ABO/Rh, DAT, antibody screen), each linking to `/reactions?patientId=6`
  - the other six rows are unmarked
  - no server errors

### Next ranked residual

Rescan operations: the specimen desk or Issue desk for remaining manual steps.

## Iteration 410 — Recollection after a rejected tube (2026-11-05)

### Capability matrix rescan (specimen desk)

Rejecting a tube recorded the reason and warned about open antibody workups and reservations. It said nothing about the orders still on the tube. On the bench their lines read "Blocked: Specimen X is Rejected; only Accepted specimens allow result entry", with no next step. The technologist had to remember to request a recollection and work out how to relink (questions 2, 5).

### Design

- **`SpecimenRecollectionRule`:** rejected or cancelled tubes need a recollection.
- **Bench lines on such a tube:**
  - block reason: "Specimen X was rejected (reason). Collect a new specimen and link it to this order."
  - labeled **Recollect**
  - `NeedsSpecimenLink`, so the worklist row, chart Tests tab, and entry panel offer Accession
- **`SpecimenService.RejectAsync`:** adds the warning `SPEC-RECOLLECT` naming the open orders linked to the tube (optional order repositories).
- No schema change. Notifying the ward or EHR of a rejection (HL7) is not done here; it needs the interface's message choice.

### Requirements

- URS-BB-509, FRS-BB-587. Help: patient specimens.

### Tests

- TEST-BB-451:
  - rejecting a tube with an open DAT order names the order
  - its line is Recollect, needs a specimen link, and gives the reason
  - a tube with no open orders is rejected without the warning
- Full suite: 3,735 passed, 0 failed.
- Run check: the worklist loads (11 rows) and there are no server errors. No demo tube is rejected with open orders, and none was rejected live, so the Recollect line is covered by the test only.

### Next ranked residual

Rejection notice to the EHR (HL7 order status) needs the interface choice; otherwise the Issue desk rescan.

## Iteration 411 — Recollect filter and home count (2026-11-05)

### Capability matrix rescan (Issue desk, then the bench)

- **Issue desk return path:** already covered. A returned unit has storage-time and inspection checks and a retype hold, and **Reserve again for this patient** follows the return. Nothing is re-reserved automatically, by design.
- **Recollect lines (Cycle 410):** visible only by scrolling the whole worklist, and the home screen did not count them, so a recollection could wait unnoticed (question 5).

### Design

- **`TestWorklistView.Recollect`:** lines on a rejected or cancelled tube.
- **Worklist:**
  - a **Recollect (n)** chip appears when there are any (or when chosen)
  - `?show=recollect` opens on it
  - the empty message: "No test is waiting for a new specimen."
  - the All / To verify / Recollect state is one setting instead of a flag
- **Home:** the Pending tests note (`DashboardQueueCatalog.PendingTestsNote`) names lines to verify before their specimen expires and lines that "need a new specimen". Either brings the card under Needs attention.
- No server change.

### Requirements

- FRS-BB-588 (under URS-BB-509). Help: test worklist, dashboard.

### Tests

- TEST-BB-452:
  - the note reads "1 to verify before its specimen expires; 2 need a new specimen", or "1 needs a new specimen", or nothing
  - Recollect keeps the rejected and cancelled lines in order
  - the label is Recollect
- Full suite: 3,736 passed, 0 failed.
- Run check (development API and web):
  - `/test-worklist?show=recollect` shows All (11), To verify (2), and Recollect (0) selected, with the empty message
  - choosing To verify shows its 2 rows and hides the empty Recollect chip
  - no server errors
  - no demo tube is rejected, so a non-zero count is covered by the test only

### Next ranked residual

Rejection notice to the EHR needs the interface choice. Otherwise rescan inventory operations (transfers, location moves).

## Iteration 412 — Move units by scan (2026-11-05)

### Capability matrix rescan (inventory locations)

A unit could be moved only from its own page, by choosing a location from a list. Stocking a satellite or operating-room refrigerator with ten units meant opening ten unit pages and choosing the same location ten times, with no scanning (questions 1, 4).

### Design

- **`InventoryService.TransferByScanAsync`:**
  - **Scan handling:** cleans the scan, parses a "=" DIN (an unreadable one is refused), and matches exactly on unit number or DIN (new `IInventoryRepository.ListByUnitNumberOrDinAsync`) or the component label.
  - **Nothing moves when:** there is no match, the status cannot move, the unit is already in the location, or several units match (a divided DIN). Each says why.
  - **One movable unit:** goes through `TransferAsync` (active location, component allowed, storage range; status history and audit), reason "Moved by scan" unless one is given.
- **API:** `POST /api/inventory/units/transfer-by-scan`, permission *inventory.transfer*.
- **Page `/inventory/move` (Move units on Inventory):**
  - choose Move to and an optional reason, then scan
  - each scan moves at once and is listed under **This run**, newest first: Moved, or Not moved with the reason
  - breadcrumb Inventory / Move units
- No schema change.

### Requirements

- URS-BB-510, FRS-BB-589. Help: inventory (route and section).

### Tests

- TEST-BB-453:
  - a lower-case, padded unit number moves the unit, with history from/to and the reason
  - refused: already there, unknown number, unreadable "=" scan, Issued unit, divided DIN (2 units, none moved), blank scan
- Full suite: 3,737 passed, 0 failed.
- Run check (development API and web), using refusals only so demo stock did not move:
  - with FRIDGE-1 chosen: W0001230009203 "is Issued; it cannot be moved", W9999999999999 not found, W0001230009201 "is already in that location"
  - "This run: 0 moved, 3 not moved"
  - no server errors
  - the not-found message showed the 13-character DIN rather than the 14 characters scanned; it now echoes the scan
  - a successful move is covered by the test only

### Next ranked residual

Rescan: cooler and transport checkouts, or inventory count (cycle count by scan per location).

## Iteration 413 — Stock count by scan (2026-11-05)

### Capability matrix rescan (inventory)

Nothing compared what is physically in a storage location with what the record says is there. Two kinds of mismatch went unnoticed until someone stumbled on them (question 6):
- a unit recorded in the fridge but gone
- a unit on the shelf whose record says issued, transfused, or discarded

Physical inventory reconciliation is expected of mature transfusion systems.

### Design

- **`InventoryService.ReconcileCountAsync(location, scans, finish)`:**
  - **Expected:** units recorded at the location in on-hand statuses, plus Expired units not yet discarded (`InventoryUnitScopes.OnShelf`).
  - **Results:** Found, Missing, Recorded elsewhere, Not on hand, and unresolved scans (unknown, unreadable, divided number). Expired units are flagged for discard.
  - A unit scanned twice counts once.
  - Scan resolution is shared with Move units (`ResolveScanAsync`).
  - Nothing changes during the count. **Finish** writes an audit event against the location with the counts and the item lists.
- **API:** `POST /api/inventory/counts`, permission *inventory.transfer*.
- **Page `/inventory/count` (Stock count on Inventory):**
  - choose the location and scan; the summary badges and lists update after each scan
  - **Move here** for misplaced units uses the audited scan transfer
  - **Finish count** records the outcome; **Count again** starts over
- No schema change.

### Requirements

- URS-BB-511, FRS-BB-590. Help: inventory (route and section).

### Tests

- TEST-BB-454:
  - found (including an expired unit, flagged)
  - missing
  - recorded elsewhere with its location
  - an issued unit not on hand
  - an unknown scan
  - a duplicate scan counted once
  - nothing moved or audited before finishing
  - finishing writes one audit event naming the missing unit
  - a full count is reconciled
- Full suite: 3,738 passed, 0 failed.
- Run check (development API and web):
  - FRIDGE-1 expects 37 units
  - scanning W0001230009201, W0001230009203, and an unknown number gives "Found 1 of 37, Missing 36, Not on hand 1, Unresolved scans 1"
  - the issued W0001230009203 is listed as "On the shelf, but the record says otherwise"
  - Finish not pressed (no audit row); no server errors

### Observation

An issued unit keeps its last storage location, so a count lists it as not on hand rather than missing, which is the useful signal.

### Next ranked residual

Cooler and transport checkout rescan, or a count history view (the audit holds each finished count).

## Iteration 414 — Stock count history (2026-11-05)

### Capability matrix rescan (inventory)

Finished counts (Cycle 413) went only into the audit trail. A supervisor checking that each refrigerator is counted regularly, and what the last count found, had to search the audit trail by location (questions 1, 2).

### Design

- **`InventoryService.ListCountHistoryAsync(location?)`:** reads the "Stock count at …" audit events per location, newest first, with who, when, the summary, and whether all units were found.
- **API:** `GET /api/inventory/counts`, permission *inventory.transfer*.
- **Stock count page:**
  - the chosen location shows "Last counted … by …" with All found or Differences, or "No finished count recorded"
  - a **Recent counts** table lists the latest 20
- No schema change; the audit stays the record.

### Requirements

- FRS-BB-591 (under URS-BB-511). Help: inventory.

### Tests

- TEST-BB-455 (in the stock count test):
  - the two finished counts are listed, one with differences ("1 missing") and one all found, with the counting user
  - the location filter
  - refused without *inventory.transfer*
- Full suite: 3,738 passed, 0 failed.
- Run check (development API and web): choosing OR-FRIDGE shows "No finished count recorded for this location", with no Recent counts card (none finished in the dev database, and none was recorded live); no server errors.

### Next ranked residual

Cooler and transport container checkout rescan.

## Iteration 415 — Cooler check before dispatch (2026-11-05)

### Capability matrix rescan (issue, transport)

Issue records a cooler id on each unit, and ward receipt lists the rest of a cooler, but nothing checked the cooler before it left the blood bank. Two errors travelled unnoticed until the ward found them (question 6):
- a unit for one patient packed in another patient's cooler
- a unit that was never issued

Packing was checked by eye against memory or the issue screen (questions 2, 4).

### Design

- **`CoolerCheckService.CheckAsync(cooler, scans, finish)`:**
  - **Expected:** issues under the cooler id (case and spacing ignored) still in transit (Issued, not received on the ward).
  - **Results:** Packed, Missing, Issued to another cooler (with its patient and cooler), Not issued, and unresolved scans. It also counts the patients in the cooler.
  - **Ready** means everything packed and nothing else in the cooler.
  - Nothing changes during the check. **Finish** writes an audit event against each issue in the cooler.
- **`ListOpenAsync`:** coolers in transit with units, patients, destinations, and the latest check (read from the audit), so the cooler is chosen rather than remembered.
- **`InventoryScanResolver`:** scan resolution moved out of `InventoryService`, shared with Move units and Stock count.
- **API:** `GET /api/issues/coolers` and `POST /api/issues/coolers/check`, permission *issue.create*.
- **Pages:**
  - `/issuing/cooler-check`: **Check a cooler** on In transit
  - after a run of issues with a cooler id, **Check cooler … before it leaves** opens the check on that cooler
- No schema change. A massive transfusion pack keeps its own pack record (MTP pack report); this check covers the cooler id on the issue.

### Requirements

- URS-BB-512, FRS-BB-592. Help: issuing (route and section).

### Tests

- TEST-BB-456:
  - packed
  - missing
  - issued under another cooler, with the patient
  - not issued
  - an unknown scan
  - a duplicate scan counted once
  - the cooler id matched regardless of case and spacing
  - a unit received on the ward not expected
  - nothing audited before finishing; finishing audits each issue with the outcome
  - a full cooler is ready
  - the open-cooler list shows the latest check
  - finishing an empty cooler refused
  - refused without *issue.create*
- Full suite: 3,739 passed, 0 failed.
- Run check (development API and web):
  - Coolers in transit lists Cooler 1 (4 units), CLR-TRAUMA-1 and Cooler M2, all "Not checked"
  - on Cooler M2, scanning W0001230009203, the crossmatched (not issued) W0001230009201, and an unknown number gives "Packed 1 of 2, Missing 1, Not issued 1, Unresolved scans 1"
  - Finish not pressed (no audit row); no server errors
  - the summary badge read "Cooler Cooler M2", so it now shows the bare id

### Next ranked residual

Show the latest cooler check on the In transit board and at ward receipt.

## Iteration 416 — Cooler check on In transit and ward receipt (2026-11-05)

### Capability matrix rescan (issue, transport)

The cooler check (Cycle 415) was recorded only in the audit trail and on its own page. The In transit board and the person receiving a cooler could not see whether it had been checked before it left (questions 2, 5).

### Design

- **In transit:** loads the coolers in transit with the rows, then marks each unit with a cooler id **Cooler checked**, **Cooler differences**, or **Cooler not checked**. The badge links to the check.
- **Ward receipt:** names the cooler with its check (`ReadyToIssueFormCue.CoolerCheckNotice`).
- **Without *issue.create*:** the check state is not read, so only the cooler id is shown; nothing is claimed.
- An id like "Cooler M2" is no longer shown as "Cooler Cooler M2".
- No schema or API change.

### Requirements

- FRS-BB-593 (under URS-BB-512). Help: issuing.

### Tests

- TEST-BB-457:
  - checked with all units packed
  - checked with differences
  - not checked
  - unknown state
  - the "Cooler" prefix not doubled
  - no cooler
- Full suite: 3,740 passed, 0 failed.
- Run check (development API and web):
  - In transit marks the seven cooler rows (CLR-TRAUMA-1, Cooler M2, Cooler 1) "Cooler not checked", each linking to its check
  - Receive on CLR-TRAUMA-1 shows "Cooler CLR-TRAUMA-1: not checked before it left" (nothing received)
  - no server errors

### Next ranked residual

Rescan inventory and operations queues for the next workflow gap (candidates: a demo cooler check, shift handoff of coolers still out).

## Iteration 417 — Return a cooler in one run (2026-11-05)

### Capability matrix rescan (issue, return)

Issue (Cycle 376), ward receipt, and the bedside each keep a run for several units of one patient. Return did not. When a cooler came back unused, each unit had to be found again on Issued units, and the reason retyped (questions 1, 2, 5).

### Design

- **`IssueRun.NextToReturn`:**
  - offers the patient's other units from the same cooler (or the same destination when no cooler was recorded) that can still be returned (transfusion not started)
  - earliest issued first
- **Return card:**
  - appears as soon as a unit is chosen
  - lists what was returned and where it went
  - **Return next** only selects the next unit
- **Carried over and cleared:**
  - the reason stays while more of the shipment is out
  - temperature, seal, and visual inspection start unanswered for every unit (not carried over: question 6)
  - a unit from another shipment starts a new run
- No server, schema, or API change; each return runs the full return checks.

### Requirements

- URS-BB-513, FRS-BB-594. Help: issuing.

### Tests

- TEST-BB-458:
  - same cooler and patient, earliest first
  - another cooler, another patient, and a transfusing unit left out
  - destination matching without a cooler
  - nothing left once all are returned
- Full suite: 3,741 passed, 0 failed.
- Run check (development API and web):
  - Return on a Cooler M2 unit (W0001230009203) shows "Cooler M2: Still out (1), Return next: W0001230009204"
  - with a reason typed, Return next selects W0001230009204, keeps the reason, and offers W0001230009203
  - nothing returned; no server errors

### Next ranked residual

Rescan the remaining Partial areas (interfaces, product modification workflows) for an operational gap.

## Iteration 418 — Stock count follow-up (2026-11-05)

### Capability matrix rescan

I looked at these areas and found them already covered:
- interfaces
- product modifications (a batch exists)
- tube receipt by scan
- antigen-ranked unit selection

The stock count had a gap. It named units missing from a location, and units on a shelf whose record says Missing, but acting on either meant opening each unit and retyping the context (questions 1, 2, 5).

### Design

- **Missing list, after Finish count (*inventory.release*):**
  - each unit offers **Mark missing**, with the reason "Not found in stock count at {location}"
  - marked rows show "Marked missing", and the unit is no longer expected there
  - not offered before finishing: an unscanned unit may still turn up
- **Found here, before finishing:**
  - offered on a scanned unit recorded as Missing
  - locates it into quarantine for inspection with "Found in stock count at {location}"
  - then redoes the count; Move here still covers a unit recorded elsewhere
- **`CountTable`:** an optional row action.
- No server, schema, or API change: the existing audited mark-missing and locate paths are used.

### Requirements

- FRS-BB-595 (under URS-BB-511). Help: inventory.

### Tests

- TEST-BB-459:
  - a finished count's missing unit marked missing with the count in the reason
  - no longer expected at the location
  - scanned later, it is Not on hand (Missing)
  - located into quarantine, the next count is reconciled and finds it
  - locating a unit that is not missing is refused
  - marking missing needs *inventory.release*
- Full suite: 3,742 passed, 0 failed.
- Run check (development API and web):
  - FRIDGE-1 before finishing: "Found 0 of 37, Missing 37", no Mark missing or Found here offered
  - Finish not pressed (no audit row); no server errors

### Next ranked residual

Shift handoff: coolers still out and not checked.

## Iteration 419 — Coolers in the shift handoff (2026-11-05)

### Capability matrix rescan (operations)

The shift handoff counted units in transit, but not the coolers carrying them or whether those coolers were checked before they left (Cycles 415–416). An incoming shift could not tell that a cooler went out unchecked, or with differences, without opening the cooler check (questions 2, 5).

### Design

- **New `ShiftHandoff` columns:**
  - `CoolersInTransit`
  - `CoolersNotChecked` (never checked)
  - `CoolersWithDifferences` (latest check not all packed)
- **Schema:** migration `20261115000000_ShiftHandoffCoolers` and the SQLite bootstrap columns.
- **`CoolerCheckService.SummarizeOpenAsync`:** the read-only open-cooler list without its own *issue.create* check. The handoff is authorized by *shift.handoff* and needs only the counts. `ListOpenAsync` keeps its check.
- **Handoff line:** "coolers out n, x not checked, y checked with differences", only when coolers are out. The audit snapshot includes the three counts.

### Requirements

- FRS-BB-596 (under URS-BB-512). Help: shift handoff.

### Tests

- TEST-BB-460:
  - the line wording, and quiet at zero
  - a recorded handoff counts:
    - an unchecked cooler
    - a cooler checked with differences
    - a unit with no cooler not counted
    - a cooler already received not counted
  - recorded by a user with only *shift.handoff* rights in the service
- Full suite: 3,744 passed, 0 failed.
- Run check (development API and web):
  - the startup bootstrap added the three columns to the development database
  - the Shift handoff page loads (no handoffs recorded there)
  - nothing recorded; no server errors

### Next ranked residual

Rescan the operations and testing queues for the next workflow gap.

## Iteration 420 — Cooler checks on the home In transit card (2026-11-05)

### Capability matrix rescan (operations)

The home In transit card named only units past the arrival window. A cooler that left unchecked, or with differences, was visible on the In transit board and in the shift handoff, but not on the home screen where the issuing desk starts (question 5).

### Design

- **`DashboardQueueCatalog.InTransitNote(overdue, coolers)`:** the card note names:
  - units past the arrival window
  - coolers not checked
  - coolers checked with differences
- Any of these brings the card under Needs attention.
- The home page reads the open-cooler list alongside the in-transit units. When it cannot be read, only overdue units are named.
- No server, schema, or API change.

### Requirements

- FRS-BB-597 (under URS-BB-512). Help: dashboard.

### Tests

- TEST-BB-461:
  - quiet with nothing due or every cooler checked
  - overdue units only
  - both overdue units and cooler checks, singular and plural
- Full suite: 3,745 passed, 0 failed.
- Run check (development API and web): the home In transit card reads "8 · 1 past the arrival window; 3 coolers not checked"; no server errors.

### Next ranked residual

Add a checked cooler to the demo data so all three cooler states can be shown.

## Iteration 421 — Demo cooler checks (2026-11-05)

### Capability matrix rescan (sample data)

The demo had three coolers in transit, all unchecked, so the checked and differences states of Cycles 415–420 could not be shown without finishing a check live.

### Design

- **`DatabaseSeeder.EnsureCoolerCheckDemoAsync`** (runs with the extended demo scenarios, once per cooler):
  - **Cooler M2** (multi-unit patient): checked with all units packed.
  - **Cooler 1** (massive transfusion pack): checked with its first-issued unit not scanned ("3 of 4 packed, 1 missing").
  - **CLR-TRAUMA-1:** stays unchecked.
- **Rows:** written as the cooler check writes them, one audit row per issue in the cooler. Each is attributed to the user who issued, from workstation DEMO-SEED, with `DemoData: true` in the value.
- **Hash chain:** the rows join it like any other audit row.
- No schema or API change.

### Tests

- TEST-BB-462:
  - after seeding twice, the open-cooler list shows Cooler M2 all packed, Cooler 1 with differences, and CLR-TRAUMA-1 not checked
  - one check row per unit (no duplicates)
  - the Cooler 1 reason names the missing unit count
- Full suite: 3,746 passed, 0 failed.
- Run check (development API and web):
  - restarting the API seeded the checks, with hash-chained rows by tech2 at DEMO-SEED
  - Coolers in transit shows Cooler 1 "Differences", CLR-TRAUMA-1 "Not checked", and Cooler M2 "All packed"
  - the home In transit card reads "1 cooler not checked; 1 cooler checked with differences"
  - no server errors

### Next ranked residual

Rescan the remaining queues (test worklist, antibody ID, reactions) for the next workflow gap.

## Iteration 422 — Reaction workup on the home Reactions card (2026-11-05)

### Capability matrix rescan (testing and reaction queues)

The test worklist and the antibody ID worklist already show their next actions. The home Reactions card counted open cases and named only fatality follow-ups. Since Cycle 407, a case cannot close until its pre- and post-transfusion specimens are tested and verified. The post-transfusion specimen should be drawn promptly after the reaction, yet nothing on the home screen said a case was still waiting for it (questions 5, 6).

### Design

- **`DashboardQueueCatalog.ReactionsNote`:** names:
  - fatality follow-ups (closed cases included, as before)
  - open cases with no post-transfusion specimen linked
  - open cases with workup tests not all verified
- Any of these brings the card (and the Start here banner) under Needs attention.
- The reactions list already carries each case's workup, so no server, schema, or API change.

### Requirements

- FRS-BB-598 (under URS-BB-507). Help: dashboard.

### Tests

- TEST-BB-463:
  - no post specimen
  - a test still to verify
  - a complete workup is quiet
  - a closed case counts only for fatality follow-up
- Full suite: 3,747 passed, 0 failed.
- Run check (development API and web): the demo investigation (MRN0006, post tube not resulted) shows "1 with workup tests to verify" on the Reactions card and the Start here banner; no server errors.

### Next ranked residual

Rescan the antibody ID worklist and supervisor queue for an operations gap.

## Iteration 423 — Missing and damaged units on the supervisor queue (2026-11-05)

### Capability matrix rescan (supervisor queues)

The supervisor queue listed:
- antibody-identification reviews
- results to verify
- quarantine releases
- override reviews

Missing and damaged units, which a supervisor must locate, inspect, or discard, were only on the Inventory Discrepancy board. Units marked missing from a stock count (Cycle 418) could sit there unnoticed by the person who resolves them (questions 2, 5).

### Design

- **`SupervisorQueueRule.KindDiscrepancy`** ("Missing or damaged"):
  - included with *inventory.release*
  - sorted after quarantine releases and before override reviews
- **Each row:**
  - "Missing — unit" or "Damaged — unit"
  - the product, type, recorded reason, and last recorded location
  - **Locate** or **Inspect**, opening the unit
- Rows come from the existing discrepancy list, so a unit leaves the queue once located, inspected, or discarded.
- No schema or API change.

### Requirements

- FRS-BB-599 (under URS-BB-337). Help: supervisor queue.

### Tests

- TEST-BB-464:
  - a missing unit appears with its reason and last location
  - it is not shown with only *result.verify*
  - it leaves the queue once located
  - the kind's sort position
- Full suite: 3,748 passed, 0 failed.
- Run check (development API and web):
  - the queue shows "Missing or damaged (2)"
  - Damaged W000123DMG0001 shows "Bag seam leak discovered on storage inspection", with Inspect
  - Missing W000123MISS001 shows "Not found during physical inventory count", with Locate
  - both last recorded at the Main Blood Bank Refrigerator
  - no server errors

### Next ranked residual

Rescan specimen and order workflows for the next operations gap.

## Iteration 424 — Tests with no specimen (2026-11-05)

### Capability matrix rescan (orders and specimens)

The bench separated lines whose tube was rejected (Recollect, Cycle 411), but lines ordered with no tube at all were mixed into the full list. A STAT type and screen waiting for its draw had nothing on the home screen to say so. The bench had to notice it among results in progress (questions 2, 5, 6: a delayed STAT specimen delays blood).

### Design

- **`TestWorklistView.NoSpecimen`:**
  - lines with no tube (a rejected tube stays Recollect)
  - STAT, urgent, emergency release, and MTP first, then oldest order first
- **`RushOrdersWithoutSpecimen`:** counts rush orders, not lines, still without a tube.
- **Test worklist:** a **No specimen (n)** view (`?show=nospecimen`), each line with its Accession step.
- **Home Pending tests note:** "n STAT/urgent orders waiting for a specimen" brings the card under Needs attention. Routine orders without a tube do not, as they are normal before collection.
- **Demo:** STAT type and screen ORD0024 for MRN0023, with no tube.
- No server, schema, or API change.

### Requirements

- FRS-BB-600 (under URS-BB-509). Help: test worklist, dashboard.

### Tests

- TEST-BB-465:
  - the No specimen view's order and exclusions (linked tube, rejected tube)
  - the note counts STAT orders once each and stays quiet for routine ones
  - seeded ORD0024 is STAT, with two lines and no tube (order count 20)
- Full suite: 3,749 passed, 0 failed.
- Run check (development API and web):
  - the worklist shows "No specimen (2)" with ORD0024's ABO/Rh and antibody screen for MRN0023 (Stat, Accession step)
  - the home Pending tests card reads "13 · 1 STAT/urgent order waiting for a specimen"
  - no server errors

### Next ranked residual

Rescan patient and encounter workflows for the next operations gap.

## Iteration 425 — Possible duplicate patients at registration (2026-11-05)

### Capability matrix rescan (patients)

Merge and unmerge exist, but registration refused only an existing MRN. Registering the same person under a second MRN makes a new chart with no ABO/Rh history, no antibodies, and no special requirements. The issue gate then reads the empty chart (question 6). Nothing told the registrar the person might already exist (questions 2, 3).

### Design

- **`PossibleDuplicatePatientRule`:** a possible duplicate is an active (not merged) patient with the same date of birth and either:
  - the same last name and first initial (letters only, case ignored)
  - first and last names swapped
- **`PatientService.ListPossibleDuplicatesAsync`:** `GET /api/patients/possible-duplicates`, permission *patient.write*.
- **`CreateAsync`:**
  - refuses while any match is not in `ConfirmedDifferentFromMrns`, naming each MRN, so a match registered meanwhile is caught
  - the creation audit records the MRNs confirmed as different
- **Register patient form:**
  - once the names and date of birth are entered, lists the matches with **Open chart**
  - **Register** stays disabled until "I checked: this is a different person" is ticked
  - the tick clears when the matches change
- **OCD-060:** the matching criteria, who confirms, and whether ADT-created patients are checked, pending SME and HIM confirmation.
- No schema change.

### Requirements

- URS-BB-514, FRS-BB-601, OCD-060. Help: patients.

### Tests

- TEST-BB-466:
  - **rule:** initial, punctuation and case, swapped names, a different last name, a blank name, a different birthday
  - **service:**
    - the match listed (the merged chart not)
    - refused with the MRN named
    - confirming another MRN does not count
    - confirmation (any case) registers and is audited
    - a different first initial is not a match
    - listing needs *patient.write*
- Full suite: 3,758 passed, 0 failed.
- Run check (development API and web):
  - entering Demo-Multi, Riley, 1958-11-21 lists MRN0022 with Open chart, and Register is disabled
  - ticking the confirmation enables it; nothing was registered
  - no server errors
  - both servers exited on their own once during the check (no errors logged) and were restarted

### Next ranked residual

A possible-duplicates report over existing patients (and ADT-created ones), pending OCD-060.

## Iteration 426 — Possible duplicates report (2026-11-05)

### Capability matrix rescan (patients)

Cycle 425 stops new manual duplicates. It does not cover charts that already exist, or charts the EHR feed creates (ADT registration is not checked, OCD-060). Those duplicates could only be found by chance (questions 2, 6).

### Design

- **`PatientService.ListPossibleDuplicatePairsAsync`** (*patient.merge*; `GET /api/patients/possible-duplicates/pairs`):
  - pairs of active charts by `PossibleDuplicatePatientRule`, ADT-registered ones included
  - older registration first
  - each with its ABO/Rh, active antibodies, and special requirements
- **`/patients/duplicates`** (linked from Patients for *patient.merge*):
  - each pair, with **Keep this, merge … in** for either chart
  - the link opens that chart with the merge form naming the other MRN (`?mergeMrn=`)
  - the reason, confirmation, and audit are the merge's own; the list changes nothing
- **Demo:** MRN0024 DEMO-MULTI, RILEY, an ADT-style second chart for MRN0022.
- No schema change.

### Requirements

- FRS-BB-602 (under URS-BB-514); OCD-060 updated. Help: patients (route and section).

### Tests

- TEST-BB-467:
  - an ADT-style upper-case chart pairs with the older chart, older first
  - merged charts and a different first initial are left out
  - needs *patient.merge*
  - the demo seeds MRN0024 (patient count 14)
- Full suite: 3,759 passed, 0 failed.
- Run check (development API and web):
  - Possible duplicates shows "1 pair to review": MRN0022 (O+) and MRN0024 (None on file)
  - Keep this, merge MRN0024 in opens MRN0022 with the merge form's Duplicate MRN filled
  - nothing merged; no server errors

### Next ranked residual

Rescan inventory receipt and supplier workflows for the next operations gap.

## Iteration 427 — Supplier order received vs ordered (2026-11-05)

### Capability matrix rescan (inventory receipt and supply)

Linking a supplier order to its delivery marked it Received, whatever arrived. A short shipment of group O red cells went unnoticed until the minimum-stock screen showed the shortfall again. The receipt record did not say what was missing (questions 2, 5).

### Design

- **`SupplierOrderService.Reconcile`:** counts the delivery's units against the lines:
  - named-ABO lines take matching units first
  - any-ABO lines take what remains of the product
  - no unit is counted twice; extra or unordered units are ignored
- **Order DTO:**
  - `SupplierOrderLineDto` gains `Received` and `Short`, null and 0 until received
  - `SupplierOrderDto.UnitsShort`
- **Receiving:** the audit reason and value name each short line ("RBC-SO O: 2 of 5 short").
- **Supplier orders page:**
  - Received and Short columns on a received order
  - **Short n** in the orders list
- No schema or API change (the service reads units by `DeliveryId`).

### Requirements

- FRS-BB-603 (under URS-BB-411). Help: supplier orders.

### Tests

- TEST-BB-468:
  - **reconciliation:** named group first, then any group, each unit counted once, a product with no units
  - **service:** 3 of 5 group O received (an unordered A unit ignored), 2 short, audited; no received count before receipt
- Full suite: 3,760 passed, 0 failed.
- Run check (development API and web): Supplier orders renders (no orders in the development database, so no shortfall shown live); no server errors.

### Next ranked residual

Demo delivery and supplier order data, so supply workflows can be shown, or the next operations gap from the rescan.

## Iteration 428 — Supply demo data (2026-11-05)

### Capability matrix rescan (sample data)

The demo had no deliveries or supplier orders, so the supply workflows could not be shown (Cycle 427 could not be seen live):
- minimum stock, then order
- order, then delivery
- received versus ordered

### Design

- **`DatabaseSeeder.EnsureSupplyDemoAsync`** (once):
  - **Delivery:** from American Red Cross, packing list PL-DEMO-0001, completed. One red-cell line expecting 3, and three group O red cells (W0001230009301–9303) on FRIDGE-1.
  - **Supplier order:** SO-DEMO-001 for five group O red cells, submitted and received against that delivery, so it shows 2 short.
- **Supplier orders list:** the Short badge is spaced from the status badge (found in the run check: "ReceivedShort 2").
- No schema, API, or service change.

### Tests

- TEST-BB-469: the seed has SO-DEMO-001 received, three units on its delivery, and a line for five (unit count 68).
- Full suite: 3,760 passed, 0 failed.
- Run check (development API and web):
  - Supplier orders lists SO-DEMO-001 Received with Short 2; opening it shows RBC-LR O, quantity 5, received 3, short 2, against delivery 1
  - Delivery 1 shows RBC expected 3, received 3 (Even), and the three units
  - no server errors

### Next ranked residual

Rescan the remaining Partial areas for the next operations gap.

## Iteration 429 — J/K between tests on the worklist (2026-11-05)

### Capability matrix rescan (UX, keyboard)

Keyboard shortcuts on the bench had been the deferred residual of several cycles. The worklist already opens the next test after a save. Moving between tests otherwise needed the mouse for each row (questions 1, 4).

### Design

- **`ui.js`:**
  - J opens the next test and K the previous one, outside a text field with no modifier keys
  - it clicks only that row's own open/hide button (`data-bench-toggle`), so the page behaves as for a click
  - it then puts the cursor in the first field of the row's result panel (`data-bench-result`)
- **Saving and verifying:** never done by a key; they stay deliberate clicks.
- **Blocked rows:** open with their reason and no field to focus.
- **Shortcut panel:** lists J / K under **?**.
- No server change.

### Requirements

- URS-BB-515, FRS-BB-604. Help: test worklist.

### Tests

- TEST-BB-470 (file checks in the style of TEST-BB-313):
  - the shortcut clicks only `data-bench-toggle` buttons, once, and ignores typing
  - the worklist rows carry the toggle, the open state, and the result panel marker
  - J / K is in the shortcut panel
- Full suite: 3,763 passed, 0 failed.
- Run check (development API and web):
  - J opened row 1, J row 2, K back to row 1; J pressed inside a field did nothing
  - on rows that take results, the cursor landed in the result panel (Interpreted ABO, Result); blocked rows opened with "No specimen is linked…"
  - nothing saved; no server errors
- **Found in the run check and fixed:** the cursor first went to the line comment box, then to its code picker. It now goes to the result panel.

### Next ranked residual

The compatible-first unit picker in the Compatibility crossmatch section.

## Iteration 430 — Crossmatch unit picker offers the patient's units (2026-11-05)

### Capability matrix rescan (compatibility)

Reserve on the Compatibility page offers the compatible units first (Cycle 406). Record crossmatch did not: its picker listed every unit in the crossmatch scope, so the technologist had to remember which units were reserved for the patient, or find them among all of stock (questions 2, 3).

### Design

- **`CrossmatchUnitOffer.Build`:**
  - offers the units reserved for the patient plus the compatible units in stock, each once
  - the note gives the count of each
  - when the compatible list cannot be read (for example no current ABO/Rh), every unit is offered and the reason is given
- **Record crossmatch:**
  - loads the offer when the patient is chosen (also from `?patientId=`)
  - **Show all units** and **Show this patient's units only** switch the picker
- The retrospective emergency follow-up (`?from=retro`) keeps every unit, since its unit was already issued.
- Recording still checks whichever unit is chosen.
- No server, schema, or API change.

### Requirements

- FRS-BB-605 (under URS-BB-506). Help: compatibility.

### Tests

- TEST-BB-471:
  - reserved plus compatible, counted once
  - singular and none wording
  - unreadable compatible list: every unit, with the reason
- Full suite: 3,766 passed, 0 failed.
- Run check (development API and web):
  - for MRN0022 the note reads "2 units reserved for this patient, and 18 more compatible units in stock"
  - the picker lists those 20 units
  - Show all units widens the list (40 matching "W0") and offers "Show this patient's units only"
  - nothing recorded; no server errors

### Next ranked residual

Rescan the chart's Products tab and the Issue desk for the next workflow gap.

## Iteration 431 — Discard expired units by scan (2026-11-05)

### Capability matrix rescan (Products tab, Issue desk, inventory)

- **Products tab:** already offers the compatible units.
- **Issue desk:** already offers the patient's ready units.
- **Inventory (the gap):** the expiration sweep marks units Expired, but nothing listed the expired units still on a shelf. Each had to be found by search and discarded from its own page, choosing the reason each time. Expired units can wait on the shelf, a selection risk if a label is misread (questions 1, 2, 5, 6).

### Design

- **`InventoryService.ListExpiredOnShelfAsync`** (*inventory.discard*; `GET /api/inventory/expired`):
  - units on a shelf that are Expired, or past expiry before the sweep runs
  - oldest first, with their location
- **`DiscardExpiredByScanAsync`** (`POST /api/inventory/units/discard-expired-by-scan`):
  - discards the scanned expired unit through `DiscardAsync` (permission, second verifier when required, audit, status history)
  - reason code EXPIRED, comment "Discarded by scan from the expired list"
  - refused: a unit not expired, one already discarded, an unknown scan, or a divided number matching several expired units
- **`/inventory/discard-expired`:**
  - linked from Inventory (**Discard expired**) and from a stock count's Expired badge
  - the second verifier stays chosen for the run; each scan's result is listed and the shelf list refreshes
- No schema change.

### Requirements

- URS-BB-516, FRS-BB-606. Help: inventory (route and section).

### Tests

- TEST-BB-472:
  - a marked-expired unit and a past-expiry unit are listed oldest first
  - a fresh unit is refused and stays
  - the expired ones are discarded with EXPIRED and the comment (scan case ignored)
  - a second scan says already discarded, and they leave the list
  - listing needs *inventory.discard*
- Full suite: 3,767 passed, 0 failed.
- Run check (development API and web):
  - the page lists W0001230001028 (AB−, expired 2026-11-02, Main Blood Bank Refrigerator)
  - scanning W0001230009301 (Available) shows "Not discarded … is not expired"
  - nothing discarded; no server errors

### Next ranked residual

A home or handoff count of expired units still on a shelf.

## Iteration 432 — Expired on shelf home card (2026-11-05)

### Capability matrix rescan (operations)

Cycle 431 added Discard expired. Nothing on the home screen said expired units were still on a shelf, so the page had to be remembered and checked (questions 2, 5, 6).

### Design

- **`DashboardQueueCatalog.ExpiredOnShelf`** ("Expired on shelf"):
  - for *inventory.discard*
  - counts the Discard expired list and opens it
- **`DashboardHome`:**
  - above zero, Needs attention, ahead of near expiry
  - in the Issue desk and Supervisor focus
- No server, schema, or API change.

### Requirements

- FRS-BB-607 (under URS-BB-516). Help: dashboard.

### Tests

- TEST-BB-473:
  - the card for discarders only
  - focus membership
  - needs attention above zero (ordered before near expiry), not at zero
- Full suite: 3,768 passed, 0 failed.
- Run check (development API and web): the home page shows "Expired on shelf 1" under Needs attention; no server errors.

### Next ranked residual

Rescan the remaining desks for an operations gap.

## Iteration 433 — Expired units on a shelf in the shift handoff (2026-11-06)

### Capability matrix rescan (operations)

The home card (Cycle 432) shows expired units still on a shelf to whoever can discard. The shift handoff, the record of what the next shift inherits, did not count them (questions 2, 5).

### Design

- **`ShiftHandoff.ExpiredOnShelf`:**
  - migration `20261116000000_ShiftHandoffExpiredOnShelf` and the SQLite bootstrap column
  - filled from `InventoryService.CountExpiredOnShelfAsync`, read-only, so the handoff needs only *shift.handoff*
- **Handoff line:** ends "EXPIRED ON SHELF n, to discard" above zero; the audit snapshot includes it.
- No API change.

### Requirements

- FRS-BB-608 (under URS-BB-516). Help: shift handoff.

### Tests

- TEST-BB-474:
  - the line wording at the end, quiet at zero
  - the count matches the expired list, read without the discard permission, and drops as units are discarded
- Full suite: 3,769 passed, 0 failed.
- Run check (development API and web): the startup bootstrap added `ExpiredOnShelf` to the development database; the Shift handoff page loads (no handoffs recorded); no server errors.

### Next ranked residual

Rescan the remaining desks; candidates needing a decision first: bulk verify of plain negatives (medical director), and the rejection notice to the EHR (interface choice).

## Iteration 434 — Return to supplier by scan (2026-11-06)

### Capability matrix rescan (inventory)

Returning units to the supplier (a recall, short-dated stock, a consignee reject) was one unit page at a time, with the reason typed for each. No list went with the shipment (questions 1, 2, 4).

**Found while building:** the status transition table treats a move to the same status as allowed. So returning a unit already returned replaced its recorded return reason, and marking a missing unit missing again replaced its missing reason, silently on both the API and the unit page (audit principle: never silently overwrite).

### Design

- **`InventoryService.ReturnToSupplierByScanAsync`** (*inventory.receive*; `POST /api/inventory/units/return-to-supplier-by-scan`):
  - returns the scanned unit through `ReturnToSupplierAsync`: audit, status history, reason
  - refused with the reason: a status that does not allow a return, a unit already returned, a divided number, an unknown scan, a blank reason
- **`/inventory/return-to-supplier`:**
  - linked from Inventory
  - the reason is given once per run
  - each scan's result is listed (supplier, product code, ABO/Rh); **Print list** prints them
- **`ReturnToSupplierAsync` and `MarkMissingAsync`:** refuse a unit already in that status, so the recorded reason stays.
- No schema change.

### Requirements

- URS-BB-517, FRS-BB-609. Help: inventory (route and section).

### Tests

- TEST-BB-475:
  - **by scan:** returned with the shipment reason and audited (scan case ignored); blank reason refused
  - **refusals:** a second scan says already returned; a reserved (Assigned) unit refused and unchanged; an unknown scan refused
  - **no overwrite:** the unit-page return of an already-returned unit is refused and the reason stays; marking an already-missing unit missing is refused
- Full suite: 3,770 passed, 0 failed.
- Run check (development API and web):
  - with a reason, scanning the reserved W0001230009201 shows "Not returned … is Crossmatched; it cannot be returned to the supplier"
  - nothing returned; no server errors

### Next ranked residual

Look for other same-status repeat actions that replace a recorded reason (hold, damaged), then rescan the desks.

## Iteration 435 — Repeated status actions keep the first reason (2026-11-06)

### Capability matrix rescan (audit, no silent change)

Cycle 434 found that the status transition table allows a move to the same status. Every unit action that writes its reason before the status change had the same flaw, through the API (the unit page hides the buttons):
- recall, direct and lookback (lookback already skips recalled units)
- hold
- mark damaged
- discard
- quarantine

Repeating any of them replaced the reason recorded the first time (audit principle: never silently overwrite).

### Design

- **`InventoryService.RefuseRepeat`:** returns "… is already X; the reason recorded then stays".
  - used by recall, hold, mark damaged, discard, mark missing, and return to supplier (the last two replace Cycle 434's own checks)
- **Quarantine:**
  - refuses a unit already quarantined for a specific reason, naming that reason
  - a unit held only by the routine receipt hold (PendingRelease or Unspecified) can still be quarantined for a specific reason, a legitimate escalation
- No schema, API, or page change. `docs/safety-rules.md` §7 records the guarantee.

### Requirements

- FRS-BB-610 (under URS-BB-517).

### Tests

- TEST-BB-476:
  - each of recall, hold, damaged, and discard twice: the second is refused and the first reason stays
  - a received unit (PendingRelease) is quarantined for a visual defect; a second quarantine is refused, naming "Clots seen", and the code and note stay
- Full suite: 3,771 passed, 0 failed.
- Run check: not applicable. The unit page already hides these actions for a unit in that status, so the repeat path is the API, covered by the test.

### Next ranked residual

Rescan the desks for the next operations gap.

## Iteration 436 — Test turnaround reports (2026-11-06)

### Capability matrix rescan (specimens, reports)

Specimen rejection already refuses a repeat (no overwrite there). The report catalog covered inventory, wastage, reactions, overrides, and rejections, but not turnaround time, the usual measure of whether STAT type and screens are verified in time. A supervisor had to work it out from result and order times (questions 2, 3).

### Design

- **Source `test_turnaround`** (view `rpt_test_turnaround`, SQLite `julianday` and SQL Server `DATEDIFF`):
  - one row per verified, current (not superseded) result on an order
  - minutes from order, and from tube receipt, to verification
  - with priority, test, order, accession, and patient
- **Seeded reports:**
  - **RPT-TAT:** detail, with a date range on verification
  - **RPT-TAT-BY-PRIORITY:** count, average, and longest minutes from order, by month, priority, and test
  - added to existing databases by the report seeding
- **Visual designer:** gains **Average** (numeric fields only; blanks ignored).
- No targets are judged; the facility compares with its own.

### Requirements

- URS-BB-518, FRS-BB-611. Help: reports.

### Tests

- TEST-BB-477:
  - **counted:** verified current results only (superseded and unverified left out); a correction counts at its own verification
  - **minutes:** from order (90) and from receipt (75)
  - **by priority:** Stat ABORH 2 results, average 50, longest 60; Routine 1, 240
  - **SQL view:** the same minutes
  - the seeded report count is 13, and every seeded report runs
- Full suite: 3,772 passed, 0 failed.
- Run check (development API and web):
  - Reports lists both turnaround reports
  - Test turnaround by priority runs on the development data (for example "2026-11 Routine ABORH 5 results, average 76, longest 100")
  - no server errors

### Next ranked residual

Rescan the desks for the next gap. Pending user decisions: bulk verify of negatives, and the EHR rejection notice.

## Iteration 437 — Patient antibody card (2026-11-06)

### Capability matrix rescan (antibody history, alerts)

Utilization (C:T ratio) already exists. Antibody history drives selection inside this hospital, but nothing gave the patient a card to carry. A patient with anti-K transfused elsewhere may have an antibody too weak to detect there, which risks a delayed hemolytic reaction (question 6). Mature transfusion services print antibody cards.

### Design

- **`DowntimePatientSummaryService.GetAntibodyCardAsync`:** reuses the one-patient summary row (current type, active antibodies, requirements in effect).
- **`RecordAntibodyCardPrintedAsync`:**
  - records a Print audit event against the patient, with what the card carried
  - refuses when there is nothing to carry
- **API** (*result.enter*): `GET /api/patients/{id}/antibody-card` and `POST …/antibody-card/printed`.
- **`/patients/{id}/antibody-card`:**
  - from **Antibody card** on the chart header
  - a wallet-size card (85.6 × 54 mm print)
  - **Print card** records, then prints
- No schema change.

### Requirements

- URS-BB-519, FRS-BB-612. Help: patient chart (route and section).

### Tests

- TEST-BB-478:
  - the card carries the type and only active antibodies (anti-Fya, not inactive anti-E)
  - the print is audited against the patient
  - a patient with nothing to carry is refused and not audited
  - an unknown patient is refused
- Full suite: 3,773 passed, 0 failed.
- Run check (development API and web):
  - MRN0004's card shows "Kellerman, Diane", DOB 1957-02-18, O+, anti-K, the instruction, and the issuer
  - MRN0024 shows "nothing to put on a card" with Print disabled
  - the chart header links to the card
  - not printed; no server errors

### Next ranked residual

Rescan the desks; still pending user decisions: bulk verify of negatives, and the EHR rejection notice.

## Iteration 438 — User access review (2026-11-06)

### Capability matrix rescan (administration)

Admin › Users listed accounts, roles, and status, but not when each person last signed in. Nothing recorded a periodic access review, which quality and inspection programmes expect: who has access, still needed, reviewed by whom. A dormant account with blood bank privileges is an access-control risk (question 7).

### Design

- **Admin › Users:**
  - **Last sign-in** column ("Never")
  - a filter for active, non-service accounts not signed in for a chosen number of days, default 90 (`DormantAccountRule`; the days are the reviewer's choice, not software policy), marked **Dormant**
- **Record access review** (*admin.users.manage*; `POST /api/admin/users/access-review`):
  - needs a note
  - writes one UserRole audit event (entity `AccessReview`) with every active account's roles, last sign-in, and lock and service flags
  - changes no account
- **Last review:** the page shows when, who, the note, and how many accounts (`GET …/access-review`).
- No schema change.

### Requirements

- URS-BB-520, FRS-BB-613. Help: users (Administrator Guide).

### Tests

- TEST-BB-479:
  - **dormant rule:** days since sign-in, never signed in, inactive and service accounts, zero days
  - **refused:** a blank note; recording without *admin.users.manage*
  - **recorded:** active accounts in, inactive ones out, no account changed; the last review is read back
- Full suite: 3,780 passed, 0 failed.
- Run check (development API and web):
  - Users shows "No access review recorded yet" and the Last sign-in column
  - the dormant filter lists 9 of 10 demo accounts (never signed in; development runs as DEV_ADMIN)
  - not recorded; no server errors
- **Fixed in the run check:** spacing between "Never" and the Dormant badge.

### Next ranked residual

Rescan the desks. Still pending user decisions: bulk verify of negatives, and the EHR rejection notice.

## Iteration 439 — Ward issue requests (2026-11-06)

### Capability matrix rescan (patient-product workflows)

Ready to issue lists every reserved unit, oldest reservation first, but the issue desk could not tell which units the ward wanted now, or where to send them. That came by phone and had to be remembered (questions 2, 3, 5). Mature systems carry a dispense or pick-up request from the floor.

### Design

- **On the reservation:**
  - `IssueRequestedUtc`, `IssueRequestedBy`, and `IssueRequestedTo` on `Allocation` (migration `20261117000000_AllocationIssueRequest`, SQLite bootstrap columns)
  - the audit keeps each request and cancellation with the old values
- **`IssueRequestService`** (*transfusion.document* or *issue.create*):
  - **Request:** destination required; refused for an unreserved unit or an open request
  - **Cancel:** reason required; audited with the request it ends
  - endpoints `POST /api/allocations/{id}/issue-request` and `/cancel`
- **Ready to issue:**
  - requested units first within each group, earliest request first
  - badge "Requested → destination time"
  - choosing one fills Issued to location when empty
  - the home Ready card names how many are requested
- **Chart Products tab:** `IssueRequestControl` on each reserved, unissued unit (Request issue, Cancel request).
- **Demo:** W0001230009201 for MRN0022 requested to ICU 4, once (marker setting).
- Issue still runs every check; a request ends when the reservation is no longer reserved. Inbound HL7 dispense requests are not included.

### Requirements

- URS-BB-521, FRS-BB-614. Help: issuing, patient products.

### Tests

- TEST-BB-480:
  - **refused:** blank destination; no permission; unreserved unit; a second request
  - **recorded:** the request (trimmed destination, requester) audited; the cancel needs a reason, clears the request, and is audited with the old values; a second cancel is refused
  - **ordering:** requested-first, earliest first; the card note, quiet without requests
- Full suite: 3,783 passed, 0 failed.
- Run check (development API and web):
  - Ready to issue lists W0001230009201 first with "Requested → ICU 4"
  - the home Ready card reads "5 · 1 requested by the ward"
  - MRN0022's Products tab shows the request and a Request issue button on the other reserved unit
  - no request sent live; no server errors

### Next ranked residual

Inbound HL7 dispense requests (interface choice), or the next desk rescan.

## Iteration 440 — Visit location suggested at issue (2026-11-06)

### Capability matrix rescan (issue form)

The issue form's **Issued to (location)** started empty unless the ward had requested the unit (Cycle 439). The patient's visit already records where they are, so the issuer typed what the system knew (questions 2, 3). Filling it in silently would be unsafe: ADT location lags behind moves to theatre or imaging (question 6).

### Design

- **`PatientVisitLocation.For`:** the reservation's open visit location, else the latest open visit with a location. Discharged, cancelled, or location-less visits suggest nothing.
- **`IssuingService`:** the Ready to issue rows carry it (`ReadyToIssueWorkItemDto.PatientVisitLocation`, optional encounter repository).
- **Issue form:** while the field is empty, shows "Patient's visit: location — **Use it**" (one click; the tooltip asks to confirm the patient is still there). A ward request's destination still fills first.
- No schema or API change.

### Requirements

- FRS-BB-615 (under URS-BB-521). Help: issuing.

### Tests

- TEST-BB-481:
  - the reservation's open visit wins; otherwise the latest open visit (trimmed)
  - a discharged reservation visit falls back to another open one
  - nothing when no open visit has a location
- Full suite: 3,785 passed, 0 failed.
- Run check (development API and web):
  - Issue from W0001230009202 (MRN0022) shows "Patient's visit: ICU 4 Use it"
  - Use it fills ICU 4 and the suggestion goes away
  - nothing issued; no server errors

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 441 — Chart issue panel destination (2026-11-06)

### Capability matrix rescan (issue from the chart)

The chart's Products tab has its own issue panel. Its **Location to issue** started empty, ignoring both the ward's request (Cycle 439) and the visit location the issue desk now suggests (Cycle 440). The two issue paths differed (question 2, consistent terminology).

### Design

- **Chart issue panel:**
  - starts with the request's destination (`PatientDetail.BeginIssueAllocation`)
  - while empty, offers "Patient's visit: location — **Use it**" (`PatientChartProductsTab`)
- **`PatientVisitLocation.For`:** gains an overload for the chart's visit list, sharing one choice with the issue desk.
- No server, schema, or API change.

### Requirements

- FRS-BB-616 (under URS-BB-521). Help: patient products.

### Tests

- TEST-BB-482: the chart's visit list gives the same choice: the reservation's open visit; otherwise the latest open one; nothing when empty.
- Full suite: 3,786 passed, 0 failed.
- Run check (development API and web), MRN0022 Products tab:
  - Issue on W0001230009202 shows "Patient's visit: ICU 4 Use it" with the field empty
  - Issue on the requested W0001230009201 starts with ICU 4
  - nothing issued; no server errors

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 442 — Overdue transfusion completions surfaced (2026-11-06)

### Capability matrix rescan (outstanding transfusions)

A transfusion started at the bedside and not completed within the reminder hours raises an alert, is counted on the shift handoff, and shows a red badge on Issued units. But the home **Outstanding issued** card gave only a count, and Issued units listed it by issue time, so the row could sit below routine issues (question 5, next action not surfaced).

### Design

- **`IssuingService.ListOutstandingIssuedAsync`:** overdue completions first; the rest keep issue order.
- **Issued units:** a warning names how many and says to complete them or check they are still running.
- **Home card:** `DashboardQueueCatalog.OutstandingNote` names "n transfusion completion(s) overdue", which puts the card under Needs attention.
- Same flag as the badge and the handoff (`TransfusionCompletionRule`, facility reminder hours). No schema or API change; no new rule.

### Requirements

- FRS-BB-617 (under URS-BB-451). Help: issuing, dashboard.

### Tests

- TEST-BB-483: an overdue transfusion issued later leads one issued earlier; the card note counts it.
- Full suite: 3,786 passed, 0 failed.
- Run check (development API and web): the running demo transfusion W0001230000099 is inside the 6-hour reminder, so the card has no note, Issued units no warning, and rows stay in issue order; no server errors. The overdue path is covered by TEST-BB-483.

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 443 — Supervisor card names what is urgent (2026-11-06)

### Capability matrix rescan (supervisor queue)

The supervisor queue mixes antibody ID reviews, results to verify, quarantine releases, missing or damaged units, and override reviews. The home card gave only a total, so a supervisor could not tell a result about to lose its specimen from routine quarantine work without opening the queue (question 5).

### Design

- **`DashboardQueueCatalog.SupervisorNote`** names, from the same queue rows (already permission-scoped):
  - results due before their specimen expires
  - missing or damaged units
  - overrides waiting for review
- Any of these puts the card under Needs attention. Antibody ID reviews and quarantine releases stay in the count only.
- No server, schema, or API change.

### Requirements

- FRS-BB-618 (under URS-BB-337). Help: dashboard.

### Tests

- TEST-BB-484: silent for empty or routine-only queues; names each kind with singular and plural wording.
- Full suite: 3,787 passed, 0 failed.
- Run check (development API and web): the card reads "8 · 2 missing or damaged units; 1 override to review", matching the queue (1 result verify not due soon, 4 quarantine, 2 missing or damaged, 1 override review); no server errors; nothing changed.

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 444 — Test history: who entered it, and the correction chains (2026-11-06)

### Capability matrix rescan (result review, timelines)

Test History kept every verified version, but each row showed only who verified it. Who entered the result and how it arrived (manual, instrument, interface) were stored but not shown, though the help said each version keeps them. A corrected result's versions were also scattered: current rows first, then prior ones by date. Explaining a change meant reading the whole list (questions 2, 5; traceability).

### Design

- **`PatientTestHistoryRowDto`** gains, from the stored result:
  - `EnteredBy`, `EnteredUtc`, `InvalidatedBy`
  - `HasRevision`: a later version, a superseded one, or a Corrected or Invalidated status
- **Test History, Entered / verified column:**
  - who entered it (entry time on hover)
  - a source badge
  - verified by, and invalidated by when it applies
- **Corrected or invalidated only (n):** appears when there are any. It shows each changed result's versions together, by tube and test, newest first (`PatientTestHistoryView.RevisedOnly`), with the reason on each row.
- Read-only; no schema or API shape change beyond the added DTO fields.
- **No demo data:** a seeded correction would put a superseded, different screen result on a demo patient. The integration test covers the chain instead.

### Requirements

- FRS-BB-619 (under URS-BB-067). Help: patient test history.

### Tests

- TEST-BB-485: an HGB result verified, corrected, and re-verified, beside an unchanged HCT.
  - every row names the enterer and Manual
  - HCT is not revised
  - the revised view is HGB only, 10.0 (current) then 9.0 (prior)
- Full suite: 3,788 passed, 0 failed.
- Run check (development API and web): MRN0001 Test History rows read "tech1 Manual · Verified by tech2" under Entered / verified; no corrected results there, so the revised-only toggle stays hidden; no server errors; nothing changed.

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 445 — New orders start from the visit (2026-11-06)

### Capability matrix rescan (orders: location and provider)

The chart's new order form chose the visit, but its ordering location started on the first catalog entry, whatever the visit said. The provider started empty even when the visit recorded an attending. An order placed quickly could be attributed to an unrelated location, which feeds utilization reports and billing (questions 2, 3).

### Design

- **`OrderVisitDefaults.LocationFor`:** the visit's current location (else admission location) is matched against active ordering locations by code, HL7 mapping code (as the interface already resolves locations), or name. It counts only when exactly one matches; otherwise nothing.
- **Order form:**
  - on opening and on changing the visit, the location is set from the visit when it matches, with "From the visit (place)" beneath
  - an unmatched place is named so the user chooses
  - while no provider is chosen, "Visit attending: name — **Use**" is offered. It is not filled in, because the ordering provider is whom the order is attributed to.
- `OrderingLocationRefDto` carries the HL7 mapping code. Server validation is unchanged; no schema change.
- **Demo:** the Intensive Care Unit ordering location gets HL7 mapping code "ICU 4" (set once, only when empty), the unit MRN0022's visit reports.

### Requirements

- URS-BB-522, FRS-BB-620. Help: patient orders.

### Tests

- TEST-BB-486:
  - match by code, mapping code, or name (trimmed, any case); admission location as fallback
  - no default for a name shared by two locations, an inactive location, an unknown place, or no visit
  - the attending is found by id; an unknown or missing attending gives nothing
- Full suite: 3,790 passed, 0 failed.
- Run check (development API and web), new order form opened, nothing saved:
  - MRN0022 (visit ICU 4) starts on Intensive Care Unit, "From the visit (ICU 4)"
  - MRN0001 (visit 4W Med/Surg) keeps the default and says 4W Med/Surg is not one ordering location
  - MRN0001 offers "Visit attending: Dr. Jane Smith"; Use fills the provider and the offer goes away
  - no server errors

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 446 — Alert inbox by kind, with time open (2026-11-06)

### Capability matrix rescan (alerts)

The Alerts inbox sorts open alerts most severe first, but:

- its own text named five of the nine alert kinds, and its "How it works" said only minimum stock, near expiry, and reagent QC close on their own, which no longer matched the help page
- with several kinds open there was no way to see one kind (say, every near-expiry unit) at a time
- each row gave only the raw UTC time it was raised, so how long an alert had waited had to be worked out (questions 2, 5)

### Design

- **`AlertInboxView.Kinds`:** one filter per open kind, named by `AlertRule.HelpName` (the help page's names) with a count, most severe kind first. A filter whose kind has cleared falls back to All.
- **`AlertInboxView.OpenFor`:** "open 45 min", "open 5 h", "open 3 d" under the raised time.
- **Page text:** lists which kinds close on their own and which wait for acknowledgement, as the help page does.
- Display only; acknowledging and the alert sources are unchanged. No server, schema, or API change.

### Requirements

- FRS-BB-621 (under URS-BB-409). Help: alerts.

### Tests

- TEST-BB-487:
  - kinds use the help-page names, most severe first, with counts; an unknown source keeps its code
  - time open in minutes, then hours under two days, then days
- Full suite: 3,792 passed, 0 failed.
- Run check (development API and web), nothing acknowledged:
  - three open alerts, filters "All (3) · Below minimum stock (1) · Transfusion not completed (1) · Near expiry (1)"
  - rows show "open 29 min" and "open 6 h"; Near expiry shows only that alert
  - no server errors
- The demo transfusion W0001230000099 has now passed its 6-hour reminder, so the Cycle 442 overdue path was seen live: Issued units lists it first under the warning, and the home card reads "1 transfusion completion overdue".

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 447 — Chart links land on what they name (2026-11-06)

### Capability matrix rescan (navigation inside the chart)

Several links dropped their context:

- Test History's accession opened the Specimens tab, not the tube (though Specimens can already open a tube's history from `?specimen=`), and its order number opened Orders unfiltered.
- On Visits, the visit number and **Open Orders** opened all orders, not that visit's.
- The pending-specimens worklist's patient and **Chart** links opened Specimens generally.

The user then searched again for what they had just clicked (questions 1, 2).

### Design

- **`PatientChartUrls`:** `Specimen` (tube history), `Order` (Orders searched for the number; blank opens unfiltered), `VisitOrders` (Orders filtered to the visit).
- **Chart:** reads `order` and `visit`, and re-applies a link whenever its tab, order, or visit changes, so a second link to Orders still lands.
- **Links:**
  - Test History: accession, order
  - Visits: visit number, Open Orders
  - pending-specimens worklist: patient, Chart
- Navigation only; no server, schema, or API change.

### Requirements

- URS-BB-523, FRS-BB-622. Help: visits, test history.

### Tests

- TEST-BB-488:
  - the links name the tube, the order (trimmed, escaped; blank gives plain Orders), and the visit
  - the chart reads every query name the links use
- Full suite: 3,794 passed, 0 failed.
- Run check (development API and web), MRN0001, navigation only:
  - Test History links are `?tab=specimens&specimen=1` and `?tab=orders&order=ORD0001`; clicking the order opens Orders searched for ORD0001, showing only it
  - Visits → Open Orders on VIS-2025-882 opens Orders filtered to that visit
  - the specimen link opens ACC0001's history
  - no server errors

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 448 — Deviations close with their corrective action (2026-11-06)

### Capability matrix rescan (quality records)

The help said a deviation's "closure requires the outcome, not a blank completion", but the service closed one with no corrective action. An empty corrective action also overwrote a recorded one; only the audit kept it. Reopening left the old close stamp on an open record. The list showed only id, title, severity, status, and context: no description, no reporter or date, no age, and no ordering by severity (questions 2, 5, 7; never silently overwrite).

### Design

- **`DeviationStatusRule`** (DEV-STATUS, HardStop):
  - closing needs a corrective action, recorded or entered; it may say why none is needed
  - a recorded action cannot be replaced with blank text
- **`DeviationService.UpdateStatusAsync`:**
  - applies the rule and trims the action
  - stamps the close only on the move to Closed, and clears the stamp on reopening
  - audits old and new status, action, closed by, and closed time
- **`DeviationDto`** adds closed by and time.
- **Deviations page:**
  - order: open first, most severe, oldest (`DeviationListView.Order`)
  - each row: description, corrective action, reporter, reported time, time open; closed rows say by whom and when
  - the update panel shows the description and the rule's message, with Save disabled until it passes
- No schema change.
- **Demo:** two deviations, audited as the service would, added once: an open Major one (W0001230000001 found in the wrong refrigerator, under review) and a closed Minor one with its corrective action (ACC0001 label smudged).

### Requirements

- URS-BB-524, FRS-BB-623; safety rule DEV-STATUS. Help: deviations.

### Tests

- TEST-BB-489:
  - rule: close without an action (none or blank) stopped; with a recorded or entered action allowed; blanking a recorded action stopped; editing it allowed
  - service: close without an action refused; close trims and stamps; blanking refused; reopening clears the stamp and the audit keeps it; closing again keeps the recorded action
  - list order
- Full suite: 3,802 passed, 0 failed.
- Run check (development API and web; the dev seed added the two demo deviations):
  - Deviations lists the open Major one first ("tech2 · open 30 h"), then, with Show closed, the Minor one "Closed by supervisor1"
  - on the open one, choosing Closed with no corrective action disables Save with the rule's message; typing one enables it; cancelled without saving
  - no server errors

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 449 — Open deviations on the home screen (2026-11-06)

### Capability matrix rescan (quality queue)

Cycle 448 made Deviations a proper record, but nothing on the home screen said one was open. A critical deviation, including those raised automatically for an incompatible retrospective crossmatch, was found only by visiting the page (question 5).

### Design

- **Open deviations card** for `deviation.manage`, opening `/deviations`:
  - counts every deviation not closed
  - names critical ones (`DashboardQueueCatalog.DeviationsCard`), which puts the card under Needs attention
- In the Supervisor focus; ranked after the shift handoff among attention cards.
- Reads the existing deviation list; no server, schema, or API change.

### Requirements

- FRS-BB-624 (under URS-BB-524). Help: dashboard.

### Tests

- TEST-BB-490:
  - shown for deviation managers, not for others; in the Supervisor focus, not Bench
  - closed rows are not counted; no note without a critical deviation; "1 critical" with one open
- Full suite: 3,803 passed, 0 failed.
- Run check (development API and web): the home screen shows "Open deviations 1" (the open Major demo deviation) under Queues with work, with no note since none is critical; no server errors; nothing changed.

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 450 — Deviations on the unit and patient pages (2026-11-06)

### Capability matrix rescan (traceability of quality records)

A deviation could be linked to a unit or patient, but neither page showed it: someone opening W0001230000001 could not see that it had been found in the wrong refrigerator. Recording one meant choosing the record type and typing the record's internal id, which users do not know (questions 2, 3, 5).

### Design

- **`DeviationService.ListForRecordAsync`:** the deviations linked to one record, matching older stored spellings (`DeviationRecordTypes`), open first then newest.
- **API:** `GET /api/deviations?contextType=&contextId=`, same `deviation.manage` gate.
- **`LinkedDeviations` panel** on the unit overview and the patient chart overview (deviation managers only):
  - each linked deviation, its severity, status, and reporter, linking to its update panel
  - **Record deviation** opens Deviations with the record type and id filled in, only for a known type
- No schema change.

### Requirements

- FRS-BB-625 (under URS-BB-524). Help: deviations.

### Tests

- TEST-BB-491: a unit's deviations include the older "Unit" spelling, open first; a patient with the same id and another unit are excluded; an unlinked type gives none.
- Full suite: 3,804 passed, 0 failed.
- Run check (development API and web), nothing created:
  - unit W0001230000001 shows "#1 Unit found in the wrong refrigerator · Major · UnderReview · reported by tech2"
  - Record deviation opens Deviations with "Blood unit" and id 1 filled in
  - MRN0001's chart overview says none are linked
  - no server errors

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 451 — Link a deviation's record by scan (2026-11-06)

### Capability matrix rescan (deviation entry)

Cycle 450 prefilled the record when a deviation is started from a unit or patient page. Started from Deviations itself, linking still meant choosing a type and typing an internal id. The DIN, MRN, accession, or order number in hand could not be used, though the top-bar lookup already resolves them safely (questions 2, 4).

### Design

- **Related record: scan or type** on the Deviations form, through the existing lookup:
  - an exact single match links at once and is named ("Linked: Blood unit W0001230000001 (...)")
  - several matches are offered to pick from; none says so
- **`DeviationRecordTypes.FromLookup`:** patient, unit, specimen, order map to the stored types.
- **Type plus id** stays for issues, results, and HL7 messages; changing either clears the linked label.
- A lookup result applies only if the field still holds what was looked up, so a scan typed while a lookup runs is not cleared.
- No server, schema, or API change.

### Requirements

- FRS-BB-626 (under URS-BB-524). Help: deviations.

### Tests

- TEST-BB-492: every lookup kind maps to a record type; record pages find older spellings.
- Full suite: 3,806 passed, 0 failed, before the two page-only fixes below; after them the web build, Help.Tests (36), the catalog check, and TEST-BB-492 passed.
- Run check (development API and web), nothing created:
  - ACC0001 + Enter links "Specimen ACC0001 (Specimen for Demo, Patricia ...)"
  - MRN002 offers four patients with "Several records match; choose one"; picking MRN0021 links it as a Patient
  - an unknown identifier says nothing matches
  - no server errors
- Found and fixed during the run check:
  - the prompt was hidden while an earlier link was shown; both now show
  - a second identifier typed during a lookup was cleared when the first result arrived; stale results are now dropped, so nothing typed is lost and no wrong record is linked

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 452 — The shift handoff carries open deviations (2026-11-06)

### Capability matrix rescan (shift handoff)

The handoff counts the open bench, issue, inventory, cooler, and reaction work, but not quality deviations. A critical deviation raised on the night shift (for example the one opened automatically for an incompatible crossmatch after emergency release) was not passed on to the next shift (question 5).

### Design

- **`ShiftHandoff`:** gains `OpenDeviations` (not closed) and `CriticalDeviations`.
  - migration `20261118000000_ShiftHandoffDeviations`, dev bootstrap columns
  - read straight from the deviation rows, so `shift.handoff` is enough, as for the other counts
- **Audit:** the handoff snapshot includes both.
- **Packet line:**
  - leads with "CRITICAL DEVIATION OPEN: n" after the MTP and incompatible-after-release items
  - ends with "deviations open n"
  - silent at zero

### Requirements

- FRS-BB-627 (under URS-BB-524). Help: shift handoff.

### Tests

- TEST-BB-493:
  - packet line quiet at zero; critical leads in capitals; open count last
  - a handoff by a user with only `shift.handoff` counts open and critical deviations (a closed critical one is not counted) and keeps them in the audit snapshot
- Full suite: 3,808 passed, 0 failed.
- Run check (development API and web): the dev bootstrap added both columns; the handoff page loads and its queries read `OpenDeviations` and `CriticalDeviations` without error; no server errors.
- The dev database has no handoffs, and none was recorded (it would accept responsibility for live work), so the packet line was not seen live. TEST-BB-493 covers it.

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 453 — Reagent QC on the bench home screen (2026-11-06)

### Capability matrix rescan (bench queues)

Reagent QC has a worklist (overdue daily QC, failed QC, QC waiting for review) and raises alerts, but the bench home screen had no card for it. A tech starting the shift saw pending tests and specimens, not that the day's QC was overdue or had failed (question 5).

### Design

- **Reagent QC card** for `result.enter` (who records and reviews QC), opening `/reagents`:
  - counts the existing worklist
  - names failed and overdue QC (`DashboardQueueCatalog.ReagentQcNote`), which puts the card under Needs attention; review stays in the count
- In the Bench focus; ranked after in-transit units among attention cards.
- No server, schema, or API change.

### Requirements

- FRS-BB-628 (under URS-BB-391). Help: dashboard.

### Tests

- TEST-BB-494:
  - shown for result entry, not for issue-only users; in the Bench focus
  - no note when the worklist is empty or only waiting for review; "1 failed; 2 overdue" otherwise
- Full suite: 3,809 passed, 0 failed.
- Run check (development API and web): the home screen lists Reagent QC under Clear (the dev QC worklist is empty, matching Alerts); the failed and overdue note was not seen live, TEST-BB-494 covers it; no server errors; nothing changed.

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

### Iteration 453 follow-up — the dev bootstrap kept recreating the database for additive columns

While checking Cycle 453 live, the demo times had moved: the development database had been deleted and re-seeded when the Cycle 452 API first started. The bootstrap looks for missing required (NOT NULL) columns before it applies its additive `ALTER TABLE ... DEFAULT` list. The two new handoff columns counted as missing, so it recreated the file from the model, dropping any data entered in it.

- **Fix:** a missing required column that is on the additive list no longer triggers the rebuild. It is added in place with its default. A required column missing from the list still rebuilds, as before.
- **Test:** `DevelopmentSqliteBootstrapTests.InitializeAsync_AddsAMissingAdditiveColumn_WithoutRecreating`. Drop `ShiftHandoffs.CriticalDeviations` from a database holding a marker setting; the bootstrap adds the column back and the marker survives. The existing recreate test still passes.
- Production (SQL Server) uses migrations and was never affected.

## Iteration 454 — The reagent QC block is shown before results are keyed (2026-11-06)

### Capability matrix rescan (result entry)

With facility policy *Block results when reagent QC is open* on, Save refuses patient results while QC is overdue, failed, or unreviewed (QC-RESULT-BLOCK). The bench only learned this at Save, after keying the results, and the message did not say which lot (questions 1, 5).

### Design

- **`ReagentQcResultBlock.For`:** applies the same `ReagentQcResultGate` Save uses, and returns the message and the open QC work when it blocks.
- **Test Worklist:** reads the policy and the QC worklist on opening. When blocked, it shows a red notice with each open item (kind, lot, message) and **Open Reagent QC**.
- **Policy access:** the flag is added to the readable policy keys (`GET /api/reference/policies/{key}`).
- Save still enforces the gate; no schema change.

### Requirements

- FRS-BB-629 (under URS-BB-391). Help: test worklist.

### Tests

- TEST-BB-495: nothing when the policy is off or no QC is open; with both, the gate's own message and the open lot.
- Full suite: 3,811 passed, 0 failed.
- Run check (development API and web), nothing changed:
  - `GET /api/reference/policies/Result.BlockWhenReagentQcOpen` answers 200 (an unknown key still 404)
  - the worklist loads its 13 rows with no notice, since the policy is off by default and no QC is open
  - no server errors
- The facility policy was not turned on live to show the notice; TEST-BB-495 covers that path.

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 455 — Daily reagent QC: scan the lot, see the history (2026-11-06)

### Capability matrix rescan (reagent QC)

The Daily QC form picked the lot from a list and the tech typed the expected reaction every day, though it rarely changes for a reagent. Once reviewed, a QC row disappeared from the page: nobody could see last week's QC, who did it, or who reviewed it without the audit trail (questions 2, 3, 4; traceability).

### Design

- **`ReagentQcService.ListRecentQcAsync`** (`GET /api/reagents/qc?days=`, default 14, 1 to 90): newest first, with reagent and lot.
- **Recent QC table** on Reagent QC: expected, observed, passed or failed, performer, reviewer or "Not reviewed".
- **Scan lot:** selects the lot only when exactly one open lot carries the number (`ReagentQcHistory.LotForScan`); otherwise says so.
- **Expected:** while empty, the form offers the expected result last recorded for that reagent (`ReagentQcHistory.LastExpected`), "Use it" to fill. It is never filled without the click, because the package insert is the authority.
- Recording and review are unchanged; no schema change.

### Requirements

- FRS-BB-630 (under URS-BB-391). Help: reagents.

### Tests

- TEST-BB-496:
  - QC older than 14 days is left out; newest first with reagent code; a failed row reads failed
  - a new lot is offered the reagent's last expected; an unknown reagent gets nothing
  - a scan selects the one lot (trimmed, any case); an unknown or duplicated number selects nothing
- Full suite: 3,812 passed, 0 failed.
- Run check (development API and web), nothing recorded:
  - the dev database has no reagents, lots, or QC, so Recent QC says none in 14 days and the lot list is empty
  - scanning "A-100" says no single open lot carries it
  - no server errors
- The table rows, the selection, and the expected offer were not seen live; TEST-BB-496 covers them.

### Next ranked residual

Rescan the desks; open decisions: bulk verify of negatives, the EHR rejection notice, inbound HL7 dispense requests.

## Iteration 456 — Batch verification of routine results (OCD-061) (2026-11-06)

### Decision

Decided 2026-11-06 by the product owner:

- tests: ABORH, ABSC, and DAT; manual and instrument results; no cap on the batch size
- ABO/Rh only when it matches the patient's historical type
- patients with antibody history or special requirements are left out
- the verifier must not be the person who entered the result

### Design

- **`BatchVerifyRule`** (BATCH-VERIFY), per result:
  - ABSC or DAT plainly Negative, or ABORH matching the current historical type
  - waiting for its first verification (not a correction)
  - entered by someone else
  - no antibody history (active or not) and no enforced special requirement in effect on the patient
- **Facility policy** `Result.AllowBatchVerify`, default off.
- **`BatchVerifyService`:**
  - lists candidates from the pending worklist
  - verifies a chosen set: re-applies the rule to each id (the list may be stale, and any id may be sent), then runs the normal `VerifyResultAsync` with all its checks (specimen, order hold, self-verify, ABO/Rh history)
  - leaves any result that fails unverified with the reason
  - adds an audit row "Verified in batch {id}" per verified result
- **API** (result.verify): `GET /api/results/batch-verify/candidates`; `POST /api/results/batch-verify`, which also captures billing per verified result, as single verification does.
- **Test Worklist, To verify:** **Verify in a batch**, with all candidates ticked, **Verify n selected**, and the outcome listing anything not verified and why.
- No schema change.

### Requirements

- URS-BB-525, FRS-BB-631; safety rule BATCH-VERIFY; OCD-061 recorded. Help: test worklist.

### Tests

- TEST-BB-497:
  - rule: plain negatives and matching types qualify; other tests, positives, qualified negatives, corrections, own or unknown entries, antibody history, special requirements, first types, differing types, and unreadable values do not
  - service: off by default (nothing verified, candidates disabled); once on, only the matching ABO/Rh and the negative screen are verified, each skip names its reason, and both verified results carry the batch audit row
- Full suite: 3,815 passed, 0 failed.
- Run check (development API and web), nothing verified:
  - with the setting off (default), candidates answer `{"enabled":false}` and a batch is refused ("Batch verification is turned off in Facility Policies.")
  - To verify (2) shows no batch panel
  - no server errors
- The setting was not turned on in the dev database (a facility decision), so the panel was not seen live; TEST-BB-497 covers the on path.

### Next

OCD-062 (no-result notice to the EHR on rejection), then OCD-063 (inbound dispense requests).

## Iteration 457 — The EHR is told when a tube is rejected (OCD-062) (2026-11-06)

### Decision

Decided 2026-11-06 by the product owner: send a "no result" message (ORU, OBR-25 X, OBX-11 X) with the reason and "recollect"; the order stays open.

### Design

- **`ISpecimenRejectionPublisher`** (application abstraction), implemented by `Hl7OutboundService.PublishSpecimenRejectedAsync`.
- **`SpecimenService.RejectAsync`** calls it after the rejection is saved. A failure never undoes the rejection; it returns warning SPEC-REJECT-EHR (phone the ward, check the HL7 page).
- **What is queued:**
  - only when an enabled outbound Results interface exists
  - one ORU^R01 per open test line on an open order linked to the tube (not completed, cancelled, or discontinued; active; not already verified)
- **`Hl7OruBuilder.BuildNoResult`:**
  - OBR-2 placer order number; OBR-3 filler number or accession; OBR-4 test; OBR-25 X
  - OBX-3 test; OBX-5 "Specimen {accession} rejected: {reason}. Recollect."; OBX-11 X
- Sent and retried by the existing outbound transport and error queue. No schema change.

### Requirements

- FRS-BB-632 (under URS-BB-509); OCD-062 recorded. Help: patient specimens.

### Tests

- TEST-BB-498:
  - message: OBR-2 order, OBR-3 accession, OBR-25 X, OBX-11 X, the reason text
  - service: nothing queued without an enabled results interface; with one, one ORU per open line (ABORH and ABSC), none for the verified DAT line, each naming the order and "rejected: Clotted. Recollect."; a publisher failure keeps the rejection and warns
- Full suite: 3,818 passed, 0 failed.
- Run check (development API and web): the API starts with the new publisher wired; the pending specimens list loads (2 rows); no server errors.
- No tube was rejected live (a real clinical action), so no notice was queued; TEST-BB-498 covers it.

### Next

OCD-063: inbound dispense requests from the EHR.

## Iteration 458 — Dispense requests from the EHR (OCD-063) (2026-11-06)

### Decision

Decided 2026-11-06 by the product owner: accept the EHR's request to send a reserved unit now as an issue request, built to the HL7 v2.5 standard (OMB^O27) with the field mapping documented.

### Design

- **Interface:** the blood-product interface accepts OMB (`RAS,BPS,OMB`); the processor routes it to `HandleDispenseRequestAsync`.
- **Patient:** found by PID-3, following merges; a merged-away chart is refused.
- **`Hl7DispenseRequestMapper`:**
  - ORC-1 (CA/OC/DC withdraws), ORC-2 placer order, ORC-12 requester
  - destination: BPO-14, else PV1-3
  - DIN only when the facility maps a path (none by default; a standard OMB carries none)
- **`InterfaceDispenseRequestService`:**
  - the unit by DIN when given, else the one unit reserved on the placer order
  - sets the same request fields as a ward request (Cycle 439): requested by "EHR (HL7): name", audited with MSH-10; the tracked reservation is changed, as the ward path does
  - a cancel clears an open request (nothing to cancel is acknowledged)
  - the same destination again is acknowledged without change
  - no match, several matches, an unknown order, no destination, or a different destination already requested is refused to the error queue with the reason
  - never issues; no user permission is checked here (interface trust, OCD-034)
- **Data items** added to the Bpam catalog with defaults; mapping documented in hl7-design.md §2.6.
- No schema change.

### Requirements

- FRS-BB-633 (under URS-BB-521); OCD-063 recorded. Help: issuing; HL7 design.

### Tests

- TEST-BB-499:
  - by placer order: the one reserved unit is requested to BPO-14, requested by "EHR (HL7)", audited with the control id; a CA withdraws it
  - with no BPO-14, PV1-3 is used
  - an order with two reserved units goes to the error queue and nothing changes
  - by DIN, that unit is chosen even when the order has two; an unreserved DIN is refused
- Found while testing: the reservation search reads without tracking, so changing that copy collided with the tracked entity at save. The service now changes the tracked reservation by id.
- Full suite: 3,822 passed, 0 failed.
- Run check (development API and web): the API starts with the dispense service wired into the HL7 processor; the HL7 page loads; no server errors.
- No OMB was sent to the dev database (it would change a demo reservation); TEST-BB-499 covers the message path.

### Next ranked residual

Confirm OCD-062 and OCD-063 message details with the EHR team. Otherwise rescan the desks.

## Iteration 459 — Batch verification names what it left unverified (2026-11-06)

### Capability matrix rescan (batch verification)

The batch outcome listed each result it left unverified as "Result 1234: reason". The internal id cannot be matched to a patient or test on the worklist, so the tech had to work out which row it meant (question 2).

### Design

- **`BatchVerifySkippedDto.Label`:** "ABSC for Doe, Jane (MRN0001)", built on the server from the result and its patient (optional patient repository).
- **Worklist:** the outcome lists the label with the reason, falling back to the id only when the result no longer exists.
- No schema change.

### Requirements

- FRS-BB-631 updated. Help: test worklist.

### Tests

- TEST-BB-497 extended: the positive screen left unverified is labeled "ABSC for Batch, NEW (MRN-BV-NEW)".
- Full suite: 3,822 passed, 0 failed.
- Run check (development API): starts with the patient repository wired; candidates answer `{"enabled":false}` and a batch is still refused while the setting is off; no server errors.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team.

## Iteration 460 — Supervisor queue: deadlines, patients, and links (2026-11-06)

### Capability matrix rescan (supervisor queue)

The home card names results due before specimen expiry (Cycle 443), but in the queue such a row looked like any other; the deadline was only in the detail text. Rows about a patient did not link to the chart, and nothing could link to one kind of work, since the kind filter was click-only (questions 1, 5).

### Design

- **Due before specimen expiry:** a red badge on results whose verification deadline is near (the existing `DueSoon`).
- **Patient link:** a row with a patient links its title to the chart.
- **`/supervisor?kind=`:** opens the queue on that kind (`SupervisorQueueRule.KindFromQuery`, any case); an unknown kind, or one with no items, opens all.
- Display only; no server, schema, or API change.

### Requirements

- FRS-BB-634 (under URS-BB-337). Help: supervisor queue.

### Tests

- TEST-BB-500: known kinds match in any case and trimmed; unknown, blank, or missing opens all; five distinct kinds.
- Full suite: 3,823 passed, 0 failed.
- Run check (development API and web), nothing changed:
  - `?kind=inventorydiscrepancy` opens on "Missing or damaged (2)"
  - `?kind=ResultPendingVerification` shows "Verify ABSC — Interface, Helen (MRN0009)" linking to `/patients/9`; it is not near its deadline, so no badge
  - no server errors

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team.

## Iteration 461 — Audit trail by scan (2026-11-06)

### Capability matrix rescan (audit review)

The Audit Trail filtered one record by entity type and a numeric entity id, the internal id staff never see. Reviewing everything done to a unit or a patient meant finding that id first, and no record page linked to its trail (questions 2, 4; traceability).

### Design

- **Record: scan or type** on the Audit Trail, through the top-bar lookup:
  - an exact single match sets the entity type and id and searches
  - several matches are offered; none says so
  - a stale lookup result is dropped, as on the Deviations form
- **`LookupRecordType.For`:** the lookup kind to the stored entity type. `DeviationRecordTypes.FromLookup` now uses it.
- **Link:** `/audit?entityType=&entityId=` opens with those filters. The unit page's Status history links to it as **Full audit trail** (audit.read only).
- Read-only; no server, schema, or API change.

### Requirements

- URS-BB-526, FRS-BB-635. Help: audit trail.

### Tests

- TEST-BB-501:
  - every lookup kind maps to the audit entity type
  - the unit page's link uses the query names the Audit page reads
- Full suite: 3,825 passed, 0 failed, before the page-only fix below; after it the web build, Help.Tests (37), and the catalog check passed.
- Run check (development API and web), read-only:
  - unit W0001230000001's Status history shows **Full audit trail**, which opens `/audit?entityType=BloodUnit&entityId=1` with only that unit's rows
  - scanning "MRN0001" sets Patient / 1 and says "Showing the trail of MRN0001 (Demo, Patricia)."
  - no server errors
- Found and fixed during the run check: from the link, the Entity type select showed a stale option (ActionBilling) while the rows were correctly filtered to BloodUnit, because the type was set before the type list loaded. The link's filters are now applied after the lists load, and the select shows BloodUnit.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team.

## Iteration 462 — Audit trail from the patient chart and the specimen history (2026-11-06)

### Capability matrix rescan (audit review)

Cycle 461 linked a unit's Status history to its audit trail. The patient chart's Timeline and a specimen's History panel, the two other record histories, did not, so their full audit trail still had to be searched for (question 1).

### Design

- **`AuditUrls.For(entityType, id)`:** builds the Audit Trail link; the unit link uses it too.
- **Full audit trail** (audit.read only) on the patient chart's Timeline header (Patient) and the specimen History panel (Specimen).
- Read-only; no server, schema, or API change.

### Requirements

- FRS-BB-635 extended. Help: audit trail.

### Tests

- TEST-BB-501 extended: the patient timeline and the specimen history use `AuditUrls.For` with their types; the helper builds the expected link.
- Full suite: 3,825 passed, 0 failed.
- Run check (development API and web), read-only:
  - MRN0001's Timeline links to `/audit?entityType=Patient&entityId=1`
  - ACC0001's History panel links to `/audit?entityType=Specimen&entityId=1`, which opens on that specimen's rows
  - no server errors

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team.

## Iteration 463 — Duplicate testing warned at order entry (2026-11-06)

### Capability matrix rescan (safety rules: duplicate testing)

Nothing warned when a test was ordered for a patient who already had the same test open on another order, so a duplicate antibody screen meant a second draw and a second result to reconcile. Separately, the create-order endpoint returned only the new id, dropping every warning from order creation, including the order rule engine's (question 6).

### Design

- **`DuplicateTestOrderRule`** (ORD-DUP-TEST, warning): names each test being ordered that the patient already has open (order not completed, cancelled, or discontinued; line active; result not verified), with those order numbers.
- It never blocks: a repeat can be intended (a second ABO/Rh determination, a new specimen after rejection), and the new order can be cancelled in one step.
- **`OrderService.CreateAsync`:** checks before saving, so the order never finds itself, and returns the warning with the rule engine's.
- **`POST /api/patients/{id}/orders`:** now returns `{data, warnings}` through `EndpointResults.Created`, so the chart shows these warnings (and the rule engine's, which were dropped before).
- HL7 inbound orders use the same path; the warning is returned but not shown anywhere there.
- No schema change; no new time window or policy.

### Requirements

- URS-BB-527, FRS-BB-636; safety rule ORD-DUP-TEST. Help: patient orders.

### Tests

- TEST-BB-502:
  - rule: each repeated test named with its open orders (any case, trimmed); nothing when none repeats
  - service: a second order with ABSC names "ABSC on ORD-DUP-1"; its verified DAT line and an unordered ABORH are not named; once the first order is completed, ordering ABSC again does not warn
- Full suite: 3,827 passed, 0 failed.
- Run check (development API), read-only:
  - the order list answers 200
  - an invalid create is still refused with its message (400 "At least one test or product is required.")
  - no server errors
- No order was created live (it would add a demo order), so the warning was not seen in the chart; TEST-BB-502 covers it.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted).

## Iteration 464 — Warnings no longer lost on the way to the screen (2026-11-06)

### Capability matrix rescan (warning delivery)

Cycle 463 found the create-order endpoint dropping warnings. This cycle checked every service method that returns warnings against the endpoint that serves it:

- all others pass them through: specimen reject and edit, result entry, verification, correction and invalidation, blood type, antigen profile, antibody, special requirement, merge, billing cancel
- order **edit** evaluated the order rules but returned `Ok(order)` with no warnings, so a rule warning raised by an edit was lost
- an edit could also add a test already open on another order without the Cycle 463 duplicate warning

### Design

- **`OrderService.UpdateAsync`** returns the rule engine's warnings and the duplicate-test warning (`RuleEvaluation`); the PUT endpoint already passes evaluations through.
- **Duplicate check on edit:** it leaves out the order being edited, so keeping its own lines is not a duplicate.
- No schema or API shape change.

### Requirements

- FRS-BB-636 extended. Help: patient orders.

### Tests

- TEST-BB-502 extended:
  - editing an order and keeping only its own DAT does not warn
  - adding ABSC, open on another order, warns naming that order and not the order being edited
- Full suite: 3,828 passed, 0 failed.
- Run check (development API), read-only: the order list answers 200; an invalid edit is refused with its message (400); no server errors. No order was edited live.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted).

## Iteration 465 — The current visit in the chart header (2026-11-06)

### Capability matrix rescan (patient and encounter)

The chart header showed identity, blood type, the current specimen, and alerts, but not the patient's current visit. Patient type, location, and attending (where blood goes, who is responsible) meant opening the Visits tab. A patient with two open visits looked the same as one with one (questions 1, 2, 6).

### Design

- **Current visit line** in the header: visit number (linking to that visit's orders), then type, location (current, else admission), and attending, each only when recorded.
- **`CurrentVisit.For`:** the most recently admitted open visit (active, not discharged), plus how many others are open.
- **Edge cases:** "+n more open" when another visit is open; "No open visit" when none is.
- Display only; uses the visits the chart already loads. No server, schema, or API change.

### Requirements

- URS-BB-528, FRS-BB-637. Help: patient chart.

### Tests

- TEST-BB-503:
  - the latest open visit is chosen; discharged and cancelled visits are left out and the other open one counted; none when nothing is open
  - the description holds only the recorded parts (unknown type and blank attending left out; admission location when there is no current one)
- Full suite: 3,830 passed, 0 failed.
- Run check (development API and web), read-only:
  - MRN0001: "Current visit VIS-2026-001 · Inpatient · 4W Med/Surg · Attending Dr. Jane Smith", linking to `?tab=orders&visit=1`
  - MRN0022: "Current visit VIS-2026-022 · Inpatient · ICU 4" (no attending recorded)
  - MRN0024 (no visits): "No open visit"
  - no server errors
- No demo patient has two open visits, so "+n more open" was not seen live; TEST-BB-503 covers it.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted).

## Iteration 466 — Find a patient by visit number (2026-11-06)

### Capability matrix rescan (patient and encounter; search everywhere)

The top-bar find box took a DIN, accession, order number, MRN or prior MRN, or a name. Not a visit (encounter) or account number, the number on requisitions and many wristbands, nor an account or other identifier recorded for the patient (those matched only partially, so never opened). Since Cycle 465 the header shows the visit number, yet it could not be searched. An order hit opened the Orders tab unfiltered, though the chart can open one order since Cycle 447 (questions 1, 2, 4).

### Design

- **Visit or account number**, exact and any case: a patient hit opening that visit's orders (`?tab=orders&visit=`).
  - The row says "Visit {number} · type · location · open", or "discharged {date}" or "cancelled", so an old requisition is not taken for the current visit (question 6).
  - A visit whose patient was merged away opens the survivor plainly, saying it was merged.
- **Other identifiers:** an active account number or other identifier matches exactly like a prior MRN, saying which it matched. Prior names stay partial-only (never open directly).
- **Order hits** open that order (`OrderHref`, now shared with the chart links).
- A patient matched by more than one identifier is one row. Read-only; no server write, schema, or API change.
- **Found in the live check:** the browser tab title kept the first page's title after in-app navigation ("Chart MRN0022" while MRN0001 was open), because the page head was not interactive. Fixed by making `HeadOutlet` interactive, as the routes are; every page's title now follows navigation (question 6: two open tabs must not name the wrong patient).

### Requirements

- URS-BB-529, FRS-BB-638. Help: finding records.

### Tests

- TEST-BB-504:
  - a visit number (any case) and its account number open that visit, saying it is open, with the location
  - a discharged visit of a merged patient opens the survivor, saying discharged and merged
  - an account number and an other identifier open the patient; an inactive one does not; a prior name stays a partial match
  - an order hit opens that order
- Full suite: 3,833 passed
- Live (read-only): `vis-2025-882` opened MRN0001's chart on Orders filtered to that discharged visit, the tab titled "Chart MRN0001"; `ORD0022` opened MRN0022 on that order; the placeholder names visits; no server errors. Account and other identifiers are not in the demo data (tests cover them).

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted).

## Iteration 467 — Where the patient is, on the list and in the find box (2026-11-06)

### Capability matrix rescan (patient and encounter: location)

Since Cycle 465 the chart header shows the current visit, but the patient list and the find box did not say where a patient is. Two similar names were told apart only by MRN and date of birth, and a call for blood names the ward, not the MRN (questions 2, 3, 6). Considered and set aside: registering an unidentified (trauma) patient in the LIS, which needs naming and MRN-assignment policy from registration (left for the SME, not invented here).

### Design

- **One rule:** `CurrentVisit.For` now also takes stored visits (one private selection for both), so the list, the find box, and the chart header choose the same visit: active, not discharged, latest admitted.
- **`CurrentVisit.Where`:** "Inpatient · ICU 4" (type and current, else admission, location; no attending).
- **Patients:** a **Location** column with the place (else the visit number), "+n more open" in amber, or "No open visit". Filled in `ListDtosAsync` from open visits in one query.
- **Find box:** patient rows add "Now {where}" ("+n more open"), left out when the row is for that same visit.
- Read-only; no schema or API shape change beyond three optional list fields.

### Requirements

- URS-BB-530, FRS-BB-639. Help: patients, finding records.

### Tests

- TEST-BB-505:
  - stored visits pick the same visit as the chart header; "where" leaves out the attending and blank parts
  - the list takes the latest open visit's place and counts the other open one; a discharged visit, and an active one with a discharge time, are not open
  - a find-box row says where the patient is now; a row for an old visit adds it; a row for the current visit does not repeat it
- Full suite: 3,836 passed
- Live (read-only): Patients showed a Location for all 14 demo patients (MRN0001 "Inpatient · 4W Med/Surg", MRN0022 "Inpatient · ICU 4", MRN0024 "No open visit", matching their chart headers); the find box for "Demo" added "Now ..." to each row, so MRN0022 and MRN0024 (same name and birthday) are told apart. No demo patient has two open visits (the badge is covered by tests). No server errors.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted). Unidentified (trauma) patient registration needs registration policy (naming, MRN assignment, later merge): ask the SME before building.

## Iteration 468 — Patient pickers say where the patient is (2026-11-06)

### Capability matrix rescan (patient-product workflows: patient selection)

The patient pickers on Issue, Compatibility, and unit receipt name the patient with MRN, ABO/Rh, and alerts (Cycle 268), but not where the patient is. At issue, the person asking for blood names the ward; choosing between similar names meant opening each chart (questions 2, 6). Cycle 467 put the current open visit on the patient list, which is what these pickers load.

### Design

- **`PatientDto.PickerLabel`:** after "Name (MRN)", the current open visit's place ("Inpatient · ICU 4", else "visit {number}"), with "(+n more open)" when another visit is open; nothing when none is open.
- A merged-away record still reads only "merged into another record".
- The Issue desk still searches by name and MRN only; the label is display. No server, schema, or API change.

### Requirements

- URS-BB-531, FRS-BB-640. Help: patients.

### Tests

- TEST-BB-506:
  - the label puts the place after the MRN, flags another open visit, falls back to the visit number, adds nothing without an open visit, and keeps a merged record's label
  - the patient list's label carries the place and the other open visit
- Full suite: 3,837 passed
- Live (read-only): the Compatibility patient picker for "Demo" listed "Demo-Multi, Riley (MRN0022) · Inpatient · ICU 4 · O+" and "DEMO-MULTI, RILEY (MRN0024) · no type on file" (no open visit), so the two same-name charts read apart; nothing was chosen. No server errors.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted). Unidentified (trauma) patient registration needs registration policy: ask the SME before building.

## Iteration 469 — Below minimum stock on the home screen (2026-11-06)

### Capability matrix rescan (operations: inventory levels)

Minimum stock levels were set and compared on the Minimum stock page, and the alert scanner raised alerts, but the home screen had no card for them. The issue desk saw a shortage of group O red cells only by opening that page or the alert inbox. Near expiry and expired on shelf already had cards (questions 1, 5; supply risk for emergency release and massive transfusion).

### Design

- **Card `LowStock`** "Below minimum stock", opening `/inventory/minimum-stock`, for the same staff as Near expiry (inventory receive or discard, issue, allocate).
- **`LowStockCard`:** the count of minimum stock rows with a shortfall; the note names the three largest ("RBC O: 2 of 6", "any ABO" when the level names none, "and n more"). Ties go by product code.
- Any shortage carries a note, so it is under Needs attention, ranked after expired on shelf and before near expiry.
- In the Issue desk and Supervisor focus. Read-only from the existing endpoint; no schema or API change.
- Not in the shift handoff yet (its counts are stored columns; that needs a migration).

### Requirements

- URS-BB-532, FRS-BB-641. Help: dashboard, minimum stock.

### Tests

- TEST-BB-507:
  - the card is offered to issuers, not result entry, and is in the Issue desk and Supervisor focus, not Bench
  - the count and note name the largest shortfalls with what is on hand, "any ABO", a missing product code as "Product n", and "and n more"; no shortage gives no count or note
  - a shortage ranks ahead of near expiry under Needs attention
- Full suite: 3,838 passed
- Live (read-only): the home screen showed **Below minimum stock 1, RBC-LR A: 2 of 4**, matching the Minimum stock page (the only short row; FFP, PLT-A, and RBC-LR O at or above minimum). No server errors.

### Next ranked residual

Count products below minimum stock in the shift handoff (stored counts: needs a migration). Confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted). Unidentified (trauma) patient registration needs registration policy: ask the SME before building.

## Iteration 470 — The shift handoff counts products below minimum stock (2026-11-06)

### Capability matrix rescan (operations: shift handoff)

Cycle 469 put shortages on the home screen, but the shift handoff, the record of what the outgoing shift passed on, did not count them, while it did count near-expiry and expired units (question 5).

### Design

- **`ShiftHandoff.ProductsBelowMinimum`**, stored when the handoff is recorded and kept in its audit snapshot: minimum stock rows with a shortfall, read through `MinimumStockService` (no admin permission needed; the handoff is shift work).
- **Packet line:** "; BELOW MINIMUM STOCK n product(s)" in capitals like expired on shelf, before open deviations; nothing at zero.
- **Schema:** migration `20261119000000_ShiftHandoffLowStock`; dev SQLite gets the additive column (the Cycle 452 fix keeps existing data).

### Requirements

- URS-BB-533, FRS-BB-642. Help: shift handoff.

### Tests

- TEST-BB-508:
  - the line names the count in capitals with the right noun, ahead of open deviations, and is quiet at zero
  - a handoff stores the count of short products (a level met at zero is not short) and audits it
- Full suite: 3,840 passed
- Live: starting the API on the existing dev database added the `ProductsBelowMinimum` column and kept the data (14 patients before and after); the Shift handoff page loaded with no server errors. No handoff was recorded, to leave dev data unchanged, so the stored count is covered by the tests only.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted). Unidentified (trauma) patient registration needs registration policy: ask the SME before building.

## Iteration 471 — A shortage already on order is not ordered twice (2026-11-06)

### Capability matrix rescan (operations: stock and supplier orders)

A supplier order is drafted from the minimum stock shortfall, submitted (signed), sent outside the system, and received when the delivery is linked. Between submit and receipt the shortage still showed in full: on Minimum stock, on the new home card and handoff, and in the next drafted order, which asked for the same units again. Staff had to remember what was already ordered (questions 2, 3; double ordering wastes blood).

### Design

- **On order:** units on submitted supplier orders, by product and ABO (every ABO for an any-ABO level). Drafts, received, and cancelled orders do not count.
- **`MinimumStockRule.StillToOrder`:** the shortfall less on order, never below zero. The shortfall itself is unchanged: units on order are not on the shelf, so the card, handoff, and alerts still count the product as short.
- **New supplier order:** drafts what is still to order. When every shortage is covered it refuses, naming why and what to do (receive or cancel). The audit records units already on order.
- **Minimum stock:** an **On order** column, with "Covered by orders" or "n still to order".
- **Home card note:** ", n on order" after the shortfall.
- No schema change. Demo order SO-DEMO-002 (submitted, one group A red cell) shows it.

### Requirements

- URS-BB-534, FRS-BB-643. Help: minimum stock, supplier orders.

### Tests

- TEST-BB-509:
  - still to order is the shortfall less on order, never negative
  - after one submitted unit, the next draft asks for the one still short; a draft and a cancelled order do not count as on order
  - once submitted orders cover the shortage, no order is drafted and the message says why; the product is still short, and the card note says "2 on order"
- Full suite: 3,847 passed
- Demo data: SO-DEMO-002, a submitted order for one group A red cell, not yet received (seeded once).
- Live: Minimum stock showed RBC-LR A short 2 with **On order 1** and "1 still to order"; the other rows showed no units on order. The home card read "RBC-LR A: 2 of 4, 1 on order". No order was drafted or submitted live. No server errors.

### Next ranked residual

Rescan the desks; confirm OCD-062 and OCD-063 message details with the EHR team (spec drafted). Unidentified (trauma) patient registration needs registration policy: ask the SME before building.
