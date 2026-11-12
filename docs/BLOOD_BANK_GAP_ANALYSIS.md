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

## Capability matrix (Cycle 263)

Clinical rows are unchanged from Cycle 251 below, with these updates since then: storage temperature log and automatic quarantine on a sustained excursion (OCD-036), coded discard reasons and wastage, utilization and PBM ordering criteria (engine, plus an inactive unverified draft set since Cycle 275, OCD-038), alert inbox, supplier orders, scheduled backup, and MFA. Cycles 273 to 276 then posted verified eluate findings to antibody history, added an ICCBBA readiness report, an inactive draft PBM set, and a read-only FHIR R4 API. What remains needs a license or SME sign-off: licensed ICCBBA tables (OCD-004), PBM content (OCD-038), and the FHIR terminology mapping (OCD-043).

Cycle 263 adds the UX objectives as rows, because they were not tracked.

| UX capability | Status | Notes |
|---|---|---|
| Search everywhere | Present (Cycle 263; Cycle 696: a unit hit says why it is out of routine use) | Cycle 576: a scanned cooler label finds the cooler while its units are in transit. Cycle 551: the find box offers the user's recently opened charts on focus. Cycle 466: the find box also takes visit and account numbers (opening that visit, saying if it is open) and other recorded patient identifiers; order hits open the order. Cycle 447: chart links land on the named tube, order, or visit's orders (Test History, Visits, pending specimens). Cycle 401: patient rows show ABO/Rh, antibodies, and special requirements in effect. Top-bar find box on every page: DIN, unit number, accession, tube barcode, order number, MRN, prior MRN, partial name. |
| Barcode-first entry | Present | Scanner sanitizing and DIN parsing at receive, issue, release, and now the top bar. One exact scan opens the record. |
| Safe identification on lookup | Present (Cycle 263) | Never opens on a partial or multi-match; merged MRN names the survivor; a malformed `=` scan does not fall back to name search (RISK-BB-519). |
| Keyboard-friendly | Present (Cycle 269) | `/` and Ctrl+K focus the find box; `?` lists shortcuts. At issue, ward receipt, and bedside, Enter moves through identifiers, unit number, and the unit-label scan fields; after the last field focus returns to the form and the action stays a deliberate click (RISK-BB-525). Cycle 429: J and K move between tests on the worklist, cursor in the result entry; keys never save or verify. |
| Two-identifier check at issue and bedside | Present (Cycle 267) | Fields read from the wristband or requisition, not copied from the record, unless the facility turns on `Issue.PrefillPatientIdentifiers` (OCD-039); live match hint (RISK-BB-523). |
| Choices show real identifiers | Present (Cycle 268) | A unit or patient set on a form before its list loads now shows its unit number or name, not an internal record number; unresolved text never selects another record on blur (RISK-BB-524). |
| Attestations made by a person | Present (Cycles 270-271) | Return inspection and unit appearance at issue, ward receipt, and receipt must be recorded by a person (server hard stops, OCD-040); override Authorized by never defaults to the user (OCD-041). |
| Role-based home | Present (Cycle 266) | Focus (Bench, Issue desk, Supervisor, Everything) inferred from permissions and switchable; Start here, Needs attention in every focus, could-not-load never clear, other desks folded (RISK-BB-522). |
| Action-oriented queues | Present | Cycle 399: a Results to verify card counts what this user can verify now and opens To verify. Cycle 385: near-expiry units on the home screen, reserved ones under Needs attention. Cycle 386: each near-expiry row offers Issue or open orders to allocate. Cycle 387: reservations past their end time are flagged and released from Ready to issue. Cycle 388: minimum stock shows open product orders and flags more orders than on hand. Worklists, supervisor queue, alert inbox, near-expiry, ready to issue. |
| Next-step guidance | Present | Patient workflow ribbon and inventory desk ribbon. |
| Timeline views | Present (patient Cycle 264, specimen Cycle 265; Cycle 680: outside-antibody confirmation, infant admission specimen, how an issue was entered) | Patient chart **Timeline** tab (RISK-BB-520). Specimen **History** panel from audit rows with user, workstation, and field changes; a scanned accession opens it (RISK-BB-521). Units have status history and activity. |
| Alert / warning / hard-stop separation | Present | Cycle 656: alerts of one kind raised together are one inbox row, acknowledged together and recorded each. Cycle 653: electronic crossmatch switched off facility-wide shows "Off at this facility" on the chart header instead of a warning on every chart. Cycle 587: a facility-wide electronic crossmatch notice is said once on Ready to issue, not on every row. Cycle 474: an acknowledged alert that becomes more severe is raised again, open. Cycle 473: an open alert is refreshed with what is true now; a shortage covered by timely orders is information, a late one a warning. Cycle 446: the alert inbox filters by kind (most severe first) and shows how long each alert has been open; its own text now lists all nine kinds' closing behavior. Cycle 339: changing an override exception requires a recorded reason. Cycle 340: rule changes in the active compatibility table require a reason. Cycle 341: the issue gate reads the active table, stricter only (OCD-052). Cycle 343: user and role access changes require a reason and keep the before state. Rule severities Pass / Warning / HardStop with override reasons where allowed. |
| Human-readable errors | Present (Cycle 697: the issue gate's not-issuable refusal says why and what to do next) | Cycle 655: specimen and crossmatch expiry on the worklists, issue desk and chart, and the Alerts, Reactions and Deviations times, were stored UTC shown as local; converted, and the guard now catches a matched variable used on the next lines. Cycle 633: unit expiration showed the stored UTC as local on six screens (and two more times); three yes/no selects (excursions only, import Replace, check Outbound) could not be chosen; a broken guard pattern had hidden them. All fixed and guarded. Cycle 628: a field revealed by a click is focused after it renders (sign-in code field, Issue form); a source test refuses focusing it before. Cycle 618: short times on badges and status lines name their day ("Needed tomorrow 07:30"); the Verify-before badge showed the stored UTC time as local; a source test refuses a time without its day. Cycle 605: server messages give times in local time on screen and say UTC in saved text (before: "…12:49:00Z"). Cycle 604: every screen shows times in local time (96 places had shown stored UTC as if local: a specimen expiring in 3 minutes read as 5 hours away); a source test guards it. Cycle 589: status badges and columns in words too ("In process", "On hold", "Shipped out"). Cycle 588: every dropdown choice in words (74 had shown code names, the issue form's issue type and appearance among them). Recent cycles rewrote messages in plain wording. Cycle 278: a request the API cannot read returns a 400 naming the field, not a 500 (Development) or an empty 400 (production). |
| Fits the window | Present (Cycles 566–569) | Cycle 569: a source test fails any new table that is not in the scroll box (it found four more in the result entry panel). Cycle 568: every page measured (109 at 1024 px); every table scrolls inside its card when wider than it, and the chart Orders table folds type and source under the order number. Nothing runs past its card: chart special requirements are short blocks (Cycle 566 fix); chart Specimens puts row buttons under each record; in a narrow card a date-and-time cell breaks between date and time; the wide boards (Issuing desks, Test worklist, Inventory, Reactions) scroll sideways inside their card. Tables holding a searchable dropdown are not wrapped (the scroll box would clip its menu). |

Cycle 264 added the patient timeline, Cycle 265 the specimen history, and Cycle 266 the role-focused home. The remaining UX Partial row is keyboard: there is no shortcut map and no keyboard path through the issue desk. See the iteration log for the next ranking. Cycle 267 found and closed a two-identifier bypass (OCD-039) while inspecting the issue desk.

## Capability matrix (Cycle 251)

Status is relative to a mature transfusion-service LIS. Present means a
working path exists; Partial means the domain exists but a bench workflow
is incomplete. Do not treat Present as AABB/FDA certification.

