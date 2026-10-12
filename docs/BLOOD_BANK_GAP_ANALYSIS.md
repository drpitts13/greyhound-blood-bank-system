# Blood bank gap analysis

Residual gaps after existing Domain/Application controls. Ranked by
patient-safety impact among items that are still open. Cycle 1 implemented
**gap 1**. Cycle 2 implemented **gap 6**. Cycle 3 implemented **gap 7**.
Cycle 4 implemented **gap 8**. Cycle 6 implemented the bedside identity-token
slice of **gap 15**. Cycle 7 added the licensed ISBT catalog **import path**
for **gap 2**; Cycle 14 added CSV/TSV/JSON extract import if files are
available (extract not yet loaded). Cycle 8 recorded **gap 5** as
accepted transport-trust (OCD-034). Cycle 9 closed **gap 9** (OCD-033
live row) and added security headers for **gap 18**. Cycle 10 closed
**gap 10** (OCD-007 no purge) and packaged **gap 19** (`TEST-BB-*`).
Cycle 11 closed **gap 12** (OCD-035: keep order-rule hooks) and implemented
**gap 16** (read-only downtime reconciliation snapshot).

SME-blocked items must not be “fixed” by inventing a clinical rule. Record
them in [`docs/OPEN_CLINICAL_DECISIONS.md`](OPEN_CLINICAL_DECISIONS.md).
Keep them in the continuous-improvement loop: ask the decision in the
active conversation, wait for the answer, then implement. Do not skip
them silently and do not invent ICCBBA tables.

## Capability matrix (Cycle 190)

Status is relative to a mature transfusion-service LIS. Present means a
working path exists; Partial means the domain exists but a bench workflow
is incomplete. Do not treat Present as AABB/FDA certification.

| Area | Status | Notes |
|---|---|---|
| Patient demographics, MRN, visits, location, provider | Present | Chart + ADT |
| Alternate identifiers, aliases | Present | Cycle 185–186: prior name on chart edit and ADT; prior MRN from merge; search matches aliases |
| Duplicate handling / merge | Present | Merge + reason; Cycle 187 unmerge restores the ledger when nothing new has happened since. Pre-ledger merges cannot be undone |
| Orders, priority, ordering location | Present | Test and product orders |
| Specimen collection / receipt / accept | Present | Cycle 181: Collected → Received → Accepted; in-lab accession still accepts immediately |
| Specimen rejection, expiration, multiple/historical | Present | Reject; expiry gates; patient list |
| Relabeling controls | Present | Cycle 183: specimen barcode Relabel with required reason; metadata edit cannot change barcode |
| ABO/Rh, ABS, ABID, DAT, antigen, XM | Present | Workup + result paths |
| Elution / special / reflex | Partial | Chart DAT/elution workup exists. Eluate findings are not posted as structured antibody history; do not invent that rule |
| Result review, correction, invalidation | Present | Versioned; instrument pending verification |
| ISBT identifiers, codes, attributes, locations | Partial | Parser + import; licensed ICCBBA extract not loaded (OCD-004) |
| Divisions, modifications, pooling, aliquots, irradiate, wash, thaw | Partial | Cycle 188 copies concordant typing. Cycle 189 copies an autologous or directed reservation. Cycle 190 aliquot leaves the parent available and reduces its volume. A negative is copied only when every source was typed negative |
| Expiration calculations, status history, transfer | Present | |
| Selection, reserve, assign, XM, issue, emergency, MTP | Present | IssueGate |
| Return, reissue, transfusion, discard, reaction | Present | |
| Compatibility, special requirements, eXM, warning vs hard-stop | Present | OCD-001 closed. Per-product ABO and Rh grids apply at issue and allocation |
| Antibody/antigen history, phenotype, genotype, alerts | Present | Genotype vs phenotype tokens (OCD-024) |
| Worklists: tests, retype, near-expiry, issue, reactions, inbound | Present | Pending specimens Cycle 181; supervisor queue Cycle 184 |
| Assigned products / outstanding transfusions | Present | Ready to issue; outstanding issued |
| Shift handoff / supervisor queues | Present | Cycle 182 handoff; Cycle 184 permission-scoped supervisor worklist |
| Admin catalogs, rules, users, roles, locations, audit config | Present | Single live catalog row (OCD-033) |
| HL7 ADT/ORM/ORU, BPAM, retry/error, downtime snapshot | Partial | v2 present; FHIR absent; MLLP transport-trust |
| Audit of clinically important actions | Present | Append-only hash chain; no purge |

Cycle 186 implemented ADT prior-name archive on inbound demographics updates (RISK-BB-441).

