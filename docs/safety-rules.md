# Blood Bank LIS — Safety-Critical Business Rules

Status: Implemented Domain rules (not a Phase 0 draft). Each rule is a **pure function** in `BloodBankLIS.Domain` returning a `RuleResult` with a stable `Code`, a `Severity`, and a human-readable message. The Application layer aggregates them into a single `RuleEvaluation`.

## Severity model

| Severity | Behavior |
|---|---|
| **HardStop** | Operation is blocked. Cannot be overridden by any user. |
| **Warning** | Operation is blocked unless overridden with reason + authorization + electronic signature + audit. |
| **Pass** | No objection. |

Rules:
- The engine **never** auto-downgrades a HardStop to a Warning or auto-corrects data.
- An evaluation with any HardStop is a HardStop overall, regardless of other results.
- An evaluation with one or more Warnings (and no HardStop) is overridable.
- Each rule is individually unit-tested with positive and negative cases (see `validation-plan.md`).

---

## 1. Issue gate (unit must not be issued unless ALL pass)

These run in `IssueUnitCommand` before a unit leaves inventory. Reference: `workflows.md` sections 4 and 5.

| Code | Rule | Severity if violated |
|---|---|---|
| `ISS-IDENTITY` | Patient identity confirmed with two independent tokens (MRN/DOB/name) at issue and at transfusion documentation — not a checkbox | HardStop |
| `PAT-MERGED-INACTIVE` | Patient record is not a merged (losing) identity — accession/visit/order/result/immuno/issue/allocate/XM continue on the survivor | HardStop |
| `ABID-MERGE-DUP-OPEN` | Both merge records have an open antibody-identification workup | HardStop (complete or void one first) |
| `ABID-MERGE-WORKUP` | One merge record has an open workup that will follow the survivor | Warning (does not identify antibodies) |
| `ISS-SPEC-EXISTS` | A current specimen exists for the patient | HardStop |
| `ISS-SPEC-PATIENT` | Specimen belongs to the same patient | HardStop |
| `ISS-SPEC-EXPIRED` | Specimen is not past its expiration | HardStop |
| `ISS-PT-ABORH` | Patient ABO/Rh is known (current `PatientBloodTypeHistory`) | HardStop |
| `ISS-UNIT-ABORH` | Unit ABO/Rh is present | HardStop |
| `ISS-ABO-COMPAT` | Unit ABO is compatible with patient via antigen/antibody conflict check (section 3) | HardStop |
| `ISS-PRODUCT-TYPE` | Unit product type matches what the order/clinical need requires | HardStop |
| `ISS-UNIT-STATUS` | Unit status is Available/Allocated/Assigned/Crossmatched/Selected (not Quarantine/OnHold/Discarded/Issued/Transfused/Expired/ReturnedToSupplier) | HardStop |
| `ISS-UNIT-EXPIRED` | Unit is not past expiration date/time | HardStop |
| `ISS-ALLOCATION` | Unit is allocated/reserved to THIS patient | HardStop |
| `ISS-CREATE-PERM` | Caller has `issue.create` when issuing a unit | HardStop |
| `ISS-RET-PERM` | Caller has `issue.return` when returning an issued unit to inventory | HardStop |
| `TXN-DOC-PERM` | Caller has `transfusion.document` when documenting a transfusion | HardStop |
| `TXN-WARD-PERM` | Caller has `transfusion.document` when recording ward receipt of an issued unit | HardStop |
| `TXN-IFACE-PERM` | Caller has `transfusion.document` when documenting a transfusion through `InterfaceTransfusionService.DocumentAsync` | HardStop when a permission evaluator is present and the privilege is missing |
| `ISS-AUTO-DIR` | Autologous/directed unit is issued or allocated only to the reserved patient — evaluated inside `IssueGate` (not only the issuing service) | HardStop |
| `ISS-XM-REQUIRED` | If product requires crossmatch, a compatible, unexpired crossmatch exists (unless emergency release) | HardStop |
| `XM-ALLOC-PERM` | Caller has `compatibility.allocate` when reserving a unit to a patient | HardStop when a permission evaluator is present and the privilege is missing |
| `XM-REL-PERM` | Caller has `compatibility.allocate` when releasing a reservation back to Available | HardStop when a permission evaluator is present and the privilege is missing |
| `XM-PERM` | Caller has `compatibility.crossmatch` when recording a crossmatch | HardStop when a permission evaluator is present and the privilege is missing |
| `ISS-SPECIAL-REQ` | All active special requirements met (irradiated/CMV-neg/leukoreduced/washed/antigen-negative) — computer-evaluated from persisted patient requirements | HardStop |
| `ISS-ANTIGEN-NEG` | For RBC/WB: unit typed antigen-negative for each clinically significant patient antibody (current or historical) | Warning (supervisor+ override via ExceptionDefinitions, MinSecurityLevel 2) |
| `ALLOC-XM-AB-HISTORY` | Positive antibody screen (current/historical) or antibody history requires complex crossmatch (simple XM needs override) | Warning |
| `ABID-ALLOC-OPEN` | An open antibody-identification workup is the identification of record; antigen-negative needs may change when it completes | Warning (does not block reserve) |
| `ABID-XM-OPEN` | Same open workup at serologic XM; antigen-negative needs may change when it completes | Warning (does not block serologic XM; electronic XM stays HardStop) |
| `ABID-ISSUE-OPEN` | Same open workup at issue; antigen-negative needs may change when it completes | Warning after the gate (does not block or require override) |
| `ABID-DEACT-POSTED` | Chart deactivate of a specificity posted by a completed workup; antigen-neg will drop | Warning (does not block; OCD-017 keeps deactivate authorized) |
| `ABID-AG-OPEN` | Patient antigen type changed (chart save or verified result) while an antibody-identification workup is open | Warning; withdraws interpretation/review (does not block save/verify) |
| `ABID-AG-INVAL` | Verified antigen result invalidated while an antibody-identification workup is open | Warning; withdraws interpretation/review; phenotype not auto-reverted (does not block invalidate) |
| `ABID-ABO-INVAL` | Verified ABO/Rh result invalidated while an antibody-identification workup is open | Warning; withdraws interpretation/review; blood-type history not auto-reverted (does not block invalidate) |
| `ABID-ABO-OPEN` | Patient ABO/Rh changed (manual or verified result) while an antibody-identification workup is open | Warning; withdraws interpretation/review (does not block save/verify) |
| `ABID-TYPE-CORR` | ABO/Rh or antigen correction entered while an antibody-identification workup is open | Warning; does not withdraw or change stored type (does not block correct) |
| `ABID-TYPE-PENDING` | Current unverified ABO/Rh or antigen correction exists on the identification-of-record specimen | HardStop complete and supervisor Accept (not ack-able) |
| `ABID-AG-DONE` | Patient antigen type changed after a completed in-scope antibody-identification workup | Warning; does not reopen or retract history (does not block save/verify) |
| `ABID-ABO-DONE` | Patient ABO/Rh changed after a completed in-scope antibody-identification workup | Warning; does not reopen or retract history (does not block save/verify) |
| `ABID-SR-OPEN` | Special requirement added or deactivated while an antibody-identification workup is open | Warning (does not block add/deactivate) |
| `ABID-VOID-PRODUCTS` | Reserved or issued units exist when voiding; identification of record is abandoned | Warning (does not block void or require extra ack) |
| `ISS-ABORH-DISCREPANCY` | Current ABO/Rh determination agrees with historical record (computed from history, not an operator flag) | Warning (HardStop if unresolved on a crossmatch-required product) |
| `ISS-VISUAL` | Unit passed visual inspection at issue | HardStop |
| `ISS-APPEAR` | Coded appearance at issue is Acceptable (not Clots/Hemolysis/Leaking/…) | HardStop |
| `ISS-SPEC-NEAR-EXPIRY` | Specimen expires within configurable warning window | Warning |
| `ISS-UNIT-NEAR-EXPIRY` | Unit expires within configurable warning window | Warning (issue gate); worklist `GET /api/inventory/units/near-expiry` (`Inventory.NearExpiryWarningHours`, default 24) |
| `TX-DUAL-ID` | Distinct second verifier, or electronic identification completed by `PatientIdentityMatchRule` plus a verified ISBT bedside unit scan | HardStop when facility policy requires it |
| `TX-SECOND-USER` | Named second verifier is an active, unlocked application user (not free-text initials) | HardStop when a second verifier is supplied |
| `TX-WARD-RECEIPT` | Receiving location acknowledged the issued unit (`WardReceivedUtc`) | HardStop when facility policy `Transfusion.RequireWardReceipt` is true (default) |
| `TX-WARD-APPEAR` | Coded appearance at ward receipt is Acceptable (not Clots/Hemolysis/Leaking/…) | HardStop |
| `ISS-RETRO-XM-PENDING` | Emergency/MTP issue released without a compatible crossmatch remains on the retrospective XM worklist until a post-issue compatible XM is recorded | Worklist / Warning when overdue (`Issue.RetrospectiveCrossmatchDueHours`, default 24) |
| `ISS-IN-TRANSIT` | Issued unit has not been acknowledged at the receiving location (cooler / remote-issue custody) | Worklist / Warning when overdue (`Issue.InTransitDueHours`, default 4) |
| `INV-Q-RELEASE-2ND` | Distinct directory user as second verifier to release a unit from quality quarantine | HardStop when `Inventory.RequireQuarantineReleaseVerifier` is true (default) |
| `INV-REL-PERM` | Caller has `inventory.release` when releasing from quarantine | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-DIR-PERM` | Caller has `inventory.release` when converting a directed unit to allogeneic | HardStop when a permission evaluator is present and the privilege is missing. Conversion writes `ProductStatus`. |
| `INV-HOLD-PERM` | Caller has `inventory.release` when releasing a unit from operational hold | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-LOC-PERM` | Caller has `inventory.release` when locating a missing unit into quarantine | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-INSP-PERM` | Caller has `inventory.release` when inspecting a damaged unit into quarantine | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-Q-PERM` | Caller has `inventory.release` when placing a unit in quality quarantine | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-HOLD-SET-PERM` | Caller has `inventory.release` when placing a unit on operational hold | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-MISS-PERM` | Caller has `inventory.release` when marking a unit missing | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-DMG-PERM` | Caller has `inventory.release` when marking a unit damaged | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-DISC-2ND` | Distinct directory user as second verifier to discard a unit | HardStop when `Inventory.RequireDiscardVerifier` is true (default) |
| `INV-RCV-VISUAL` | Unit passed visual inspection at receipt (no clots, hemolysis, or container defects) | HardStop when `Inventory.RequireReceiveVisualInspection` is true (default) |
| `INV-RCV-APPEAR` | Coded appearance at receipt is Acceptable (not Clots/Hemolysis/Leaking/…) | HardStop when visual inspection is required (default) |
| `INV-RCV-TEMP` | Shipping-container temperature at receipt is recorded and within 1–10 °C | HardStop when `Inventory.RequireReceiveTemperature` is true (default); out-of-range units are not received |
| `INV-AUTO-DIR` | Autologous/directed unit names the intended recipient at receive (or packing-list expect) | HardStop |
| `INV-EXPECT-OVERDUE` | Packing-list / ASN unit has not arrived by `ExpectedArrivalDueUtc` | Worklist / Warning when overdue (`Inventory.ExpectedArrivalDueHours`, default 24) |
| `INV-Q-REASON` | Quality quarantine uses a coded catalog reason (not Unspecified; Other requires notes) | HardStop |
| `INV-DISCREPANCY` | Missing or damaged unit is awaiting locate or inspect | Worklist `GET /api/inventory/units/discrepancy` |
| `INV-DIR-ALLO` | Unused directed unit may be converted to allogeneic inventory; autologous cannot; reserved/issued statuses must be released first | HardStop |
| `INV-DIR-CONV-2ND` | Distinct directory user as second verifier to convert a directed unit to allogeneic | HardStop when `Inventory.RequireDirectedConversionVerifier` is true (default) |
| `INV-RCV-2ND` | Distinct directory user as second verifier when receiving a unit (walk-in, expected arrival, ISBT) | HardStop when `Inventory.RequireReceiveVerifier` is true (default) |
| `RES-SELF-VERIFY` | The user who entered a unit ABO/Rh retype may not verify it | HardStop when `Inventory.BlockRetypeSelfVerify` is true (default) |
| `RES-SELF-VERIFY` | The user who entered a patient ABO/Rh result may not verify it | HardStop when `Result.BlockAboSelfVerify` is true (default); `MarkComplete` does not auto-verify ABO/Rh |
| `RES-VERIFY-PERM` | Caller has `result.verify` when verifying a test result or unit ABO/Rh retype | HardStop when a permission evaluator is present and the privilege is missing |
| `RES-ENTER-PERM` | Caller has `result.enter` when entering or updating an unverified result or unit retype | HardStop when a permission evaluator is present and the privilege is missing |
| `RES-CORRECT-PERM` | Caller has `result.correct` when correcting a verified result | HardStop when a permission evaluator is present and the privilege is missing |
| `SPEC-ACC-PERM` | Caller has `specimen.accession` when accessioning a specimen | HardStop when a permission evaluator is present and the privilege is missing |
| `SPEC-EDIT-PERM` | Caller has `specimen.edit` when editing specimen collection metadata | HardStop when a permission evaluator is present and the privilege is missing |
| `SPEC-REJ-PERM` | Caller has `specimen.reject` when rejecting a specimen | HardStop when a permission evaluator is present and the privilege is missing |
| `PAT-WRITE-PERM` | Caller has `patient.write` when updating patient demographics | HardStop when a permission evaluator is present and the privilege is missing. Update writes `PatientAccess` with old/new. |
| `PAT-CREATE-PERM` | Caller has `patient.write` when creating a patient record | HardStop when a permission evaluator is present and the privilege is missing. Create writes `PatientAccess`. |
| `PAT-MERGE-PERM` | Caller has `patient.merge` when merging a duplicate into a survivor from the workspace | HardStop when a permission evaluator is present and the privilege is missing |
| `USR-CREATE-PERM` | Caller has `admin.users.manage` when creating a directory user | HardStop when a permission evaluator is present and the privilege is missing. Create/update writes `UserRole`. |
| `USR-UPD-PERM` | Caller has `admin.users.manage` when updating a directory user | HardStop when a permission evaluator is present and the privilege is missing |
| `USR-ASSIGN-PERM` | Caller has `admin.users.manage` when assigning roles | HardStop when a permission evaluator is present and the privilege is missing |
| `USR-ACTIVE-PERM` | Caller has `admin.users.manage` when activating or deactivating a directory user | HardStop when a permission evaluator is present and the privilege is missing |
| `USR-LOCK-PERM` | Caller has `admin.users.manage` when locking or unlocking a directory user | HardStop when a permission evaluator is present and the privilege is missing |
| `USR-RESET-PERM` | Caller has `admin.users.manage` when requesting a password reset | HardStop when a permission evaluator is present and the privilege is missing |
| `TEST-CREATE-PERM` | Caller has `admin.tests.manage` when creating a test definition | HardStop when a permission evaluator is present and the privilege is missing |
| `TEST-UPD-PERM` | Caller has `admin.tests.manage` when updating a test definition | HardStop when a permission evaluator is present and the privilege is missing |
| `TEST-ACT-PERM` | Caller has `admin.tests.manage` when activating a test definition | HardStop when a permission evaluator is present and the privilege is missing |
| `TEST-DEACT-PERM` | Caller has `admin.tests.manage` when deactivating a test definition | HardStop when a permission evaluator is present and the privilege is missing |
| `TEST-CLONE-PERM` | Caller has `admin.tests.manage` when cloning a test definition | HardStop when a permission evaluator is present and the privilege is missing |
| `ATTR-CREATE-PERM` | Caller has `admin.config.edit` when creating a blood attribute definition | HardStop when a permission evaluator is present and the privilege is missing |
| `ATTR-UPD-PERM` | Caller has `admin.config.edit` when updating a blood attribute definition | HardStop when a permission evaluator is present and the privilege is missing |
| `ATTR-ACT-PERM` | Caller has `admin.config.activate` when activating a blood attribute definition | HardStop when a permission evaluator is present and the privilege is missing |
| `ATTR-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a blood attribute definition | HardStop when a permission evaluator is present and the privilege is missing |
| `REFLEX-CREATE-PERM` | Caller has `admin.tests.manage` when creating a reflex rule | HardStop when a permission evaluator is present and the privilege is missing |
| `REFLEX-UPD-PERM` | Caller has `admin.tests.manage` when updating a reflex rule | HardStop when a permission evaluator is present and the privilege is missing |
| `REFLEX-ACT-PERM` | Caller has `admin.config.activate` when activating a reflex rule | HardStop when a permission evaluator is present and the privilege is missing |
| `REFLEX-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a reflex rule | HardStop when a permission evaluator is present and the privilege is missing |
| `SUBTEST-CREATE-PERM` | Caller has `admin.tests.manage` when creating a subtest definition | HardStop when a permission evaluator is present and the privilege is missing |
| `SUBTEST-UPD-PERM` | Caller has `admin.tests.manage` when updating a subtest definition | HardStop when a permission evaluator is present and the privilege is missing |
| `SUBTEST-ACT-PERM` | Caller has `admin.config.activate` when activating a subtest definition | HardStop when a permission evaluator is present and the privilege is missing |
| `SUBTEST-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a subtest definition | HardStop when a permission evaluator is present and the privilege is missing |
| `GROUPER-CREATE-PERM` | Caller has `admin.tests.manage` when creating a test grouper | HardStop when a permission evaluator is present and the privilege is missing |
| `GROUPER-UPD-PERM` | Caller has `admin.tests.manage` when updating a test grouper | HardStop when a permission evaluator is present and the privilege is missing |
| `GROUPER-ACT-PERM` | Caller has `admin.config.activate` when activating a test grouper | HardStop when a permission evaluator is present and the privilege is missing |
| `GROUPER-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a test grouper | HardStop when a permission evaluator is present and the privilege is missing |
| `RULEDEF-CREATE-PERM` | Caller has `admin.tests.manage` when creating an order or test rule | HardStop when a permission evaluator is present and the privilege is missing |
| `RULEDEF-UPD-PERM` | Caller has `admin.tests.manage` when updating an order or test rule | HardStop when a permission evaluator is present and the privilege is missing |
| `RULEDEF-ACT-PERM` | Caller has `admin.config.activate` when activating an order or test rule | HardStop when a permission evaluator is present and the privilege is missing |
| `RULEDEF-DEACT-PERM` | Caller has `admin.config.activate` when deactivating an order or test rule | HardStop when a permission evaluator is present and the privilege is missing |
| `PHASE-CREATE-PERM` | Caller has `admin.tests.manage` when creating a phase definition | HardStop when a permission evaluator is present and the privilege is missing |
| `PHASE-UPD-PERM` | Caller has `admin.tests.manage` when updating a phase definition | HardStop when a permission evaluator is present and the privilege is missing |
| `PHASE-ACT-PERM` | Caller has `admin.config.activate` when activating a phase definition | HardStop when a permission evaluator is present and the privilege is missing |
| `PHASE-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a phase definition | HardStop when a permission evaluator is present and the privilege is missing |
| `EXC-CREATE-PERM` | Caller has `admin.config.edit` when creating an exception definition | HardStop when a permission evaluator is present and the privilege is missing |
| `EXC-UPD-PERM` | Caller has `admin.config.edit` when updating an exception definition | HardStop when a permission evaluator is present and the privilege is missing |
| `EXC-ACT-PERM` | Caller has `admin.config.activate` when activating an exception definition | HardStop when a permission evaluator is present and the privilege is missing |
| `EXC-DEACT-PERM` | Caller has `admin.config.activate` when deactivating an exception definition | HardStop when a permission evaluator is present and the privilege is missing |
| `SPECTYPE-CREATE-PERM` | Caller has `admin.config.edit` when creating a specimen type definition | HardStop when a permission evaluator is present and the privilege is missing |
| `SPECTYPE-UPD-PERM` | Caller has `admin.config.edit` when updating a specimen type definition | HardStop when a permission evaluator is present and the privilege is missing |
| `SPECTYPE-ACT-PERM` | Caller has `admin.config.activate` when activating a specimen type definition | HardStop when a permission evaluator is present and the privilege is missing |
| `SPECTYPE-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a specimen type definition | HardStop when a permission evaluator is present and the privilege is missing |
| `PROD-CREATE-PERM` | Caller has `admin.products.manage` when creating a product definition | HardStop when a permission evaluator is present and the privilege is missing |
| `PROD-UPD-PERM` | Caller has `admin.products.manage` when updating a product definition | HardStop when a permission evaluator is present and the privilege is missing |
| `PROD-ACT-PERM` | Caller has `admin.config.activate` when activating a product definition | HardStop when a permission evaluator is present and the privilege is missing |
| `PROD-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a product definition | HardStop when a permission evaluator is present and the privilege is missing |
| `MODRULE-CREATE-PERM` | Caller has `admin.modification-rules.manage` when creating a modification rule | HardStop when a permission evaluator is present and the privilege is missing |
| `MODRULE-UPD-PERM` | Caller has `admin.modification-rules.manage` when updating a modification rule | HardStop when a permission evaluator is present and the privilege is missing |
| `MODRULE-ACT-PERM` | Caller has `admin.config.activate` when activating a modification rule | HardStop when a permission evaluator is present and the privilege is missing |
| `MODRULE-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a modification rule | HardStop when a permission evaluator is present and the privilege is missing |
| `EXPCODE-CREATE-PERM` | Caller has `admin.modification-rules.manage` when creating an expiration modification code | HardStop when a permission evaluator is present and the privilege is missing |
| `EXPCODE-UPD-PERM` | Caller has `admin.modification-rules.manage` when updating an expiration modification code | HardStop when a permission evaluator is present and the privilege is missing |
| `EXPCODE-ACT-PERM` | Caller has `admin.config.activate` when activating an expiration modification code | HardStop when a permission evaluator is present and the privilege is missing |
| `EXPCODE-DEACT-PERM` | Caller has `admin.config.activate` when deactivating an expiration modification code | HardStop when a permission evaluator is present and the privilege is missing |
| `HL7EP-CREATE-PERM` | Caller has `admin.hl7.manage` when creating an HL7 endpoint | HardStop when a permission evaluator is present and the privilege is missing |
| `HL7EP-UPD-PERM` | Caller has `admin.hl7.manage` when updating an HL7 endpoint | HardStop when a permission evaluator is present and the privilege is missing |
| `HL7EP-ENABLE-PERM` | Caller has `admin.hl7.manage` when enabling an HL7 endpoint | HardStop when a permission evaluator is present and the privilege is missing |
| `HL7EP-DISABLE-PERM` | Caller has `admin.hl7.manage` when disabling an HL7 endpoint | HardStop when a permission evaluator is present and the privilege is missing |
| `HL7XLAT-REPLACE-PERM` | Caller has `admin.hl7.manage` when replacing HL7 value translations | HardStop when a permission evaluator is present and the privilege is missing. Replace writes `Interface`. |
| `CHG-CREATE-PERM` | Caller has `admin.config.edit` when creating a charge code | HardStop when a permission evaluator is present and the privilege is missing. Create/update writes `Configure`. |
| `CHG-UPD-PERM` | Caller has `admin.config.edit` when updating a charge code | HardStop when a permission evaluator is present and the privilege is missing |
| `CHG-ACT-PERM` | Caller has `admin.config.activate` when activating a charge code | HardStop when a permission evaluator is present and the privilege is missing |
| `CHG-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a charge code | HardStop when a permission evaluator is present and the privilege is missing |
| `CHGRULE-CREATE-PERM` | Caller has `admin.config.edit` when creating a charge rule | HardStop when a permission evaluator is present and the privilege is missing |
| `CHGRULE-UPD-PERM` | Caller has `admin.config.edit` when updating a charge rule | HardStop when a permission evaluator is present and the privilege is missing |
| `CHGRULE-ACT-PERM` | Caller has `admin.config.activate` when activating a charge rule | HardStop when a permission evaluator is present and the privilege is missing |
| `CHGRULE-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a charge rule | HardStop when a permission evaluator is present and the privilege is missing |
| `PRODBILL-CREATE-PERM` | Caller has `admin.config.edit` when creating a product billing row | HardStop when a permission evaluator is present and the privilege is missing. Create/update writes `Configure`. |
| `PRODBILL-UPD-PERM` | Caller has `admin.config.edit` when updating a product billing row | HardStop when a permission evaluator is present and the privilege is missing |
| `PRODBILL-ACT-PERM` | Caller has `admin.config.activate` when activating a product billing row | HardStop when a permission evaluator is present and the privilege is missing |
| `PRODBILL-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a product billing row | HardStop when a permission evaluator is present and the privilege is missing |
| `TSBILL-CREATE-PERM` | Caller has `admin.config.edit` when creating a test/service billing row | HardStop when a permission evaluator is present and the privilege is missing |
| `TSBILL-UPD-PERM` | Caller has `admin.config.edit` when updating a test/service billing row | HardStop when a permission evaluator is present and the privilege is missing |
| `TSBILL-ACT-PERM` | Caller has `admin.config.activate` when activating a test/service billing row | HardStop when a permission evaluator is present and the privilege is missing |
| `TSBILL-DEACT-PERM` | Caller has `admin.config.activate` when deactivating a test/service billing row | HardStop when a permission evaluator is present and the privilege is missing |
| `ORDPROV-CREATE-PERM` | Caller has `admin.config.edit` when creating an ordering provider | HardStop when a permission evaluator is present and the privilege is missing. Create/update writes `Configure`. |
| `ORDPROV-UPD-PERM` | Caller has `admin.config.edit` when updating an ordering provider | HardStop when a permission evaluator is present and the privilege is missing |
| `ORDPROV-ACT-PERM` | Caller has `admin.config.activate` when activating an ordering provider | HardStop when a permission evaluator is present and the privilege is missing |
| `ORDPROV-DEACT-PERM` | Caller has `admin.config.activate` when deactivating an ordering provider | HardStop when a permission evaluator is present and the privilege is missing |
| `ORDLOC-CREATE-PERM` | Caller has `admin.config.edit` when creating an ordering location | HardStop when a permission evaluator is present and the privilege is missing |
| `ORDLOC-UPD-PERM` | Caller has `admin.config.edit` when updating an ordering location | HardStop when a permission evaluator is present and the privilege is missing |
| `ORDLOC-ACT-PERM` | Caller has `admin.config.activate` when activating an ordering location | HardStop when a permission evaluator is present and the privilege is missing |
| `ORDLOC-DEACT-PERM` | Caller has `admin.config.activate` when deactivating an ordering location | HardStop when a permission evaluator is present and the privilege is missing |
| `BILL-REV-PERM` | Caller has `billing.review` when reviewing a captured charge | HardStop when a permission evaluator is present and the privilege is missing. Review writes `Billing`. |
| `BILL-CXL-PERM` | Caller has `billing.cancel` when cancelling a captured charge | HardStop when a permission evaluator is present and the privilege is missing. Cancel writes `Billing` with reason. |
| `BILL-EXP-PERM` | Caller has `billing.export` when exporting a reviewed charge | HardStop when a permission evaluator is present and the privilege is missing. Export writes `Export`. |
| `INVLOC-CREATE-PERM` | Caller has `admin.config.edit` when creating an inventory location | HardStop when a permission evaluator is present and the privilege is missing. Create/update writes `Configure`. |
| `INVLOC-UPD-PERM` | Caller has `admin.config.edit` when updating an inventory location | HardStop when a permission evaluator is present and the privilege is missing |
| `INVLOC-ACT-PERM` | Caller has `admin.config.activate` when activating an inventory location | HardStop when a permission evaluator is present and the privilege is missing |
| `INVLOC-DEACT-PERM` | Caller has `admin.config.activate` when deactivating an inventory location | HardStop when a permission evaluator is present and the privilege is missing |
| `ROLE-CREATE-PERM` | Caller has `admin.roles.manage` when creating a role | HardStop when a permission evaluator is present and the privilege is missing |
| `ROLE-UPD-PERM` | Caller has `admin.roles.manage` when updating a role's permissions | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-MOD-PERM` | Caller has `inventory.modify` when dividing, pooling, or applying a product modification | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-ID-PERM` | Caller has `inventory.correct-identity` when correcting unit ISBT identity | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-RCV-PERM` | Caller has `inventory.receive` when completing an ISBT scan session, creating a manual component, or receiving via walk-in / expected-arrival / normalized intake | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-SCAN-START-PERM` | Caller has `inventory.receive` when starting an ISBT scan session | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-SCAN-ADD-PERM` | Caller has `inventory.receive` when adding a scan to an ISBT session | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-EXPECT-PERM` | Caller has `inventory.receive` when recording an expected inbound packing-list unit | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-EXPECT-CXL-PERM` | Caller has `inventory.receive` when cancelling an expected inbound packing-list unit | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-ATTR-PERM` | Caller has `inventory.receive` when saving a unit antigen or antibody attribute | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-RTS-PERM` | Caller has `inventory.receive` when returning a unit to the supplier | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-DISC-PERM` | Caller has `inventory.discard` when discarding a unit | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-EXP-PERM` | Caller has `inventory.discard` when running the expiration sweep | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-XFER-PERM` | Caller has `inventory.transfer` when moving a unit between storage locations | HardStop when a permission evaluator is present and the privilege is missing |
| `INV-RCL-PERM` | Caller has `inventory.recall` when recalling a unit through inventory (not lookback DIN recall) | HardStop when a permission evaluator is present and the privilege is missing |
| `LK-RECALL-PERM` | Caller has `lookback.manage` when recalling components by DIN | HardStop when a permission evaluator is present and the privilege is missing. Recall writes `Lookback` on a collected unit id. |
| `LK-ATTEMPT-PERM` | Caller has `lookback.manage` when recording a lookback notification attempt | HardStop when a permission evaluator is present and the privilege is missing. Attempt writes `Lookback` on the notification row with old/new. |
| `PRT-LABEL-PERM` | Caller has `print.label` when printing a specimen, compatibility, or component label | HardStop when a permission evaluator is present and the privilege is missing |
| `PRT-REPRINT-PERM` | Caller has `print.reprint` when reprinting a stored print job | HardStop when a permission evaluator is present and the privilege is missing |
| `ORD-CREATE-PERM` | Caller has `patient.write` when creating an order from the workspace | HardStop when a permission evaluator is present and the privilege is missing |
| `ORD-UPD-PERM` | Caller has `patient.write` when updating an order | HardStop when a permission evaluator is present and the privilege is missing |
| `ORD-CXL-PERM` | Caller has `patient.write` when cancelling an order | HardStop when a permission evaluator is present and the privilege is missing |
| `ORD-LINK-PERM` | Caller has `patient.write` when linking a specimen to an order | HardStop when a permission evaluator is present and the privilege is missing |
| `ENC-CREATE-PERM` | Caller has `patient.write` when creating a visit from the workspace | HardStop when a permission evaluator is present and the privilege is missing. Create/update writes `PatientAccess`. |
| `ENC-UPD-PERM` | Caller has `patient.write` when updating a visit from the workspace | HardStop when a permission evaluator is present and the privilege is missing |
| `RXN-PERM` | Caller has `reaction.investigate` when updating a reaction investigation or recording fatality notifications | HardStop when a permission evaluator is present and the privilege is missing. Open, update, CBER, and written-report write `ReactionInvestigation` with the row id. |
| `DEV-PERM` | Caller has `deviation.manage` when creating or updating a quality-system deviation | HardStop when a permission evaluator is present and the privilege is missing. Create and status update write `Deviation` with the row id. |
| `RET-REISSUE` | Returned unit may re-enter Available only when temperature, seal, visual, and time-out-of-storage checks pass | HardStop / Warning |

If `IssueType = EmergencyRelease`, `ISS-XM-REQUIRED` is evaluated as a Warning within that workflow (see section 5) rather than a HardStop, and an `Override` + signature is mandatory. Issuing any unit is HardStop `ISS-CREATE-PERM` without `issue.create`. Emergency or MTP issue is also HardStop `ISS-EMERG-PERM` unless the user has `issue.emergency-release`. Non-emergency warning overrides are HardStop `ISS-OVR-PERM` without `issue.override`. Returning an issued unit is HardStop `ISS-RET-PERM` without `issue.return`. Documenting a transfusion is HardStop `TXN-DOC-PERM` without `transfusion.document`. Recording ward receipt is HardStop `TXN-WARD-PERM` without `transfusion.document` and writes `Transfusion` on the issue. Adding a special requirement writes `Antibody` (entity id, type, antigen, reason); deactivating writes `Deactivate` with old/new.

---

## 2. Specimen expiration logic

- When the patient was transfused or had a documented pregnancy in the lookback window (default 90 days), specimen validity is the alloimmunization-risk window (default **72 hours / 3 days** from collection). Otherwise a longer configured standard window applies (default 168 hours). Keys: `Specimen.ValidityHours.AlloimmunizationRisk`, `Specimen.ValidityHours.Standard`, `Specimen.LookbackDays`.
- Accessioning stores two independent identifiers (typically MRN + DOB) that must match the patient record.
- `Specimens.ExpiresUtc` is computed at accessioning and recomputed (with audit) when alloimmunization-risk status changes.
- Rule `SPEC-EXPIRED` (HardStop on issue) and `SPEC-NEAR-EXPIRY` (Warning) both read `IClock` for deterministic testing.

---

## 3. ABO/Rh compatibility (antigen/antibody)

- ABO compatibility is evaluated by deriving antigens and naturally occurring isoagglutinins from each side’s ABO type, then detecting antigen/antibody conflicts. Example: type A expresses A antigen and is assumed to have anti-B. Compatibility-table version and rule create/update write `Configure`.
- Conflict rule: whenever either side has an antigen, the other side must not carry the corresponding antibody (and symmetrically).
- Component direction:
  - **RBC / granulocytes:** patient antibodies vs unit antigens.
  - **Whole blood:** bidirectional (cellular + plasma), because the unit carries both red cells and plasma.
  - **Plasma / cryoprecipitate / platelets:** unit antibodies vs patient antigens (inverse).
- Equivalent clinical outcomes for RBC: O→{O}; A→{A,O}; B→{B,O}; AB→{AB,A,B,O}.
- Rh(D): Rh-negative recipients must not receive Rh-positive RBC or whole blood (HardStop). Anti-D is not assumed from Rh-negative typing alone; immunized anti-D is handled by antigen-negative rules (`ISS-ANTIGEN-NEG`).
- Exhaustively unit-tested across recipient/donor combinations and component directions.

---

## 4. Inventory status transitions (guarded)

Allowed transitions are enforced by a transition guard; anything not listed is a HardStop.

```
# Authoritative allow-list is InventoryStatusTransition (expanded for ISBT 128).
# See docs/isbt128-module.md. Legacy core paths remain:
Expected   -> Received | Quarantine | CancelledAssignment | Missing | Discarded | ReturnedToSupplier
Quarantine -> Available | Discarded | Expired | Recalled | Damaged | Missing | ReturnedToSupplier
OnHold     -> Available | Quarantine | Discarded | Expired | Recalled | Damaged | Missing | ReturnedToSupplier
Available  -> Allocated | Assigned | Selected | Crossmatched | Quarantine | OnHold | Discarded | Expired | Recalled | Transferred | Modified | Missing | Damaged | ReturnedToSupplier
Allocated  -> Issued | Available (release) | Assigned | Crossmatched | Discarded | Expired | Recalled
Assigned   -> Issued | Available | Crossmatched | CancelledAssignment | Recalled | ...
Selected   -> Assigned | Crossmatched | Allocated | Recalled | ...
Crossmatched -> Issued | Available | Recalled | ...
Issued     -> Transfused | TransfusionStarted | Returned | ReturnPending | Recalled | Missing
Missing    -> Quarantine | Available | Discarded | Damaged
Damaged    -> Quarantine | Discarded | ReturnedToSupplier