| Area | Status | Notes |
|---|---|---|
| Patient demographics, MRN, visits, location, provider | Present | Cycle 699: the chart header says when the patient received a unit from a recalled donation with an open recipient notification. Cycle 548: the linked newborn's Rh offered on the mother's delivery RhIG evaluation; Link mother on infants' charts. Cycle 547: mother and infant charts linked (each shows the other's type and antibodies, maternal antibodies noted; link follows merges; OCD-074). Cycle 468: the patient pickers (issue, crossmatch, receipt) say where the patient is. Cycle 467: the patient list and find box say where the patient is (current open visit) and flag another open visit. Cycle 465: the chart header shows the current visit (type, location, attending) and flags more than one open visit. Cycle 400: the patient list shows ABO/Rh, active antibodies, and special requirements in effect; Cycle 402: so do the patient pickers; Cycle 403: demo patient MRN0023 has requirements in effect to show them; Cycle 404: enforced requirements and informational notes are counted apart. Chart + ADT. Cycle 227 does not store an ordering-location code as the name when the name is blank. The list shows the location id. A recorded name stays that name. An HL7 mapping code still uses the location code when none was sent. |
| Alternate identifiers, aliases | Present | Cycle 520: inbound PID-3 takes the MR-typed repetition as the MRN (not just the first) and keeps the other identifiers (enterprise id, account, other facilities) as searchable external identifiers, audited. Cycle 185–186: prior name on chart edit and ADT; prior MRN from merge; search matches aliases |
| Duplicate handling / merge | Present | Cycle 614: unidentified charts open past a facility reminder (default 24 h, OCD-077) are named on the home card with the oldest age; Patients shows each chart's age. Cycle 554: the earlier MRN is marked on In transit, Issued units, and the transfusion form. Cycle 553: units still out under an earlier MRN after a merge are named on the chart and at identify. Cycle 542: an unidentified patient registered from the issue form during emergency release; demo patient. Cycle 541: unidentified emergency patients registered in one step (temporary UNK- MRN, estimated age, OCD-071) and merged into the identified chart from a banner; merge now moves RhIG evaluations. Cycle 524: a reviewed pair can be recorded as different people with a reason (audited); it returns on new evidence. Cycle 523: a Possible duplicates home card for chart mergers, naming pairs that share an external ID. Cycle 522: a shared identifier on the chart links to the other chart; merge keeps one copy on the survivor either way round. Cycle 521: a chart arriving with an external identifier already on another chart is accepted, marked "also on" that MRN, named in the ACK and audit, and paired on Possible duplicates with the reason (every pair now says why). Cycle 425: registration names possible duplicates (same birthday, matching name) and needs a confirmed different person (OCD-060); Cycle 426: a Possible duplicates report over every active chart, merged from the chart. Merge + reason; Cycle 187 unmerge restores the ledger when nothing new has happened since. Pre-ledger merges cannot be undone |
| Orders, priority, ordering location | Present | Cycle 564: an order number from the find box or Blood orders to prepare opens its history. Cycle 563: an order's history on the chart (placed, audited changes, specimens, results, units from reservation to transfusion). Cycle 445: a new chart order takes its ordering location from the visit (exact single match) and offers the visit attending. Test and product orders. Cycle 235 keeps the product code on a new or edited order. A blank name stays the product id and is not replaced by the code. The stored name is unchanged. Cycle 244 keeps the test code on a new or edited order. A blank name stays the test id and is not replaced by the code. The stored name is unchanged. |
| Specimen collection / receipt / accept | Present | Cycle 652: linking a specimen to an order names a tube already drawn or received. Cycle 651: the chart header's no-current-specimen warning names a tube already drawn or received, with a link to receive or accept it. Cycle 637: Pending specimens puts the cursor in the scan field when opened and after Receive/Accept changes. Cycle 515: accepting a collected tube later offers to link it to its waiting orders in one click. Cycle 514: a tube accepted at Specimens-tab accession links to the waiting test orders it suits in the same step. Cycle 513: Specimens-tab accession names the orders waiting for a specimen and their accepted types, starts on a suitable type, and warns on a mismatch. Cycle 502: order entry names the accepted types, marks those that suit every selected test, and says when one tube cannot serve them all. Cycle 501: a test can accept several specimen types (one preferred, prefilled at accession); result entry accepts any of them. Cycle 477: the chart accession form chooses the tube's visit; Specimens shows it. Cycle 476: an accessioned tube is linked to its visit (the order's, or the only open one). Cycle 475: a facility setting (default off, OCD-064) requires the collector before a tube is accepted. Cycle 181: Collected → Received → Accepted; in-lab accession still accepts immediately |
| Specimen rejection, expiration, multiple/historical | Present | Cycle 672: with the neonatal rules on (off by default), an infant's specimen is valid for the admission, ended at discharge (OCD-074, part one). Cycle 638: rejecting a tube on Pending specimens offers Collect a new specimen for the patient. Cycle 615: tubes due for discard are a pull list grouped by place, the oldest place first, each tube with its age past date. Cycle 540: home card for tubes due for discard. Cycle 539: stored tubes discarded by scan, kept while an order or reaction investigation needs them, early discard with a reason, due-for-discard list from the facility retention days (OCD-070). Cycle 538: the bench is offered to store a tube when its last test is done. Cycle 536: one spelling per storage place, known places offered. Cycle 535: a scanned place lists the tubes recorded in it. Cycle 534: the stored place shows on the lookup and on reaction workup tubes; demo tube stored and moved. Cycle 533: tube storage by scan (rack, shelf) with every earlier place kept, shown on the chart and the tube timeline; retention pending (OCD-070). Cycle 457: rejecting a tube sends the EHR a "no result" notice per open test, with the reason and "recollect" (OCD-062). Cycle 366: an expired waiting tube says to reject it, and the dashboard names expired tubes. Cycle 354: a facility can require receipt by scan only (OCD-056, default off). Cycle 308: a tube is received by scanning its accession or label barcode. Cycle 309: accepted by scan too, with draw-to-receipt time on the list. Cycle 307: accession records the identifiers read from the tube label (never copied from the record unless the facility pre-fills) and who drew the specimen and where. Reject with a coded reason from an Admin list (Cycle 290); rejection reports by cause, ward, and collector (Cycle 291); expiry gates; patient list |
| Relabeling controls | Present | Cycle 333: chart specimen edit uses the server's policy expiration (including the alloimmunization-risk window); the override authorizer is never defaulted. Cycle 183: specimen barcode Relabel with required reason; metadata edit cannot change barcode |
| ABO/Rh, ABS, ABID, DAT, antigen, XM | Present | Cycle 707: panel lots expiring within 7 days with no later lot of their kind, and no current primary panel lot, show on the panel lots page, the antibody worklist and a home card. Cycle 706: a lot's antigram can be corrected, with a reason, until a workup uses the lot (replaced cells kept, audited before and after); after that the lot is withdrawn and recreated. Cycle 705: a panel lot's antigram is imported from CSV or Excel (a manufacturer's sheet saved in a documented layout), entered by hand, or downloaded as CSV with a blank template; fetching from manufacturers' sites is not done (customer logins). Cycle 593: a hand-recorded chart type and a unit's type at delivery receipt start unchosen; a unit's type is no longer carried from the previous unit; a source test fails on any type default. Cycle 592: no typing form pre-fills an ABO/Rh or a reaction: the unit retype no longer takes its interpretation from the label it confirms, the chart ABO/Rh panel and bench start unchosen; check cells never count toward a crossmatch suggestion. Cycle 591: the crossmatch result starts unchosen on every entry screen (before: Compatible, kept when mixed-field reactions matched no interpretation), and a positive counted reaction cannot be saved as Compatible (server hard stop, check cells aside). Cycle 590: required reactions start blank at result entry (before: "0", so a form saved untouched recorded negative reactions nobody read); the save names the readings still to enter. Cycles 478–480: the catalog describes numeric results (units, limits, L/H/LL/HH flags) and titers (dilution series, endpoint, score, trend); example tests (titers, fetal bleed, DAT IgG/C3, elution, weak D panel, Rh/Kell phenotype) seeded as drafts (OCD-065). Workup + result paths |
| Elution / special / reflex | Present (Cycle 273) | Cycle 519: a reflex test whose trigger was invalidated or corrected away is flagged for review (not cancelled). Cycle 518: a reflex line records the verified result that added it, carries a Reflex from badge on the worklist, and the bench is told when a verify adds one. Chart DAT / Elution tab (the tab had no screen before Cycle 273). Structured eluate findings; catalog-matched alloantibodies post to antibody history on second-person verification; autoantibody, panreactive, and nonspecific findings never post (OCD-042, RISK-BB-528). |
| Result review, correction, invalidation | Present | Cycle 512: an open reagent lot near expiry with no later lot on hand asks for the next lot to be received. Cycle 511: reagent lots refused when received expired or duplicate, not opened or tested once expired; open and close audited; days left shown. Cycle 510: reagent QC failures stand only while latest; expired open lots flagged on the worklist, card, alert, and at entry. Cycle 499: each result to review for a failed lot is marked reviewed with a note (audited), and the count and alert go down. Cycle 498: a failed reagent QC names and opens the results tested with the lot since it last passed. Cycle 497: worklist lines show the result's reagent lots for the verifier. Cycle 496: the chart shows each result's reagent lots; manual entry warns when a reagent for the test has no open lot or a lot has no passed QC today. Cycle 495: manual results record the reagent lots open for their test; Reagent QC lists the results tested with a lot (lot lookback). Cycle 456: batch verification of plain negative screens and DATs and history-matching ABO/Rh by a second person (OCD-061, policy default off). Cycle 444: Test History names who entered each result and how (manual, instrument, interface), and lists corrected or invalidated results as chains. Cycle 313: a hand-recorded ABO/Rh counts as a determination only when documented (OCD-048). Cycle 311: the second ABO/Rh determination must come from a separate specimen. Cycle 310: correcting a verified ABO/Rh retires its type at once (closes the Cycle 289 known limit). Cycle 289: invalidating a result retires the blood type recorded from it, and a retired type is never evidence; a specimen cannot be rejected while its results are active. Versioned; instrument pending verification |
| ISBT identifiers, codes, attributes, locations | Partial | Cycle 683: Print packing list gives a sheet for the one shipment, with spaces for the receiving blood bank, instead of printing the Ship units page. Cycle 682: a shipped unit's page shows how it was packed and the receiving contact, and a recall notification for it names that contact. Cycle 606: Inventory search lists units still on a shelf by default (discarded and modified units had led the list), and a scanned unit number finds any unit. Cycle 412: Move units moves a batch to one location by scan, with the transfer checks and audit. Cycle 413: a stock count by scan reconciles a location (found, missing, elsewhere, not on hand), audited when finished; Cycle 414: last count per location and recent counts; Cycle 418: mark missing and found here from the count; Cycle 423: missing and damaged units wait on the supervisor queue. Cycle 431: expired units on a shelf are listed and discarded by scan (EXPIRED reason, discard checks); Cycle 432: a home card counts them; Cycle 433: so does the shift handoff. Cycle 434: return shipments to the supplier by scan, with a printed list; a repeat return or repeat missing no longer replaces the recorded reason; Cycle 435: nor does a repeat recall, hold, damaged, discard, or specific quarantine. Cycle 427: a received supplier order shows units received and short per line; Cycle 428: demo supplier order SO-DEMO-001 received 2 short. Parser + import; licensed ICCBBA extract not loaded (OCD-004). Cycle 274 readiness report shows exactly what in use relies on placeholder or unknown codes (development data: 24 placeholder, 1 unknown, no licensed version). Stays Partial until a licensed extract is loaded. |
| Divisions, modifications, pooling, aliquots, irradiate, wash, thaw | Partial | Cycle 685: the thaw prompt states the shelf life that applies for an open-system thaw or a pool and refuses before Yes when it is not set (one shared prompt for the chart and the unit page). Cycle 684: active thaw codes without an open-system or pooled shelf life are named on the settings-to-enter home card, Facility Policies and Auto thaw products, before a thaw is refused. Cycle 667: thawed shelf life per code for an open system and for a pooled unit, refused until set (OCD-068 decided). Cycle 648: Modify Products opened from the menu puts the cursor in the source unit field for the first scan. Cycle 517: thawed shelf life per auto-thaw product code (was a fixed 24 hours for every product), capped at the frozen expiration, stated in the thaw prompt (OCD-068). Cycle 516: a modification records its device; irradiation records the irradiator and dose indicator, refuses an indicator that did not confirm, and can be required by policy (OCD-067). Cycle 188 copies concordant typing. Cycle 189 copies an autologous or directed reservation. Cycle 190 aliquot leaves the parent available and reduces its volume. Cycle 191 requires a known source volume to be fully assigned on divide, and refuses a result volume larger than the source. Cycle 192 refuses a pool that would drop a known source volume. Cycle 193 refuses a pool whose sources are not in the same storage location. Cycle 194 shows the location where a modification result was made, including after the unit is moved. Cycle 195 also says when that result is no longer there. Cycle 196 adds when that move was recorded and why, when a status row says so. Cycle 197 adds who recorded that move. Cycle 198 shows the storage location name on the selected unit and on each pool source. A missing name stays the location id. Cycle 199 shows that same place on each new unit and points a quarantined result at release. Cycle 200 says an aliquot parent stayed in place and names the volume left on it. Cycle 201 names each retired source and the status just read, and does not claim Modified when that read fails. Cycle 202 closes the modify form for a unit that is no longer available. Cycle 203 shows the storage location name on the quality-quarantine board. Cycle 204 shows the recorded volume there too, and does not invent one when it was never recorded. Cycle 205 shows the storage location name on the inventory discrepancy board. Cycle 206 shows that same name on the operational-hold board. Cycle 207 shows that same name on the main inventory search list. A blank or unknown name stays the location id. A unit with no location says none. A location code is not used when the name is missing. Cycle 208 shows the storage location name on the near-expiry board, including the discard row. A blank or unknown name stays the location id. A unit with no location says none. A missing location record does not invent a name. Cycle 209 shows that same name on the unit chart header and on the unit identity. A blank or unknown name stays the location id. A unit with no location says none. A location code is not used when the name is missing. Cycle 210 names the storage locations on a unit status row when the place changed. A blank or unknown name stays the location id. A row that stays in the same place does not say it moved. A missing destination is not invented. Cycle 211 names the patient on an allocation the same way an issue does, and includes the medical record number when it is recorded. A missing or blank name stays the patient id. Cycle 212 names the patient on a crossmatch the same way. The medical record number is included when it is recorded. A missing or blank name stays the patient id. Cycle 213 names the patient on a transfusion the same way. The medical record number is included when it is recorded. A missing or blank name stays the patient id. Cycle 214 names the patient on a return from the issue that return belongs to. The medical record number is included when it is recorded. A missing or blank name stays the patient id. A return whose issue cannot be read does not invent a patient. Cycle 215 names the patient on an order-linked modification from that product order. The medical record number is included when it is recorded. A missing or blank name stays the patient id. A modification whose order cannot be read does not invent a patient. Cycle 216 says a bench modification has no patient order when no product order points at it. A blank reason is omitted. A bench row that a product order does point at does not claim that no order was recorded. Cycle 217 names the patient on that source or result row. The medical record number is included when it is recorded. A missing or blank name stays the patient id. A missing order does not invent a patient. Linked orders that do not name one patient do not pick one. Cycle 218 names that same patient on modification history. The medical record number is included when it is recorded. A missing or blank name stays the patient id. No product order says so. A missing order does not invent a patient. Linked orders that do not name one patient do not pick one. Cycle 219 names that same patient on a status row linked to the modification and does not change the recorded reason. A status row with no modification id does not invent a patient. A negative is copied only when every source was typed negative |
| Equipment register, scheduled checks, out of service | Present | Cycle 708: bench equipment is registered with the facility's checks and intervals, checks are recorded pass/fail and listed when due, and equipment is taken out of service and back with a reason (audited). Cycle 709: a home card counts checks to do, and a modification on registered equipment out of service or retired is refused. Cycle 710: the shift handoff counts checks to do and equipment out of service. Cycle 711: each piece's check history is shown and downloaded as a CSV log. Cycle 712: details and check intervals are changed with a reason, keeping records. Cycle 713: the reagent register refuses a repeated code, and reagents are renamed, retired (no lot on hand) and restored with a reason. Cycle 714: a received or open reagent lot's number or expiry is corrected with a reason. Cycle 715: checks are stopped and equipment retired and restored on the page. | Equipment page | |
| Expiration calculations, status history, transfer | Present | Cycle 704: the unit's Status history notes a transfusion reported over its status (OCD-083). Cycle 686: Return to supplier lists recent returns and prints a return list per return, instead of printing the page. Cycle 668: a shipment to another facility records the container, packing temperature, packer and receiving contact (OCD-072 decided). Cycle 550: shipment references found by the top-bar lookup; shipped units say where they went. Cycle 545: recent shipments by facility and reference with their units, facility suggestions, demo shipment. Cycle 544: units shipped to another facility by scan (receiving facility kept, final status, recall notifies that facility, OCD-072). Cycle 369: locating a missing unit or inspecting a damaged one records what was found. Cycle 368: hold release needs a reason and keeps the hold reason in the audit; expired held units are discarded, not released. Cycle 367: quarantine release follows why the unit is held, and the board names the next step. Cycle 225 keeps the location code on a transfer choice and on a delivery-receipt choice. A blank name stays the location id and is not replaced by the code. Cycle 228 keeps the supplier code on a delivery choice. A blank name stays the supplier id and is not replaced by the code. Cycle 230 shows a blank supplier name as the supplier id on an open delivery and on unit entry. That label is not written onto the unit. The stored name stays unchanged. Cycle 231 shows that supplier id on the unit when the delivery names the supplier. The stored supplier and collection facility stay unchanged. Cycle 232 shows that supplier id on the expected-inbound board when the delivery names the supplier. A unit with no delivery does not invent a supplier. The stored supplier and collection facility stay unchanged. |
| Selection, reserve, assign, XM, issue, emergency, MTP | Present | Cycle 650: Blood orders to prepare say to receive a tube already drawn for the patient instead of drawing again. Cycle 649: Compatibility opened from the chart puts the cursor in the crossmatch unit for the first scan. Cycle 586: the chart's reserve form lists each order once per product and offers only units of the chosen order's product, with how many are in stock. Cycle 573: the worklist and issue desk say when a specimen or crossmatch ends before the order's needed-by time. Cycle 572: Ready to issue shows when each unit's order is needed (passed, needed soon) and lists the earliest first. Cycle 543: reservations no longer needed (ended, or crossmatch and specimen expired) reviewed and released to stock together, each audited. Cycle 439: the ward requests a reserved unit for a destination; requested units lead Ready to issue. Cycle 458: the EHR can make the same request by HL7 (OMB^O27, OCD-063). Cycle 440: the issue form suggests the patient's visit location as the destination; Cycle 441: so does the chart issue panel, which also starts with the request's destination. Cycle 346: emergency or MTP release before a specimen is drawn, group O red cells and AB or A plasma only, under override (OCD-053). Cycle 337: a reserved unit near expiry names the patient on the board and in a warning alert. Cycle 317: a reserved unit that expires ends its reservation, and the alert says to reserve a replacement. Cycle 318: a reservation whose compatible crossmatch has expired shows "Crossmatch expired", not "Ready for issue". Cycle 319: the dashboard Ready to issue card counts them. Cycle 320: the Ready to issue board lists units that can be issued now first, with a count of those needing attention. Cycle 321: no crossmatch on an expired specimen by any path; the chart offers the repeat crossmatch. Cycle 322: retrospective crossmatch on the expired pre-transfusion specimen allowed as an audited exception (OCD-050). Cycle 292: an incompatible crossmatch after emergency or MTP release raises a critical alert naming the patient and whether the unit is issued, transfusing, or transfused, and leads the retrospective board (notification procedure OCD-046). Cycle 280: MTP is an activation (who called it, why, when it ended) with a live board of units per component class; an MTP issue needs an active activation. Cycle 281: facility-defined packs (none shipped, OCD-012) give round guidance on the board; they never block or add a unit. Cycle 282: each container sent is recorded (transport container catalog, temperature, packer, units) with a printable pack report for bedside check-in and provider sign-off (OCD-045). IssueGate. Cycle 220 names the storage location on an issue when the issued-to place is that location's code. A blank name stays the location id. A place that is not one location code stays as typed. A blank place is omitted. A location list that cannot be loaded does not invent a name. Cycle 221 names that storage location on the issuing board and on patient product history. A blank name stays the location id. A place that is not one location code stays as typed. A code that is also another location's name stays as typed. A missing location list does not invent a name. The recorded place is unchanged. The issue form still shows the place as typed. Cycle 222 names that storage location on lookback. A blank name stays the location id. A place that is not one location code stays as typed. A code that is also another location's name stays as typed. A missing location list does not invent a name. The recorded place is unchanged. Cycle 223 keeps a place that already says Location {id} on a receive, return, or transfusion notice and does not prefix Location again. A ward name or a storage location name still says Location before that place. |
| Return, reissue, transfusion, discard, reaction | Present | Cycle 703: a unit reported transfused keeps its status; release, locate, inspection, discard and expiry are refused, and it leaves the Quarantine and Discrepancy boards. Cycle 702: a recall notifies the recipient of a unit the EHR reported transfused over its status, and does not recall it. Cycle 694: a unit reported damaged or missing while issued does not return to stock, and the issuing paths can no longer write a status change the rules do not allow. Cycle 693: a unit quarantined while its issue is open stays in quarantine on return (it had gone to Available), and the Recalled board sends an on-ward unit to its return. Cycle 692: a unit recalled in transit is flagged on In transit and not received on the ward. Cycle 691: a unit recalled while issued is flagged on Issued units, refused at the bedside with the recall named, and returns to quarantine for the recall; a passing return inspection had put it back in Available stock. Cycle 687: a recall by DIN no longer stops at a component it cannot recall (returned to the supplier, modified, expired); the rest are recalled and those are named with why. Cycle 676: the product history says how each issue was entered (late, downtime paper record, outside every outage, infant without a crossmatch). Cycle 666: issuing or starting another unit while a reaction is under investigation needs an override (reason, authorizer), reviewed by a supervisor (OCD-078 decided). Cycle 636: the reaction report states the units issued while the investigation was open, or none. Cycle 635: an investigation lists the units issued to the patient while it was open. Cycle 634: an issue records the reaction investigation open at the time; Product History marks the unit. Cycle 632: the chart header says a reaction is under investigation on every tab, covering the chart's own issue path. Cycle 630: the reactions list and home card name open cases whose patient still has units issued or reserved. Cycle 627: Ready to issue and the Issue form warn of a reaction under investigation before the unit leaves the bank; the Issue form no longer focuses before it has rendered. Cycle 626: Issued units and the Transfusion desk warn before another unit is started for a patient whose reaction is under investigation (OCD-078, warning only). Cycle 624: the bedside reaction answer never bound (a select bound to a bool, Cycle 596), so every Complete and Document on Issue & Transfuse was refused; fixed, guarded, and the finished unit now leaves the form for the next scan. Cycle 623: after a receipt or return, Unit number empties and takes the next scan, and the button cannot resend the last unit. Cycle 622: Ward receipt, Return, and Transfusion put the cursor in Unit number when opened, so the first scan lands. Cycle 620: Review and release to stock leaves units reserved for an order still open unticked, naming the order (all offered units had started ticked). Cycle 612: the Final disposition home card names its oldest unit's age past due and leads under Needs attention; each row says how far past due. Cycle 611: overdue final-disposition rows open the document, return, or receive desk on the unit. Cycle 602: an open MTP with nothing issued for the reminder time (default 240 min, OCD-076) says "Still open" on the banner and raises an alert to close it. Cycle 597: the disposition too (before: Completed). Cycle 596: finishing a transfusion requires an explicit answer to "reaction suspected?" (before: an unticked box recorded "no reaction"); the API refuses a missing answer too. Cycle 389: several units reserved for one order without reopening Allocate (demo-able since Cycle 390). Cycle 376: several units issued to one patient in a run (patient, destination, and cooler kept; next ready unit offered). Cycle 415: a cooler check before dispatch catches units issued under another cooler or not issued, audited when finished; Cycle 416: its result on In transit and at ward receipt. Cycle 417: a cooler coming back is returned in one run (reason kept, each unit inspected). Cycle 419: the shift handoff counts coolers out, not checked, and checked with differences; Cycle 420: so does the home In transit card. Cycle 421: the demo shows each cooler state (checked, differences, not checked). Cycle 377: a cooler received at the ward unit after unit, with the rest of the cooler offered. Cycle 379: the bedside offers the patient's next unit to start or complete. Cycle 380: demo patient MRN0022 shows all three runs; the rest of a cooler or bedside shows as soon as one unit is opened. Cycle 381: pick lists commit a scan on Enter; a unit scanned on Issue fills its reserved patient. Cycle 382: the unit page shows its reservation and issues from there, or says what stops routine issue. Cycle 383: an issued unit's page shows where it went and its next step (receive, start, complete, return); a transfusion started without a recorded receipt is offered Complete on the board too. Cycle 384: timelines show the crossmatch result (previously always NotPerformed) and each ward receipt. Cycle 370: closing records the reaction concluded and the treatment from admin lists with No reaction and No treatment (OCD-057). Cycle 365: the reaction board shows one next step per case and lists the most urgent first. Cycle 364: a special requirement can be added from a reaction investigation and is linked to it. Cycle 363: a reaction noticed after the transfusion can be reported from the chart and opens the investigation. Cycle 332: the unit chart offers Available on return only when the facility window and location allow it, as the return does. Cycle 306: reports, lookback, unit activity, and utilization never report a running transfusion as finished. Cycle 305: the chart and timeline show a running transfusion as started and in progress, with Complete on the row. Cycle 304: a started transfusion not completed within the facility reminder hours raises an alert and is flagged on the board. Cycle 299: a transfusion is started at the bedside (identity checks then) and completed later; the unit shows Transfusing since in between. Cycle 298: a transfusion is judged against the unit at its start time (documentable after expiry if started in date; a start before issue is refused). Cycle 297: after a return to stock, one step reserves the unit again for the same patient through the normal reservation. Cycle 296: a return never disposes of a unit; it quarantines, and disposal goes through the coded discard with its permission and second verifier. Cycle 285: the one-click "mark transfused" shortcut that skipped every bedside check is removed; chart links open bedside documentation; times and volume must be consistent. Cycle 283: a signed reaction investigation is locked; changing it needs a reopen with a reason and a new signature, and its written conclusions are now in the audit. Cycle 279: a unit returned, or released from quarantine or hold, before its required retype goes back to Received for retype; a unit whose verified retype contradicts its label cannot be issued, even in an emergency |
| Compatibility, special requirements, eXM, warning vs hard-stop | Present | Cycle 674: group O red cells to an infant with a negative screen need no crossmatch under the neonatal rules (OCD-074 complete; other groups OCD-082). Cycle 673: an infant's ABO/Rh from the admission specimen stands without a second determination for routine red-cell issue (OCD-074, part two; neonatal rules off by default). Cycle 654: a test ordered from the EHR that repeats an open one raises an alert (the warning was computed and dropped). Cycle 595: electronic crossmatch requires a verified negative antibody screen on the crossmatch specimen (eligibility: on a current specimen); before, "no positive screen on file" passed with no screen at all, and a pre-ticked box attested the screen. Cycle 585: requirement hard stops at reserve and issue name the requirement as the chart does, never by its code. Cycle 566: one wording for a requirement on every screen ("Antigen-negative (K)", "CMV-negative"; never a code name). Cycle 463: duplicate testing warned at order entry (ORD-DUP-TEST); order warnings now reach the chart. Cycle 408: a printable, audited reaction investigation report for the medical director. Cycle 422: the home Reactions card names cases waiting for the post-transfusion specimen and with workup tests to verify. Cycle 409: workup lines are marked on the bench and link to the investigation. Cycle 407: transfusion reaction workups test a pre- and a post-transfusion specimen by Test Group (setting), resulted on the bench; closing needs them verified. Cycle 405: reserving from the Compatibility page checks special requirements too (before: only the chart and issue), with the requirements shown for the chosen patient. Cycle 406: its unit picker offers the compatible units first; Cycle 430: so does Record crossmatch, with the patient's reserved units. Cycle 373: special requirements by unit category (unit typings incl. ABO group, RhD negative, CMV/HbS by product code or unit test; unit modifications; informational banner; issue procedures), emergency release only under override, facility-day start and end dates. Cycle 374: CMV/HbS results recorded on the unit page, latest result counts, corrections with a reason. Cycle 375: units reserved before a requirement show what they lack on the chart. OCD-001 closed. Per-product ABO and Rh grids apply at issue and allocation. Cycle 277: an emergency override can no longer release red cells that conflict with an established ABO group (OCD-044) |
| Antibody/antigen history, phenotype, genotype, alerts | Present | Cycle 678: a reviewed antibody ID workup identifying an outside-reported antibody confirms the report. Cycle 670: unconfirmed outside reports are listed on Antibody ID and counted on a home card. Cycle 669: an outside antibody report must be documented (copy or phone) and reads "not yet confirmed here" until this laboratory confirms it (OCD-073 decided). Cycle 645: Complete workup says whether it waits for the interpretation, review, or a new interpretation after a rejection, instead of failing on the click. Cycle 644: supervisor review waits for an interpretation and tells the interpreting technologist before Accept that another supervisor must accept. Cycle 643: a specificity typed into the interpretation is listed as the catalog antibody it will post as, or marked as outside the catalog. Cycle 642: the demo data has a workup in progress (MRN0004, ACC-DEMO-ABID1) for walking the antigram. Cycle 641: the antigram sets the cells still NT in one phase to 0 in one step (Rest 0), outlined until saved (OCD-079). Cycle 640: a new workup selects the only in-date panel lot and says why when there is none. Cycle 594: re-saving unchanged antibody identification grades no longer rewrites them or clears the interpretation and review; a changed grade also gets a workup-level audit naming the grade replaced (rows were already captured by the generic entity audit). Cycle 584: the issue desk, bench worklist, and reaction investigation name outside antibody reports too (one wording, guarded by a source test). Cycle 583: brief antibody lists (patient list, find box, Blood orders, downtime) name outside reports, one definition. Cycle 582: the chart's antibody warning names outside reports (facility, date identified); safety banners tested as rendered. Cycle 546: antibodies reported by another facility recorded with the facility and date identified, shown as outside reports on the chart and antibody card (OCD-073). Cycle 437: a printable patient antibody card (type, antibodies, requirements), print audited. Genotype vs phenotype tokens (OCD-024) |
| Worklists: tests, retype, near-expiry, issue, reactions, inbound | Present | Cycle 698: the Recalled units to pull card says the oldest recall's age and how many are on the ward. Cycle 690: a recalled unit found is scanned on the Recalled board to quarantine it for the recall. Cycle 688: recalled units not yet pulled have an Inventory board and a home card (an issued one is marked on the ward). Cycle 619: every collect-a-specimen next step (Ready to issue, Retrospective, Blood orders) opens the accession form. Cycle 613: Second ABO/Rh rows say whether an ABO/Rh is already ordered (linked) or order one, and the home card names patients with nothing ordered. Cycle 609: the Retype board opens a unit by scanning its label. Cycle 607: the Near expiry board's discard works (it sent free text as the reason code and the verifier as the comment) with coded reasons and a verifier, after the use-it actions. Cycle 600: timelines say what became of each reservation in words, with the expiry reason. Cycle 599: Blood orders to prepare names the reserved unit an order lost to expiry. Cycle 598: when reserved units expire, the alert names each unit and patient and opens Blood orders to prepare (before: a count). Cycle 574: the blood order next step is a new specimen when none lasts until the needed-by time. Cycle 570: blood orders needed soon (facility warning window, OCD-075, off by default). Cycle 565: Blood orders to prepare names each patient's special requirements. Cycle 561: the reserve form shows the order's progress and leads back to the worklist when covered. Cycle 558: needed-by time changed from the order row, audited. Cycle 557: orders carry a needed-by time (chart entry, HL7 TQ1-7); blood orders prepared by it. Cycle 556: Reserve N units opens the reserve form on the order and specimen. Cycle 555: blood orders to prepare across patients (still needed, specimen, next step; home card). Cycle 509: the home Shift handoff card names the open handoff's urgent items. Cycle 508: handoff queues link to their boards, urgent first. Cycle 507: the handoff page previews the counts before recording. Cycle 506: the handoff also counts open HL7 errors and unmapped interface values. Cycle 505: the shift handoff counts open instrument failures (held results) and failed reagent lots (results to review). Cycle 410: lines on a rejected tube say Recollect with the reason, and rejecting names the orders waiting; Cycle 411: a Recollect filter and the home card count them. Cycle 453: a Reagent QC home card on the bench names failed and overdue QC. Cycle 454: when policy blocks resulting during open QC, the worklist says so before anything is keyed. Cycle 455: daily QC by lot scan, the last expected result offered, and 14 days of QC history. Cycle 424: a No specimen view (STAT first), and the home card names STAT/urgent orders still without a tube. Cycle 396: scan a tube to show and open its tests; accepting a tube offers Start testing. Cycle 397: the label barcode works too. Cycle 391: manually entered results waiting for verification say Verify on the bench (before: Enter). Cycle 392: a To verify filter shows only what this user can verify now. Pending specimens Cycle 181; supervisor queue Cycle 184. Cycle 243 puts ready to issue, second ABO, retrospective crossmatch, in transit, outstanding issued, final disposition, pending tests, pending specimens, reactions, the supervisor queue, and an open shift handoff on the dashboard for the permissions that can already open them. A failed load stays an em dash. |
| Assigned products / outstanding transfusions | Present | Ready to issue; outstanding issued. Cycle 224 keeps a blank storage location name as the location id on Ready to issue and on reserved products. A location code is not shown. A unit with no location stays omitted. A location list that cannot be loaded does not invent a name. |
| Shift handoff / supervisor queues | Present | Cycle 689: the handoff counts recalled units not yet pulled (urgent, opening the Recalled board). Cycle 679: the handoff counts overdue final dispositions and outside antibodies to confirm. Cycle 647: a home card counts open antibody workups and names those needing attention. Cycle 646: the shift handoff counts open antibody workups and those waiting for review. Cycle 629: the handoff makes Reaction investigations urgent and names those whose patient still has units out or reserved. Cycle 603: the handoff names idle massive transfusion protocols ("n idle, to close"). Cycle 571: the handoff names blood orders needed soon (urgent). Cycle 562: blood order turnaround reports (order to first unit reserved and issued, ready by needed-by, by priority). Cycle 559: the handoff names blood orders to prepare, urgent when past needed-by. Cycle 552: the handoff names unidentified patients still to merge (urgent) and tubes due for discard. Cycle 472: a submitted supplier order past its arrival window is marked overdue (order, Minimum stock, home card). Cycle 471: units on submitted supplier orders show as on order; a new order drafts only what is still to order. Cycle 470: the shift handoff counts products below minimum stock. Cycle 469: a Below minimum stock home card names the largest shortfalls (issue desk, supervisor). Cycle 436: test turnaround reports (order and receipt to verification, by priority). Cycle 393: the handoff counts results waiting for verification, reservations past their end, and near-expiry units (reserved). Cycle 352: an override authorizer outside the directory can be added as an outside provider, and review shows it as such (OCD-055). Cycle 351: an incompatible retrospective crossmatch opens a critical deviation automatically (OCD-046). Cycle 312: the handoff counts running and overdue transfusions and incompatible retrospective crossmatches; Cycle 442: overdue completions lead Issued units and are named on the home card. Cycle 443: the Supervisor card names results due before specimen expiry, missing or damaged units, and overrides to review. Cycle 460: the queue marks those deadlines, links rows to the chart, and opens on one kind from a link. Cycle 449: an Open deviations card (critical named) for deviation managers. Cycle 450: unit and patient pages list their linked deviations and record one already linked. Cycle 451: the deviation form links its record by scan or identifier. Cycle 452: the shift handoff counts open deviations and leads with critical ones. Cycle 294: an override under massive transfusion is reviewed as a massive-transfusion release. Cycle 293: each override records whether its authorizer matched the provider or user directory, shown in the queue. Cycle 288: overrides get a second-person review in the supervisor queue (`override.approve`). Cycle 286: handoff needs the bench permission `shift.handoff` (not `audit.read`), the recipient is checked against the directory, and active MTPs lead the packet. Cycle 182 handoff; Cycle 184 permission-scoped supervisor worklist. Cycle 243 shows an open handoff and the supervisor count on the dashboard when those permissions are already held. |
| Admin catalogs, rules, users, roles, locations, audit config | Present | Cycle 677: settings decided but not entered (RhIG dose, retention days) are marked on Facility Policies and counted on an administrator home card. Cycle 664: a tube label design must carry two patient identifiers to be saved or print (OCD-080 decided); aliquot designs warn. Cycle 659: the label designer warns about overlapping elements and text off the edge, outlining the overlaps. Cycle 658: the label designer test prints the layout as it stands, on screen or to a printer, with sample data that cannot pass for a patient. Cycle 657: a specimen label designer: several tube and aliquot designs, each its own size, the barcode and data elements placed on a canvas of the label, one default per purpose, chosen at print time; aliquot labels print one per aliquot (OCD-080). Cycle 438: user access review (last sign-in, dormant accounts, recorded review). Cycle 371: storage location edits and activation record a reason; Cycle 372: so do transport containers and MTP packs. Cycle 358: every facility policy default in code matches Facility Policies (test-checked); the second verifier now defaults on, as the page showed. Cycle 359: missing settings are stored at their defaults at startup, with history. Single live catalog row (OCD-033). Cycle 226 keeps the code in the inventory-location catalog. A blank name on that list stays the location id. The stored name and the edit form are unchanged. Cycle 229 keeps the code in the blood-supplier catalog. A blank name on that list stays the supplier id. The stored name and the edit form are unchanged. Cycle 233 keeps the product code on a modification-rule choice and on a charge-rule product choice. A blank name stays the product id and is not replaced by the code. The stored name is unchanged. Cycle 234 keeps the code in the product catalog. A blank name on that list stays the product id. The stored name and the edit form are unchanged. Cycle 245 keeps the code in the test catalog. A blank name on that list stays the test id. The stored name and the edit form are unchanged. Cycle 246 keeps the code in the subtest catalog. A blank name on that list stays the subtest id. The stored name and the edit form are unchanged. Cycle 247 keeps the code in the test-grouper catalog. A blank name on that list stays the grouper id. The stored name and the edit form are unchanged. Cycle 248 keeps the code in the phase catalog. A blank name on that list stays the phase id. The stored name and the edit form are unchanged. Cycle 249 keeps the code in the blood-attribute catalog. A blank antigen name stays the attribute id. A blank antibody name stays that same id and is not replaced by the antigen name or the code. The stored names and the edit form are unchanged. Cycle 250 keeps the code in the special-requirement catalog. A blank name on that list stays the requirement id. The stored name and the edit form are unchanged. Cycle 251 keeps the code in the reflex-rule catalog. A blank name on that list stays the reflex id. The stored name and the edit form are unchanged. |
| HL7 ADT/ORM/ORU, BPAM, retry/error, downtime snapshot | Partial | Cycle 701: such a transfusion reads so on the patient's product history and timeline, and the supervisor card counts those to review. Cycle 700 (OCD-083 option B): an EHR transfusion for a unit recalled, quarantined, damaged or missing is recorded, the unit keeps its status, and a supervisor reviews it on the supervisor queue. Cycle 695: such a message had been held in the error queue. Cycle 665: a back-entry outside every recorded outage needs a reason, kept on the issue (OCD-081 decided). Cycle 663: an outage left open shows on the home screen (Issue desk, Supervisor) and the shift handoff until ended. Cycle 662: outages are recorded (start, end, what was down) on Downtime Snapshot; back-entry fills its reason from the outage and warns on a paper time outside every outage (OCD-081); each outage counts its back-entries. Cycle 661: the shift handoff counts them too. Cycle 660: outbound results and charges that have not gone (no interface, retries spent) are listed with why on the HL7 page, read Queued, and count on a home card after 15 minutes. Cycle 617: instrument messages show their date when not from today (time only had read a day-old failure as today's); open failures and the home card say how long they have waited. Cycle 616: the error queue and home card say how long each error, and the oldest, has waited. Cycle 560: the downtime patient sheet lists blood orders still to prepare with needed-by. Cycle 549: ADT PID-21 links a newborn to the registered mother (never replacing a staff link). Cycle 504: FHIR Specimen (state, rejection reason, validity), referenced by results and reports. Cycle 503: FHIR DiagnosticReport per order (partial, final, corrected). Cycle 500: the FHIR API serves verified test results as Observations with the test's LOINC code, quantity, range, and flag (corrected and entered-in-error marked). Cycle 494: the Instrument failures card says how many results are held and opens the open failures; the Instruments page filters All / Open failures / Held and marks held rows. Cycle 493: each error code's action and meaning come from the instrument's error code table (OCD-066 decided). Cycle 492: a result held for an instrument error code can be released after review with a reason (result verifiers), posting it with the held code shown, instead of being retyped. Cycles 488–491: instrument translations per instrument (order and result test codes, subtest codes, values, interpretations, error codes as Hold/Warn/Ignore, OCD-066); unknown or unordered instrument codes no longer post (and never to another test's line); instrument values on the shared Unmapped list and home card. Cycle 487: translation rows imported from a spreadsheet paste or CSV (bad lines named) and exported as CSV, still saved with a reason. Cycles 483–486: interface translations for all interfaces or one interface's overrides, with a reason; test and grouper procedure codes (grouper wins; TNS ↔ LAB10117 seeded; unknown order codes go to the error queue); race, ethnicity, and patient class in and out; OBX-8 flag translated; an Unmapped values list and home card; Translations page with search, local value lists, and Test a value. Cycle 482: a LOINC code per test goes to the EHR in OBX-3 beside the local code. Cycle 481: numeric results go to the EHR with OBX-6 units, OBX-7 reference range, and OBX-8 flag. Cycle 395: the downtime snapshot also counts results to verify (and due before specimen expiry) and lapsed reservations. Cycle 398: Print summary on the chart prints one patient's sheet. Cycle 394: a printable downtime patient summary (type, second determination, antibodies, special requirements, specimens, reserved and issued units) for every patient being worked for, print audited. Cycle 378: outbound BPS^O29 tells the EHR when a unit is dispensed or returned (with an enabled BPAM endpoint). Cycle 362: dashboard cards and the downtime snapshot count open instrument failures and HL7 errors. Cycle 361: failed instrument messages are replayed only while open and are closed by a successful replay or a reason, audited. Cycle 360: every closed interface error records why (manual reason, replay, or acknowledged resend), automatic closes are audited, and the HL7 page lists recently resolved errors. Cycle 315: outbound results carry OBX-11 F/C/W. Cycle 316: corrections (C) and withdrawals (W) of verified results go to the EHR automatically when a results interface is enabled (OCD-049); first final results stay on request. Cycle 323: instrument and interface results apply the manual specimen gate on re-sends and replacements too. Cycle 324: corrections can be completed after the specimen expires (OCD-051, confirmed). Cycle 326: results waiting for verification show a deadline before their specimen expires and are listed first. Cycle 327: the dashboard Pending tests card counts them. Cycle 328: so does the shift handoff. Cycle 329: the chart Tests tab shows the deadline too. Cycle 330: the chart header's "Second ABO on file" uses the same determinations as issue (same specimen and undocumented manual entries do not count). Cycle 353: a paper issue whose unit or specimen expired since can be back-entered by a role that allows it, judged as of the paper time (OCD-054); Cycle 355: its ward receipt and transfusion can be documented from the paper DIN; Cycle 356: the receipt keeps the paper time and In transit no longer shows it overdue; Cycle 357: the downtime snapshot counts back-entries still to complete. Cycle 287: issues entered after the fact are late entries with a reason and both times. v2 present; read-only FHIR R4 API since Cycle 276 (OCD-043, local codes until mapped); MLLP transport-trust |
| Audit of clinically important actions | Present | Cycle 461: a record's audit trail is found by scanning its identifier, and opened from the unit page. Cycle 448: a deviation closes only with its corrective action, which is never blanked; reopening keeps the earlier close in the audit. Append-only hash chain; no purge |

