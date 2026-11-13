---
slug: issuing
title: Issue and transfuse
order: 260
chapter: Compatibility and issue
summary: Issue reserved units, release blood in an emergency, acknowledge ward receipt, document transfusion, and take units back.
routes:
  - /issuing
  - /issuing/cooler-check
---

![Issue and transfuse](figures/issuing.png)

**Issue and Transfuse** is the desk for blood that is leaving the bank and for blood that is coming back. One board is open at a time: Ready to issue, Second ABO/Rh, Retrospective, In transit, Issued units, Issue, Ward receipt, Return, and Document transfusion. The issue gate runs on every routine issue. It checks identity, the specimen, ABO and Rh, allocation, crossmatch, special requirements, appearance, autologous or directed restrictions, and whether the chart you are using was merged away. Storage-location rules and remote electronic issue are part of the same gate.

## Ready to issue

> Scan first. Opening **Ward receipt**, **Return**, or **Transfusion** from its button puts the cursor in **Unit number**, so scan the unit straight away. Opened from a row, the unit is already filled. After a receipt or a return is recorded, **Unit number** empties and takes the next scan; who received the cooler and the return reason stay for the rest of it. The button stays off until the next unit is chosen. On **Transfusion**, Start keeps the unit on the form for Complete; after Complete or Document the unit leaves the form and Unit number takes the next scan, keeping the patient identifiers and transfusionist entered at this bedside for the same patient.

> Reaction under investigation. When a transfusion reaction investigation is open or under review for the patient, Issued units marks each of the patient's units **Reaction under investigation**, and the Transfusion desk says so above the form with the investigation number. Start another unit only if the physician has decided to continue: type the reason and who authorized it in the two fields under the warning. Without both, Start and Document are refused. The override is recorded and goes to the supervisor queue for review (OCD-078). A unit started before the reaction was reported (for example one documented afterwards with its earlier start time) does not ask. After you answer **Yes, reaction suspected** and finish, the investigation opens and the warning stays for the patient's other units. Before a unit leaves the bank, Ready to issue marks the patient's reserved units the same way, and issuing one is a warning that needs the usual override, the reason and who authorized it, also reviewed by a supervisor. A late entry whose issue time is before the reaction was reported does not ask.

> New specimen. A unit blocked for want of a usable specimen offers **New specimen**, and Retrospective offers **Accession** when a released unit's specimen is missing or expired. Both open the patient's accession form, which links the new tube to the orders waiting for one.

> Times on badges. A short time is today's. Any other day is named: "Needed tomorrow 07:30", "Transfusing since yesterday 22:10", or the date for days further away.

Ready to issue opens first. Reserved units are listed so you do not retype the patient and the unit. A unit the ward has asked for shows **Requested → destination** with the time and is listed first (the request can also come from the EHR, shown as requested by "EHR (HL7)"); choosing it fills **Issued to location** with that destination when the field is empty. Without a request, the form offers the patient's current visit location ("Patient's visit: ICU 4 — **Use it**"); check the patient is still there before you use it, because the visit can lag behind a move to theatre or imaging. When every unit on the board carries the same electronic crossmatch notice (for example, electronic crossmatch disabled until your facility validates it), the notice is shown once above the list instead of on each row; a unit whose notice differs keeps its own. A unit reserved for an order with a needed-by time (a surgery start, a scheduled transfusion) shows **Needed HH:mm**: red with "(passed)" once it has passed, amber with the minutes left when it is inside your facility's warning window. **Expires before needed-by** means the unit's crossmatch or its specimen ends before that time: issue before then, or draw a new specimen and crossmatch again. Units you can issue now come first; within them, the ward's requests, then the earliest needed-by time, then the oldest reservation; a line above the list says how many can be issued now and how many need attention, and those are listed after them. The row tells you when something is wrong before you start:

- A red cell that still needs a second concordant ABO or Rh is not offered for routine issue.
- A missing or expired specimen is not offered for routine issue. If the unit is group O red cells or group AB or A plasma, the row offers **Emergency** to those with emergency-release permission (see Before a specimen is drawn, below).
- An outdated unit is not offered.
- A unit that still needs a compatible crossmatch is not offered for routine issue. When its compatible crossmatch has expired, the status reads **Crossmatch expired** instead of Ready for issue, here and on the patient's Products tab. Repeat the crossmatch on a current specimen.
- An open antibody workup is marked and can still be issued.
- A unit inside the near-expiry window is marked and can still be issued.
- When electronic crossmatch is not eligible, the row shows the reason. Serologic issue is unchanged.

**Issue** on the row switches to the Issue board with the unit and patient filled in. A link that already names the patient or the unit opens that same board.

**Patient identifiers.** Scan the MRN from the wristband, or type it from the requisition, then the date of birth. The scanner's Enter moves you to the date of birth. Under each field a line says **Matches the patient record** or **Does not match this patient**. It never shows the value it expected. By default both fields start empty, so the check compares what you read with the record. A facility can choose to start them filled in (Facility Policies › *Pre-fill patient identifiers at issue and bedside*, OCD-039), and staff then confirm them visually. The server still refuses an issue whose two identifiers do not match.

Then complete appearance, location, and who is taking the unit.

**Scanning a unit into the Issue form.** Scan or type the unit number in **Unit number** and the scanner's Enter selects it. A unit reserved for a patient fills that patient, so you can start from the bag instead of the board. If the form already has a different patient, it says whose unit it is and leaves the form alone. Pick lists everywhere accept a scan followed by Enter the same way.

**Scanning without the mouse.** On Issue, Ward receipt, and Transfusion, Enter moves to the next field: patient ID 1, patient ID 2, then the unit label fields (DIN, product code, ABO/RhD, expiration, extended division). A scanner sends Enter after each scan, so you can scan wristband, requisition, and bag in a row. After the last field the form gets a green outline and keeps the focus. **Issue unit**, **Acknowledge receipt**, and **Document** are never pressed by Enter, so a stray scan cannot issue a unit. Click the button, or press Tab to reach it and then Enter. Press **?** (outside a text field) for the list of shortcuts. Print the compatibility tag when the issue requires a P-tag, then scan the P-tag id before you issue. Facility policy decides whether the tag is required.

> Hard stop. Two independent identity tokens are required. Confirming a single checkbox does not satisfy the check. A hard stop cannot be signed away.

> Warning. A warning (near expiry, an antigen-negative gap, a historical ABO discrepancy that is overridable, or a complex crossmatch that was not done) asks for a reason, the authorizer, and an electronic signature. The signature is re-authentication. The override is stored with the issue. It does not change the result that caused it.

**Reservation ended.** Each reservation has an end time. Past it, the row says **Reservation ended** with the time, and the unit is still held for the patient: nothing releases it on its own. Issue it if the patient still needs it, or choose **Release**, give the reason, and the unit goes back to stock. The dashboard's Ready to issue card counts these.

**Release to stock together.** When reservations have ended, or a unit's crossmatch has expired on a specimen that has also expired (a new crossmatch needs a new specimen), Ready to issue says how many and offers **Review and release to stock**. Every offered unit is listed with why it is offered. Units are ticked, except a unit reserved for an order that is still open: it starts unticked and says **Open order** with the number, so releasing it takes a deliberate tick once you know the order no longer needs it. A unit the ward has asked for is never offered. Untick any other the patient still needs, give one reason, and choose **Release N to stock**. Each unit is released and audited on its own; one that cannot be released is named and the others still go. A unit the ward has asked to issue is never offered. You need the allocate permission.

**A ward request that cannot be filled.** A unit the ward asked for stays on Ready to issue with **Requested →** and the place. When it cannot be issued (its reservation has ended, or its crossmatch has expired), Ready to issue says how many are held this way, and the row offers **Cancel request…**: give a reason and the request is cancelled and audited; the unit stays reserved and can then be issued or released. You need the issue or transfusion documentation permission.