Cycle 187 records a patient-merge ledger and lets staff with `patient.merge` undo that merge when the survivor has had no new clinical activity (RISK-BB-442). Merges already in the database have no ledger and cannot be reversed.

Cycle 188 copies recorded unit antigen and antibody typing onto divide, pool, and single-modification results (RISK-BB-443). A pool stops when sources disagree. A negative result is copied only when every source was typed negative.

Cycle 189 copies an autologous or directed reservation onto each modification result (RISK-BB-444). A pool stops when the sources do not share that reservation, or when a restricted unit has no intended recipient.

Cycle 190 removes an aliquot from a parent unit without retiring the parent (RISK-BB-445). The parent stays available at the reduced volume. The aliquot is a new unit.

| Rank | Priority | Gap | Residual | Cycle 1 action |
|---|---|---|---|---|
| 1 | P0 via P6 | API identity spoofing: `X-User` trusted after login; empty password hash signs in | Low (Bearer session; RISK-BB-254) | **Implemented Cycle 1** |
| 2 | P1 | Unlicensed ISBT / ICCBBA catalogs (RISK-BB-014, OCD-004) | Medium (admin import dialog; licensed extract not yet loaded) | **Import path Cycle 7/14/42**; do not invent tables |
| 3 | P3 | Antigen phenotype updated in place (OCD-022) | Low (in-place + result lock) | **Closed 2026-09-18** — stay in place; result-sourced change needs `immuno.override` |
| 4 | P1 | Patient merge has no second authorizer (OCD-010) | Low | **Closed 2026-09-14** — keep `patient.merge` + reason only |
| 5 | P6 / P8 | HL7 MLLP/file-drop inbound is transport-trust; HTTP inbound is session + `hl7.manage` | Low (accepted residual) | **Closed 2026-09-14** — keep transport-trust; do not invent a credential (OCD-034) |
| 6 | P6 | Blazor circuit permissions stale after role change | Low (sessions revoked; `/api/me` refresh) | **Implemented Cycle 2** |
| 7 | P3 | Concurrent antibody-history edit vs electronic XM | Low (re-read before save; RISK-BB-256) | **Implemented Cycle 3** |
| 8 | P4 | Audit is append-only but has no hash chain | Low (hashed tip; RISK-BB-257) | **Implemented Cycle 4** |
| 9 | P5 | Configuration effective-dating incomplete (`FRS-BB-070`) | Low | **Closed 2026-09-14** — keep single live row (OCD-033); do not invent as-of dates |
| 10 | P5 | Record retention / purge not implemented (OCD-007) | Low | **Closed 2026-09-14** — do not purge; retention years stay metadata |
| 11 | P7 | RhIG workflow missing | Medium | SME for indications |
| 12 | P2 | Neonatal irradiation / CMV product-selection defaults | Low | **Closed 2026-09-14** — keep order-rule hooks; do not invent product defaults (OCD-035) |
| 13 | P7 / P9 | Quality metrics / reporting | Low (operational catalog, visual designer, guarded SQL, PDF/Excel). Starter list is not a regulatory claim | **Implemented** — eight facility reports; review sign-off not included |
| 14 | P8 | FHIR | Low | HL7 v2 exists |
| 15 | P1 | Bedside dual-ID / administration device path incomplete | Low (PPID tokens + ISBT scan; RISK-BB-258). Residual: no dedicated administration-device protocol or required vitals (do not invent) | **Implemented Cycle 6** (identity tokens; device path still thin) |
| 16 | P5 | Downtime reconciliation tooling thin | Low (snapshot; no paper OCR / failover) | **Implemented Cycle 11** |
| 17 | P2 | Electronic XM policy defaults (OCD-001, OCD-006) | Low | **Closed 2026-10-02** — any antibody history, including deactivated, keeps blocking electronic XM (`XM-EC-HISTORY`). OCD-006 stays off until Facility Policy. |
| 18 | P6 | Broader CSRF / XSS / secrets review | Low (headers + no wildcard CORS; Blazor CSP still allows inline/eval) | **Implemented Cycle 9** |
| 19 | P5 | Validation packaging: no `TEST-BB-*` IDs; stale “Phase 0” headers; missing iteration log | Low (core safety IDs through TEST-BB-030) | **Implemented Cycle 10**; Cycles 12–13 assigned leftover high-safety class-name citations |
| 20 | P6 | DevMode auto-admin | Low | Keep Development-only |

Later (not ranked into Cycle 1): no Blazor E2E suite; open antibody-identification
workup still allows allocate / serologic XM / issue as Warning (OCD-023).

