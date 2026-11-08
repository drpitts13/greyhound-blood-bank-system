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

Each record lists the facts on file next to what you entered: the patient's current Rh type, whether anti-D is on the antibody history, the infant Rh, and the screen and quantitation. The application does not decide candidacy or calculate a dose; those follow your facility's RhIG procedure (OCD-069).

A supervisor with result verification uses **Complete**. Completion is refused until the record says what was decided: a candidate needs the vials recommended, and after a positive screen the quantitation the dose was based on; a non-candidate needs the reason. A completed record cannot be changed; record a new evaluation if something was wrong. Recording, edits, and completion are in the audit trail.

Once a candidate evaluation is completed, **Record RhIG given** takes the RhIG lot (scan or type), the vials given (starting at the vials recommended), the time, and an optional note. It is recorded once and audited. If the vials given differ from those recommended, the record and the audit say so ("1 vial given; 2 recommended."); it is not refused. Until RhIG given is recorded, the evaluation stays on the RhIG list as **To give**.

> Recording needs the immunohematology record permission; completing needs result verification; recording RhIG given needs issue permission.

**Patients & testing › RhIG** lists every open evaluation across patients, oldest event first, each linked to its chart. **Ready to complete** means the record says what was decided; the others still need an entry. The home screen's **Open RhIG evaluations** card counts them and says how many are ready to complete.

For a recall, scan the lot into the top-bar lookup (it opens the trace), or use **Who received an RhIG lot** on the RhIG list. Either finds every patient the lot was recorded as given to, with the time, vials, and who gave it. The patient **Timeline** shows each evaluation when it was recorded and completed, and the RhIG given with its lot; a dose that differed from the recommendation is marked.

> Demo data. A development database includes two O-negative demo mothers: MRN0025 (an antepartum dose given from lot RHIG-DEMO-01, and a delivery completed as a candidate and waiting to be given) and MRN0026 (this morning's delivery, not yet decided). Their decisions and vials are demonstration entries, not a dosing rule.

The downtime patient sheet lists patients whose RhIG evaluation still needs work, with what is outstanding (for example "RhIG: Delivery 2026-11-07: candidate, 1 vial recommended; RhIG not yet given").

For a delivery, when the newborn's chart is linked to the mother's (Link mother on the newborn's chart) and the newborn's type is on file, **Infant Rh** shows "Linked infant … is Rh …" with **Use it**. Nothing is filled in for you, and nothing is offered for twins, an untyped newborn, or an antepartum evaluation.
