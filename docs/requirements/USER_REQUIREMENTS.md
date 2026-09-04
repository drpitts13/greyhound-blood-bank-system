# User requirements

IDs are stable. Citations support validation evidence. They are **not** a claim
that this software is AABB-accredited or FDA-cleared.

| ID | Need | Priority | Related FRS |
|---|---|---|---|
| URS-BB-001 | Staff can identify a patient by enterprise identifiers (MRN and a second identifier), not by display name alone. | P1 | FRS-BB-001 |
| URS-BB-002 | Staff can see current ABO/Rh, historical types, antibodies (including currently undetectable), and special requirements on the patient record. | P0 | FRS-BB-002 |
| URS-BB-003 | Staff can accession a specimen with collection metadata and computed validity. | P1 | FRS-BB-010 |
| URS-BB-004 | Staff can enter, verify, and amend immunohematology results without destroying prior released values. | P0 | FRS-BB-020 |
| URS-BB-005 | Staff can evaluate compatibility with an explainable rule outcome (rule ID + reason). | P0 | FRS-BB-030 |
| URS-BB-006 | Staff can reserve, crossmatch, and issue a unit only when safety checks pass, or via a controlled emergency-release path. | P0 | FRS-BB-031, FRS-BB-040 |
| URS-BB-007 | Two users cannot reserve or issue the same unit. | P0 | FRS-BB-041 |
| URS-BB-008 | Autologous and directed units can be issued only to the designated recipient. | P0 | FRS-BB-032 |
| URS-BB-009 | Electronic (computer) XM is available only when configured prerequisites hold. | P0 | FRS-BB-033 |
| URS-BB-010 | Inventory states and modifications remain historically traceable. | P3 | FRS-BB-050 |
| URS-BB-011 | Clinical and configuration changes are auditable (who/what/when/where/old/new/why). | P4 | FRS-BB-060 |
| URS-BB-012 | Administrators can configure tests, products, rules, and facility policies with versioning. | P5 | FRS-BB-070 |
| URS-BB-013 | Interfaces preserve raw messages and do not silently discard errors. | P8 | FRS-BB-080 |
| URS-BB-014 | The facility can operate and recover from downtime without losing traceability. | P5 | FRS-BB-090 |
| URS-BB-015 | A merged (losing) patient record cannot be used for testing, allocation, or issue. Work continues on the surviving record. | P1 | FRS-BB-003 |
| URS-BB-017 | Authorized staff can merge a duplicate patient into the surviving record without deleting history. | P1 | FRS-BB-003 |
| URS-BB-016 | A result cannot be verified from an expired or rejected specimen. | P0 | FRS-BB-010 |
| URS-BB-018 | A unit that requires ABO/Rh retype is not Available until a verified matching retype exists. | P0 | FRS-BB-051 |
| URS-BB-019 | Manually setting the current ABO/Rh (not from a verified result) is limited to authorized staff. | P0 | FRS-BB-021 |
| URS-BB-020 | The user who entered a patient ABO/Rh cannot verify it. Current type is established only after a second user verifies. | P0 | FRS-BB-022 |
| URS-BB-021 | Changing special transfusion requirements (irradiated, CMV-neg, antigen-negative, …) is limited to authorized immuno staff. | P0 | FRS-BB-023 |
| URS-BB-022 | A donor lookback recall must actually recall in-date components, including reserved or crossmatched units, or fail closed. | P0 | FRS-BB-024 |
| URS-BB-023 | Releasing a unit from quality quarantine to Available is limited to staff with inventory.release. | P0 | FRS-BB-052 |
| URS-BB-024 | Converting an unused directed unit to allogeneic inventory is limited to staff with inventory.release. | P0 | FRS-BB-053 |
| URS-BB-025 | Releasing a unit from operational hold to Available is limited to staff with inventory.release. | P0 | FRS-BB-054 |
| URS-BB-026 | Verifying a test result or a unit ABO/Rh retype is limited to staff with result.verify. | P0 | FRS-BB-055 |
| URS-BB-027 | Allocating a unit to a patient is limited to staff with compatibility.allocate. Recording a crossmatch is limited to staff with compatibility.crossmatch. | P0 | FRS-BB-056 |
| URS-BB-028 | Issuing a unit is limited to staff with issue.create. Emergency/MTP still also requires issue.emergency-release. | P0 | FRS-BB-057 |
| URS-BB-029 | Entering a result or unit ABO/Rh retype is limited to staff with result.enter. Correcting a verified result is limited to staff with result.correct. | P0 | FRS-BB-058 |