Cycle 15 (workflow-audit loop): pending test worklist now surfaces ABO/Rh,
antibody history, and specimen expiration; `/compatibility` uses MRN/name and
accession pickers (RISK-BB-267). This is not a new AABB default.

Cycle 16: expected inbound arrival can be confirmed on the packing-list
worklist (RISK-BB-268). Receive gates are unchanged.

Cycle 17: in-transit and retrospective boards show unit number, ABO/Rh,
antibody history, and specimen expiration; Receive prefills ward receipt;
generic issue identifies the recipient by MRN/name (RISK-BB-269). Issue
gates are unchanged.

Cycle 18: reaction investigations list MRN, name, unit number, type,
antibodies, workup completeness, and remainder hold; documenting a
reaction or the patient product-history badge opens the case
(RISK-BB-270). Close and quarantine gates are unchanged.

Cycle 19: the HL7 error queue shows control id and message type and
replays from that row; an accepted replay resolves the original mapping
error (RISK-BB-271). MSH-10 idempotency is unchanged.

Cycle 20: seeded `MRN0009` is an ADT/ORM/ORU load onto an accepted
specimen; the test worklist verifies the posted interface value without
re-keying (RISK-BB-272). OBX-11 and specimen gates are unchanged.

Cycle 21: seeded Patricia Demo verify/issue/transfusion charges appear
on the review queue with MRN, unit/test, and queued DFT control id
(RISK-BB-273). Capture stays automatic; review-before-export is unchanged.

Cycle 22: the pending test worklist shows electronic-XM eligibility and
an open antibody-identification workup (RISK-BB-274). AABB 5.16 criteria
and OCD-006 (now closed: off until Facility Policy) remain the existing eligibility service.

Cycle 23: the quality-quarantine board shows product and a labeled-versus-
interpreted retype mismatch; Release is available on the row with the
existing second-verifier gate (RISK-BB-275). Retype auto-quarantine and
release privilege are unchanged.

Cycle 24: outstanding Issued units stay on `/issuing` after ward receipt
with unit number and patient context; Return and Document prefill from
the row (RISK-BB-276). Return and identity-token gates are unchanged.

Cycle 25: a completed transfusion with a suspected reaction now quarantines
the remainder; the reaction board shows labeled unit ABO/Rh and the first
workup hold (RISK-BB-277). Clerical/visual/DAT close gates are unchanged.

Cycle 26: the HL7 error queue and message log show PID MRN and name
(RISK-BB-278). Replay and MSH-10 idempotency are unchanged.

Cycle 27: seeded Helen Interface RAS documents transfusion of
`W000123BPAM001`; product history shows `HL7-BPAM` without a second
Issuing Document (RISK-BB-279). Bedside-scan and issue gates are unchanged.

Cycle 28: the billing queue keeps reviewed charges until export; inbound
RAS captures the completed-transfusion charge; Helen seed includes DFT
stubs (RISK-BB-280). Dedupe keys and export-after-review are unchanged.

Cycle 29: the pending test worklist and patient Tests tab show the posted
result and use Verify for interface/instrument PendingVerification; ABO
self-verify is hidden for the enterer (RISK-BB-281). Specimen and OCD-018
gates are unchanged.

Cycle 30: discrepancy and operational-hold boards show product and ABO/Rh;
Locate, Inspect, and Release from hold run from the row (RISK-BB-282).
Locate-to-quarantine and `inventory.release` gates are unchanged.

Cycle 31: the retrospective crossmatch board shows specimen accession and
opens Record crossmatch with patient, issued unit, and specimen prefilled
(RISK-BB-283). Compatible XM still closes the queue; emergency defaults
are unchanged.

Cycle 32: the reaction board badges labeled ABO/Rh incompatibility using
the existing issue-path rule and shows issue type / crossmatch status
(RISK-BB-284). Close and workup completeness gates are unchanged.

Cycle 33: the HL7 error queue shows placer/test/unit and direction;
inbound Replay and outbound Send run from the row; outbound AA resolves
the queue item (RISK-BB-285). ReplayAllowed stays unenforced.

Cycle 34: the patient Orders tab shows HL7 source and the posted
interface value, and Verifies without re-keying (RISK-BB-286). Specimen
and OCD-018/019 gates are unchanged.

Cycle 35: verify, issue, and transfusion capture one charge when a
catalog row matches, even if a ChargeRule also matches; demo seed
deactivates overlapping rules (RISK-BB-287). Review-before-export and
dedupe keys are unchanged.

Cycle 36: pending ABO/Rh worklist rows show a historical discrepancy
badge from `AboRhDeltaRule` before verify (RISK-BB-288). Override and
self-verify gates are unchanged.