Cycle 186 implemented ADT prior-name archive on inbound demographics updates (RISK-BB-441).

Cycle 187 records a patient-merge ledger and lets staff with `patient.merge` undo that merge when the survivor has had no new clinical activity (RISK-BB-442). Merges already in the database have no ledger and cannot be reversed.

Cycle 188 copies recorded unit antigen and antibody typing onto divide, pool, and single-modification results (RISK-BB-443). A pool stops when sources disagree. A negative result is copied only when every source was typed negative.

Cycle 189 copies an autologous or directed reservation onto each modification result (RISK-BB-444). A pool stops when the sources do not share that reservation, or when a restricted unit has no intended recipient.

Cycle 190 removes an aliquot from a parent unit without retiring the parent (RISK-BB-445). The parent stays available at the reduced volume. The aliquot is a new unit.

Cycle 191 refuses a divide that would retire a source while leaving recorded volume unassigned, and refuses a single modification whose result volume is larger than the source (RISK-BB-446). The source stays available. A source with no recorded volume still stores the volumes that were entered.

Cycle 192 refuses a pool that mixes a recorded volume with a missing or non-positive volume (RISK-BB-447). The sources stay available. When every source has a volume, the pool volume is the sum. When none do, the pool volume stays unrecorded. The modify audit records the source volumes and the result volumes.

