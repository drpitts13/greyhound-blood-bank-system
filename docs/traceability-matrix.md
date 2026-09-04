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
| R-TX-06 | Cooler / in-transit worklist | InTransitPendingRule ISS-IN-TRANSIT; IssuingService.ListInTransitAsync; Issues.CoolerId, InTransitDueUtc | Issued units stay in transit until ward receipt or return; overdue after Issue.InTransitDueHours (default 4) | D,A,I | SoftBank cooler checkout; AABB chain of custody |
| R-TX-07 | ISBT scan at ward receipt | ComponentScanVerifier; IssuingService.RecordWardReceiptAsync; Issues.WardScanJson | Fresh scan required when ComponentIdentity is set; mismatch HardStop; HL7 BPAM implicit receipt skips scan | D,A,I | AABB PPID; SoftBank remote-issue ward scan |
| R-TX-08 | Coded appearance catalog at issue | IssueAppearanceRule ISS-APPEAR; Issues.IssueAppearance | Defect codes HardStop; Acceptable stored on the issue | D,A,I | SoftBank/SafeTrace coded appearance at issue; AABB product integrity |
| R-TX-09 | Coded appearance catalog at ward receipt | WardAppearanceRule TX-WARD-APPEAR; Issues.WardAppearance | Defect codes HardStop; Acceptable stored on the issue; return to blood bank | D,A,I | SoftBank/SafeTrace remote-issue appearance; AABB product integrity |
| R-INV-01 | Two-person quarantine release | QuarantineReleaseVerifierRule INV-Q-RELEASE-2ND; SecondVerifierDirectoryRule; InventoryService.ReleaseFromQuarantineAsync | Distinct directory user required; same-user and unknown verifier HardStop | D,A,I | AABB quality release; SoftBank/SafeTrace dual control |
| R-INV-02 | Visual inspection at receive | ReceiveVisualInspectionRule INV-RCV-VISUAL; ReceiveUnitAsync; ISBT receive | Failed appearance HardStop; no unit created; notes stored on pass | D,A,I | AABB receipt inspection; SoftBank/SafeTrace appearance check |
| R-INV-05 | Coded appearance catalog at receive | ReceiveAppearanceRule INV-RCV-APPEAR; BloodUnit.ReceiveAppearance | Defect codes HardStop; Acceptable stored on the unit | D,A,I | SoftBank/SafeTrace coded appearance |
| R-INV-09 | Receipt temperature at consignee receive | ReceiveTemperatureRule INV-RCV-TEMP; BloodUnit.ReceiveTemperatureCelsius | Missing or out-of-range (1–10 °C) HardStop; no unit created; value stored on pass | D,A,I | SoftBank/SafeTrace cooler temp at receive; AABB product integrity |
| R-INV-03 | Expected inbound / packing-list units | InventoryService.ExpectUnitAsync; ReceiveExpectedUnitAsync; UnitStatus.Expected | ASN creates Expected; arrival visual gate; cancel to CancelledAssignment | D,A,I | SoftBank/SafeTrace expected inventory; 21 CFR 606.165 |
| R-INV-13 | Overdue expected inbound worklist | ExpectedArrivalPendingRule INV-EXPECT-OVERDUE; InventoryService.ListExpectedAsync; BloodUnit.ExpectedArrivalDueUtc | Expected units stay on the worklist until arrival, cancel, or supplier return; overdue after Inventory.ExpectedArrivalDueHours (default 24) | D,I | SoftBank/SafeTrace ASN follow-up; 21 CFR 606.165 |
| R-INV-14 | Near-expiry inventory worklist | BloodUnitExpirationRule UNIT-NEAR-EXPIRY; InventoryService.ListNearExpiryAsync | On-hand units expiring within Inventory.NearExpiryWarningHours (default 24); packing-list and already-expired excluded | D,I | SoftBank/SafeTrace FIFO outdate; AABB expiration |
| R-INV-15 | Coded quality-quarantine disposition | QuarantineReasonRule INV-Q-REASON; BloodUnit.QuarantineReasonCode; InventoryService.QuarantineAsync, ListQuarantineAsync | Unspecified HardStop; Other requires notes; worklist GET /api/inventory/units/quarantine | D,A,I | SoftBank/SafeTrace coded quarantine; 21 CFR 606.165 |
| R-INV-04 | Two-person receive | ReceiveVerifierRule INV-RCV-2ND; ReceiveUnitAsync; ReceiveExpectedUnitAsync; ISBT receive | Distinct directory user required; same-user and unknown verifier HardStop | D,A,I | AABB receipt dual control; SoftBank/SafeTrace two-person check |
| R-INV-06 | Two-person discard | DiscardVerifierRule INV-DISC-2ND; SecondVerifierDirectoryRule; InventoryService.DiscardAsync | Distinct directory user required; same-user and unknown verifier HardStop | D,A,I | AABB disposition dual control; SoftBank/SafeTrace two-person discard |
| R-INV-07 | Missing unit / locate | InventoryService.MarkMissingAsync, LocateMissingAsync; UnitStatus.Missing | Reason required; not issuable; locate enters Quarantine for inspection | D,I | AABB 21 CFR 606.165; SoftBank/SafeTrace inventory discrepancy |
| R-INV-08 | Damaged unit / inspect | InventoryService.MarkDamagedAsync, InspectDamagedAsync; UnitStatus.Damaged | Reason required; not issuable; inspect enters Quarantine; discard allowed | D,I | AABB product integrity; SoftBank/SafeTrace damaged disposition |
| R-INV-16 | Missing / damaged discrepancy worklist | InventoryService.ListDiscrepancyAsync | Missing and damaged units stay on GET /api/inventory/units/discrepancy until locate or inspect | D,I | SoftBank/SafeTrace physical inventory; 21 CFR 606.165 |
| R-INV-10 | Return to supplier | InventoryService.ReturnToSupplierAsync; UnitStatus.ReturnedToSupplier | Reason required; terminal; not issuable; closes failed consignee receipt or unused stock | D,A,I | SoftBank/SafeTrace return-to-vendor; AABB product integrity |
| R-INV-11 | Autologous / directed recipient lock | AutologousDirectedRule INV-AUTO-DIR / ISS-AUTO-DIR; BloodUnit.DonationRestriction, ReservedPatientId | Recipient required at receive; allocate/issue to another patient HardStop | D,I | AABB autologous/directed; SoftBank/SafeTrace reserved unit |
| R-INV-12 | Directed-to-allogeneic conversion | AutologousDirectedRule INV-DIR-ALLO; DirectedConversionVerifierRule INV-DIR-CONV-2ND; InventoryService.ConvertDirectedToAllogeneicAsync | Directed unused stock converts with reason + second verifier; autologous HardStop; reservation statuses HardStop | D,I | AABB directed release; SoftBank/SafeTrace convert to volunteer |
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
| URS-BB-007 / FRS-BB-041 | One active reservation and one open issue per unit | `IX_Allocations_OneReservedPerUnit`; `IX_Issues_OneOpenIssuePerUnit`; `InventoryConcurrency` | `AllocationIssueConcurrencyTests` | I | RISK-BB-006 |
| URS-BB-008 / FRS-BB-032 | Autologous/directed issue only to designated recipient | `IssueGate` + `AutologousDirectedRule` `ISS-AUTO-DIR` | `IssueGateSafetyRegressionTests`; Phase 4 | D,I | RISK-BB-007 |
| URS-BB-009 / FRS-BB-033 | Electronic XM blocked by any antibody history row | `ElectronicCrossmatchEligibilityService` `XM-EC-HISTORY` | `ElectronicXmHistoryRegressionTests` | I | RISK-BB-005; OCD-001 |
| URS-BB-015 / FRS-BB-003 | Merged patient record cannot be used clinically | `PatientMergeRule` `PAT-MERGED-INACTIVE`; IssueGate; CompatibilityService; SpecimenService; EncounterService; OrderService; ResultService (enter/verify/correct); ImmunohematologyService; SpecialRequirementService | `MergedPatientClinicalUseTests` | D,I | RISK-BB-015; OCD-009 |
| URS-BB-003 / FRS-BB-010 | Expired specimen cannot establish results | `ResultService.ValidateSpecimenForEntryAsync` on enter and verify | `MergedPatientClinicalUseTests` expired-verify case | I | RISK-BB-016 |

Citations support validation evidence. They are **not** a claim that this software is AABB-accredited or FDA-cleared.

Stable ID map: `docs/requirements/USER_REQUIREMENTS.md`, `FUNCTIONAL_REQUIREMENTS.md`, `DESIGN_REQUIREMENTS.md`.
Living risk register: `docs/risk/RISK_REGISTER.md`.
