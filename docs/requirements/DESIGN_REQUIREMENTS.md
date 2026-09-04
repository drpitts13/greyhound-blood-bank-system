# Design requirements

| ID | Design constraint | FRS |
|---|---|---|
| SRS-BB-001 | Domain rules are pure functions. They do not read the clock or database. Application services assemble facts and call the rule. | FRS-BB-030, FRS-BB-031 |
| SRS-BB-002 | `IssueGate` is the single issue-time rule aggregation point. Additional HardStops may be appended (dual ID, second-verifier directory) but must not replace the gate. | FRS-BB-031 |
| SRS-BB-003 | Electronic XM eligibility is computed by `ElectronicCrossmatchEligibilityService` / `ElectronicCrossmatchEligibilityRule` and reused by the patient workspace and issue pathway. | FRS-BB-033 |
| SRS-BB-004 | Assign and issue persist inside one unit-of-work. Unique filtered indexes enforce one active reservation and one open issue per unit. | FRS-BB-041 |
| SRS-BB-005 | AntibodyHistory, PatientBloodTypeHistory, InventoryStatusHistory, AuditEvent, and ElectronicSignature cannot be deleted through normal application saves. | FRS-BB-002, FRS-BB-060 |
| SRS-BB-006 | SQLite development databases apply additive columns/indexes via `DevelopmentSqliteBootstrap` so unique safety indexes exist on existing files. | FRS-BB-041 |
| SRS-BB-007 | Authorization is evaluated in the Application layer, not only in the UI. | FRS-BB-040 |
| SRS-BB-008 | ISBT raw scan, parsed fields, and display values are stored separately. | URS identity / ISBT docs |
| SRS-BB-009 | A merged (losing) patient is rejected at each clinical write path (accession, visit, order, result entry/verify/correct, immunohematology, special requirements, allocate, XM, issue), not only at issue. | FRS-BB-003 |
| SRS-BB-010 | Result verification reuses the specimen entry gate (accepted, unexpired, surviving patient) before ABO/antibody history is posted. | FRS-BB-010, FRS-BB-003 |
| SRS-BB-011 | ADT and ORM resolve a merged MRN to the surviving patient (`PatientMergeFollow` / `FindByMrn` follow) before writing demographics or placing orders. | FRS-BB-080, FRS-BB-003 |
| SRS-BB-012 | Manual patient merge is authorized with `patient.merge`, not `patient.write`. The API default-denies; the UI hides the action. A second authorizer is not required (OCD-010). | FRS-BB-003 |
| SRS-BB-013 | Product retype record does not change unit status. Verify is a separate Application action that applies `SelfVerifyRule` before Available/Quarantine. | FRS-BB-051 |
| SRS-BB-014 | Emergency/MTP issue is authorized with `issue.emergency-release` in `IssuingService`, not only in the UI. Standard issue does not grant emergency release. The issue-type list hides Emergency/MTP without that permission. | FRS-BB-040 |
| SRS-BB-015 | Immunohematology writes are authorized inside `ImmunohematologyService`: `immuno.override` for manual ABO and antibody deactivate; `immuno.record` for antibody add and antigen profile. Callers that skip the API filter cannot change history used at issue. | FRS-BB-021, FRS-BB-002 |
| SRS-BB-016 | `ResultService.VerifyResultAsync` fail-closes ABO self-verify when the policy service is null. `SaveTestResultAsync` never auto-verifies `ABORH`. Billing capture after save runs only when the result is Verified. | FRS-BB-022 |
| SRS-BB-017 | Special-requirement writes are authorized inside `SpecialRequirementService`: `immuno.record` to add; `immuno.override` to deactivate. The UI already hides those actions; the service is the control. | FRS-BB-023 |
| SRS-BB-018 | `LookbackService.RecallByDinAsync` checks `lookback.manage` when a permission evaluator is present, then fails closed if any `RecallAsync` does not succeed. Reserved-state → Recalled is an allowed inventory transition. | FRS-BB-024 |
| SRS-BB-019 | Quarantine release is authorized inside `InventoryService` with `inventory.release`. Callers that skip the API filter cannot move a held unit to Available. | FRS-BB-052 |
| SRS-BB-020 | Directed-to-allogeneic conversion is authorized inside `InventoryService` with `inventory.release`. Callers that skip the API filter cannot clear the directed reservation. | FRS-BB-053 |
| SRS-BB-021 | Operational-hold release is authorized inside `InventoryService` with `inventory.release`. Callers that skip the API filter cannot move a held unit to Available. | FRS-BB-054 |
| SRS-BB-022 | Result verify and unit retype verify are authorized inside `ResultService` and `ProductRetypeService` with `result.verify`. Callers that skip the API filter cannot establish current type or move a unit to Available. | FRS-BB-055 |
| SRS-BB-023 | Allocation and crossmatch are authorized inside `CompatibilityService` with `compatibility.allocate` and `compatibility.crossmatch`. The patient workspace allocate path uses the same service. | FRS-BB-056 |
| SRS-BB-024 | `IssuingService.IssueUnitAsync` requires `issue.create` before the issue gate. Emergency/MTP still requires `issue.emergency-release` in addition. | FRS-BB-057 |
| SRS-BB-025 | Result enter and in-place update are authorized inside `ResultService` with `result.enter`. Verified-result correction requires `result.correct`. Unit retype record requires `result.enter` in `ProductRetypeService`. | FRS-BB-058 |
| SRS-BB-026 | Specimen accession, collection-metadata edit, and rejection are authorized inside `SpecimenService` with `specimen.accession`, `specimen.edit`, and `specimen.reject`. Callers that skip the API filter cannot bind a specimen to a patient or change collection time used at issue. | FRS-BB-059 |
| SRS-BB-027 | Patient demographic updates are authorized inside `PatientService` with `patient.write`. Callers that skip the API filter cannot change name, date of birth, sex, status, or pregnancy history used at identification and issue. | FRS-BB-061 |