Cycle 193 refuses a pool whose sources are not in the same storage location (RISK-BB-448). The sources stay available. Units that share a location, or that are all unassigned, can still be pooled, and the result stays at that location. The modify page shows each location. The modify audit records the source and result location ids.

Cycle 194 shows, on modification history, the storage location where each result was made (RISK-BB-449). A later move does not change that location. A result with no location shows none. The status history row for the new unit records the modification id.

Cycle 195 says when a modification result is no longer at the location where it was made (RISK-BB-450). History still names the original place, and adds the current place only when they differ. A unit that is still there does not get that note.

Cycle 196 adds the time and reason from the status row that put the result in its current place (RISK-BB-451). A move with no status row still names the new place and does not invent a time or reason. A unit that is still where it was made does not get that note.

Cycle 197 adds who recorded that move (RISK-BB-452). A blank name is omitted. A move with no status row still does not invent a person.

Cycle 198 shows the storage location name for the selected unit and for each unit in a pool (RISK-BB-453). A blank or unknown name stays `Location {id}`. A unit with no location still shows none. A location list that cannot be loaded does not invent names.

Cycle 199 shows that place on each new modification result and, when the result is in quarantine, says to release it before issue (RISK-BB-454). Staff who can release get a link that opens that unit's release row. An assigned result does not get a release step. A unit that is not on the quarantine board does not open a release row.

