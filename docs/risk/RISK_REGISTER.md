# Risk register

Status: living register. Severity is potential patient harm. Likelihood assumes
the listed control is absent. Residual risk assumes the control is in place and
tested.

Scoring: Severity (Sev) and Likelihood (Lik) on Low / Medium / High / Critical.

The legacy copy at `docs/risk-register.md` points here.

| ID | Workflow | Hazard | Cause | Potential consequence | Sev | Lik | Existing controls | Required mitigation | Verification | Residual | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|
| RISK-BB-001 | Issue | ABO-incompatible unit issued | Matrix not applied or overridden | Wrong-blood transfusion | Critical | Medium | `ISS-ABO-COMPAT` HardStop | Keep HardStop; never auto-downgrade except listed emergency Warning conversion | `IssueGateSafetyRegressionTests` | Low | Open / controlled |
| RISK-BB-002 | Issue | Expired specimen used | Validity not recomputed | Missed alloantibody | Critical | Medium | `SPEC-EXPIRED` HardStop | Configurable windows; SME verify defaults (OCD-002) | Domain expiry tests | Low | Open / controlled |
| RISK-BB-003 | Issue | Expired unit issued | Clock/status drift | Unsuitable component transfused | High | Medium | `ISS-UNIT-EXPIRED` HardStop | Near-expiry worklist | Domain + inventory tests | Low | Open / controlled |
| RISK-BB-004 | Issue | Unit issued without required XM | EXM treated as a checkbox | Incompatible transfusion | Critical | Medium | `ElectronicCrossmatchEligibilityService`; `ISS-XM-REQUIRED` | Eligibility returns reasons | Eligibility + issue tests | Low | Open / controlled |
| RISK-BB-005 | Issue | Historical antibody ignored | Deactivation treated as deletion | Antigen-positive unit issued / illicit EXM | Critical | Medium | All antibody rows drive antigen-neg and EXM block | Banner shows historical antibodies | `ElectronicXmHistoryRegressionTests` | Low | Mitigated this cycle |
| RISK-BB-006 | Assign/Issue | Double reservation or double issue | Concurrent users pass application checks | Same unit assigned to two patients | Critical | Medium | Filtered unique indexes + fail-closed messages | Keep indexes on SQL Server and SQLite bootstrap | `AllocationIssueConcurrencyTests` | Low | Mitigated this cycle |
| RISK-BB-007 | Issue | Autologous/directed unit to wrong patient | Rule only in service, not gate | Wrong-blood transfusion | Critical | Low | `ISS-AUTO-DIR` inside `IssueGate` | Keep in gate | IssueGate + Phase 4 tests | Low | Mitigated this cycle |
| RISK-BB-008 | Emergency | Unaudited emergency release | "Ignore all rules" path | Untraced incompatible issue | High | Low | Distinct issue type; override reason/authorizer; retro XM worklist | Keep retrospective follow-up | Phase 4 emergency tests | Low | Open / controlled |
| RISK-BB-009 | Results | Verified result overwritten | Destructive update | Lost original interpretation | High | Low | Versioned correction | No delete of released results | Result tests | Low | Open / controlled |
| RISK-BB-010 | Interfaces | HL7 replay duplicates or corrupts history | Missing idempotency / ADT overreach | Wrong patient data / duplicate orders | High | Medium | Control-id idempotency; ADT does not write IH history | Keep error queue | HL7 tests | Medium | Open / controlled |
| RISK-BB-011 | Audit | Lost or mutated audit | Separate transaction / update path | Untraceable clinical change | High | Low | Same save pipeline; append-only interceptor | Keep interceptor | Audit tests | Low | Open / controlled |
| RISK-BB-012 | Identity | Wrong patient selected | Name-only matching | Wrong-blood transfusion | Critical | Medium | Two-token identity HardStop | Barcode-first workflows | Identity tests | Low | Open / controlled |
| RISK-BB-013 | Recall | Recalled unit issued | Status not consulted | Recalled component transfused | Critical | Low | Non-issuable status + early recall reject | Keep both layers | IssueGate recalled test | Low | Open / controlled |
| RISK-BB-014 | ISBT | Incorrect DIN/product parse | Unvalidated ICCBBA tables | Wrong component identity | Critical | Medium | Raw/parsed/display separation; placeholder catalogs | OCD-004 licensed tables | ISBT parser tests | Medium | Open — SME |
| RISK-BB-015 | Testing / issue / XM / allocate | Work performed on a merged (losing) patient record | Accession, visit, order, result, XM, allocate, or issue used the retired id | Wrong-patient transfusion or split history | Critical | Medium | `PAT-MERGED-INACTIVE` HardStop in IssueGate, CompatibilityService, SpecimenService, EncounterService, OrderService, ResultService (enter/verify/correct), ImmunohematologyService, SpecialRequirementService | Keep survivor-only clinical use; Inactive remains allowed (OCD-009) | `MergedPatientClinicalUseTests`; IssueGate merge test | Low | Mitigated this cycle |
| RISK-BB-016 | Results | ABO/antibody history posted from an expired specimen | Verify skipped the specimen expiry gate | Current type established from an invalid sample | Critical | Medium | `ValidateSpecimenForEntryAsync` runs on enter and verify | Keep gate on both paths | `MergedPatientClinicalUseTests` expired-verify case | Low | Mitigated this cycle |

Citations are validation evidence, not certification.
