# Blood Bank LIS — Requirements Traceability Matrix

Status: Phase 0 (design). This matrix links each major requirement to its design location and the planned test(s). Test IDs are placeholders until the test projects exist (Phase 1+); the `Test (planned)` column names the intended test and its layer. Update this file whenever a rule, use case, or test is added (see `validation-plan.md` Definition of Done).

Layers: D = Domain.Tests, A = Application.Tests, H = HL7.Tests, I = Integration.Tests.

| Req ID | Requirement | Design reference | Test (planned) | Layer | Regulatory note |
|---|---|---|---|---|---|
| R-PT-01 | Patient demographics + MRN + alternate identifiers | erd.md Patients/PatientIdentifiers | CRUD + unique MRN | I | AABB two identifiers |
| R-PT-02 | Encounter/visit support | erd.md Encounters | Encounter create + visit uniqueness | I | |
| R-PT-03 | ABO/Rh history append-only | erd.md PatientBloodTypeHistory; safety-rules 6/7 | History append + IsCurrent flip | A,I | 21 CFR 606.160 |
| R-PT-04 | Antibody history | erd.md AntibodyHistory; safety-rules 6 | Manual add/deactivate; verified ABID posts catalog/free-text specificities | D,A,I | 21 CFR 606.151 |
| R-PT-05 | Special transfusion requirements | erd.md SpecialTransfusionRequirements; IssueGate ISS-SPECIAL-REQ | Requirement enforced on issue | D,A,I | AABB special needs; computer check |
| R-SP-01 | Specimen accessioning + barcode | workflows 2; erd.md Specimens | Accession + unique accession no. | A,I | AABB 5.11 two identifiers |
| R-SP-02 | Specimen expiration logic | safety-rules 2 | 3-day vs standard window | D,I | AABB 3-day when transfused/pregnant 3 mo |
| R-SP-03 | Specimen rejection/cancellation | workflows 2 | Reject sets status + reason | A | |
| R-TS-01 | ABO/Rh, antibody screen, DAT, antigen typing result entry | erd.md Tests/TestResults | Result entry per test type | A | 21 CFR 606.151 |
| R-TS-02 | Result verification | workflows 3 | Verify sets verifier/utc; optional RES-SELF-VERIFY | A | CLIA/AABB self-verify control |
| R-TS-03 | Result correction (versioned) | safety-rules 6 | Correction creates new version | A | 21 CFR 606.160 |
| R-TS-04 | Delta check vs history | safety-rules 6 | ABO/Rh delta raises warning | D,A | |
| R-IN-01 | Blood unit intake | workflows 1; erd.md BloodProducts | Intake into Quarantine | A,I | 21 CFR 606.165 |
| R-IN-02 | Inventory search | erd.md indexes | Search by unit/status/expiry | I | |
| R-IN-03 | Status transitions guarded | safety-rules 4 | Allowed/disallowed transitions | D | |
| R-IN-04 | Location transfers | workflows; erd.md InventoryStatusHistory | Transfer writes history | A | |
| R-IN-05 | Expiration enforcement | safety-rules 4 | Expired blocks allocate/issue | D | |
| R-IN-06 | Discard workflow | workflows 8; safety-rules 5 | Discard requires reason + audit | A | |
| R-IN-07 | Product modification (divide/pool/irradiate/thaw/volume-reduce/leukoreduce) | workflows 8a; safety-rules 4a; erd.md ModificationRules/UnitModifications/UnitModificationUnits | Rule eligibility + capped expiration + Modify audit for all six types | D,A,I | |
| R-CM-01 | ABO/Rh compatibility matrix | safety-rules 3 | Full matrix truth table | D | 21 CFR 606.151 |
| R-CM-02 | Crossmatch records | erd.md Crossmatches | Crossmatch create + expiry | A | |
| R-CM-03 | Allocation/reservation | erd.md Allocations | One active allocation per unit | A,I | |
| R-CM-04 | Issue gate (all checks) | safety-rules 1; workflows 5 | Each ISS-* rule pos/neg; computer-evaluated identity/product/ABO | D,A,I | 21 CFR 606.151 |
| R-CM-05 | Emergency release | workflows 6; safety-rules 5 | Override + signature required; tests-incomplete banner | A | 21 CFR 606.151(b) |
| R-CM-06 | Retrospective crossmatch follow-up | RetrospectiveCrossmatchPendingRule ISS-RETRO-XM-PENDING; IssuingService worklist; CompatibilityService close | Pending queue until post-issue compatible XM; incompatible stays open; return drops | D,A,I | 21 CFR 606.151(b); AABB emergency release; SoftBank/SafeTrace |
| R-CM-06 | Return to inventory | workflows 7; RET-REISSUE | Temperature/seal/visual/time enforced | D,A | AABB return policy |
| R-CM-07 | P-tag data generation | printing-billing A.4 | P-tag model reflects issue | A | |
| R-CM-08 | Electronic XM two ABO/Rh determinations | ElectronicCrossmatchEligibilityRule | Second concordant ABO required | D,A,I | AABB computer XM |
| R-TX-01 | Transfusion documentation | erd.md TransfusionEvents | Start/stop/volume/disposition | A | |
| R-TX-02 | Reaction investigation | erd.md ReactionInvestigations; 21 CFR 606.170 | Auto-open, AABB workup checklist, remainder quarantine, fatality due dates | A,I | 21 CFR 606.170 |
| R-TX-03 | Dual identification at issue/transfusion | DualIdentificationRule TX-DUAL-ID | Distinct second verifier or electronic ID | D | AABB administration |
| R-TX-04 | Second verifier is a directory user; ISBT scan at issue/bedside | SecondVerifierDirectoryRule TX-SECOND-USER; ComponentScanVerifier; issue/transfusion UI | Unknown verifier HardStop; VerifiedScan/BedsideScan posted from Web | D,A,I | AABB PPID; SoftBank/SafeTrace two-person check |
| R-TX-05 | Ward/remote-issue receipt before transfusion | WardReceiptRule TX-WARD-RECEIPT; IssuingService.RecordWardReceiptAsync; Issues.WardReceivedUtc | HardStop until receiving location acknowledges; return allowed without receipt | D,A,I | AABB chain of custody; SoftBank remote issue |
| R-LB-01 | DIN lookback and recall | LookbackService; 21 CFR 610.46–47 | Recall all components; notification worklist | I | 21 CFR 610.46–47, 606.165 |
| R-LB-02 | Recipient traceback (patient → units → source DINs → co-recipients) | LookbackService.FindByRecipientAsync; 21 CFR 606.165 | Issued units, related components, co-recipients; merge follow | I | 21 CFR 606.165 bidirectional trace |
| R-QS-01 | Deviation / nonconformance | erd.md Deviations | Create + CAPA status | I | AABB Standards 7 |
| R-HL-01 | Inbound ADT | hl7-design 2.1 | ADT updates demographics only | H,A | |
| R-HL-02 | Inbound ORM/OML | hl7-design 2.2 | Order created from message | H,A | |
| R-HL-03 | Outbound ORU | hl7-design 2.3 | Verified result builds ORU | H | |
| R-HL-04 | ACK/NAK | hl7-design 3 | AA/AE/AR per outcome | H | |
| R-HL-05 | Message log + replay | hl7-design 4/5 | Replay is idempotent + audited | A,I | |
| R-HL-06 | File-drop transport | hl7-design 4; InterfaceTransport.File | Inbound poll + ACK/archive; outbound write; path required | D,I | SoftBank/SafeTrace folder interfaces |
| R-PR-01 | Specimen label print | printing-billing A.1 | Label renders from model | A | |
| R-PR-02 | Compatibility/P-tag print | printing-billing A.1 | Tag renders from model; incomplete-testing banner | A | 21 CFR 606.151(b) |
| R-PR-03 | Reprint audit controls | printing-billing A.3; safety-rules 5 | Reprint requires reason + audit | A | |
| R-PR-04 | Print preview | printing-billing A.2 | Preview without printing | A | |
| R-PR-05 | ISBT component / product label | ComponentLabelTemplate; PrintService.PrintComponentLabelAsync | DIN barcode + product + ABO + expiry; reprint rebuilds | A,I | SoftBank/SafeTrace unit labeling |
| R-BL-01 | Charge rules + codes | printing-billing B.1 | Trigger maps to charge code | A | |
| R-BL-02 | Charge trigger events | printing-billing B.2 | TestVerified/UnitIssued create event | A | |
| R-BL-03 | Duplicate prevention | printing-billing B.3 | Dedupe key unique | A,I | |
| R-BL-04 | Charge review queue | printing-billing B.4 | Status flow + cancel audit | A | |
| R-SE-01 | Roles/permissions | architecture 4.2; erd.md; validation-scripts S-12 | PermissionPolicy truth table; evaluator role→permission resolution; default-deny | D,I | |
| R-SE-02 | Electronic signature | erd.md ElectronicSignatures; safety-rules 5; 21 CFR Part 11 | Re-auth + unused-signature binding | I | 21 CFR Part 11 |
| R-AU-01 | Audit every clinical action | architecture 4.1 | Each use case writes AuditEvent | A | 21 CFR 606.160(a) |
| R-AU-02 | No silent data change | safety-rules 7 | Corrections versioned, originals kept; append-only interceptor | A,I | 21 CFR 606.160 |
| R-AU-03 | Record retention | SystemSettings Record.RetentionYears | 10-year metadata; no purge of product/compatibility/transfusion/audit | I | 21 CFR 606.160(d) |

Citations support validation evidence. They are **not** a claim that this software is AABB-accredited or FDA-cleared.