## Second ABO/Rh

**Second ABO/Rh** lists patients with reserved units whose type is not yet confirmed by a second concordant ABO/Rh (from history or a separately collected specimen), so routine red-cell issue stays blocked. Each row says what is under way. **Ordered** names an open ABO/Rh order (not yet verified) and links to it on the chart. **Order ABO/Rh** opens the chart's add-order form with ABO/Rh ticked. You choose the visit, the location, and the specimen, which must come from a separate draw. Without permission to add orders the row reads **Not ordered**. On the home screen the card names patients with no ABO/Rh ordered, which puts it under Needs attention after In transit.


After a unit is issued, the form keeps the patient, the two identifiers, the destination, who it was issued to, and the cooler. It clears the unit, its label scan, the appearance, and the P-tag, because those belong to one unit. **This patient's units** lists what has gone out in this run and offers **Issue next** for each other reserved unit of the patient that is ready for routine issue, oldest reservation first. Issue next only selects the unit: scan its label, record its appearance, and issue it as usual. Every unit still goes through the issue gate. Units that need attention first, such as a crossmatch or a second ABO/Rh, are counted but not offered; open them from Ready to issue. Choosing another patient starts a new list.

## Emergency release and massive transfusion

Emergency and MTP are a different issue type. They are not an instruction to ignore every rule. The missing crossmatch becomes a warning only inside this issue type, and only for someone who has the emergency-release permission. Technologists who can issue routine units cannot release uncrossmatched blood unless that permission was granted.

> Hard stop. When the patient's blood group is established, red cells or whole blood whose group conflicts with it cannot be issued, even as an emergency release. For example, group A red cells for a group O patient are refused whatever reason is entered. Choose group O or another ABO-compatible unit. Rh mismatch and missing crossmatch stay warnings that an authorized person can override.

**Before a specimen is drawn.** An emergency or massive-transfusion release can go ahead when the patient has no current specimen (or only an expired one), but only for group O red cells or whole blood and group AB or A plasma, and only through the emergency override with a reason and the authorizer (OCD-053). Any other group, platelets, and cryoprecipitate are refused until a specimen is accessioned. A specimen that belongs to someone else is always refused. Collect a pre-transfusion specimen as soon as you can: the Retrospective board says **No specimen on file: collect one** for that patient, with a link to accession it.

### Massive transfusion protocol

Before the first unit, **activate** the protocol: choose **MassiveTransfusion** as the issue type, pick the patient, and record who called it, why, and where. A unit cannot be issued under MTP until the protocol is active. Only people with the emergency-release permission can activate or close it.

While it is active, a red banner on every Issue & Transfuse desk shows the patient, how long it has run, and how many units of each component have been issued, returned, and transfused. When no unit has been issued under it for the facility reminder minutes (240 by default), the banner says **Still open** with how long, and an alert asks for it to be closed if the bleeding is controlled. **Issue under this MTP** opens the issue form for that patient. **Use as override reason** fills the reason from the activation. Every unit still goes through every safety check and its own signature.

If your facility has defined packs (Admin › [MTP packs](mtp-packs)), you can choose one when you activate. The banner then shows the round and, for each component, how many units have gone in this round against the pack, for example "Red cells 2/4". Returned units do not count. The pack is guidance only: it never blocks a unit.

Record each container you send with **Pack a cooler…** on the banner, then print its sheet. See [MTP pack report](mtp-pack-report).

When the protocol ends, choose **Close…** and give the reason. The units stay linked to it on the record. Emergency release of a single unit does not need an activation.

