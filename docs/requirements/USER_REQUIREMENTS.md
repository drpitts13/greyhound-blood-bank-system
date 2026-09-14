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
| URS-BB-018 | A unit that requires ABO/Rh retype is not Available until a verified matching retype exists. | P0 | FRS-BB-051, FRS-BB-106, FRS-BB-126 |
| URS-BB-019 | Manually setting the current ABO/Rh (not from a verified result) is limited to authorized staff. | P0 | FRS-BB-021 |
| URS-BB-020 | The user who entered a patient ABO/Rh cannot verify it. Current type is established only after a second user verifies. | P0 | FRS-BB-022 |
| URS-BB-021 | Changing special transfusion requirements (irradiated, CMV-neg, antigen-negative, …) is limited to authorized immuno staff. | P0 | FRS-BB-023 |
| URS-BB-022 | A donor lookback recall must actually recall in-date components, including reserved or crossmatched units, or fail closed. | P0 | FRS-BB-024, FRS-BB-165 |
| URS-BB-023 | Releasing a unit from quality quarantine to Available is limited to staff with inventory.release. | P0 | FRS-BB-052 |
| URS-BB-024 | Converting an unused directed unit to allogeneic inventory is limited to staff with inventory.release. | P0 | FRS-BB-053, FRS-BB-136 |
| URS-BB-025 | Releasing a unit from operational hold to Available is limited to staff with inventory.release. | P0 | FRS-BB-054 |
| URS-BB-026 | Verifying a test result or a unit ABO/Rh retype is limited to staff with result.verify. | P0 | FRS-BB-055 |
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
| URS-BB-072 | Staff can find unit ABO/Rh retype entry, verification, and the resulting Available/Quarantine change as named Result, Verify, and ProductStatus events. | P0 | FRS-BB-106 |
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
| URS-BB-134 | Staff can see on `/audit` the interpreted ABO/Rh of a unit retype on enter, update, and verify, because that type is used at later compatibility and issue. | P0 | FRS-BB-168 |
| URS-BB-135 | Staff can see the interpreted ABO/Rh after saving or verifying a unit retype, not only a generic entered/verified message. | P3 | FRS-BB-169 |
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
