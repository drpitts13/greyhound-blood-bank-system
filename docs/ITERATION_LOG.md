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
