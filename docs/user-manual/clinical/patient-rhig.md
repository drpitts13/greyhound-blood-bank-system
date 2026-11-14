---
slug: patient-rhig
title: RhIG evaluations
order: 121
chapter: Patients and orders
summary: Record a pregnancy event's Rh immune globulin evaluation on the chart, with the facts on file beside the decision and dose your procedure gave.
routes:
  - /patients/{id}?tab=rhig
  - /rhig
---

The **RhIG** tab holds Rh immune globulin evaluations for this patient: one per delivery, antepartum event, or other exposure.

**New evaluation** records the event and its time, the infant's Rh (or Not known), the fetal bleed screen (Not done, Negative, Positive) and, when it is positive, the fetal cells percentage from quantitation. Then record the decision your procedure reached: **Candidate** with the vials recommended, or **Not a candidate** with the reason. You can save a record before the decision is made and come back to it.

Each record lists the facts on file next to what you entered: the patient's current Rh type, whether anti-D is on the antibody history, the infant Rh, and the screen and quantitation. Under the facts, an open evaluation shows a **suggestion** and a **calculated dose** (OCD-069). You decide: the suggestion and the dose are only recorded when you enter them.

- **Suggestion.**
  - Rh positive on file, or an Rh-negative infant: suggested not a candidate.
  - Rh negative with no anti-D, and the infant Rh positive or not known: suggested candidate.
  - No Rh type on file, or anti-D on the antibody history: no suggestion. Anti-D may be passive (from earlier RhIG) or immune, and the reviewer decides which.
  - Weak D and partial D are treated as Rh negative (OCD-069). Record either on the chart's Overview, under the blood type: **D variant** › **Change**, with the reason (for example the test that found it). It is kept on the patient across new typings, shown beside the blood type in the chart header, and audited. It affects the RhIG suggestion only, not product selection.
- **Calculated dose.** From a positive screen's quantitation: fetal whole blood = % fetal cells × maternal blood volume, divided by the mL one vial covers, then rounded. The three **RhIG dose** settings on Facility Policies (mL per vial, maternal blood volume, rounding) ship unset. Until your medical director enters them, the evaluation says no dose was calculated and why. Once they are set, it shows the working, for example "0.9% × 5000 mL = 45 mL fetal blood; ÷ 30 mL per vial = 1.5; rounded to the nearest and one added: 3 vials".
- **Use the suggestion** opens the evaluation with the suggested decision and, for a candidate, the calculated vials filled in. Check them and save.

Completing an evaluation keeps what was suggested and calculated at that moment, so the record does not change if the settings change later. A completed record whose decision or vials differ from them is marked **Differs from the suggestion**.

A supervisor with result verification uses **Complete**. Completion is refused until the record says what was decided: a candidate needs the vials recommended, and after a positive screen the quantitation the dose was based on; a non-candidate needs the reason. A completed record cannot be changed; record a new evaluation if something was wrong. Recording, edits, and completion are in the audit trail.

Once a candidate evaluation is completed, **Record RhIG given** takes the RhIG lot (scan or type), the vials given (starting at the vials recommended), the time, and an optional note. It is recorded once and audited. If the vials given differ from those recommended, the record and the audit say so ("1 vial given; 2 recommended."); it is not refused. Until RhIG given is recorded, the evaluation stays on the RhIG list as **To give**.

> Recording needs the immunohematology record permission; completing needs result verification; recording RhIG given needs issue permission.

**Patients & testing › RhIG** lists every open evaluation across patients, oldest event first, each linked to its chart. **Ready to complete** means the record says what was decided; the others still need an entry. The **Suggested** column shows what the facts suggest and the calculated vials (for example "Candidate, 3 vials"), or a dash when the facts suggest nothing; **Differs** marks a recorded decision or dose that is not the suggestion, so it can be checked before completing. The home screen's **Open RhIG evaluations** card counts them and says how many are ready to complete.

For a recall, scan the lot into the top-bar lookup (it opens the trace), or use **Who received an RhIG lot** on the RhIG list. Either finds every patient the lot was recorded as given to, with the time, vials, and who gave it. The patient **Timeline** shows each evaluation when it was recorded and completed, and the RhIG given with its lot; a dose that differed from the recommendation is marked.

> Demo data. A development database includes two O-negative demo mothers: MRN0025 (an antepartum dose given from lot RHIG-DEMO-01, and a delivery completed as a candidate and waiting to be given) and MRN0026 (this morning's delivery, not yet decided). Their decisions and vials are demonstration entries, not a dosing rule.

The downtime patient sheet lists patients whose RhIG evaluation still needs work, with what is outstanding (for example "RhIG: Delivery 2026-11-07: candidate, 1 vial recommended; RhIG not yet given").

For a delivery, when the newborn's chart is linked to the mother's (Link mother on the newborn's chart) and the newborn's type is on file, **Infant Rh** shows "Linked infant … is Rh …" with **Use it**. Nothing is filled in for you, and nothing is offered for twins, an untyped newborn, or an antepartum evaluation.

## RhIG lots

**RhIG lots** on the RhIG page registers each lot of Rh immune globulin as it is received: the lot number, the product as on the carton, the expiry and the vials received. **Retire** takes a lot out of use (used up, recalled, expired) with a reason. The list shows, for each lot, the vials given and the vials left.

Once any lot is registered, **Record RhIG given** must name a registered lot that is active and was in date on the day it was given; an unknown, retired or expired lot is refused. The lot field offers the registered lots. Giving more vials than the lot received is allowed but noted in the audit trail (the count may be off). Until the first lot is registered, the lot given is recorded as typed.

## Give-by time

When the medical director has set Facility Policies › RhIG › **RhIG: give within (hours of the event)**, the RhIG queue's **Give by** column shows when each open candidate's RhIG is due (the event time plus that window) and marks **Past** once it has gone. It is a reminder and blocks nothing. It is not shown for a patient decided not a candidate, after RhIG is given, or while the setting is not entered (OCD-087).

While the setting is not entered, the column says so and gives how long ago the event was ("The event was 5 days 12 hours ago"), so a blank never reads as no deadline.