Cycle 37: `/inventory` shows pending product ABO/Rh retype with inline
Enter (RISK-BB-289). Match/mismatch release and quarantine gates are
unchanged.

Cycle 38: `/issuing` lists reserved units as Ready to issue and prefills
the issue form from the row or `/compatibility` (RISK-BB-290). IssueGate
and emergency defaults are unchanged.

Cycle 39: the reaction board badges a recorded repeat ABO/Rh that
disagrees with the current patient type or labeled unit type
(RISK-BB-291). Close and workup completeness gates are unchanged.

Cycle 40: the HL7 message log shows placer, test, and unit and links
the patient (RISK-BB-292). ReplayAllowed stays unenforced.

Cycle 41: product history shows inbound RAS volume, location, and
transfusionist; default transfusionist map is `RXA-10` XCN
(RISK-BB-293). Issue and bedside-scan gates are unchanged.

Reporting: `/reports` runs eight prebuilt operational reports (inventory,
issue/transfusion, discards, reactions, lookback, open deviations,
emergency release/overrides, crossmatch and transfusion activity) and
exports PDF or Excel. A visual designer filters, groups, and aggregates.
SQL reports are one SELECT against `rpt_*` views. This starter set is
pending regulatory review and is not an FDA, AABB, or CAP claim. There
is no reviewer sign-off.

Cycle 80: the compatible-unit list omits another patient's autologous or directed unit, lists this patient's restricted units first, then ABO/Rh-identical stock, then a compatible substitute (RISK-BB-335). Antigen-negative units precede untested units, which precede an antigen-negative override, and shortest expiration still orders each band. This is a selection order. ABO, antigen, and issue gates are unchanged. It does not assign a compatibility grade.

Cycle 81: the compatible-unit list omits a unit at or past expiration (RISK-BB-336). An in-date unit stays listed. Status is unchanged, and `UNIT-EXPIRED` remains the issue-gate hard stop.

Cycle 82: a compatible unit inside the existing facility near-expiry window is marked expires soon (RISK-BB-337). A unit outside that window is not. Both stay listed. `UNIT-NEAR-EXPIRY` remains the issue-gate warning.

Cycle 83: the compatible-unit list omits a unit that fails an active irradiated, CMV-negative, leukoreduced, washed, or antigen-negative requirement (RISK-BB-338). An acknowledgment-only requirement does not remove units. Status is unchanged, and `ISS-SPECIAL-REQ` remains the issue-gate hard stop.

Cycle 84: the patient chart names the latest accepted specimen and warns when that specimen is missing, undated, expired, or inside the 12-hour issue-gate window (RISK-BB-339). An older accepted specimen is not shown as current. The issue gate is unchanged.

Cycle 85: within each selection group, a unit with a current compatible crossmatch for this patient is listed before an unmatched unit and marked Crossmatch compatible (RISK-BB-340). Another patient's crossmatch and an expired crossmatch do not count. Issue uses the same predicate.

Cycle 86: the patient chart says when red-cell issue needs a second concordant ABO/Rh, and says when that type is already on file (RISK-BB-341). The notice follows `Compatibility.RequireSecondAboForCellularIssue`. `ISS-SECOND-ABO` remains the issue-gate hard stop.

Cycle 87: within each selection group, a unit with an incompatible crossmatch for this patient is listed after an unmatched unit and marked Crossmatch incompatible (RISK-BB-342). Another patient's result does not count. A later current compatible crossmatch still ranks first. The unit stays listed. `ISS-XM-REQUIRED` remains the issue-gate warning that requires an override.

Cycle 88: the patient chart header shows the existing electronic-crossmatch blocking reason on every tab (RISK-BB-343). An eligible result shows no notice. Eligibility criteria are unchanged. OCD-001 remains open.

Cycle 89: Ready to issue marks a reserved unit when the patient has an open antibody-identification workup (RISK-BB-344). A completed workup is not marked. The row stays issuable. `ABID-ISSUE-OPEN` remains the issue warning.

Cycle 90: Ready to issue marks Expires soon when a reserved unit is inside the issue near-expiry window, including the shorter post-thaw window (RISK-BB-345). An outdated unit stays Outdated. The row stays issuable. `UNIT-NEAR-EXPIRY` remains the issue-gate warning.

Cycle 91: Ready to issue shows the existing electronic-crossmatch blocking reason on a reserved unit (RISK-BB-346). An eligible patient shows no notice. Serologic issue stays available. Eligibility criteria are unchanged. OCD-001 remains open.

Cycle 92: Ready to issue marks a reserved unit that still needs a current compatible crossmatch and does not offer routine issue (RISK-BB-347). An incompatible result is marked and stays issuable. A current compatible result is not marked. `ISS-XM-REQUIRED` remains the issue-gate rule.