Returned   -> Available | Quarantine | Discarded | Expired
Transfused -> (terminal)
Discarded  -> (terminal)
ReturnedToSupplier -> (terminal)
Expired    -> Discarded
Modified   -> (terminal)
```

- `Expected` is packing-list / ASN inventory that is not yet in house. It is not transferable or issuable. Arrival confirmation applies `INV-RCV-VISUAL` and lands in `Received` or `Quarantine`.
- `OnHold` is an operational hold (paperwork, pending review). It is not a quality quarantine: quarantine cannot move to hold, and a held unit cannot be issued until released to Available or escalated to Quarantine. Release from hold requires `inventory.release` (`INV-HOLD-PERM`).
- `Missing` is a physical-inventory discrepancy (SoftBank/SafeTrace). It is not issuable. Locating a missing unit lands in `Quarantine` for inspection, not Available. Missing and damaged units appear on the discrepancy worklist (`GET /api/inventory/units/discrepancy`).
- `Damaged` is container integrity failure found after the unit is already in inventory. It is not issuable. Inspection lands in `Quarantine`; discard is the terminal alternative.
- `ReturnedToSupplier` is the SoftBank/SafeTrace consignee reject / unused-stock return to the vendor. Distinct from ward `Returned` and from packing-list `CancelledAssignment`. Terminal; not issuable.
- Releasing a unit from quality quarantine requires a distinct directory second verifier (`INV-Q-RELEASE-2ND`) when `Inventory.RequireQuarantineReleaseVerifier` is true (default).
- Placing a unit in quality quarantine requires a coded catalog reason (`INV-Q-REASON`). Intake, locate, inspect, retype discrepancy, failed return, reaction remainder, and modification results store the matching code automatically.
- Unit ABO/Rh retype entry writes `Result` with interpreted ABO/Rh. An unverified update writes Result with old/new interpreted type. Verification writes interpreted ABO/Rh on `Verify` old/new and `ProductStatus` (Received → Available or Quarantine). Save and verify feedback name that interpreted type. Product-definition create/update writes `Configure` (including the required-retype flag).
- Unit ISBT identity correction writes `Correct` on the unit after the correction row has an id, with field, old/new value, and `CorrectionId`. Post-issue lock and `inventory.correct-identity` stay as-is.
- Discarding a unit requires a distinct directory second verifier (`INV-DISC-2ND`) when `Inventory.RequireDiscardVerifier` is true (default).
- Any transition writes `InventoryStatusHistory` + `AuditEvent`.
- Expiration is enforced automatically: a unit past `ExpiresUtc` cannot move to Allocated/Issued (HardStop) and is eligible to be marked Expired.
- `Modified` is the terminal state for a source unit consumed into a product modification (divide/pool/irradiate/thaw/volume-reduce/leukoreduce); the resulting unit(s) are new `BloodProducts` rows in `Quarantine` (see section 4a).

---

## 4a. Product modification rules

Enforced by `UnitModificationEligibilityRule` and `ModificationExpirationRule` in `BloodProductModificationService`. Reference: `workflows.md` §8a.

| Code | Rule | Severity if violated |
|---|---|---|
| `MOD-STATUS-INVALID` | Source unit status is `Available` | HardStop |
| `MOD-EXPIRED` | Source unit is not past its expiration date/time | HardStop |
| `MOD-PRODUCT-MISMATCH` | Source unit's product type matches the modification rule's source product | HardStop |
| `MOD-POOL-MIN-SOURCES` | Pool has at least two source units | HardStop |
| `MOD-POOL-ABO-MISMATCH` | All pooled source units share the same product type, ABO, and Rh(D) | HardStop |
| `MOD-DIVIDE-MIN-TARGETS` | Divide requests at least two result units | HardStop |
| `MOD-VOLUME-EXCEEDS-SOURCE` | Divide's requested child volumes (when supplied) do not exceed the source unit's volume | HardStop |
| `MOD-COLLECTION-REQUIRED` | Collection-relative expiration codes require every source unit to have a collection date/time | HardStop |

Admin `ExpirationModificationCodes` catalog validation (`ExpirationModificationCodeValidator`), all HardStop: `EXPCODE.CODE.REQUIRED`, `EXPCODE.AMOUNT.INVALID`, `EXPCODE.UNIT.INVALID`, `EXPCODE.RELATIVE.INVALID`, `EXPCODE.CODE.DUPLICATE`. Create/update writes `Configure`; activate/deactivate stay Activate/Deactivate.

Admin `ModificationRules` catalog validation (`ModificationRuleValidator`), all HardStop: `MODRULE.CODE.REQUIRED`, `MODRULE.CODE.DUPLICATE`, `MODRULE.SOURCE.REQUIRED`, `MODRULE.TARGET.REQUIRED`, `MODRULE.EXPCODE.REQUIRED`, `MODRULE.EXPCODE.INACTIVE`, `MODRULE.TRIPLE.DUPLICATE` (another active rule already maps the same source product + type + target product), `MODRULE.SOURCE.INACTIVE`/`MODRULE.TARGET.INACTIVE`. `MODRULE.SAMEPRODUCT` is a non-blocking Warning. Create/update writes `Configure`; activate/deactivate stay Activate/Deactivate.

Expiration: `ResultExpiresUtc = min(anchor + offset, earliest source ExpiresUtc)` — the anchor is `PerformedUtc` when the expiration code is relative to modification, or the earliest source collection timestamp when it is relative to collection. A result unit can never outlive the shortest-lived unit consumed to produce it.

---

## 5. Dangerous actions (confirmation + reason + e-signature + audit)

| Action | Confirmation | Reason | E-signature | Audit event |
|---|---|---|---|---|
| Emergency release (uncrossmatched) | Yes | Yes | Yes (authorizer) | `Issue` + `Override` |
| Override a compatibility/issue Warning | Yes | Yes | Yes | `Override` |
| Discard a unit | Yes | Yes | No (reason required) | `Discard` |
| Change a verified result | Yes | Yes | Yes | `Correct` |
| First specimen, compatibility, or component label | Yes | No | No | `Print` after the job has an id; interceptor Create still writes |
| Reprint a compatibility tag | Yes | Yes | No (reason required) | `Reprint`; HardStop `PRT-REPRINT-PERM` without `print.reprint` |
| Return an issued unit to inventory | Yes | Yes | No (reason required) | `Return` |
| Manually alter ABO/Rh history | Yes | Yes | Yes | `Override` (blood type history); HardStop `IH-ABO-PERM` without `immuno.override` |
| Add an antibody record | Yes | No | No | `Antibody`; HardStop `IH-AB-ADD-PERM` without `immuno.record` |
| Save or update an antigen phenotype | Yes | No | No | `Antibody` (old/new); in-place update (OCD-022); HardStop without `immuno.record` |
| Deactivate an antibody record | Yes | Yes | No (reason required) | `Antibody`; HardStop `IH-AB-DEACT-PERM` without `immuno.override` |
| Add special transfusion requirement | Yes | Yes | No (reason required) | `Create`; HardStop `SR-ADD-PERM` without `immuno.record` |
| Deactivate special transfusion requirement | Yes | Yes | No (reason required) | `Deactivate`; HardStop `SR-DEACT-PERM` without `immuno.override` |
| Modify a product (divide/pool/irradiate/thaw/volume-reduce/leukoreduce) | No | Yes | No (reason required) | `Modify` |

- HardStops are never part of an override path — only Warnings can be overridden.
- All override and signature records are append-only (`Overrides`, `ElectronicSignatures`).

---

## 5a. Transfusion reaction workup (close gate)

Recorded on `ReactionInvestigation` and evaluated by `ReactionWorkupCompletenessRule` before close.

| Code | Rule | Severity if violated |
|---|---|---|
| `RXN-WORKUP-INCOMPLETE` | Clerical check, visual inspection, and DAT are recorded; DAT-positive requires elution notes | HardStop |

Opening a reaction investigation quarantines the implicated unit when `InventoryStatusTransition` allows (typically `TransfusionStopped` remainder). Fully transfused units stay terminal; the checklist still records segment/bag retention.

---

## 6. Result integrity rules

- Result entry and verification require an Accepted, unexpired specimen on a surviving (not merged) patient. Verification does not post ABO or antibody history from an invalid specimen.
- Patient ABO/Rh is entered, then verified by a different user (`Result.BlockAboSelfVerify`, default on). Save-and-complete does not auto-verify ABO/Rh or write `PatientBloodTypeHistory`.
- Result provenance is stored (`Manual`, `Instrument`, `Interface`, `Calculated`). ABO/Rh panels and catalog interpretation logic are tagged Calculated unless the observation already arrived as Instrument or Interface. Instrument and interface values start `PendingVerification` (OCD-018). Inbound ORU uses the same Application path and never overwrites a verified row (OCD-019). HL7 endpoint create/update writes `Interface` so the trail of the intake configuration is searchable apart from generic Create/Update. The test worklist and patient Tests tab show the current row's source. Worklist expand-to-enter and the specimen ABO panel state that catalog-matching panel entry is stored as Calculated, and save feedback names the stored source. The specimen typed-entry form (test code + value, no panel) states that the stored source is Manual, and enter feedback names the stored source. Specimen-card verify, ABO/Rh-override verify, correct, submit-for-verification, and invalidate feedback also name that stored source. Worklist XM save feedback also names method and Compatible or Incompatible.

- Verified results are immutable; corrections create a new `TestResults` version and supersede (never overwrite) the prior row. The patient result panel shows the current row and expands retained prior versions (value, status, source, reason, who, when). The Test History tab lists every previously verified value, including superseded rows, and omits never-verified entries.
- Invalidation of a verified result creates a new `Invalidated` version and retains the original. An unverified correction is marked Invalidated and superseded by the restored prior verified row so only one current clinical row remains (OCD-016). Posted ABO/antibody history is not auto-reverted (OCD-017). Requires `result.invalidate` and a reason. Submit-for-verification, verify, invalidate, and re-entry after invalidation named events include the stored `ResultSource` on old/new.
- Delta check: a new ABO/Rh result that disagrees with the current historical record raises `RES-ABORH-DELTA` (**Warning**). At **verify**, the Warning **blocks** until an authorized override supplies reason + electronic signature + **Retain** (keep historical `IsCurrent`) or **Replace** (append and flip `IsCurrent` to the verified type). Override eligibility is gated by the admin `ExceptionDefinitions` catalog (`MinSecurityLevel` vs the user's max role `SecurityLevel`). Create/update of that catalog writes `Configure`. Unresolved discrepancy still contributes a HardStop to the issue gate on crossmatch-required products.
- Result-entry phase catalog create/update writes `TestChange` (check-cell and interpretation flags). Activate/deactivate stay Activate/Deactivate.
- Order/test rule-definition create/update writes `Configure` (for example Weak D reflex at verify). Test-grouper create/update writes `TestChange` (which tests are bundled for entry). Specimen-type create/update writes `TestChange` (excluded tests).
- Critical/special flags on results are surfaced to the verifier and carried into compatibility evaluation.
- Verifying a free-text or coded test with `ContributesToAntibodyHistory` (typically ABID) resolves catalog specificities (`anti-K`, `anti-E`) and posts them to `AntibodyHistory`, which then drives antigen-negative selection and the complex-crossmatch gate. Unmatched `anti-*` tokens post as free-text history and raise `RES-ABID-UNMATCHED` (Warning). Historical antibodies are never removed by a later negative or different identification. An open antibody-identification workup in scope HardStops a posting ABID verify or a blood-attribute antibody verify that would post or deactivate history (`ABID-WORKUP-OPEN`). Adding or deactivating antibody history from the patient chart HardStops while any open workup exists for that patient (`ABID-WORKUP-OPEN`); the Antibodies card disables Add antibody and Deactivate. After complete or void, deactivate remains the authorized immuno path (OCD-017); deactivating a specificity posted by a completed workup warns (`ABID-DEACT-POSTED`) and still deactivates. Changing a patient antigen while a workup is open — chart save or a verified antigen result — warns (`ABID-AG-OPEN`), still records, and withdraws interpretation and review. Invalidating that verified antigen result warns (`ABID-AG-INVAL`) and withdraws again; phenotype is not auto-reverted. Manually changing ABO/Rh, or verifying an ABO/Rh result that changes current type, while a workup is open warns (`ABID-ABO-OPEN`), still records, and withdraws interpretation and review. Invalidating that verified ABO/Rh result warns (`ABID-ABO-INVAL`) and withdraws again; posted blood-type history is not auto-reverted. Correcting a verified ABO/Rh or antigen result while a workup is open warns (`ABID-TYPE-CORR`) and does not withdraw; complete and supervisor Accept HardStop (`ABID-TYPE-PENDING`) until verify or invalidate. Changing antigen or ABO/Rh after a completed in-scope workup warns (`ABID-AG-DONE` / `ABID-ABO-DONE`) and does not reopen or retract posted history. An antigen-positive type that conflicts with posted Identified history also warns `ABID-AG-DONE` (that antigen only). Adding or deactivating a special requirement while a workup is open warns (`ABID-SR-OPEN`) and still records. Electronic XM HardStops while any open workup exists for that patient (`XM-EC-ABID-OPEN`); serologic XM remains available and warns (`ABID-XM-OPEN`) without blocking. Allocating a unit while a workup is open warns (`ABID-ALLOC-OPEN`) and still reserves so serologic XM can proceed. Issue after the safety gate warns (`ABID-ISSUE-OPEN`) and still issues; it does not require override. Completing while reserved or issued units remain warns (`ABID-COMPLETE-PRODUCTS`) and requires acknowledgment; it does not identify antibodies or release those units. A completed workup on the same specimen or linked result is the identification of record: free-text or blood-attribute antibody verify does not change history (`ABID-WORKUP-AUTHORITATIVE`) and warns if the verified result disagrees (`ABID-WORKUP-DISAGREE`). Rejecting a linked specimen warns (`ABID-SPEC-OPEN`), withdraws interpretation and review, still rejects, and keeps the open workup as the patient-wide identification of record until void or re-link so a new draw cannot post history around it. A second workup HardStops (`ABID-WORKUP-DUP-OPEN`) while that unusable link remains; the worklist, Antibodies card, and dashboard flag `HasUnusableSpecimen` / `UnusableSpecimenCount`. A clock-expired or expired-status linked specimen is likewise patient-wide (`HasExpiredSpecimen` / `ExpiredSpecimenCount`); complete still warns and requires acknowledgment. A Received (not yet accepted) linked specimen is likewise patient-wide (`HasUnacceptedSpecimen` / `UnacceptedSpecimenCount`, OCD-031); complete still warns and requires acknowledgment (OCD-026). A Collected (not yet received) linked specimen is likewise patient-wide (`HasNotReadySpecimen` / `NotReadySpecimenCount`, OCD-032); complete and Accept remain HardStop (OCD-026). Dashboard and worklist specimen-attention badges open `/antibody-id?unusable=1`, `/antibody-id?expired=1`, `/antibody-id?unaccepted=1`, or `/antibody-id?notready=1`. Next-action counts open `/antibody-id?action=reactions`, `interpret`, or `review`. Inactive/expired lot counts open `/antibody-id?lotAttention=1`; named lots still open `/antibody-id?lot=`. Withdrawn-judgment counts open `/antibody-id?withdrawn=1`. Pending type-correction counts open `/antibody-id?typePending=1` (`ABID-TYPE-PENDING` still HardStops complete and Accept). Reserved or issued unit counts open `/antibody-id?products=1` (`ABID-COMPLETE-PRODUCTS` still warns and requires acknowledgment). Editing collection or expiration warns (`ABID-SPEC-EDIT`) and withdraws interpretation. See OCD-023.
- Antibody-identification workups (`AntibodyIdentificationService`) record manufacturer, lot, expiration, cells, antigen profiles, phases, strengths, autocontrol, DAT, selected cells, comments, assist findings, technologist interpretation, and supervisor review. Expired or inactive lots HardStop a new workup (`ABID-LOT-EXPIRED` / `ABID-LOT-INACTIVE`). On an open workup, a deactivated attached lot HardStops Accept and complete; a clock-expired attached lot warns and requires acknowledgment (OCD-027). Designated staff create manufacturers (`ABID-MFG-CREATE-PERM`) and lots (`ABID-LOT-CREATE-PERM`) in Administration. Lots require cells and typed antigens (`ABID-LOT-CREATE-CELLS` / `ABID-LOT-CREATE-AG`). Create writes `TestChange` and does not identify antibodies. `GetAsync` exposes the cell/antigen map and open workups that still use the lot; review does not identify antibodies. Lot and manufacturer list items include `OpenWorkupCount`, `CompletedWorkupCount`, and `PostedHistoryWorkupCount` (completed workups that posted Identified findings). `GetAsync` and `GetManufacturerAsync` list open workups and completed identifications of record (patient, MRN, lot). Completed rows include `PostedHistoryCount` (posted Identified findings only; no specificities). Manufacturer rows list every attached lot from that manufacturer. Voided workups are omitted. The lists do not identify antibodies. `ListOpenWorkupsAsync` exposes `AttachedLotNumbers`, `InactiveLotNumbers`, `ExpiredLotNumbers`, `ManufacturerName`, and `AttachedManufacturers` (name and code of every attached reagent source) so a withdrawn selected-cell lot or vendor is findable on the worklist. The open workup names manufacturer on selected-cell attach and names the inactive or expired lot on the header alert. A deactivated manufacturer cannot be used for a new lot (`ABID-LOT-CREATE-MFG`). Designated staff deactivate or restore lots (`ABID-LOT-ACT-PERM` / `ABID-LOT-DEACT-PERM`); deactivation requires a reason (`ABID-LOT-DEACT-REASON`) and does not identify antibodies or void the workup. Opening without a specimen warns `ABID-WORKUP-UNSCOPED`. The `/antibody-id` worklist lists every open workup (including unscoped) so a forgotten identification-of-record is visible without opening each patient. The patient Antibodies card lists those open workups with next action, specimen scope, and named lot flags. List items flag `HasInactiveLot` / `HasExpiredLot` when any attached reagent (primary or selected-cell) is withdrawn or clock-expired, and `NextAction` (record reactions, interpret, or supervisor review; `None` when completed or voided) from `AntibodyIdentificationWorklistRule`. `GET /api/antibody-id/summary` and the dashboard card show those counts plus distinct `InactiveLotNumbers` / `ExpiredLotNumbers`. The flags and counts do not identify antibodies or complete the workup. An open workup can link a specimen (`ABID-WORKUP-SPECIMEN`); completed or voided workups cannot. Linking to a specimen that already has another open workup HardStops (`ABID-WORKUP-DUP-OPEN`). Changing specimen scope after interpretation withdraws interpretation and review. A second overlapping open workup HardStops (`ABID-WORKUP-DUP-OPEN`), including when the open workup is linked to a Received (not yet accepted) or Collected (not yet received) specimen (OCD-031, OCD-032). Create and link HardStop rejected, cancelled, expired-status, clock-expired, or collected-not-received specimens (`ABID-WORKUP-SPEC-UNUSABLE` / `ABID-WORKUP-SPEC-EXPIRED` / `ABID-WORKUP-SPEC-NOT-READY`). Received-not-accepted warns (`ABID-WORKUP-SPEC-UNACCEPTED`). Complete HardStops a rejected, cancelled, or collected-not-received linked specimen. Complete on a clock-expired or received-not-accepted specimen warns and requires acknowledgment (OCD-025, OCD-026).
- Assistance (`AntibodyIdentificationAssistEvaluator`) may propose Excluded, Possible, CannotExclude, Inconclusive, or Historical findings. It never classifies Identified (`ABID-ASSIST-ADVISORY`). Stored assist findings refresh when reactions, selected-cell lots, DAT, or specimen scope change so the workup does not display a stale rule-out. Refresh does not identify antibodies and does not post history. Antigen codes are case-sensitive (`C` is not `c`). Dosage-aware evaluation (default on) does not rule out dosage-sensitive antibodies from heterozygous cells only and warns `ABID-SEL-CELL` when a homozygous selected cell is needed. Phenotype/genotype antigen-positive patients downgrade a Possible finding (`ABID-PHENO-CONFLICT` / `ABID-GENO-CONFLICT`). Method tokens genotype/molecular/predicted/dna/pcr mark predicted genotype (OCD-024). Identifying an antibody against a patient antigen-positive type warns (`ABID-INTERP-PHENO` / `ABID-INTERP-GENO`) and does not HardStop. Historical antibodies remain visible; a current exclusion of a historical specificity is `ABID-HIST-UNDETECTED` (Warning), not removal. Autocontrol is never used to rule out alloantibodies (`ABID-AC-POS`). Expired or inactive lots HardStop attaching additional lots as well as opening a workup.
- Completing a workup HardStops without technologist interpretation (`ABID-INTERP-REQUIRED`), if any Identified finding is Assist-sourced (`ABID-ASSIST-IDENTIFIED`), if supervisor review is required and missing/rejected (`ABID-REVIEW-REQUIRED` / `ABID-REVIEW-REJECTED`), if the same user reviews their own interpretation (`ABID-REVIEW-SELF`), or if an unverified ABO/Rh or antigen correction is current (`ABID-TYPE-PENDING`). Supervisor Accept HardStops Identified sign-off on an incomplete panel (`ABID-INCOMPLETE-RXN`), a rejected, cancelled, or collected-not-received linked specimen (`ABID-WORKUP-SPEC-UNUSABLE` / `ABID-WORKUP-SPEC-NOT-READY`), a deactivated attached lot (`ABID-LOT-INACTIVE`), and an unverified ABO/Rh or antigen correction (`ABID-TYPE-PENDING`). Accepting with leftover CannotExclude, history, DAT, none-identified, conflicting findings, or reserved/issued units requires acknowledgment (`ABID-REVIEW-ACK`). Acknowledgment does not identify antibodies. Reject remains available without acknowledgment. Panel reactions, selected-cell attach, DAT, specimen-scope, ABO/Rh, or antigen-type change after interpretation clear that interpretation and review (`ABID-INTERP-STALE` / `ABID-REVIEW-STALE`); complete HardStops until both are repeated. The open workup names that withdrawal so staff do not re-interpret against the wrong fact. Completing with Identified findings while panel or selected cells lack an interpretive-phase reaction HardStops (`ABID-INCOMPLETE-RXN`); completing with none identified and a blank cell warns. Completing re-evaluates assistance: leftover `CannotExclude` specificities warn `ABID-UNEXCLUDED` and dosage homozygous-cell needs warn `ABID-SEL-CELL`. Identifying a specificity that assistance would exclude warns `ABID-INTERP-EXCLUDED` at interpret and complete and HardStops interpret without a rationale (`ABID-INTERP-EXCL-REASON`, OCD-028). Interpretation also warns on leftover `CannotExclude` (`ABID-UNEXCLUDED`), homozygous selected-cell need (`ABID-SEL-CELL`), existing history (`ABID-HIST-REMAINS` / `ABID-HIST-UNDETECTED`), and incomplete reactions without identifying those specificities. Completing with existing active antibody history warns `ABID-HIST-REMAINS` (history is not removed) and, when the current panel would exclude a historical specificity, `ABID-HIST-UNDETECTED`. Those warnings do not identify antibodies. Completing while any of those clinical warnings (or `ABID-COMPLETE-NONE`, `ABID-DAT-INDICATED`, `ABID-INCOMPLETE-RXN` when posting nothing, phenotype/genotype conflict, `ABID-COMPLETE-PRODUCTS`) are present HardStops without an acknowledgment (`ABID-COMPLETE-ACK`). Acknowledgment does not identify antibodies. Completing with no Identified findings warns `ABID-COMPLETE-NONE` and posts nothing. Autocontrol reactive with DAT not performed warns `ABID-DAT-INDICATED` at complete and does not HardStop. History is posted only from technologist Identified findings after supervisor acceptance. Existing active history with the same catalog id or ordinal specificity is not duplicated. Identified specificities are resolved to the blood-attribute catalog so they drive antigen-negative selection; unmatched Identified specificities warn `ABID-INTERP-UNMATCHED` and post as free-text. Voiding requires a reason (`ABID-VOID-REASON`) and is blocked after complete (`ABID-VOID-COMPLETED`). Voiding while reserved or issued units remain warns (`ABID-VOID-PRODUCTS`) and still voids. A voided workup posts nothing and is no longer in scope for free-text ABID gating.
- Patient antigen phenotypes used at issue are updated in place (OCD-022); the prior result is retained on the named `Antibody` audit. Reflex tests added from a verified trigger write `OrderChange` on the added line after it has an id (`OrderId` and `LineId` in newValue), not generic Create. A matching test-level order/test rule at verify also writes `OrderChange` on the order (rule code, `TestResultId`, actions), not catalog `Configure`. A matching order-level rule at create/update writes `OrderChange` after the order has an id; blocked orders write no match event.
- Antibody-identification workup open, specimen link, lot attach, assist refresh, interpretation, void, and stale-panel write `Antibody` on the workup (not Result). Supervisor accept and complete stay `Verify`. Posted Identified findings stay `Antibody` on `AntibodyHistory`. Verified-result ABID posting writes that event on the history row after it has an id (`PatientId` and `HistoryIds` in newValue). Workup-complete posting also writes on the history row after it has an id (`PatientId`, `WorkupId`, `HistoryId`). Dual posting paths remain (OCD-023).

---

## 7. No-silent-change guarantees

- No clinical row is hard-deleted; status/void columns and history tables preserve prior state.
- The system never auto-resolves an ABO/Rh discrepancy, antibody conflict, or compatibility failure. Resolution is always an explicit, audited user action.
- Audit writes occur in the same transaction as the change; a failed audit rolls back the operation.
- New `AuditEvent` rows store `PreviousHash` and `RecordHash`. A payload edit or a missing hashed row fails `AuditHashChainRule.Verify` (`AUDIT-CHAIN-PAYLOAD` / `AUDIT-CHAIN-LINK`). Rows written before the chain are not rewritten. This is not a retention or purge policy (OCD-007).

---

## 8. Interactive authentication (Cycle 1)

Interactive HTTP identity is a server-issued session, not a self-asserted user name.
Application permission HardStops (`ISS-EMERG-PERM`, merge, override, and the rest of
section 1) still run after the session is accepted.

| Code | Rule | Severity if violated |
|---|---|---|
| `AUTH-SESSION-REVOKED` | Session has not been revoked | HardStop (401) |
| `AUTH-SESSION-ABS-EXPIRED` | Session is within its absolute lifetime | HardStop (401) |
| `AUTH-SESSION-IDLE` | Session is within idle timeout (default 30 minutes, sliding) | HardStop (401) |
| `AUTH-SESSION-INACTIVE` | Session user is still active | HardStop (401) |
| `AUTH-SESSION-LOCKED` | Session user is not locked | HardStop (401) |

Login fail-closes when `PasswordHash` is missing, the password is wrong, the account
is locked or inactive, or the account is a service account. Five failed sign-ins lock
the account and revoke open sessions. Logout, admin lock, and deactivate also revoke.
The raw token is never stored; only `SecretHasher.HashOpaqueToken` is persisted.
`X-User` is ignored unless `Auth:AllowLegacyIdentityHeader` is explicitly on (test
hosts only; Production forces the flag off). Assigning roles or updating a role's
permissions revokes outstanding sessions for the affected user(s). Development
`DevMode` remains a Development-only bypass.

---

## 9. Licensed ISBT catalog import (Cycle 7)

The engine does not invent ICCBBA product or ABO/RhD codes (OCD-004). A facility
that holds a license may replace placeholder rows through
`IsbtLicensedCatalogImportRule`.

| Code | Rule | Severity if violated |
|---|---|---|
| `ISBT-IMPORT-PERM` | Caller has `admin.config.edit` when importing a licensed catalog | HardStop when a permission evaluator is present and the privilege is missing |
| `ISBT-IMPORT-LICENSE` | Caller acknowledges the payload is from a current ICCBBA license | HardStop |
| `ISBT-IMPORT-VERSION` | `StandardVersion` is present and is not a placeholder or pending-ICCBBA token | HardStop |
| `ISBT-IMPORT-EMPTY` | Payload includes at least one licensee-supplied product or ABO/RhD row | HardStop |

---

## 10. HL7 inbound authentication (Cycle 8 / OCD-034)

HTTP `/api/hl7/*` requires an interactive session and `hl7.manage`. MLLP and
file-drop inbound do not evaluate a permission or shared secret. Enabling an
endpoint still requires `admin.hl7.manage` (`HL7EP-ENABLE-PERM`). The accepted
residual is a peer that can reach the bound port or write the drop folder.

---

## 11. Browser security headers (Cycle 9)

| Code | Rule | Severity if violated |
|---|---|---|
| `SEC-CORS-ANY` | `Cors:AllowAnyOrigin` is false outside Development | HardStop at API startup |

API and Web responses receive nosniff, `X-Frame-Options: DENY`, `frame-ancestors 'none'`,
and Permissions-Policy camera/microphone/geolocation disabled. API also sends
`Cache-Control: no-store`. Interactive identity remains Bearer in circuit memory
(not a cookie). Blazor Server CSP still allows `'unsafe-inline'` and `'unsafe-eval'`
for the circuit.

---

## 12. Downtime reconciliation snapshot (Cycle 11)

| Code | Rule | Severity if violated |
|---|---|---|
| `DT-RECON-PERM` | Caller has `audit.read` when viewing the downtime reconciliation snapshot | HardStop when a permission evaluator is present and the privilege is missing |

The snapshot counts existing unresolved interface errors, pending outbound HL7,
open issues, and pending retrospective crossmatches, and reports
`AuditHashChainRule.Verify`. It does not import paper records, invent a
facility SOP, or purge rows (OCD-007). Neonatal irradiation/CMV product
defaults remain order-rule hooks only (OCD-035).