Cycle 200 says, after an aliquot, that the parent stayed in place and how much volume remains (RISK-BB-455). The status and volume come from a fresh read of the parent. A missing volume is not invented. If that read fails, the card does not invent a status or a remaining volume.

Cycle 201 says, after a divide, pool, or other retiring modification, that each source was retired and shows the status just read (RISK-BB-456). A failed read does not claim the source is Modified. An aliquot does not use this line.

Cycle 202 stops offering another modification after the source is retired (RISK-BB-457). A Modified unit has no eligibility list. An assigned unit stays listed for a product-order action and is not offered from the ordinary modify screen. If the retired unit cannot be reread, the form closes and does not keep the previous rules.

Cycle 203 shows the storage location name on each quality-quarantine row, including the release row (RISK-BB-458). A blank name stays the location id. A unit with no location says none is recorded. A missing location record does not invent a name.

Cycle 204 shows the recorded volume on each quality-quarantine row and on the release row (RISK-BB-459). A unit with no volume says so. A zero volume is shown as recorded.

Cycle 205 shows the storage location name for a missing or damaged unit (RISK-BB-460). A blank or unknown name stays the location id. A unit with no location says none. A missing location record does not invent a name.

Cycle 206 shows the storage location name for a unit on operational hold (RISK-BB-461). A blank or unknown name stays the location id. A unit with no location says none. A missing location record does not invent a name.