You still need a current specimen for the patient (except as described in Before a specimen is drawn, above), an in-date unit, acceptable appearance, and two identity tokens. You record the reason and sign. The issue is flagged for retrospective crossmatch. It stays on the retrospective queue until someone records the crossmatch, by default within the hours set in facility policy (the shipped default is 24). **Record crossmatch** opens Compatibility with the issue filled in. If that crossmatch is incompatible, the row stays on the queue, moves to the top, and is marked **Incompatible after release**, and a critical alert is raised naming the patient and whether the unit is issued, being transfused, or already transfused. A critical deviation is opened for the issue at the same time, and the warning and alert give its number. Follow your facility's procedure, then acknowledge the alert on **Alerts** and note who you told and when, and record the follow-up as the corrective action on **Deviations**.

## Transfusion

**Start transfusion** records the bedside checks when the unit is spiked: both patient identifiers, the unit scan, and the second verifier. The unit then shows **Transfusing since** on Issued units and cannot be returned. When the transfusion ends, open it again and choose **Complete transfusion** with the disposition, volume, and the answer to **Transfusion reaction suspected?** (No reaction, or Yes); you are not asked to identify the patient again. That question starts unanswered for every unit, and completing or documenting without answering it is refused: "no reaction" is never assumed. **Disposition** (Completed, Stopped, or Wasted) is asked the same way: it starts unanswered, so a transfusion stopped early is never recorded as completed by default. Returning an unused unit is the Return desk, not a disposition. **Vital signs** (optional): before starting, enter the temperature, pulse, blood pressure and respiratory rate taken at the bedside; when finishing, the same after the transfusion. Any can be left blank, and none is required (your facility's policy says when vitals are taken). A value that cannot be right (a temperature of 370 °C, a blood pressure with one number) is refused so it can be corrected. They are kept on the transfusion record and shown in a reaction workup with the temperature change. **Document start and finish** records everything at once, for a transfusion charted afterwards; it is refused once a transfusion has been started, so the same transfusion is not recorded twice. A started transfusion that is not completed within the hours your facility sets (Facility Policies > *Transfusion completion reminder hours*, 6 by default) raises an alert and turns red on Issued units, where it is listed first under a warning, and the home **Outstanding issued** card names it. The alert opens the transfusion and clears once it is completed. This is a reminder to finish the record, not an infusion time limit.

**Times from the transfusion record.** **Complete transfusion** has **Stopped at**, and **Document start and finish** has **Started at** and **Stopped at**: enter the times from the transfusion record when you chart it afterwards. Left blank, the time is recorded as now. A transfusion started more than six hours ago says so under **Stopped at**, so a late completion is not recorded as running until now. A time in the future, or a stop before the start, is refused.

The transfusion desk records the bedside checks and the transfusion itself. The chart, product history, and unit pages open it with the unit already selected. Times and volume must make sense: a start or stop time more than a few minutes in the future, a stop before the start, a volume of zero, or more volume than the bag held is refused so it can be corrected. A start time before the unit was issued is refused too. The unit is judged at the moment the transfusion started: if it was in date then, you can document it even after it has expired; if it had already expired when it was started, documentation is refused and names the expiry.

## In transit and ward receipt

**In transit** lists units that left in a cooler or with a courier until the receiving location acknowledges custody. **Receive** on the row opens Ward receipt. Receipt repeats the product, expiration, type, specimen, crossmatch, who it was issued to, the cooler, and the destination so the person receiving can compare them with the unit in hand. Transfusion documentation waits until receipt is recorded when that is the path the unit took.

**A cooler of several units.** As soon as you open one unit of a cooler, **Also in** lists the rest of that cooler still in transit. After you acknowledge one unit, the form keeps **Received by** and lists what has been received from that cooler, or for that patient and destination when no cooler was recorded. **Receive next** selects each unit still in transit from the same shipment, earliest issued first. Scan its label and record its appearance as usual. A downtime back-entry is not offered here; receive it from the paper record.

## Check a cooler before it leaves

**Check a cooler** on In transit opens the cooler check. After you issue units with a cooler id, **Check cooler … before it leaves** opens it on that cooler. Choose the cooler from **Coolers in transit**, or scan its id, then scan every unit packed in it. After each scan the check shows:

- **Packed:** issued to this cooler and scanned.
- **Missing:** issued to this cooler, not scanned yet.
- **Issued to another cooler:** issued to someone, under another cooler or none. The row names the patient and that cooler. Take it out and pack it in its own cooler.
- **Not issued:** take it out. It must not leave the blood bank.
- **Unresolved scans:** a number no unit has, or a divided number; check those labels.

A badge warns when the cooler holds units for more than one patient. Scanning changes nothing. **Finish check** records the outcome against each issue in the cooler, and Coolers in transit shows the latest check (All packed or Differences). Units already received on the ward are not expected in the cooler. **Receipt due** shows the earliest ward receipt due time of the units in each cooler; a cooler past it is marked **Overdue**. Find it: units out past their container's validated time need assessment before use.

On **In transit**, each unit in a cooler shows **Cooler checked**, **Cooler differences**, or **Cooler not checked**; the badge opens the check. **Ward receipt** names the cooler with its check, for example "Cooler 3: checked 2026-11-05 09:30, all units packed" or "not checked before it left".

## Document the transfusion

**Issued units** lists outstanding issues so you can document or return them without looking up an internal issue number. **Start / document** (or **Complete**, once started) opens the transfusion desk and records the identifiers checked at the bedside, the start, and the disposition your procedure captures. Dual identification is required here the same way it is required at issue. Read both identifiers from the patient's wristband at the bedside. The fields start empty unless your facility has turned on pre-fill, and the same match line shows under each.

**Several units for one patient.** After you start, complete, or document a unit, **This patient at the bedside** lists what you have done and offers the patient's other units received on the ward: units still transfusing first (**Complete**), then the next to start. Moving to the next unit keeps the transfusionist and the patient identifiers you entered. Scan each unit, and answer the second verifier, volume, and reaction again for each one.

## Final disposition

**Final disposition** lists issued units that are past the facility window and still have no transfusion document, return, or discard. The window ships at 24 hours. Change it in Facility Policy if your procedure uses another operational interval. The board is a warning. It does not stop you from issuing another unit, documenting the transfusion, returning the unit, or discarding it. Each row has the next step, as on Issued units: **Document transfusion** (or **Complete** once started), **Return** while the transfusion has not started, or **Receive** when the ward has not received it; each opens that desk on the unit. The Due column also says how long the unit has been past its window (hours under two days, then days).

> Warning. A unit on this board still needs a final fate. The list does not change the unit.

## Return

**Return** on an issued row opens the Return board for a unit that came back unused. Record temperature, seal, and appearance. A unit that fails those checks does not go back to available stock. The issued-units board shows whether the storage device is governed. A governed device cannot return to available stock after the facility window, which ships at 30 minutes. Saying the time out was acceptable does not change that. An exempt device does not fail the window. Reissue follows the return rules on the product. A unit that left on an emergency release before its ABO/Rh retype goes back to **Received**, not available stock: record the retype on the Retype board before it can be allocated or issued. Do not document a transfusion for a unit you are returning, and do not return a unit that was transfused.

**A unit recalled while it is out.** A recall can reach a unit already issued. Issued units then marks it **Recalled: do not transfuse, return it** (hover for the recall reason). If it is still on its way, **In transit** marks it **Recalled: do not accept, return it**, and recording its receipt on the ward is refused with the recall named: it stays in transit until it is returned. Starting its transfusion is refused with the recall named. Return it: whatever the inspection, it goes to quarantine for the recall (*Lookback recall*), never back to stock, and asking for Available is refused. A unit already quarantined while it was out (for example from its unit page) stays in quarantine with its reason when its issue is returned. A unit reported **damaged** while out stays Damaged when returned, for its damage inspection on the Discrepancy board; one reported **missing** that comes back is quarantined as *Located after missing*, pending inspection. Neither goes back to stock. A unit in any other status than issued cannot be returned, and the screen says which.

**A cooler coming back.** When you choose a unit to return, a card for its cooler lists the patient's other units from the same cooler that can still be returned, or from the same destination when no cooler was recorded. After each return the card lists what went back and where, and the reason stays filled in. **Return next** selects the next unit; answer temperature, seal, and visual inspection for it as usual. A unit whose transfusion has started is not offered.

## What the gate will not do

A unit whose status cannot be issued is refused with the status, why, and what to do next: for example *not received yet, receive it first*; *modified into other units, issue one of those instead*; *expired, discard it and choose another unit*; *missing or damaged, resolve it on the Discrepancy board*.

The gate will not issue a unit whose verified retype does not match its label (even for emergency release), the losing chart of a merged patient, a unit allocated to someone else, an autologous or directed unit to the wrong patient, a unit that fails appearance, or an unmet issue procedure such as the blood warmer acknowledgment. A unit that does not meet one of the patient's unit typings or modifications (for example group O red cells only, or irradiated) is refused on a routine issue; on emergency or MTP release it can go only under the authorized override, which is recorded. Fix the record or choose another unit. Informational requirements for the patient show as a blue banner on the issue form. See [Safety checks](safety) for the list you will see most often.

## Return inspection

When you return a unit, answer **Temperature**, **Seal**, and **Visual inspection**. Each starts as **Not answered**. Until all three are answered, **Return unit** stays unavailable when the unit would go back to available stock. A unit you send to **Quarantine** can be returned without them, and the record says they were not answered. A unit returned to available stock is no longer reserved for the patient. The result says so and offers **Reserve again for this patient**, which opens Compatibility with the unit, patient, and specimen filled in; reserving runs every check again. A return never throws a unit away. A unit that failed its seal or visual check, or that you want disposed of, goes to Quarantine. Discard it on the unit page with a discard reason, and a second verifier if your facility requires one. Your facility can choose to start them as acceptable (Facility Policies › *Start unit inspections as acceptable*, OCD-040). Time out of storage is always worked out by the system.

## Appearance

**Appearance at issue** and **Appearance at receipt** start at **Choose appearance...** with *Not recorded. Look at the bag.* underneath. Look at the unit and pick what you see. The issue or receipt is refused until it is recorded. After each issue or receipt the field clears, so the next unit gets its own look. The same applies when units are received into inventory. If your facility chooses to start appearance as acceptable (OCD-040), the field starts at **Acceptable** and the record notes that the facility default supplied it.

## Who authorized an override

**Authorized by** starts empty. Type the name of the physician or supervisor who approved the override. If you approved it yourself, press **Me**. As you type, the field suggests ordering providers and LIS users; picking one records exactly who it was. A name that is not in either list is still accepted, so an emergency is never held up, but the field says so and the supervisor review shows it as not in the directory. If the person is a real authorizer from outside your directory (a transferring or visiting physician, for example), press **Add as outside provider**, give a reason, and optionally their NPI or local id. They are added to the directory as an outside provider, and this and later overrides they authorize are shown in review as "outside provider". You need the *provider.outside.add* permission; a name already in the directory is refused, so pick it from the list instead. A blank field is never filled in for you. If you give a reason but no name, the message says so. Your electronic signature separately records who signed.

> Unidentified patient. When an emergency patient has no identification yet, choose **Unidentified patient…** under Patient on the issue form (you need patient registration and emergency release permissions). Enter sex and estimated age; the patient is registered with a temporary MRN (for example UNK-20261108-01), chosen, and the issue type becomes Emergency release unless Massive transfusion is chosen. Record the emergency reason and authorizer as usual. Merge the chart into the identified patient's once known (Patients help).

> Issued under an earlier MRN. When a patient's chart changes MRN after a unit was issued (for example an unidentified patient identified and merged), the In transit and Issued units rows show **Issued under** the earlier MRN, and the transfusion form warns that the wristband or unit tag may still show it. Make sure they agree before transfusing; the bedside check uses the current MRN.

A unit whose transfusion has already been documented (for example entered after a downtime, without the ward's receipt) has reached the patient and is not listed **In transit**.
