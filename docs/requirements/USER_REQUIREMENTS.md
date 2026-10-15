# User requirements

IDs are stable. Citations support validation evidence. They are **not** a claim
that this software is AABB-accredited or FDA-cleared.

| ID | Need | Priority | Related FRS |
|---|---|---|---|
| URS-BB-001 | Staff can identify a patient by enterprise identifiers (MRN and a second identifier), not by display name alone. | P1 | FRS-BB-001 |
| URS-BB-002 | Staff can see current ABO/Rh, historical types, antibodies (including currently undetectable), and special requirements on the patient record. | P0 | FRS-BB-002 |
| URS-BB-003 | Staff can accession a specimen with collection metadata and computed validity. | P1 | FRS-BB-010 |
| URS-BB-004 | Staff can enter, verify, and amend immunohematology results without destroying prior released values. | P0 | FRS-BB-020, FRS-BB-099, FRS-BB-101 |
| URS-BB-047 | Staff can distinguish manual, instrument, interface, and calculated result sources, and instrument/interface values wait for verification. | P3 | FRS-BB-079, FRS-BB-089, FRS-BB-124, FRS-BB-158, FRS-BB-159 |
| URS-BB-046 | Staff can invalidate a result with a reason without destroying the original row. | P0 | FRS-BB-077 |
| URS-BB-005 | Staff can evaluate compatibility with an explainable rule outcome (rule ID + reason). | P0 | FRS-BB-030 |
| URS-BB-006 | Staff can reserve, crossmatch, and issue a unit only when safety checks pass, or via a controlled emergency-release path. | P0 | FRS-BB-031, FRS-BB-040 |
| URS-BB-007 | Two users cannot reserve or issue the same unit. | P0 | FRS-BB-041 |
| URS-BB-008 | Autologous and directed units can be issued only to the designated recipient. | P0 | FRS-BB-032 |
| URS-BB-009 | Electronic (computer) XM is available only when configured prerequisites hold. | P0 | FRS-BB-033, FRS-BB-183 |
| URS-BB-010 | Inventory states and modifications remain historically traceable. | P3 | FRS-BB-050 |
| URS-BB-011 | Clinical and configuration changes are auditable (who/what/when/where/old/new/why). | P4 | FRS-BB-060, FRS-BB-078, FRS-BB-085, FRS-BB-087, FRS-BB-089, FRS-BB-093, FRS-BB-095, FRS-BB-104, FRS-BB-106, FRS-BB-109, FRS-BB-111, FRS-BB-114, FRS-BB-116, FRS-BB-119, FRS-BB-121, FRS-BB-126, FRS-BB-129, FRS-BB-131, FRS-BB-134, FRS-BB-136, FRS-BB-139, FRS-BB-141, FRS-BB-144, FRS-BB-146, FRS-BB-148, FRS-BB-149, FRS-BB-150, FRS-BB-151, FRS-BB-152, FRS-BB-153, FRS-BB-154, FRS-BB-155, FRS-BB-156, FRS-BB-157, FRS-BB-160, FRS-BB-161, FRS-BB-162, FRS-BB-163, FRS-BB-165, FRS-BB-166, FRS-BB-167, FRS-BB-168, FRS-BB-172, FRS-BB-173, FRS-BB-174, FRS-BB-175, FRS-BB-176, FRS-BB-177, FRS-BB-178, FRS-BB-179, FRS-BB-180, FRS-BB-181, FRS-BB-182 |
| URS-BB-012 | Administrators can configure tests, products, rules, and facility policies with versioning. | P5 | FRS-BB-070 |
| URS-BB-013 | Interfaces preserve raw messages and do not silently discard errors. | P8 | FRS-BB-080 |
| URS-BB-014 | The facility can operate and recover from downtime without losing traceability. | P5 | FRS-BB-090, FRS-BB-190 |
| URS-BB-015 | A merged (losing) patient record cannot be used for testing, allocation, or issue. Work continues on the surviving record. | P1 | FRS-BB-003 |
| URS-BB-017 | Authorized staff can merge a duplicate patient into the surviving record without deleting history. | P1 | FRS-BB-003 |
| URS-BB-016 | A result cannot be verified from an expired or rejected specimen. | P0 | FRS-BB-010 |
| URS-BB-018 | A unit that requires ABO/Rh retype is not Available until a matching retype is recorded. The retype is the second confirmation of the supplier type; a second reviewer is not required. | P0 | FRS-BB-051, FRS-BB-106, FRS-BB-126 |
| URS-BB-019 | Manually setting the current ABO/Rh (not from a verified result) is limited to authorized staff. | P0 | FRS-BB-021 |
| URS-BB-020 | The user who entered a patient ABO/Rh cannot verify it. Current type is established only after a second user verifies. | P0 | FRS-BB-022 |
| URS-BB-021 | Changing special transfusion requirements (irradiated, CMV-neg, antigen-negative, …) is limited to authorized immuno staff. | P0 | FRS-BB-023 |
| URS-BB-022 | A donor lookback recall must actually recall in-date components, including reserved or crossmatched units, or fail closed. | P0 | FRS-BB-024, FRS-BB-165 |
| URS-BB-023 | Releasing a unit from quality quarantine to Available is limited to staff with inventory.release. | P0 | FRS-BB-052 |
| URS-BB-024 | Converting an unused directed unit to allogeneic inventory is limited to staff with inventory.release. | P0 | FRS-BB-053, FRS-BB-136 |
| URS-BB-025 | Releasing a unit from operational hold to Available is limited to staff with inventory.release. | P0 | FRS-BB-054 |
| URS-BB-026 | Verifying a test result is limited to staff with result.verify. Recording a unit ABO/Rh retype is limited to staff with result.enter and confirms the unit without a second reviewer. | P0 | FRS-BB-055 |
| URS-BB-027 | Allocating a unit to a patient is limited to staff with compatibility.allocate. Recording a crossmatch is limited to staff with compatibility.crossmatch. | P0 | FRS-BB-056 |
| URS-BB-028 | Issuing a unit is limited to staff with issue.create. Emergency/MTP still also requires issue.emergency-release. | P0 | FRS-BB-057 |
| URS-BB-029 | Entering a result or unit ABO/Rh retype is limited to staff with result.enter. Correcting a verified result is limited to staff with result.correct. | P0 | FRS-BB-058 |
| URS-BB-030 | Accessioning a specimen is limited to staff with specimen.accession. Editing collection metadata requires specimen.edit. Rejecting a specimen requires specimen.reject. | P1 | FRS-BB-059 |
| URS-BB-031 | Updating patient name, date of birth, sex, status, or pregnancy history is limited to staff with patient.write. | P1 | FRS-BB-061, FRS-BB-153 |
| URS-BB-032 | Dividing, pooling, or applying a product modification is limited to staff with inventory.modify. | P0 | FRS-BB-062 |
| URS-BB-033 | Correcting a unit DIN, product code, ABO/Rh code, or related ISBT identity field is limited to staff with inventory.correct-identity. | P1 | FRS-BB-063 |
| URS-BB-034 | Releasing a reserved unit back to Available is limited to staff with compatibility.allocate. | P0 | FRS-BB-064 |
| URS-BB-035 | Receiving an ISBT unit by scan-session complete or manual entry is limited to staff with inventory.receive. | P0 | FRS-BB-065 |
| URS-BB-036 | Updating a transfusion-reaction investigation, including fatality CBER timestamps, is limited to staff with reaction.investigate. | P0 | FRS-BB-066, FRS-BB-163 |
| URS-BB-037 | Receiving a unit by walk-in, expected-arrival confirmation, or normalized component intake is limited to staff with inventory.receive. | P0 | FRS-BB-067 |
| URS-BB-038 | Returning an issued unit to inventory is limited to staff with issue.return. | P0 | FRS-BB-068 |
| URS-BB-039 | Documenting a transfusion is limited to staff with transfusion.document. | P0 | FRS-BB-069 |
| URS-BB-040 | Recording ward receipt of an issued unit is limited to staff with transfusion.document. | P0 | FRS-BB-071, FRS-BB-134 |
| URS-BB-041 | Discarding a unit is limited to staff with inventory.discard. | P0 | FRS-BB-072 |
| URS-BB-042 | Transferring a unit between storage locations is limited to staff with inventory.transfer. | P1 | FRS-BB-073 |
| URS-BB-043 | Recalling a unit from inventory (not via lookback DIN recall) is limited to staff with inventory.recall. | P0 | FRS-BB-074 |
| URS-BB-044 | Creating or closing a quality-system deviation is limited to staff with deviation.manage. | P4 | FRS-BB-075, FRS-BB-162 |
| URS-BB-045 | Creating a patient record is limited to staff with patient.write. | P1 | FRS-BB-076, FRS-BB-156 |
| URS-BB-046 | Saving a unit antigen or antibody attribute used at compatibility is limited to staff with inventory.receive. | P2 | FRS-BB-077 |
| URS-BB-048 | Returning a unit to the supplier is limited to staff with inventory.receive. | P3 | FRS-BB-081 |
| URS-BB-049 | Staff can receive instrument/LIS results by inbound ORU without those values becoming verified or overwriting a released result. | P0 | FRS-BB-082 |
| URS-BB-050 | Locating a missing unit or inspecting a damaged unit into quality quarantine is limited to staff with inventory.release. | P3 | FRS-BB-083 |
| URS-BB-051 | Recording or cancelling an expected inbound packing-list unit is limited to staff with inventory.receive. | P3 | FRS-BB-084 |
| URS-BB-052 | Staff can filter the audit trail by named clinical event (specimen, order, assignment, crossmatch, emergency release, transfusion, user/role), not only generic Create/Update. | P4 | FRS-BB-085 |
| URS-BB-053 | Recording a lookback recipient-notification attempt is limited to staff with lookback.manage. | P4 | FRS-BB-086, FRS-BB-165 |
| URS-BB-054 | Staff can search the audit trail by named event type, user, entity, and time window, and can see the recorded old and new values. | P4 | FRS-BB-087 |
| URS-BB-055 | Printing a specimen, compatibility, or component label is limited to staff with print.label. Reprinting a stored job is limited to staff with print.reprint. | P1 | FRS-BB-088, FRS-BB-160 |
| URS-BB-056 | Staff can see when a result was calculated from panel or catalog interpretation logic, and can find test-catalog create/update/clone as TestChange rather than generic Create/Update. | P3 | FRS-BB-089, FRS-BB-158 |
| URS-BB-057 | Updating or cancelling an order, or linking a specimen to an order, is limited to staff with patient.write. | P1 | FRS-BB-091 |
| URS-BB-058 | Creating or updating a visit from the workspace is limited to staff with patient.write. | P1 | FRS-BB-092, FRS-BB-157 |
| URS-BB-059 | Staff can find manual ABO/Rh overrides and antibody add/deactivate as named Override and Antibody audit events, not only generic Update. | P0 | FRS-BB-093 |
| URS-BB-060 | Placing a unit in quarantine or on hold, or marking it missing or damaged, is limited to staff with inventory.release. | P3 | FRS-BB-094 |
| URS-BB-061 | Staff can find antigen-phenotype changes and result-triggered reflex order lines as named Antibody and OrderChange events, including the prior phenotype when a profile is updated. | P0 | FRS-BB-095 |
| URS-BB-062 | Running the inventory expiration sweep is limited to staff with inventory.discard. | P3 | FRS-BB-096 |
| URS-BB-063 | Staff can record antibody-identification panel reactions, review assistance, interpret, and obtain supervisor review before identified antibodies post to history. Assistance must not silently replace technologist judgment. | P0 | FRS-BB-097, FRS-BB-161 |
| URS-BB-064 | Merging a duplicate patient into a survivor from the workspace is limited to staff with patient.merge. | P1 | FRS-BB-098 |
| URS-BB-065 | Staff can see retained prior result versions (original value, status, source, reason, who, when) on the patient result panel, and an invalidated correction is not shown as the current result. | P0 | FRS-BB-099 |
| URS-BB-066 | Creating an order from the workspace is limited to staff with patient.write. | P1 | FRS-BB-100 |
| URS-BB-067 | Staff can see retained prior released result versions on the patient Test History tab, not only the current verified row. | P0 | FRS-BB-101 |
| URS-BB-068 | Creating or updating a directory user, assigning roles, or creating or updating a role catalog entry is limited to staff with admin.users.manage or admin.roles.manage. | P6 | FRS-BB-102, FRS-BB-139 |
| URS-BB-069 | Documenting a transfusion through the interface service (not inbound BPAM) is limited to staff with transfusion.document. | P0 | FRS-BB-103 |
| URS-BB-070 | Staff can find create/update of subtest, blood-attribute, and reflex-rule catalogs as TestChange, because those definitions change calculated results, antibody/antigen history, and reflex-added tests. | P3 | FRS-BB-104 |
| URS-BB-071 | Starting an ISBT scan session or adding a scan line is limited to staff with inventory.receive. | P3 | FRS-BB-105 |
| URS-BB-072 | Staff can find unit ABO/Rh retype record and the resulting Available/Quarantine change as named Result, Verify, and ProductStatus events from the same action. | P0 | FRS-BB-106 |
| URS-BB-073 | Activating, deactivating, locking, unlocking, or requesting a password reset for a directory user is limited to staff with admin.users.manage. | P6 | FRS-BB-107 |
| URS-BB-074 | Creating, updating, activating, deactivating, or cloning a test definition is limited to staff with admin.tests.manage. | P2 | FRS-BB-108 |
| URS-BB-075 | Staff can find exception-definition create/update as Configure (override eligibility at result verify) and phase-definition create/update as TestChange (result-entry phases). | P3 | FRS-BB-109 |
| URS-BB-076 | Creating or updating a blood-attribute definition is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-110 |
| URS-BB-077 | Staff can find order/test rule-definition create/update as Configure and test-grouper create/update as TestChange. | P3 | FRS-BB-111 |
| URS-BB-078 | Creating or updating a reflex rule is limited to staff with admin.tests.manage. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-112 |
| URS-BB-079 | Creating or updating a subtest definition is limited to staff with admin.tests.manage. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-113 |
| URS-BB-080 | Staff can find specimen-type create/update as TestChange, because excluded tests change which results can be entered on that specimen. | P3 | FRS-BB-114 |
| URS-BB-081 | Creating or updating a test grouper is limited to staff with admin.tests.manage. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-115 |
| URS-BB-082 | Staff can find HL7 endpoint create/update as Interface, including the inbound results (ORU) endpoint that feeds result entry. | P3 | FRS-BB-116 |
| URS-BB-083 | Creating or updating an order or test rule is limited to staff with admin.tests.manage. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-117 |
| URS-BB-084 | Creating or updating a phase definition is limited to staff with admin.tests.manage. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-118 |
| URS-BB-085 | Staff can find compatibility-table version and rule create/update as Configure, because those rules use verified results at issue. | P3 | FRS-BB-119 |
| URS-BB-086 | Creating or updating an exception definition is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-120 |
| URS-BB-087 | Staff can find special-requirement add as Antibody (with entity id, old/new, reason) and deactivate as Deactivate. | P1 | FRS-BB-121 |
| URS-BB-088 | Creating or updating a specimen-type definition is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-122 |
| URS-BB-089 | Creating or updating a product definition is limited to staff with admin.products.manage. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-123 |
| URS-BB-090 | Staff can see the current result source on the test worklist and the patient Tests tab, including instrument values waiting for verification. | P3 | FRS-BB-124, FRS-BB-158, FRS-BB-159 |
| URS-BB-091 | Creating or updating a modification rule is limited to staff with admin.modification-rules.manage. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-125, FRS-BB-131 |
| URS-BB-092 | Staff can find product-definition create/update as Configure, because those flags decide whether a unit needs ABO/Rh retype or crossmatch before results release it. | P3 | FRS-BB-126 |
| URS-BB-093 | Creating or updating an expiration modification code is limited to staff with admin.modification-rules.manage. Activating or deactivating one is limited to staff with admin.config.activate. | P2 | FRS-BB-127, FRS-BB-129 |
| URS-BB-094 | Creating, updating, enabling, or disabling an HL7 endpoint is limited to staff with admin.hl7.manage. | P3 | FRS-BB-128 |
| URS-BB-095 | Staff can find expiration-modification-code create/update as Configure, because those offsets decide how long a modified unit remains usable. | P3 | FRS-BB-129 |
| URS-BB-096 | Replacing HL7 value translations is limited to staff with admin.hl7.manage. | P3 | FRS-BB-130, FRS-BB-155 |
| URS-BB-097 | Staff can find modification-rule create/update as Configure, because those paths decide which source product, modification type, target product, and expiration offset are allowed. | P3 | FRS-BB-131 |
| URS-BB-098 | Creating or updating a charge code is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P7 | FRS-BB-132, FRS-BB-146 |
| URS-BB-099 | Creating or updating a charge rule is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P7 | FRS-BB-133, FRS-BB-148 |
| URS-BB-100 | Staff can find ward receipt of an issued unit as Transfusion, because that acknowledgment unblocks transfusion documentation. | P1 | FRS-BB-134 |
| URS-BB-101 | Creating or updating a product billing row is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P7 | FRS-BB-135, FRS-BB-149 |
| URS-BB-102 | Staff can find directed-to-allogeneic conversion as ProductStatus, because that change clears the reserved patient so the unit can be issued to someone else. | P1 | FRS-BB-136 |
| URS-BB-103 | Creating or updating a test/service billing row is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P7 | FRS-BB-137, FRS-BB-150 |
| URS-BB-104 | Creating or updating an ordering provider is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P7 | FRS-BB-138, FRS-BB-151 |
| URS-BB-105 | Staff can find directory user create/update and role create as UserRole, because those changes decide who can enter, verify, or issue. | P3 | FRS-BB-139 |
| URS-BB-106 | Creating or updating an ordering location is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P7 | FRS-BB-140, FRS-BB-152 |
| URS-BB-107 | Staff can find antibody panel-lot create as TestChange, because those cells and antigens decide what an identification workup can result. | P3 | FRS-BB-141 |
| URS-BB-108 | Reviewing a captured charge is limited to staff with billing.review. Cancelling is limited to staff with billing.cancel. Exporting is limited to staff with billing.export. | P7 | FRS-BB-142, FRS-BB-154 |
| URS-BB-109 | Creating or updating an inventory location is limited to staff with admin.config.edit. Activating or deactivating one is limited to staff with admin.config.activate. | P7 | FRS-BB-143, FRS-BB-144 |
| URS-BB-110 | Staff can find inventory-location create/update as Configure, because those flags decide where a unit can be stored or issued. | P3 | FRS-BB-144 |
| URS-BB-111 | Updating a facility policy is limited to staff with admin.config.edit. | P2 | FRS-BB-145 |
| URS-BB-112 | Staff can find charge-code create/update as Configure, because those codes bill after a verified result or issue. | P3 | FRS-BB-146 |
| URS-BB-113 | Finding units by DIN or tracing a recipient on lookback is limited to staff with lookback.manage. | P1 | FRS-BB-147 |
| URS-BB-114 | Staff can find charge-rule create/update as Configure, because those triggers decide which codes bill after a verified result or issue. | P3 | FRS-BB-148 |
| URS-BB-115 | Staff can find product-billing create/update as Configure, because those rows decide which ISBT product bills after issue. | P3 | FRS-BB-149 |
| URS-BB-116 | Staff can find test/service-billing create/update as Configure, because those rows decide which verified test bills after release. | P3 | FRS-BB-150 |
| URS-BB-117 | Staff can find ordering-provider create/update as Configure, because those names appear on orders that later have results. | P3 | FRS-BB-151 |
| URS-BB-118 | Staff can find ordering-location create/update as Configure, because those wards can be named on an order. | P3 | FRS-BB-152 |
| URS-BB-119 | Staff can find patient demographic update as PatientAccess, because name, date of birth, sex, status, and pregnancy change who results attach to and which specimen window applies. Chart-open stays PatientAccess with a different reason. | P1 | FRS-BB-153 |
| URS-BB-120 | Staff can find charge review and cancel as Billing, and charge export as Export, because those actions follow a verified result or issue. Capture stays interceptor Create. | P3 | FRS-BB-154 |
| URS-BB-121 | Staff can find HL7 value-translation replace as Interface, because those maps decide how inbound result values become internal codes. | P3 | FRS-BB-155 |
| URS-BB-122 | Staff can find patient create as PatientAccess, because that identity is what later results attach to. Chart-open and demographic update stay PatientAccess with different reasons. ADT inbound still bypasses PatientService. | P1 | FRS-BB-156 |
| URS-BB-123 | Staff can find workspace visit create/update as PatientAccess, because those visits are the context for orders and specimens that later have results. ADT visit upsert stays ungated and interceptor-only. | P1 | FRS-BB-157 |
| URS-BB-124 | Staff can see on the worklist and specimen result forms when panel entry will be stored as Calculated rather than a typed Manual ABO/Rh. | P3 | FRS-BB-158 |
| URS-BB-125 | Staff can see on the specimen typed-entry form that a test-code and value without a panel are stored as Manual. | P3 | FRS-BB-159 |
| URS-BB-126 | Staff can find first specimen, compatibility, and component label prints as Print, because those labels identify the specimen or unit used at result entry and issue. Reprint stays Reprint. | P3 | FRS-BB-160 |
| URS-BB-127 | Staff can find antibody-identification workup open, link, lot-attach, assist, interpret, void, and stale-panel as Antibody, not mixed with test Result entry. Supervisor accept and complete stay Verify. Posted Identified findings stay Antibody on history. | P0 | FRS-BB-161 |
| URS-BB-128 | Staff can find deviation create and status update as Deviation with the row id, because a result-context deviation must be findable apart from interceptor Create/Update. | P4 | FRS-BB-162 |
| URS-BB-129 | Staff can find reaction-investigation open, workup update (repeat ABO/Rh, DAT, elution), CBER notification, and written-report timestamps as ReactionInvestigation with the row id and old/new. Opening from a suspected transfusion stays automatic. | P0 | FRS-BB-163 |
| URS-BB-130 | Staff can see the stored result source after verify, ABO/Rh-override verify, correct, submit-for-verification, and invalidate on the specimen card, not only after enter/save. | P3 | FRS-BB-164 |
| URS-BB-131 | Staff can find DIN lookback recall as Lookback on a unit id, and recipient-notification attempts as Lookback on the notification row with old/new, because those actions follow issued/transfused units that had results. Search and traceback stay Lookback. | P1 | FRS-BB-165 |
| URS-BB-132 | Staff can see on `/audit` whether a submitted-for-verification or invalidated result was Manual, Instrument, Interface, or Calculated. Verify and correct already stored source. | P0 | FRS-BB-166 |
| URS-BB-133 | Staff can see on `/audit` the stored source on verify old/new and on re-entry after invalidation old/new, including when the replacement arrives from Interface. | P0 | FRS-BB-167 |
| URS-BB-134 | Staff can see on `/audit` the interpreted ABO/Rh of a unit retype on record (Result and Verify), because that type is used at later compatibility and issue. | P0 | FRS-BB-168 |
| URS-BB-135 | Staff can see the interpreted ABO/Rh after recording a unit retype, not only a generic saved message. | P3 | FRS-BB-169 |
| URS-BB-136 | Staff can see the crossmatch method and Compatible or Incompatible result after recording a crossmatch, not only the row id. | P3 | FRS-BB-170 |
| URS-BB-137 | Staff can see the crossmatch method and Compatible or Incompatible result after saving a worklist XM, not only Manual/Calculated source. | P3 | FRS-BB-171 |
| URS-BB-138 | Staff can see on `/audit` the identity-correction row id on a unit DIN, product-code, or ABO/Rh identity Correct event, not only the field old/new. | P1 | FRS-BB-172 |
| URS-BB-139 | Staff can see on `/audit` the antibody-history row id when a verified ABID result posts Identified antibodies, not the patient id as the AntibodyHistory entity id. | P0 | FRS-BB-173 |
| URS-BB-140 | Staff can see on `/audit` the antibody-history row id when a completed reviewed workup posts Identified antibodies, not the patient id as the AntibodyHistory entity id. | P0 | FRS-BB-174 |
| URS-BB-141 | Staff can find a verified-result test-rule match (add/cancel/warn) as OrderChange on the order, not as catalog Configure. | P3 | FRS-BB-175 |
| URS-BB-142 | Staff can find an order-level rule match at create/update as OrderChange on the order, not only the execution log. | P3 | FRS-BB-176 |
| URS-BB-143 | Staff can see on `/audit` the added order-line id when a catalog reflex fires from a verified result, not the order id as the OrderLine entity id. | P0 | FRS-BB-177 |
| URS-BB-144 | Staff can see on `/audit` the unit number and DIN on an allocation Assignment event, not only allocation id and status. | P1 | FRS-BB-178 |
| URS-BB-145 | Staff can see on `/audit` the unit number and DIN on a ward-receipt Transfusion event, not only the issue id and receiver. | P1 | FRS-BB-179 |
| URS-BB-146 | Staff can see on `/audit` the unit number and DIN on a standard or emergency Issue event, not only the unit id and issue type. | P1 | FRS-BB-180 |
| URS-BB-147 | Privileged actions (emergency release, override, patient merge, result correction) cannot be performed by sending another user's name in a request header. Interactive callers must present a server-issued session. | P0 | FRS-BB-181 |
| URS-BB-148 | After a directory role or role-permission change, the operator must sign in again before the UI treats the old privilege set as current. | P6 | FRS-BB-182 |
| URS-BB-149 | An electronic crossmatch cannot be recorded after another operator posts antibody history, even if the eligibility board was still green when the first operator opened the form. | P0 | FRS-BB-183 |
| URS-BB-150 | An investigator can detect that an audit row was altered or removed after it was written. The system does not purge audit or clinical records. | P4 | FRS-BB-184 |
| URS-BB-151 | Documenting a transfusion requires the same two independent patient identifiers as issue. A checkbox or self-asserted "positive patient identification" is not accepted. | P1 | FRS-BB-185 |
| URS-BB-152 | An administrator can replace placeholder ISBT product and ABO/RhD lookup rows with a facility-supplied licensed extract. The system does not invent ICCBBA codes. | P1 | FRS-BB-186 |
| URS-BB-153 | Interactive HTTP HL7 inbound is limited to signed-in staff with hl7.manage. MLLP and file-drop inbound trust the network or drop-folder ACLs; the system does not invent an interface secret. | P6 | FRS-BB-187 |
| URS-BB-154 | Browser clients cannot frame the application, sniff MIME types, or use a Production wildcard CORS policy. Session tokens stay off cookies. | P6 | FRS-BB-188 |
| URS-BB-155 | Validation evidence for recent safety slices can be cited by a stable TEST-BB-* identifier. Living design docs are not labeled as an unimplemented Phase 0 draft. | P5 | FRS-BB-189 |
| URS-BB-156 | After downtime, authorized staff can see a read-only snapshot of unresolved interface errors, pending outbound HL7, open issues, pending retrospective crossmatches, and audit-chain status. The system does not invent paper OCR or multi-site failover. | P5 | FRS-BB-190 |
| URS-BB-157 | Core issue-gate, merged-patient, electronic-XM, and lookback evidence can be cited by a stable TEST-BB-* identifier, and those catalog rows still point at source files. | P5 | FRS-BB-191 |
| URS-BB-158 | Uniqueness, merge, unit-retype, emergency-issue privilege, and immuno-privilege evidence can be cited by a stable TEST-BB-* identifier. | P5 | FRS-BB-192 |
| URS-BB-159 | When a facility has exported ICCBBA extract files, an administrator can import them from a drop folder or by upload. The system does not invent ICCBBA codes and does not parse licensed Access or Excel databases. | P1 | FRS-BB-193 |
| URS-BB-160 | Administrators can define default crossmatch tests for negative and positive antibody history, and the minimum negative-screen visits, specimens, and tests required for electronic XM. Those tests attach to product orders after the current antibody screen; electronic XM is auto-added and resulted when an eligible patient with a current type and screen has units selected. Downgrading complex to simple XM requires a permanent exception. | P1 | FRS-BB-194 |
| URS-BB-161 | Technologists can see current ABO/Rh, antibody history, and specimen expiration on the pending test worklist and identify the compatibility-testing patient by MRN and name rather than an internal id. | P1 | FRS-BB-195 |
| URS-BB-162 | Technologists can confirm expected inbound arrival from the packing-list worklist using the same visual, temperature, and second-verifier checks as walk-in receive, without opening the unit Manage drawer first. | P1 | FRS-BB-196 |
| URS-BB-163 | Technologists can identify in-transit and retrospective-crossmatch units by unit number, see current ABO/Rh, antibody history, and specimen expiration on those boards, receive a cooler unit from the in-transit row, and select the issue recipient by MRN and name rather than an internal id. | P1 | FRS-BB-197 |
| URS-BB-164 | Technologists can identify a transfusion-reaction investigation by patient name, MRN, and unit number, see current ABO/Rh, antibody history, workup completeness, and remainder-hold status on the board, and open the workup from a suspected-transfusion document or the patient product history without searching raw ids. | P1 | FRS-BB-198 |
| URS-BB-165 | Interface staff can identify an unresolved HL7 mapping error by message control id and type, replay that message from the error queue, and see the work item clear when replay is accepted. | P1 | FRS-BB-199 |
| URS-BB-166 | After a good inbound ORU posts to an accepted specimen, technologists can verify the interface value from the test worklist without re-keying it. | P1 | FRS-BB-200 |
| URS-BB-167 | After a verified test, issued unit, or completed transfusion, billing staff can identify the pending charge by patient name, MRN, test or unit, and queued DFT control id without a second manual capture. | P1 | FRS-BB-201 |
| URS-BB-168 | Technologists can see electronic-crossmatch eligibility and an open antibody-identification workup on the pending test worklist without opening the patient chart. | P2 | FRS-BB-202 |
| URS-BB-169 | Technologists can identify a quality-quarantine unit by product and see a labeled-versus-interpreted ABO/Rh retype mismatch on the quarantine board, then release from that row with the existing second-verifier gate. | P1 | FRS-BB-203 |
| URS-BB-170 | Technologists can identify an outstanding issued unit by unit number and patient after ward receipt, then return or document transfusion from that row instead of a raw issue id. | P1 | FRS-BB-204 |
| URS-BB-171 | When a transfusion reaction is suspected, technologists can see the labeled unit ABO/Rh for the clerical check and the remainder is held in quality quarantine even if the transfusion completed. | P1 | FRS-BB-205 |
| URS-BB-172 | Interface staff can identify an HL7 mapping error or message-log row by patient MRN and name without opening the raw payload. | P1 | FRS-BB-206 |
| URS-BB-173 | After a good inbound RAS documents an issued unit, technologists can see that transfusion on the patient product history without re-keying it on Issuing. | P1 | FRS-BB-207 |
| URS-BB-174 | After a charge is captured, billing staff can review it and then export it from the same queue, including a transfusion documented by inbound RAS, without a second capture. | P1 | FRS-BB-208 |
| URS-BB-175 | Technologists can see a posted interface or instrument value on the pending test worklist and the patient Tests tab, and Verify that row without re-keying. The ABO enterer is not offered self-verify. | P1 | FRS-BB-209 |
| URS-BB-176 | Technologists can identify a missing, damaged, or operational-hold unit by product and ABO/Rh on the inventory boards, then locate, inspect, or release hold from that row without opening Manage. | P1 | FRS-BB-210 |
| URS-BB-177 | After an emergency or MTP issue, technologists can open Record crossmatch from the retrospective board with the patient, released unit, and specimen already identified. | P2 | FRS-BB-211 |
| URS-BB-178 | Investigators can see when labeled unit ABO/Rh is incompatible with the current patient type on the reaction board, along with whether the issue was emergency or uncrossmatched. | P0 | FRS-BB-212 |
| URS-BB-179 | Interface staff can identify an HL7 error by direction and placer/test/unit when present, and recover from that row with Replay (inbound) or Send (outbound). | P1 | FRS-BB-213 |
| URS-BB-180 | After a good inbound ORU, technologists can see the posted interface value on the patient Orders tab and Verify it without re-keying. | P1 | FRS-BB-214 |
| URS-BB-181 | After verify, issue, or completed transfusion, billing staff see exactly one pending charge for that clinical event when a catalog mapping exists. | P1 | FRS-BB-215 |
| URS-BB-182 | Technologists can see on the pending test worklist when an entered ABO/Rh disagrees with the current historical type before they attempt verify. | P1 | FRS-BB-216 |
| URS-BB-183 | After receive, technologists can record the front-type ABO/Rh retype from the inventory page without opening the test worklist or the unit Manage drawer. | P1 | FRS-BB-217 |
| URS-BB-184 | After a unit is reserved, technologists can identify it on the issuing page by patient and unit number and open Issue with those identities already set. | P1 | FRS-BB-218 |
| URS-BB-185 | Investigators can see when a recorded reaction-workup repeat ABO/Rh disagrees with the current patient type or the labeled unit type without that mismatch blocking close. | P1 | FRS-BB-219 |
| URS-BB-186 | Interface staff can identify a logged HL7 message by placer order, test, or unit number when those fields are present, without opening the raw payload. | P1 | FRS-BB-220 |
| URS-BB-187 | After a good inbound RAS, staff can see the loaded transfusion volume, location, and transfusionist on the patient product-history chart without opening Issuing or the raw message. | P1 | FRS-BB-221 |
| URS-BB-188 | An administrator can open a licensed ICCBBA extract import dialog from ISBT Product Codes without replacing placeholders until a real extract is imported. | P1 | FRS-BB-222 |
| URS-BB-189 | Electronic (computer) XM stays off until an administrator enables it in Facility Policy. | P0 | FRS-BB-223 |
| URS-BB-190 | A blood attribute posted from a verified result stays on the patient chart unless a user with override privilege changes it. | P0 | FRS-BB-224 |
| URS-BB-191 | Small labs can leave second-verifier policies off in Facility Policy. | P1 | FRS-BB-225 |
| URS-BB-192 | Staff can attach a modification action (irradiate, wash, thaw, volume reduce, leukoreduce) to a normal product order instead of writing the request as a comment. | P0 | FRS-BB-226 |
| URS-BB-193 | After the reserved unit is modified for that action, staff can issue the modified unit from the same order without a separate quarantine-release step. | P0 | FRS-BB-227, FRS-BB-229 |
| URS-BB-194 | Completing a modification action captures the matching action charge and queues a DFT without a second product-issue charge for the action itself. | P0 | FRS-BB-228 |
| URS-BB-195 | Staff can run prebuilt operational reports and export them to PDF or Excel, and designers can add a visual or SQL report. The starter list is for facility review and is not claimed as a regulatory submission. | P7 | FRS-BB-230 |
| URS-BB-196 | Staff can see an untested or not-antigen-negative red cell on the allocate list, and only a user who meets the exception security level can reserve it after recording a reason. | P0 | FRS-BB-231 |
| URS-BB-197 | When an unused unit is returned, its unexported issue charge comes off the review queue with the return. An already exported issue charge stays, and the return says so. | P3 | FRS-BB-232 |
| URS-BB-198 | Saving a reaction workup still records clerical and other findings when the unit cannot be moved into remainder quarantine, and the board says the remainder cannot be held. | P2 | FRS-BB-233 |
| URS-BB-199 | Recalling a DIN puts each issued or transfused component on the notification worklist once, including a unit that left the bank before transfusion was recorded. | P1 | FRS-BB-234 |
| URS-BB-200 | When an interface has replay turned off, staff cannot reapply that interface's stored messages from the HL7 log or error queue. | P1 | FRS-BB-235 |
| URS-BB-201 | An inbound result that names an accepted specimen accession can post on that order without a separate link step. A specimen for another patient is not linked. | P1 | FRS-BB-236 |
| URS-BB-202 | When a verified result is invalidated, its unexported test charge comes off the review queue with the invalidation. An already exported test charge stays, and the invalidation says so. | P3 | FRS-BB-237 |
| URS-BB-203 | Staff who verify a posted ABO/Rh that disagrees with history can record the existing discrepancy override from the worklist or the order, without re-keying the result. | P0 | FRS-BB-238 |
| URS-BB-204 | Staff who refuse a packing-list unit at arrival can return it to the supplier from the expected-inbound row, without opening the unit. | P1 | FRS-BB-239 |
| URS-BB-205 | Staff see which reserved red cells cannot be issued routinely because a second ABO/Rh is missing, and can enter that type or start an emergency release. | P0 | FRS-BB-240 |
| URS-BB-206 | Staff still see a fatality investigation after it is closed until CBER notification and the written report are both recorded. | P1 | FRS-BB-241 |
| URS-BB-207 | Staff see recipient lookback calls that are still due without searching the DIN again. | P1 | FRS-BB-242 |
| URS-BB-208 | Staff can clear an HL7 error after handling it by hand, including when replay is turned off. | P1 | FRS-BB-243 |
| URS-BB-209 | Staff see a STAT, ASAP, timed, or pre-op order from the interface at that priority, so the chart and worklist do not treat it as routine. | P1 | FRS-BB-244 |
| URS-BB-210 | Staff reviewing a completed-action charge can see which action and unit it is for, and open that patient's orders. | P1 | FRS-BB-245 |
| URS-BB-211 | Staff see a Stat, Urgent, emergency-release, or massive-transfusion test above newer routine work on the pending worklist and the patient Tests tab. | P1 | FRS-BB-246 |
| URS-BB-212 | Staff can discard a unit that is about to expire from the near-expiry row, without opening the unit. | P1 | FRS-BB-247 |
| URS-BB-213 | Staff returning an issued unit from the issue desk record temperature, seal, and appearance, and cannot put a failed inspection back into available stock. | P0 | FRS-BB-248 |
| URS-BB-214 | Staff cannot choose Close on a reaction investigation until the clerical check, visual inspection, and DAT are recorded. | P0 | FRS-BB-249 |
| URS-BB-215 | Staff cannot complete a lookback call without the physician of record, or mark it not applicable without a note. | P1 | FRS-BB-250 |
| URS-BB-216 | Staff cannot send an outbound HL7 message again after it has used the interface retry limit. | P1 | FRS-BB-251 |
| URS-BB-217 | A later ADT that omits name, date of birth, or sex does not clear those values on the chart. | P1 | FRS-BB-252 |
| URS-BB-218 | Cancelling a charge before its DFT is sent does not leave that message available to send. | P1 | FRS-BB-253 |
| URS-BB-219 | Staff are not offered Verify on a posted result when the specimen cannot be used. | P1 | FRS-BB-254 |
| URS-BB-220 | A matching retype on an outdated unit does not put that unit into available stock. | P1 | FRS-BB-255 |
| URS-BB-221 | Staff are not offered issue of a reserved unit when the current specimen is missing or expired. | P1 | FRS-BB-256 |
| URS-BB-222 | Staff record a fatality's CBER notification and written report once, and a later CBER record does not clear the written report. | P1 | FRS-BB-257 |
| URS-BB-223 | A lookback call names the patient on the current issue, not an earlier return of the same unit. | P1 | FRS-BB-258 |
| URS-BB-224 | Staff are not offered replay of an HL7 message that already applied. | P1 | FRS-BB-259 |
| URS-BB-225 | An interface result for a cancelled or discontinued order is not filed where the bench no longer shows it. | P1 | FRS-BB-260 |
| URS-BB-226 | Staff are not offered export of a reviewed charge until its DFT is accepted. | P1 | FRS-BB-261 |
| URS-BB-227 | Staff are not offered result entry or verify on an order that is on hold. | P1 | FRS-BB-262 |
| URS-BB-228 | Staff are not offered receipt of an expected unit that is already outdated. | P1 | FRS-BB-263 |
| URS-BB-229 | Staff are not offered issue of a reserved unit that is already outdated. | P1 | FRS-BB-264 |
| URS-BB-230 | Staff who record a fatal reaction severity start the same fatality follow-up as the fatality checkbox. | P1 | FRS-BB-265 |
| URS-BB-231 | Staff are not offered another attempt on a lookback call that is already finished. | P1 | FRS-BB-266 |
| URS-BB-232 | Staff are not offered replay of an errored inbound copy once that control id has already applied. | P1 | FRS-BB-267 |
| URS-BB-233 | Staff selecting a unit see this patient's autologous and directed units first, then ABO/Rh-identical stock, and are not offered another patient's restricted unit. | P1 | FRS-BB-268 |
| URS-BB-234 | Staff selecting a unit are not offered a unit that is already outdated. | P1 | FRS-BB-269 |
| URS-BB-235 | Staff selecting a unit can see when that unit is inside the facility near-expiry window. | P1 | FRS-BB-270 |
| URS-BB-236 | Staff selecting a unit are not offered a unit that fails an active irradiated, CMV-negative, leukoreduced, washed, or antigen-negative requirement. | P1 | FRS-BB-271 |
| URS-BB-237 | Staff opening a patient chart can see the specimen the issue gate will read, and whether that specimen already blocks issue. | P1 | FRS-BB-272 |
| URS-BB-238 | Staff selecting a unit see a unit that already has a compatible crossmatch for this patient ahead of an unmatched unit in the same group. | P1 | FRS-BB-273 |
| URS-BB-239 | Staff opening a patient chart can see when red-cell issue still needs a second concordant ABO/Rh. | P1 | FRS-BB-274 |
| URS-BB-240 | Staff selecting a unit can see when that unit already crossmatched incompatible for this patient, and see it after unmatched stock in the same group. | P1 | FRS-BB-275 |
| URS-BB-241 | Staff opening a patient chart can see why electronic crossmatch is not eligible, on every tab. | P1 | FRS-BB-276 |
| URS-BB-242 | Staff on the issue desk can see when a reserved unit belongs to a patient with an open antibody-identification workup. | P1 | FRS-BB-277 |
| URS-BB-243 | Staff on the issue desk can see when a reserved unit is inside the same near-expiry window the issue gate will warn on. | P1 | FRS-BB-278 |
| URS-BB-244 | Staff on the issue desk can see why electronic crossmatch is not eligible for a reserved unit. | P1 | FRS-BB-279 |
| URS-BB-245 | Staff on the issue desk can see when a reserved unit still needs a compatible crossmatch, or already has an incompatible one, before they start issue. | P0 | FRS-BB-280 |
| URS-BB-246 | Staff on the issue desk can see when a reserved unit fails an active irradiated, CMV-negative, leukoreduced, washed, or antigen-negative requirement before they start issue. | P0 | FRS-BB-281 |
| URS-BB-247 | Staff on the issue desk can see when a reserved unit is not antigen-negative for a clinically significant antibody before they start issue. | P0 | FRS-BB-282 |
| URS-BB-248 | Staff on the issue desk can see an unresolved ABO/Rh discrepancy on a reserved unit before they start issue. | P0 | FRS-BB-283 |
| URS-BB-249 | Staff can see on every patient-chart tab when the current ABO/Rh disagrees with the historical record. | P0 | FRS-BB-284 |
| URS-BB-250 | Staff can see on every patient-chart tab when this patient has a recorded transfusion reaction. | P1 | FRS-BB-285 |
| URS-BB-251 | Staff on the issue desk can see when a reserved unit belongs to a patient with a recorded transfusion reaction. | P1 | FRS-BB-286 |
| URS-BB-252 | Staff who start issue from a reserved unit can still see that unit's Ready to issue marks on the issue form. | P1 | FRS-BB-287 |
| URS-BB-253 | Staff on the issue desk can see when a reserved unit is this patient's autologous or directed unit. | P1 | FRS-BB-288 |
| URS-BB-254 | Staff on the issue desk can see the product name of a reserved unit before they issue it. | P1 | FRS-BB-289 |
| URS-BB-255 | Staff on the issue desk can see the product name of a unit that has already been issued, including while it is in transit or waiting for a retrospective crossmatch. | P1 | FRS-BB-290 |
| URS-BB-256 | Staff on the issue desk can see when a reserved unit expires before they issue it. | P1 | FRS-BB-291 |
| URS-BB-257 | Staff on the issue desk can see when an issued unit expires, including while it is in transit or waiting for a retrospective crossmatch. | P1 | FRS-BB-292 |
| URS-BB-258 | Staff choosing or reviewing a unit for a patient can see the product name and the expiration time, not only the date. | P1 | FRS-BB-293 |
| URS-BB-259 | Staff on the issue desk can see the ABO/Rh of a reserved unit next to the patient's type before they issue it. | P0 | FRS-BB-294 |
| URS-BB-260 | Staff on the issue desk can see the ABO/Rh of a unit that has already been issued, including while it is in transit or waiting for a retrospective crossmatch. | P0 | FRS-BB-295 |
| URS-BB-261 | Staff reviewing a patient's allocated products can see the ABO/Rh of each unit. | P0 | FRS-BB-296 |
| URS-BB-262 | Staff on the issue desk can see when a current compatible crossmatch expires before they issue the unit. | P0 | FRS-BB-297 |
| URS-BB-263 | Staff on the issue desk can see which storage location holds a reserved unit before they issue it. | P1 | FRS-BB-298 |
| URS-BB-264 | Staff choosing or reviewing a unit for a patient can see which storage location holds it. | P1 | FRS-BB-299 |
| URS-BB-265 | Staff on the issue desk can see the accession of the current specimen before they issue a reserved unit. | P0 | FRS-BB-300 |
| URS-BB-266 | Staff on the issue desk can see the specimen accession of a unit that has already been issued, including while it is in transit. | P0 | FRS-BB-301 |
| URS-BB-267 | Staff on the issue desk can see when a current compatible crossmatch expires for a unit that has already been issued, including while it is in transit or waiting for a retrospective crossmatch. A unit with no current compatible crossmatch shows none. | P2 | FRS-BB-302 |
| URS-BB-268 | Staff reviewing a patient's allocated products can see when a current compatible crossmatch for that unit expires. A unit with no current compatible crossmatch shows none. | P2 | FRS-BB-303 |
| URS-BB-269 | Staff choosing a unit for a patient can see when a current compatible crossmatch for that unit expires. A unit with no current compatible crossmatch shows none. | P2 | FRS-BB-304 |
| URS-BB-270 | Staff on the issue desk can see the latest crossmatch result for a unit that has already been issued, including while it is in transit or waiting for a retrospective crossmatch. A unit with no crossmatch result shows none. | P2 | FRS-BB-305 |
| URS-BB-271 | Staff reviewing a patient's allocated products can see the latest crossmatch result for each unit. A unit with no crossmatch result shows none. | P2 | FRS-BB-306 |
| URS-BB-272 | Staff on the issue desk can see the latest crossmatch result for a reserved unit, and the issue form repeats that result. A unit with no crossmatch result shows none. | P2 | FRS-BB-307 |
| URS-BB-273 | Staff choosing a unit for a patient can see the latest crossmatch result for that unit. A unit with no crossmatch result shows none. | P2 | FRS-BB-308 |
| URS-BB-274 | Staff reviewing a patient's product history can see the latest crossmatch result on each assigned, issued, returned, or transfused unit. A unit with no crossmatch result shows none. | P2 | FRS-BB-309 |
| URS-BB-275 | Staff reviewing a patient's product history can see the specimen accession linked to an issued, returned, or transfused unit. A row with no linked specimen shows none. | P0 | FRS-BB-310 |
| URS-BB-276 | Staff reviewing a patient's product history can see when each unit expires. A row with no unit expiration shows none. | P0 | FRS-BB-311 |
| URS-BB-277 | Staff reviewing a patient's product history can see who performed each event. A row with no performer shows none. | P1 | FRS-BB-312 |
| URS-BB-278 | Staff reviewing a patient's product history can see why a unit was returned and whether it was marked eligible to reissue. A row that is not a return shows none for those facts. | P0 | FRS-BB-313 |
| URS-BB-279 | Staff reviewing a patient's product history can see the person an issued, returned, or transfused unit was issued to. A row with no issued-to person shows none. | P1 | FRS-BB-314 |
| URS-BB-280 | Staff reviewing in-transit and issued units can see the person each unit was issued to. A unit with no issued-to person shows none. | P1 | FRS-BB-315 |
| URS-BB-281 | Staff receiving, returning, or documenting an issued unit can see the person it was issued to. A unit with no issued-to person shows none. | P1 | FRS-BB-316 |
| URS-BB-282 | Staff reviewing a pending retrospective crossmatch can see the person the unit was issued to. A unit with no issued-to person shows none. | P1 | FRS-BB-317 |
| URS-BB-283 | Staff reviewing a patient's product history can see the cooler or transport id for an issued, returned, or transfused unit. A row with no cooler shows none. | P1 | FRS-BB-318 |
| URS-BB-284 | Staff receiving, returning, or documenting an issued unit can see the cooler or transport id. A unit with no cooler shows none. | P1 | FRS-BB-319 |
| URS-BB-285 | Staff reviewing outstanding issued units can see the cooler or transport id. A unit with no cooler shows none. | P1 | FRS-BB-320 |
| URS-BB-286 | Staff following an uncrossmatched emergency or MTP issue can see the cooler or transport id. A unit with no cooler shows none. | P1 | FRS-BB-321 |
| URS-BB-287 | Staff opening a retrospective crossmatch can see who received the unit and which cooler carried it. A unit with neither shows none. | P1 | FRS-BB-322 |
| URS-BB-288 | Staff reviewing outstanding issued units can see the location the unit was issued to. A unit with no location shows none. | P1 | FRS-BB-323 |
| URS-BB-289 | Staff receiving, returning, or documenting an issued unit can see the location it was issued to. A unit with no location shows none. | P1 | FRS-BB-324 |
| URS-BB-290 | Staff following an uncrossmatched emergency or MTP issue can see the location the unit was issued to. A unit with no location shows none. | P1 | FRS-BB-325 |
| URS-BB-291 | Staff reviewing a patient's product history can see whether an issued, returned, or transfused unit was a routine, emergency, or other recorded issue type. A row that is not one of those events shows none. | P1 | FRS-BB-326 |
| URS-BB-292 | Staff reviewing units still issued can see the recorded issue type on the in-transit and outstanding issued queues. | P1 | FRS-BB-327 |
| URS-BB-293 | Staff reviewing units still issued can see who released each unit on the in-transit and outstanding issued queues. | P1 | FRS-BB-328 |
| URS-BB-294 | Staff receiving, returning, or documenting a unit still issued can see who released it on that form. | P1 | FRS-BB-329 |
| URS-BB-295 | Staff completing a retrospective crossmatch can see who released the unit on that queue. | P1 | FRS-BB-330 |
| URS-BB-296 | Staff recording a retrospective crossmatch can see who released the unit on the follow-up screen. | P1 | FRS-BB-331 |
| URS-BB-297 | Staff completing a retrospective crossmatch can see the recorded reason the unit was released. | P1 | FRS-BB-332 |
| URS-BB-298 | Staff recording a retrospective crossmatch can see the recorded release reason on the follow-up screen. | P1 | FRS-BB-333 |
| URS-BB-299 | Staff reviewing a patient's product history can see the recorded reason an issued, returned, or transfused unit was released. | P1 | FRS-BB-334 |
| URS-BB-300 | Staff reviewing units still issued can see the recorded reason each unit was released on the in-transit and outstanding issued queues. | P1 | FRS-BB-335 |
| URS-BB-301 | Staff receiving, returning, or documenting a unit still issued can see the recorded reason it was released. | P1 | FRS-BB-336 |
| URS-BB-302 | Staff reviewing units still issued can see the second verifier recorded at issue on the in-transit and outstanding issued queues. | P1 | FRS-BB-337 |
| URS-BB-303 | Staff receiving, returning, or documenting a unit still issued can see the second verifier recorded at issue. | P1 | FRS-BB-338 |
| URS-BB-304 | Staff completing a retrospective crossmatch can see the second verifier recorded at issue on that queue. | P1 | FRS-BB-339 |
| URS-BB-305 | Staff recording a retrospective crossmatch can see the second verifier recorded at issue on the follow-up screen. | P1 | FRS-BB-340 |
| URS-BB-306 | Staff reviewing a patient's product history can see the second verifier recorded at issue for an issued, returned, or transfused unit. | P1 | FRS-BB-341 |
| URS-BB-307 | Staff completing a retrospective crossmatch can see who authorized the emergency or massive-transfusion release. | P1 | FRS-BB-342 |
| URS-BB-308 | Staff recording a retrospective crossmatch can see who authorized the release on the follow-up screen. | P1 | FRS-BB-343 |
| URS-BB-309 | Staff reviewing units still issued can see who authorized an emergency or massive-transfusion release on the in-transit and outstanding issued queues. | P1 | FRS-BB-344 |
| URS-BB-310 | Staff receiving, returning, or documenting a unit can see who authorized an emergency or massive-transfusion release on that form. | P1 | FRS-BB-345 |
| URS-BB-311 | Staff reviewing a patient's product history can see who authorized an emergency or massive-transfusion release on issued, returned, and transfused rows. | P1 | FRS-BB-346 |
| URS-BB-312 | Staff reviewing units still issued can see the appearance recorded at issue on the in-transit and outstanding issued queues. | P1 | FRS-BB-347 |
| URS-BB-313 | Staff receiving, returning, or documenting a unit can see the appearance recorded at issue on that form. | P1 | FRS-BB-348 |
| URS-BB-314 | Staff completing a retrospective crossmatch can see the appearance recorded at issue on that queue. | P1 | FRS-BB-349 |
| URS-BB-315 | Staff recording a retrospective crossmatch can see the appearance recorded at issue on the follow-up screen. | P1 | FRS-BB-350 |
| URS-BB-316 | Staff reviewing a patient's product history can see the appearance recorded at issue on issued, returned, and transfused rows. | P1 | FRS-BB-351 |
| URS-BB-317 | Staff reviewing units still issued can see the appearance recorded at ward receipt on the outstanding issued queue. | P1 | FRS-BB-352 |
| URS-BB-318 | Staff returning or documenting a unit can see the appearance recorded at ward receipt on that form. | P1 | FRS-BB-353 |
| URS-BB-319 | Staff reviewing a patient's product history can see the appearance recorded at ward receipt on issued, returned, and transfused rows. | P1 | FRS-BB-354 |
| URS-BB-320 | Staff reviewing units still issued can see the pickup tag scanned at issue on the in-transit and outstanding issued queues. | P1 | FRS-BB-355 |
| URS-BB-321 | Staff receiving, returning, or documenting a unit can see the pickup tag scanned at issue on that form. | P1 | FRS-BB-356 |
| URS-BB-322 | Staff completing a retrospective crossmatch can see the pickup tag scanned at issue on that queue. | P1 | FRS-BB-357 |
| URS-BB-323 | Staff recording a retrospective crossmatch can see the pickup tag scanned at issue on the follow-up screen. | P1 | FRS-BB-358 |
| URS-BB-324 | Staff reviewing a patient's product history can see the pickup tag scanned at issue on issued, returned, and transfused rows. | P1 | FRS-BB-359 |
| URS-BB-325 | Staff reviewing units still issued can see the crossmatch status recorded at issue on the in-transit and outstanding issued queues. | P1 | FRS-BB-360 |
| URS-BB-326 | Staff receiving, returning, or documenting a unit can see the crossmatch status recorded at issue on that form. | P1 | FRS-BB-361 |
| URS-BB-327 | Staff completing a retrospective crossmatch can see the crossmatch status recorded at issue on that queue. | P1 | FRS-BB-362 |
| URS-BB-328 | Staff recording a retrospective crossmatch can see the crossmatch status recorded at issue on the follow-up screen. | P1 | FRS-BB-363 |
| URS-BB-329 | Staff reviewing a patient's product history can see the crossmatch status recorded at issue on issued, returned, and transfused rows. | P1 | FRS-BB-364 |
| URS-BB-330 | A historically identified antibody that is currently undetectable still precludes electronic crossmatch. | P0 | FRS-BB-365 |
| URS-BB-331 | Staff opening the dashboard can see the bench queues they are allowed to work. | P1 | FRS-BB-366 |
| URS-BB-332 | Staff opening the dashboard can see units still in transit, including how many are past the arrival window. | P1 | FRS-BB-367 |
| URS-BB-333 | Staff opening the dashboard can see which retrospective crossmatches are overdue and which reaction cases still need fatality follow-up. | P1 | FRS-BB-368 |
| URS-BB-334 | Staff can receive a collected specimen into the blood bank and accept a received specimen before testing, from a pending-specimens worklist. | P0 | FRS-BB-369 |
| URS-BB-335 | Staff ending a shift can record a handoff packet with a required note and a snapshot of the current bench queues, and the incoming technologist can acknowledge it. | P1 | FRS-BB-370 |
| URS-BB-336 | Staff can change a specimen barcode only through a controlled relabel that requires a reason; collection metadata edit does not change the barcode. | P0 | FRS-BB-371 |
| URS-BB-337 | Supervisors can open one worklist of antibody-identification reviews, posted results awaiting verification, and quarantined units awaiting release for the permissions they already have. | P1 | FRS-BB-372 |
| URS-BB-338 | Staff changing a patient's name must give a reason, and the previous name remains searchable as a historical alias. | P0 | FRS-BB-373 |
| URS-BB-339 | When an inbound ADT changes a patient's stated name, the previous name remains as a searchable historical alias. | P0 | FRS-BB-374 |
| URS-BB-340 | Staff who merged the wrong patient records can undo that merge, and the history returns to the duplicate, when nothing new has been done on the survivor since the merge. | P0 | FRS-BB-375 |
| URS-BB-341 | When a unit is divided, pooled, or otherwise modified, recorded antigen and antibody typing stays on the result, and a pool of units that disagree on a typing cannot be made. | P0 | FRS-BB-376 |
| URS-BB-342 | When a unit is divided, pooled, or otherwise modified, an autologous or directed reservation stays on the result, and units reserved for different people cannot be pooled. | P0 | FRS-BB-377 |
| URS-BB-343 | Staff can remove an aliquot from a unit and leave the parent available at the reduced volume. | P0 | FRS-BB-378 |
| URS-BB-344 | Staff dividing a unit, or recording a result volume on another modification, cannot create or discard volume that the source record does not support. | P0 | FRS-BB-379 |
| URS-BB-345 | Staff pooling units cannot lose a recorded source volume because another unit in the pool has none. | P0 | FRS-BB-380 |
| URS-BB-346 | Staff cannot pool units that are still recorded in different storage locations. | P0 | FRS-BB-381 |
| URS-BB-347 | Staff reading modification history can see where each result unit was made, even if that unit has since been moved. | P1 | FRS-BB-382 |
| URS-BB-348 | Staff reading modification history can see when a result unit is no longer where it was made. | P1 | FRS-BB-383 |
| URS-BB-349 | Staff reading modification history can see when a result was moved and why, when that was recorded. | P1 | FRS-BB-384 |
| URS-BB-350 | Staff reading modification history can see who recorded a result's move, when that person was recorded. | P1 | FRS-BB-385 |
| URS-BB-351 | Staff preparing a pool can see each source unit's storage location by name. | P1 | FRS-BB-386 |
| URS-BB-352 | Staff who just modified a unit can see where the new unit is and how to release it from quarantine. | P1 | FRS-BB-387 |
| URS-BB-353 | Staff who just made an aliquot can see that the parent stayed in place and how much volume remains. | P1 | FRS-BB-388 |
| URS-BB-354 | Staff who just retired a unit by modification can see that the source is no longer available. | P1 | FRS-BB-389 |
| URS-BB-355 | Staff cannot start another modification on a unit that modification just retired. | P1 | FRS-BB-390 |
| URS-BB-356 | Staff releasing a quarantined unit can see the storage location by name. | P1 | FRS-BB-391 |
| URS-BB-357 | Staff releasing a quarantined unit can see its recorded volume. | P1 | FRS-BB-392 |
| URS-BB-358 | Staff looking for a missing or damaged unit can see the storage location by name. | P1 | FRS-BB-393 |
| URS-BB-359 | Staff releasing a unit from operational hold can see the storage location by name. | P1 | FRS-BB-394 |
| URS-BB-360 | Staff searching inventory can see each unit's storage location by name. | P1 | FRS-BB-395 |
| URS-BB-361 | Staff discarding a unit that is near expiry can see the storage location by name. | P1 | FRS-BB-396 |
| URS-BB-362 | Staff opening a unit can see its storage location by name. | P1 | FRS-BB-397 |
| URS-BB-363 | Staff reading a unit timeline can see the storage locations when a status row records a move. | P1 | FRS-BB-398 |
| URS-BB-364 | Staff reading a unit timeline can see who a unit was allocated to by name. | P1 | FRS-BB-399 |
| URS-BB-365 | Staff reading a unit timeline can see who a crossmatch was for by name. | P1 | FRS-BB-400 |
| URS-BB-366 | Staff reading a unit timeline can see who received a transfusion by name. | P1 | FRS-BB-401 |
| URS-BB-367 | Staff reading a unit timeline can see who a returned unit was issued to by name. | P1 | FRS-BB-402 |
| URS-BB-368 | Staff reading a unit timeline can see who an order-linked modification was for by name. | P1 | FRS-BB-403 |
| URS-BB-369 | Staff reading a unit timeline can see that a bench modification had no patient order. | P1 | FRS-BB-404 |
| URS-BB-370 | Staff reading a unit timeline can see who a bench modification was for when a product order points at it. | P1 | FRS-BB-405 |
| URS-BB-371 | Staff reading modification history can see who an order-linked modification was for. | P1 | FRS-BB-406 |
| URS-BB-372 | Staff reading a unit status row can see who a modification was for when it is linked to that modification. | P1 | FRS-BB-407 |
| URS-BB-373 | Staff reading an issue on the unit timeline can see the storage location name when the issued-to place is a location code. | P1 | FRS-BB-408 |
| URS-BB-374 | Staff reading the issuing board or patient product history can see the storage location name when the issued-to place is a location code. | P1 | FRS-BB-409 |
| URS-BB-375 | Staff reading lookback can see the storage location name when the issued-to place is a location code. | P1 | FRS-BB-410 |
| URS-BB-376 | Staff receiving, returning, or documenting a unit can read a blank storage location as Location {id} without a second Location prefix. | P1 | FRS-BB-411 |
| URS-BB-377 | Staff choosing a reserved unit can read a blank storage location as the location id. | P1 | FRS-BB-412 |
| URS-BB-378 | Staff transferring a unit or receiving a delivery can select a location by its code and still read a blank name as the location id. | P1 | FRS-BB-413 |
| URS-BB-379 | Staff reading the inventory-location catalog can see a blank name as the location id while the code stays in the catalog. | P1 | FRS-BB-414 |
