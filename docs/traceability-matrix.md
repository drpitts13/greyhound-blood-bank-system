# Blood Bank LIS — Requirements Traceability Matrix

Status: Phase 0 (design). This matrix links each major requirement to its design location and the planned test(s). Test IDs are placeholders until the test projects exist (Phase 1+); the `Test (planned)` column names the intended test and its layer. Update this file whenever a rule, use case, or test is added (see `validation-plan.md` Definition of Done).

Layers: D = Domain.Tests, A = Application.Tests, H = HL7.Tests, I = Integration.Tests.

| Req ID | Requirement | Design reference | Test (planned) | Layer |
|---|---|---|---|---|
| R-PT-01 | Patient demographics + MRN + alternate identifiers | erd.md Patients/PatientIdentifiers | CRUD + unique MRN | I |
| R-PT-02 | Encounter/visit support | erd.md Encounters | Encounter create + visit uniqueness | I |
| R-PT-03 | ABO/Rh history append-only | erd.md PatientBloodTypeHistory; safety-rules 6/7 | History append + IsCurrent flip | A,I |
| R-PT-04 | Antibody history | erd.md AntibodyHistory | Antibody add/deactivate audited | A |
| R-PT-05 | Special transfusion requirements | erd.md SpecialTransfusionRequirements | Requirement enforced on issue | D,A |
| R-SP-01 | Specimen accessioning + barcode | workflows 2; erd.md Specimens | Accession + unique accession no. | A,I |
| R-SP-02 | Specimen expiration logic | safety-rules 2 | Expiry boundary tests | D |
| R-SP-03 | Specimen rejection/cancellation | workflows 2 | Reject sets status + reason | A |
| R-TS-01 | ABO/Rh, antibody screen, DAT, antigen typing result entry | erd.md Tests/TestResults | Result entry per test type | A |
| R-TS-02 | Result verification | workflows 3 | Verify sets verifier/utc | A |
| R-TS-03 | Result correction (versioned) | safety-rules 6 | Correction creates new version | A |
| R-TS-04 | Delta check vs history | safety-rules 6 | ABO/Rh delta raises warning | D,A |
| R-IN-01 | Blood unit intake | workflows 1; erd.md BloodProducts | Intake into Quarantine | A,I |
| R-IN-02 | Inventory search | erd.md indexes | Search by unit/status/expiry | I |
| R-IN-03 | Status transitions guarded | safety-rules 4 | Allowed/disallowed transitions | D |
| R-IN-04 | Location transfers | workflows; erd.md InventoryStatusHistory | Transfer writes history | A |
| R-IN-05 | Expiration enforcement | safety-rules 4 | Expired blocks allocate/issue | D |
| R-IN-06 | Discard workflow | workflows 8; safety-rules 5 | Discard requires reason + audit | A |
| R-IN-07 | Product modification (divide/pool/irradiate/thaw/volume-reduce/leukoreduce) | workflows 8a; safety-rules 4a; erd.md ModificationRules/UnitModifications/UnitModificationUnits | Rule eligibility + capped expiration + Modify audit for all six types | D,A,I |
| R-CM-01 | ABO/Rh compatibility matrix | safety-rules 3 | Full matrix truth table | D |
| R-CM-02 | Crossmatch records | erd.md Crossmatches | Crossmatch create + expiry | A |
| R-CM-03 | Allocation/reservation | erd.md Allocations | One active allocation per unit | A,I |
| R-CM-04 | Issue gate (all checks) | safety-rules 1; workflows 5 | Each ISS-* rule pos/neg | D,A |
| R-CM-05 | Emergency release | workflows 6; safety-rules 5 | Override + signature required | A |
| R-CM-06 | Return to inventory | workflows 7 | Reissue eligibility re-check | A |
| R-CM-07 | P-tag data generation | printing-billing A.4 | P-tag model reflects issue | A |
| R-TX-01 | Transfusion documentation | erd.md TransfusionEvents | Start/stop/volume/disposition | A |
| R-TX-02 | Reaction investigation | erd.md ReactionInvestigations | Reaction linked to transfusion | A |
| R-HL-01 | Inbound ADT | hl7-design 2.1 | ADT updates demographics only | H,A |
| R-HL-02 | Inbound ORM/OML | hl7-design 2.2 | Order created from message | H,A |
| R-HL-03 | Outbound ORU | hl7-design 2.3 | Verified result builds ORU | H |
| R-HL-04 | ACK/NAK | hl7-design 3 | AA/AE/AR per outcome | H |
| R-HL-05 | Message log + replay | hl7-design 4/5 | Replay is idempotent + audited | A,I |
| R-PR-01 | Specimen label print | printing-billing A.1 | Label renders from model | A |
| R-PR-02 | Compatibility/P-tag print | printing-billing A.1 | Tag renders from model | A |
| R-PR-03 | Reprint audit controls | printing-billing A.3; safety-rules 5 | Reprint requires reason + audit | A |
| R-PR-04 | Print preview | printing-billing A.2 | Preview without printing | A |
| R-BL-01 | Charge rules + codes | printing-billing B.1 | Trigger maps to charge code | A |
| R-BL-02 | Charge trigger events | printing-billing B.2 | TestVerified/UnitIssued create event | A |
| R-BL-03 | Duplicate prevention | printing-billing B.3 | Dedupe key unique | A,I |
| R-BL-04 | Charge review queue | printing-billing B.4 | Status flow + cancel audit | A |
| R-SE-01 | Roles/permissions | architecture 4.2; erd.md; validation-scripts S-12 | PermissionPolicy truth table; evaluator role→permission resolution; default-deny | D,I |
| R-SE-02 | Electronic signature | erd.md ElectronicSignatures; safety-rules 5; validation-scripts S-06 | Signature recorded/owner+action validated; issue override gated on e-sign | I |
| R-AU-01 | Audit every clinical action | architecture 4.1 | Each use case writes AuditEvent | A |
| R-AU-02 | No silent data change | safety-rules 7 | Corrections versioned, originals kept | A,I |
