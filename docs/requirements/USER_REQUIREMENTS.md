# User requirements

IDs are stable. Citations support validation evidence. They are **not** a claim
that this software is AABB-accredited or FDA-cleared.

| ID | Need | Priority | Related FRS |
|---|---|---|---|
| URS-BB-001 | Staff can identify a patient by enterprise identifiers (MRN and a second identifier), not by display name alone. | P1 | FRS-BB-001 |
| URS-BB-002 | Staff can see current ABO/Rh, historical types, antibodies (including currently undetectable), and special requirements on the patient record. | P0 | FRS-BB-002 |
| URS-BB-003 | Staff can accession a specimen with collection metadata and computed validity. | P1 | FRS-BB-010 |
| URS-BB-004 | Staff can enter, verify, and amend immunohematology results without destroying prior released values. | P0 | FRS-BB-020 |
| URS-BB-047 | Staff can distinguish manual, instrument, interface, and calculated result sources, and instrument/interface values wait for verification. | P3 | FRS-BB-079 |
| URS-BB-046 | Staff can invalidate a result with a reason without destroying the original row. | P0 | FRS-BB-077 |
| URS-BB-005 | Staff can evaluate compatibility with an explainable rule outcome (rule ID + reason). | P0 | FRS-BB-030 |
| URS-BB-006 | Staff can reserve, crossmatch, and issue a unit only when safety checks pass, or via a controlled emergency-release path. | P0 | FRS-BB-031, FRS-BB-040 |
| URS-BB-007 | Two users cannot reserve or issue the same unit. | P0 | FRS-BB-041 |
| URS-BB-008 | Autologous and directed units can be issued only to the designated recipient. | P0 | FRS-BB-032 |
| URS-BB-009 | Electronic (computer) XM is available only when configured prerequisites hold. | P0 | FRS-BB-033 |
| URS-BB-010 | Inventory states and modifications remain historically traceable. | P3 | FRS-BB-050 |
| URS-BB-011 | Clinical and configuration changes are auditable (who/what/when/where/old/new/why). | P4 | FRS-BB-060, FRS-BB-078 |
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
| URS-BB-030 | Accessioning a specimen is limited to staff with specimen.accession. Editing collection metadata requires specimen.edit. Rejecting a specimen requires specimen.reject. | P1 | FRS-BB-059 |
| URS-BB-031 | Updating patient name, date of birth, sex, status, or pregnancy history is limited to staff with patient.write. | P1 | FRS-BB-061 |
| URS-BB-032 | Dividing, pooling, or applying a product modification is limited to staff with inventory.modify. | P0 | FRS-BB-062 |
| URS-BB-033 | Correcting a unit DIN, product code, ABO/Rh code, or related ISBT identity field is limited to staff with inventory.correct-identity. | P1 | FRS-BB-063 |
| URS-BB-034 | Releasing a reserved unit back to Available is limited to staff with compatibility.allocate. | P0 | FRS-BB-064 |
| URS-BB-035 | Receiving an ISBT unit by scan-session complete or manual entry is limited to staff with inventory.receive. | P0 | FRS-BB-065 |
| URS-BB-036 | Updating a transfusion-reaction investigation, including fatality CBER timestamps, is limited to staff with reaction.investigate. | P0 | FRS-BB-066 |
| URS-BB-037 | Receiving a unit by walk-in, expected-arrival confirmation, or normalized component intake is limited to staff with inventory.receive. | P0 | FRS-BB-067 |
| URS-BB-038 | Returning an issued unit to inventory is limited to staff with issue.return. | P0 | FRS-BB-068 |
| URS-BB-039 | Documenting a transfusion is limited to staff with transfusion.document. | P0 | FRS-BB-069 |
| URS-BB-040 | Recording ward receipt of an issued unit is limited to staff with transfusion.document. | P0 | FRS-BB-071 |
| URS-BB-041 | Discarding a unit is limited to staff with inventory.discard. | P0 | FRS-BB-072 |
| URS-BB-042 | Transferring a unit between storage locations is limited to staff with inventory.transfer. | P1 | FRS-BB-073 |
| URS-BB-043 | Recalling a unit from inventory (not via lookback DIN recall) is limited to staff with inventory.recall. | P0 | FRS-BB-074 |
| URS-BB-044 | Creating or closing a quality-system deviation is limited to staff with deviation.manage. | P4 | FRS-BB-075 |
| URS-BB-045 | Creating a patient record is limited to staff with patient.write. | P1 | FRS-BB-076 |
| URS-BB-046 | Saving a unit antigen or antibody attribute used at compatibility is limited to staff with inventory.receive. | P2 | FRS-BB-077 |
| URS-BB-048 | Returning a unit to the supplier is limited to staff with inventory.receive. | P3 | FRS-BB-081 |
| URS-BB-050 | Locating a missing unit or inspecting a damaged unit into quality quarantine is limited to staff with inventory.release. | P3 | FRS-BB-083 |
| URS-BB-051 | Recording or cancelling an expected inbound packing-list unit is limited to staff with inventory.receive. | P3 | FRS-BB-084 |
| URS-BB-053 | Recording a lookback recipient-notification attempt is limited to staff with lookback.manage. | P4 | FRS-BB-086 |
| URS-BB-055 | Printing a specimen, compatibility, or component label is limited to staff with print.label. Reprinting a stored job is limited to staff with print.reprint. | P1 | FRS-BB-088 |
| URS-BB-057 | Updating or cancelling an order, or linking a specimen to an order, is limited to staff with patient.write. | P1 | FRS-BB-091 |
| URS-BB-058 | Creating or updating a visit from the workspace is limited to staff with patient.write. | P1 | FRS-BB-092 |
| URS-BB-060 | Placing a unit in quarantine or on hold, or marking it missing or damaged, is limited to staff with inventory.release. | P3 | FRS-BB-094 |
| URS-BB-062 | Running the inventory expiration sweep is limited to staff with inventory.discard. | P3 | FRS-BB-096 |
| URS-BB-064 | Merging a duplicate patient into a survivor from the workspace is limited to staff with patient.merge. | P1 | FRS-BB-098 |
| URS-BB-066 | Creating an order from the workspace is limited to staff with patient.write. | P1 | FRS-BB-100 |
| URS-BB-068 | Creating or updating a directory user, assigning roles, or creating or updating a role catalog entry is limited to staff with admin.users.manage or admin.roles.manage. | P6 | FRS-BB-102 |