Cycle 93: Ready to issue marks a reserved unit that fails an active irradiated, CMV-negative, leukoreduced, washed, or antigen-negative requirement and does not offer issue (RISK-BB-348). A unit that meets the requirement is not marked. An acknowledgment-only requirement stays on the issue form. `ISS-SPECIAL-REQ` remains the issue-gate hard stop.

Cycle 94: Ready to issue marks a reserved unit that is not antigen-negative for a clinically significant antibody (RISK-BB-349). A unit already antigen-negative is not marked. The row stays issuable. `ISS-ANTIGEN-NEG` remains the issue-gate warning.

Cycle 95: Ready to issue marks an unresolved ABO/Rh discrepancy (RISK-BB-350). A crossmatch-required product is not offered for issue. A product that does not require a crossmatch stays issuable. A concordant history is not marked. `ISS-ABORH-DISCREPANCY` remains the issue-gate rule.

Cycle 96: the patient chart header shows the existing ABO/Rh disagreement warning on every tab (RISK-BB-351). A concordant history shows no notice. The product-specific hard stop stays on issue.

Cycle 97: the patient chart header shows a recorded transfusion reaction on every tab (RISK-BB-352). A history with no suspected reaction shows no notice. Issue eligibility is unchanged.

Cycle 98: Ready to issue shows that same recorded-reaction sentence on a reserved unit (RISK-BB-353). A transfusion that was not a suspected reaction is not marked. The row stays issuable.

Cycle 99: the issue form repeats the existing Ready to issue marks for the unit and patient just selected (RISK-BB-354). A row with no marks adds no notice. Issue eligibility is unchanged.

Cycle 100: Ready to issue shows the existing autologous or directed selection cue when that reserved unit is for this patient (RISK-BB-355). An allogeneic unit is not marked. The row stays issuable. The issue gate still refuses another patient's restricted unit.

Cycle 101: Ready to issue shows the product name of each reserved unit (RISK-BB-356). Choosing Issue or Emergency repeats that name on the issue form. Issue eligibility is unchanged.

Cycle 102: Retrospective crossmatch due, in transit, and issued units show that same product name (RISK-BB-357). Receive, Return, and Document repeat it. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 103: Ready to issue shows when each reserved unit expires (RISK-BB-358). Choosing Issue or Emergency repeats that time on the issue form. The near-expiry mark and the outdated hard stop are unchanged.

Cycle 104: Retrospective crossmatch due, in transit, and issued units show when the issued unit expires (RISK-BB-359). Receive, Return, and Document repeat that time. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 105: The patient product list shows the product name and the unit expiration time (RISK-BB-360). Unit choices use that same time. Allocation and issue eligibility are unchanged.

Cycle 106: Ready to issue shows the unit ABO/Rh beside the patient type (RISK-BB-361). Choosing Issue or Emergency repeats that unit type. Issue eligibility is unchanged.

Cycle 107: Retrospective crossmatch due, in transit, and issued units show the unit ABO/Rh beside the patient type (RISK-BB-362). Receive, Return, and Document repeat that type. Those gates are unchanged.

Cycle 108: The patient product list shows the unit ABO/Rh (RISK-BB-363). Allocation and issue eligibility are unchanged.

Cycle 109: Ready to issue shows when a current compatible crossmatch expires (RISK-BB-364). Choosing Issue or Emergency repeats that time. Issue eligibility is unchanged.

Cycle 110: Ready to issue shows the storage location of each reserved unit (RISK-BB-365). Choosing Issue or Emergency repeats that location. Issue eligibility is unchanged.

Cycle 111: The patient product list and unit choices show the storage location (RISK-BB-366). Allocation and issue eligibility are unchanged.

Cycle 112: Ready to issue shows the accession of the current specimen (RISK-BB-367). Choosing Issue or Emergency repeats that accession. Issue eligibility is unchanged.

Cycle 113: In transit and issued units show the specimen accession (RISK-BB-368). Receive, Return, and Document repeat that accession. Those gates are unchanged.

Cycle 114: Retrospective crossmatch due, in transit, and issued units show when a current compatible crossmatch expires (RISK-BB-369). Receive, Return, and Document repeat that time. A unit with no current compatible crossmatch shows none. Those gates are unchanged.

Cycle 115: The patient product list shows when a current compatible crossmatch expires (RISK-BB-370). A unit with no current compatible crossmatch shows none. Allocation and issue eligibility are unchanged.