Cycle 207 shows the storage location name on the main inventory search list (RISK-BB-462). A blank or unknown name stays the location id. A unit with no location says none. A location code is not used when the name is missing. A location list that cannot be loaded does not invent names.

Cycle 208 shows the storage location name for a unit on the near-expiry board (RISK-BB-463). A blank or unknown name stays the location id. A unit with no location says none. A missing location record does not invent a name. The discard row repeats that place.

Cycle 209 shows the storage location name on the unit chart (RISK-BB-464). The header and the unit identity use the same label. A blank or unknown name stays the location id. A unit with no location says none. A location code is not used when the name is missing. A location list that cannot be loaded does not invent names.

Cycle 210 names the storage locations on a unit status row when the recorded place changed (RISK-BB-465). A blank or unknown name stays the location id. A row that stays in the same place, including both unassigned, does not say it moved. A missing destination is not invented. A location list that cannot be loaded does not invent names.

Cycle 211 names the patient on a unit allocation the same way an issue does (RISK-BB-466). The medical record number is included when it is recorded. A missing patient record, or a blank name, stays the patient id and does not invent a name.

Cycle 212 names the patient on a unit crossmatch the same way (RISK-BB-467). The medical record number is included when it is recorded. A missing patient record, or a blank name, stays the patient id and does not invent a name.