Cycle 116: Unit choices show when a current compatible crossmatch expires (RISK-BB-371). Selecting a unit repeats that time. A choice with no current compatible crossmatch shows none. Allocation and issue eligibility are unchanged.

Cycle 117: Retrospective crossmatch due, in transit, and issued units show the latest crossmatch result (RISK-BB-372). Receive, Return, and Document repeat that result. A unit with no crossmatch result shows none. Those gates are unchanged.

Cycle 118: Allocated products on the patient chart show the latest crossmatch result beside any current compatible expiration (RISK-BB-373). A unit with no crossmatch result shows none. Allocation and issue eligibility are unchanged.

Cycle 119: Ready to issue shows the latest crossmatch result beside any current compatible expiration (RISK-BB-374). Issue and Emergency repeat that result. A unit with no crossmatch result shows none. Issue eligibility is unchanged.

Cycle 120: Unit choices and the selected unit show the latest crossmatch result beside any current compatible expiration (RISK-BB-375). A unit with no crossmatch result shows none. Allocation and issue eligibility are unchanged.

Cycle 121: Product history shows the latest crossmatch result on assigned, issued, returned, and transfused rows (RISK-BB-376). A crossmatch event shows that event's result. A unit or event with no crossmatch result shows none. Allocation and issue eligibility are unchanged.

Cycle 122: Product history shows the specimen accession linked to an issued, returned, or transfused unit (RISK-BB-377). A row with no linked specimen shows none. Allocation and issue eligibility are unchanged.

Cycle 123: Product history shows when each unit expires (RISK-BB-378). A row with no unit expiration shows none. Allocation and issue eligibility are unchanged.

Cycle 124: Product history shows who performed each event (RISK-BB-379). A row with no performer shows none. Allocation and issue eligibility are unchanged.

Cycle 125: Product history shows why a unit was returned and whether it was marked eligible to reissue (RISK-BB-380). A row that is not a return shows none for those facts. Allocation, return, and reissue eligibility are unchanged.

Cycle 126: Product history shows the person an issued, returned, or transfused unit was issued to (RISK-BB-381). A row with no issued-to person shows none. Issue eligibility is unchanged.

Cycle 127: The in-transit and issued queues show the person each unit was issued to (RISK-BB-382). A unit with no issued-to person shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 128: Receive, Return, and Document repeat the person the unit was issued to (RISK-BB-383). A unit with no issued-to person shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 129: The retrospective crossmatch queue shows the person the unit was issued to (RISK-BB-384). A unit with no issued-to person shows none. Retrospective crossmatch completion is unchanged.

Cycle 130: Product history shows the cooler or transport id for an issued, returned, or transfused unit (RISK-BB-385). A row with no cooler shows none. In-transit and issue eligibility are unchanged.

Cycle 131: Receive, Return, and Document repeat the cooler or transport id (RISK-BB-386). A unit with no cooler shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 132: The outstanding issued queue shows the cooler or transport id (RISK-BB-387). A unit with no cooler shows none. The id stays after ward receipt. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 133: The retrospective crossmatch queue shows the cooler or transport id (RISK-BB-388). A unit with no cooler shows none. Retrospective completion is unchanged.

Cycle 134: The retrospective crossmatch screen repeats who received the unit and which cooler carried it (RISK-BB-389). A unit with neither shows none. Recording the crossmatch is unchanged.

Cycle 135: The outstanding issued queue shows the location the unit was issued to (RISK-BB-390). A unit with no location shows none. The location stays after ward receipt. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 136: Receive, Return, and Document repeat the location the unit was issued to (RISK-BB-391). A unit with no location shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 137: The retrospective queue and follow-up crossmatch screen show the location the unit was issued to (RISK-BB-392). A unit with no location shows none. Retrospective completion and recording the crossmatch are unchanged.

Cycle 138: Product history shows the recorded issue type on issued, returned, and transfused rows (RISK-BB-393). A row that is not one of those events shows none. Issue eligibility is unchanged.

Cycle 139: The in-transit and outstanding issued queues show the recorded issue type (RISK-BB-394). The type stays after ward receipt. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 140: The in-transit and outstanding issued queues show who issued the unit (RISK-BB-395). The name stays after ward receipt. A blank name shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 141: Receive, Return, and Document repeat who issued the unit (RISK-BB-396). A blank name shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 142: The retrospective crossmatch queue shows who issued the unit (RISK-BB-397). A blank name shows none. Retrospective completion is unchanged.

Cycle 143: The retrospective follow-up screen repeats who issued the unit (RISK-BB-398). A blank name shows none. Recording the crossmatch is unchanged.

Cycle 144: The retrospective crossmatch queue shows the recorded emergency-release reason (RISK-BB-399). A blank reason shows none. Retrospective completion is unchanged.

Cycle 145: The retrospective follow-up screen repeats the recorded emergency-release reason (RISK-BB-400). A blank reason shows none. Recording the crossmatch is unchanged.

Cycle 146: Product history shows the recorded emergency-release reason on issued, returned, and transfused rows (RISK-BB-401). A row that is not one of those events shows none. A blank reason shows none. Issue eligibility is unchanged.

Cycle 147: The in-transit and outstanding issued queues show the recorded emergency-release reason (RISK-BB-402). The reason stays after ward receipt. A blank reason shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 148: Receive, Return, and Document repeat the recorded emergency-release reason (RISK-BB-403). A blank reason shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 149: The in-transit and outstanding issued queues show the second verifier recorded at issue (RISK-BB-404). The name stays after ward receipt. A blank second verifier shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 150: Receive, Return, and Document repeat the second verifier recorded at issue (RISK-BB-405). A blank second verifier shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 151: The retrospective crossmatch queue shows the second verifier recorded at issue (RISK-BB-406). A blank second verifier shows none. Retrospective completion is unchanged.

Cycle 152: The retrospective follow-up screen repeats the second verifier recorded at issue (RISK-BB-407). A blank second verifier shows none. Recording the crossmatch is unchanged.

Cycle 153: Product history shows the second verifier recorded at issue on issued, returned, and transfused rows (RISK-BB-408). Other rows and a blank second verifier show none. Issue eligibility is unchanged.

Cycle 154: The retrospective crossmatch queue shows who authorized the emergency or massive-transfusion release (RISK-BB-409). A blank authorizer shows none. Retrospective completion is unchanged.

Cycle 155: The retrospective follow-up screen repeats who authorized the emergency or massive-transfusion release (RISK-BB-410). A blank authorizer shows none. Recording the crossmatch is unchanged.

Cycle 156: The in-transit and outstanding issued queues show who authorized an emergency or massive-transfusion release (RISK-BB-411). The name stays after ward receipt. A blank authorizer shows none. Return, ward-receipt, and bedside identity gates are unchanged.

Cycle 157: Receive, Return, and Document repeat who authorized an emergency or massive-transfusion release (RISK-BB-412). A blank authorizer shows none. Ward receipt, return, and transfusion documentation gates are unchanged.

Cycle 158: Patient product history shows who authorized an emergency or massive-transfusion release on issued, returned, and transfused rows (RISK-BB-413). Other rows and a blank authorizer show none. Issue eligibility is unchanged.

Cycle 159: The in-transit and outstanding issued queues show the appearance recorded at issue (RISK-BB-414). The appearance stays after ward receipt. The issue gate is unchanged.

Cycle 160: Receive, Return, and Document repeat the appearance recorded at issue (RISK-BB-415). Ward receipt, return, transfusion documentation, and the issue gate are unchanged.

Cycle 161: The retrospective crossmatch queue shows the appearance recorded at issue (RISK-BB-416). Retrospective completion is unchanged.

Cycle 162: The retrospective follow-up screen repeats the appearance recorded at issue (RISK-BB-417). Recording the crossmatch is unchanged.

Cycle 163: Patient product history shows the appearance recorded at issue on issued, returned, and transfused rows (RISK-BB-418). Other rows show none. Issue eligibility is unchanged.

Cycle 164: The outstanding issued queue shows the appearance recorded at ward receipt (RISK-BB-419). A unit still awaiting receipt shows none. The ward-receipt gate is unchanged.

Cycle 165: Return and Document repeat the appearance recorded at ward receipt (RISK-BB-420). A unit that has not been received shows none. Return, transfusion documentation, and the ward-receipt gate are unchanged.

Cycle 166: Patient product history shows the appearance recorded at ward receipt on issued, returned, and transfused rows (RISK-BB-421). Other rows, and a unit that has not been received, show none. The ward-receipt gate is unchanged.

Cycle 167: The in-transit and outstanding issued queues show the pickup tag scanned at issue (RISK-BB-422). The code stays after ward receipt. A blank pickup tag shows none. The issue gate is unchanged.

Cycle 168: Receive, Return, and Document repeat the pickup tag scanned at issue (RISK-BB-423). A blank pickup tag shows none. Ward receipt, return, transfusion documentation, and the issue gate are unchanged.

Cycle 169: The retrospective crossmatch queue shows the pickup tag scanned at issue (RISK-BB-424). A blank pickup tag shows none. Retrospective completion is unchanged.

Cycle 170: The retrospective follow-up screen repeats the pickup tag scanned at issue (RISK-BB-425). A blank pickup tag shows none. Recording the crossmatch is unchanged.