Cycle 213 names the patient on a unit transfusion the same way (RISK-BB-468). The medical record number is included when it is recorded. A missing patient record, or a blank name, stays the patient id and does not invent a name.

Cycle 214 names the patient on a unit return from the issue that return belongs to (RISK-BB-469). The medical record number is included when it is recorded. A missing patient record, or a blank name, stays the patient id and does not invent a name. A return whose issue cannot be read does not invent a patient.

Cycle 215 names the patient on an order-linked modification from that product order (RISK-BB-470). The medical record number is included when it is recorded. A missing patient record, or a blank name, stays the patient id and does not invent a name. A modification whose order cannot be read does not invent a patient. A bench modification that is not tied to an order is unchanged.

Cycle 216 says when a bench modification has no product order pointing at it (RISK-BB-471). The row keeps the modification type, the source or result role, and the recorded reason, then says no patient order was recorded. A blank reason is omitted. A bench row that a product order does point at keeps the type, role, and reason, and does not claim that no order was recorded. That row does not invent a patient.

Cycle 217 names the patient on a bench modification when a product order points at it (RISK-BB-472). The medical record number is included when it is recorded. A missing patient record, or a blank name, stays the patient id and does not invent a name. A missing order does not invent a patient. Linked orders that do not name one patient do not pick one. A blank reason is still omitted.