Cycle 171: Patient product history shows the pickup tag scanned at issue on issued, returned, and transfused rows (RISK-BB-426). Other rows and a blank pickup tag show none. The issue gate is unchanged.

Cycle 172: The in-transit and outstanding issued queues show the crossmatch status recorded at issue (RISK-BB-427). The status stays after ward receipt. The issue gate is unchanged.

Cycle 173: Receive, Return, and Document repeat the crossmatch status recorded at issue (RISK-BB-428). Ward receipt, return, transfusion documentation, and the issue gate are unchanged.

Cycle 174: The retrospective crossmatch queue shows the crossmatch status recorded at issue (RISK-BB-429). Retrospective completion is unchanged. The follow-up screen and product history still omit that status.

Cycle 175: The retrospective follow-up screen repeats the crossmatch status recorded at issue (RISK-BB-430). Recording the crossmatch is unchanged. Product history still omits that status.

Cycle 176: Patient product history shows the crossmatch status recorded at issue on issued, returned, and transfused rows (RISK-BB-431). Other rows show none. The issue gate is unchanged.

Cycle 177: Electronic crossmatch stays blocked by any antibody-history row, including a deactivated or currently undetectable antibody (OCD-001, RISK-BB-432). Eligibility is otherwise unchanged.

Cycle 178: The dashboard lists retrospective crossmatch due, outstanding issued units, pending testing, and open or under-review reaction investigations for the permissions the signed-in user already has (RISK-BB-433). Closed investigations are not counted. Issue, result, and investigation gates are unchanged.

Cycle 179: The dashboard also lists units still in transit to the ward, and how many of those are past the arrival window already stored on that queue (RISK-BB-434). Ward receipt is unchanged.

Cycle 180: The dashboard says how many retrospective crossmatches are past their stored due time, and how many reaction investigations still need fatality follow-up, including closed cases (RISK-BB-435). Retrospective completion, reaction close, and the fatality follow-up rule are unchanged.

Cycle 181: Collection can stay Collected. Receive and Accept move a tube to Received then Accepted from the pending-specimens worklist (RISK-BB-436). An expired tube cannot be received or accepted. In-lab accession still accepts immediately. Result entry and order linking still require Accepted.

Cycle 182: Staff with `audit.read` record a shift handoff with a required note and a snapshot of the bench queues; a different user acknowledges (RISK-BB-437). Self-acknowledge and double-acknowledge HardStop. Clinical gates are unchanged.

Cycle 183: Specimen barcode changes require Relabel with a required reason and `specimen.edit` (RISK-BB-438). Accession number stays immutable. Rejected, cancelled, and expired tubes cannot be relabeled. Collection metadata edit cannot change the barcode. Clinical gates are unchanged.

Cycle 184: Staff with `immuno.override`, `result.verify`, or `inventory.release` open a supervisor worklist of ABID reviews, pending verifications, and quarantine releases for those permissions (RISK-BB-439). Rows open the existing workflows. Clinical gates are unchanged.

Cycle 185: Changing first, middle, or last name requires a reason and retains the previous display name as `PriorFullName` (RISK-BB-440). Patient list search matches prior names and prior MRNs. Clinical gates are unchanged.

Cycle 186: Inbound ADT that changes a stated name retains the previous display name as `PriorFullName` and audits the change (RISK-BB-441). Sparse ADT blanks do not clear or archive names. Clinical gates are unchanged.

Cycle 187: A new merge writes a ledger of moved, skipped, and demoted rows, including DAT/elution studies, pickup tags, and compatibility decisions. Unmerge requires `patient.merge` and a reason. It restores those rows and deactivates the prior-MRN alias this merge added. A new order, specimen, result, crossmatch, allocation, issue, or transfusion on the survivor since the merge HardStops, as does an edited moved row or a changed deceased or pregnancy field. Merges without a ledger cannot be undone. Rule execution logs stay on the original patient.

## Scoring used for Cycle 1 selection

| Candidate | Safety | Validation | Integrity | Workflow | Architecture | Impl. risk | Notes |
|---|---|---|---|---|---|---|---|
| Session auth vs `X-User` | 5 | 4 | 5 | 3 | 5 | 3 | Impersonation can bypass every privilege HardStop at the HTTP boundary |
| Licensed ISBT tables | 5 | 5 | 5 | 4 | 3 | 5 | Blocked on ICCBBA license |
| Phenotype versioning | 3 | 4 | 5 | 2 | 3 | 3 | OCD-022 |
| HL7 inbound auth | 4 | 3 | 4 | 3 | 4 | 4 | Next after sessions |