Cycle 218 names that patient on modification history (RISK-BB-473). The medical record number is included when it is recorded. A missing patient record, or a blank name, stays the patient id and does not invent a name. No product order says so. A missing order does not invent a patient. Linked orders that do not name one patient do not pick one.

Cycle 219 names that patient on a status row linked to the modification (RISK-BB-474). The recorded reason is unchanged. The medical record number is included when it is recorded. A missing patient record, or a blank name, stays the patient id and does not invent a name. No product order says so. A missing order does not invent a patient. A status row with no modification id does not invent a patient. Linked orders that do not name one patient do not pick one.

Cycle 220 names the storage location on an issue when the issued-to place is that location's code (RISK-BB-475). A blank name stays the location id. A place that is not one location code stays as typed. A code that is also another location's name stays as typed. A blank place is omitted. The recorded place is unchanged. A location list that cannot be loaded does not invent a name.

Cycle 221 names that storage location on the issuing board and on patient product history (RISK-BB-476). A blank name stays the location id. A place that is not one location code stays as typed. A code that is also another location's name stays as typed. A missing location list does not invent a name. The recorded place is unchanged. The issue form still shows the place as typed.

Cycle 222 names that storage location on lookback (RISK-BB-477). A blank name stays the location id. A place that is not one location code stays as typed. A code that is also another location's name stays as typed. A missing location list does not invent a name. The recorded place is unchanged.

Cycle 223 keeps a place that already says Location {id} on a receive, return, or transfusion notice (RISK-BB-478). It does not prefix Location again. A ward name or a storage location name still says Location before that place. A blank notice stays omitted.

Cycle 224 keeps a blank storage location name as the location id on Ready to issue and on reserved products (RISK-BB-479). A location code is not shown. A unit with no location stays omitted. A location list that cannot be loaded does not invent a name.

Cycle 225 keeps the location code on a transfer choice and on a delivery-receipt choice (RISK-BB-480). A blank name stays the location id and is not replaced by the code. A missing code stays the place alone.

Cycle 226 keeps the code in the inventory-location catalog (RISK-BB-481). A blank name on that list stays the location id. The stored name and the edit form are unchanged.

Cycle 227 does not store an ordering-location code as the name when the name is blank (RISK-BB-482). The list and the visit or order choice show the location id. A recorded name stays that name. An HL7 mapping code still uses the location code when none was sent. A name already stored as the code is not rewritten.

Cycle 228 keeps the supplier code on a delivery choice (RISK-BB-483). A blank name stays the supplier id and is not replaced by the code. A recorded name stays that name. The stored name is unchanged.

Cycle 229 keeps the code in the blood-supplier catalog (RISK-BB-484). A blank name on that list stays the supplier id. The stored name and the edit form are unchanged.

Cycle 230 shows a blank supplier name as the supplier id on an open delivery and on unit entry (RISK-BB-485). That label is not written onto the unit. The stored name stays unchanged.

Cycle 231 shows that supplier id on the unit when the delivery names the supplier (RISK-BB-486). The stored supplier and collection facility stay unchanged. A unit with no delivery does not invent a supplier.

Cycle 232 shows that supplier id on the expected-inbound board when the delivery names the supplier (RISK-BB-487). A unit with no delivery does not invent a supplier. The stored supplier and collection facility stay unchanged.

Cycle 233 keeps the product code on a modification-rule choice and on a charge-rule product choice (RISK-BB-488). A blank name stays the product id and is not replaced by the code. The stored name is unchanged. The charge-rule choice does not use an ISBT code as the name.

Cycle 234 keeps the code in the product catalog (RISK-BB-489). A blank name on that list stays the product id. The stored name and the edit form are unchanged.

Cycle 235 keeps the product code on a new or edited order (RISK-BB-490). A blank name stays the product id and is not replaced by the code. The stored name is unchanged.

Cycle 243 puts the existing bench queues on the dashboard (RISK-BB-498). Each card is limited to a permission that can already open that queue. A failed load stays an em dash. Retrospective due time, in-transit arrival, and fatality follow-up are named only when that stored count is above zero. Issue, ward receipt, result, and investigation gates are unchanged.

Cycle 244 keeps the test code on a new or edited order (RISK-BB-499). A blank name stays the test id and is not replaced by the code. The stored name is unchanged. The checkbox still submits the test code.

Cycle 245 keeps the code in the test catalog (RISK-BB-500). A blank name on that list stays the test id. The stored name and the edit form are unchanged.

Cycle 246 keeps the code in the subtest catalog (RISK-BB-501). A blank name on that list stays the subtest id. The stored name and the edit form are unchanged.

Cycle 247 keeps the code in the test-grouper catalog (RISK-BB-502). A blank name on that list stays the grouper id. The stored name and the edit form are unchanged.

Cycle 248 keeps the code in the phase catalog (RISK-BB-503). A blank name on that list stays the phase id. The stored name and the edit form are unchanged.

Cycle 249 keeps the code in the blood-attribute catalog (RISK-BB-504). A blank antigen name stays the attribute id. A blank antibody name stays that same id and is not replaced by the antigen name or the code. The stored names and the edit form are unchanged.

Cycle 250 keeps the code in the special-requirement catalog (RISK-BB-505). A blank name on that list stays the requirement id. The stored name and the edit form are unchanged.

Cycle 251 keeps the code in the reflex-rule catalog (RISK-BB-506). A blank name on that list stays the reflex id. The stored name and the edit form are unchanged.

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
| 11 | P7 | RhIG workflow missing | Low (evaluation record, Cycle 525; Cycle 681: the open list shows the suggestion and dose; Cycle 675: weak D / partial D recorded and treated as Rh negative; Cycle 671: suggested candidacy and calculated dose from facility settings, OCD-069 decided; settings and weak/partial D policy pending the medical director) | **Started Cycle 525** (Cycle 526: open-evaluations list and home card; Cycle 527: RhIG given lot and vials, To give on the list; Cycle 528: lot recall trace and timeline events; Cycle 529: on the shift handoff; Cycle 530: demo data; Cycle 531: RhIG lot in the top-bar lookup; Cycle 532: on the downtime sheet; Cycle 537: ready to complete on the Supervisor queue) — RhIG evaluation on the chart with facts, decision, vials, supervisor completion (OCD-069); no candidacy or dose rule invented |
| 12 | P2 | Neonatal irradiation / CMV product-selection defaults | Low | **Closed 2026-09-14** — keep order-rule hooks; do not invent product defaults (OCD-035) |
| 13 | P7 / P9 | Quality metrics / reporting | Low (operational catalog, visual designer, guarded SQL, PDF/Excel). Starter list is not a regulatory claim | **Implemented** — eight facility reports; review sign-off not included |
| 14 | P8 | FHIR | Low | **Implemented Cycle 276** — read-only R4 API (OCD-043); terminology mapping and EHR authentication still need interface / SME verification |
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
