---
slug: lookback
title: Lookback and traceback
order: 280
chapter: Quality and operations
summary: Start from a donor unit to find recipients, or from a patient to find the units they received, and record notifications.
routes:
  - /lookback
---

![Lookback](figures/lookback.png)

**Lookback / Trace** covers two directions.

**Donor lookback** starts with the donation identification number. The screen lists recipients who received a product from that donation and the units involved. Use it when a supplier notifies you about a donor.

**Recall all components** recalls every component of the donation that is still here. A component that cannot be recalled is left as it is, and the rest are still recalled. A note above the components names each one with why:

- returned to the supplier: it is not here to recall;
- modified into other units: those units are listed under the donation and are recalled;
- expired: it cannot be recalled, so discard it.

Transfused and issued components open a recipient notification, and a component shipped to another facility opens a notification naming that facility, as before. A component the EHR reported transfused while the blood bank had it quarantined, damaged or missing (OCD-083) counts as transfused: its recipient is notified, and it is not recalled, since it is no longer here.

**Recipient traceback** starts with the patient MRN. It lists the units that patient received so you can see which donations are involved. Each unit gives the issue type and status (*Massive transfusion / Issued*) and, once documented, how the transfusion ended (*Stopped (reaction)*).

## Notifications

Record each notification you make: who was called, when, and the outcome. The record is the evidence that the call happened. The software does not place the call and does not send the letter. A recall that needs a unit pulled from inventory is still an inventory action: quarantine or discard the unit, with the lookback as the reason.

You need lookback permission. Other staff can see the clinical chart but cannot open this desk.

> Facility policy. How fast you notify, and which events require a lookback, are your procedure and the supplier's instruction. The screen stores what you did. It does not decide that a notification was required.

## Finding the components

**Components for** a DIN lists each component with its product, status and current location, so each can be found and pulled when it is recalled. A component that is not in storage (issued, transfused, discarded or shipped) says *Not in storage* rather than its last refrigerator. A note above them names any component still in stock that is not quarantined, recalled or damaged (for example *W000123LSIB001 (Pending Retype, FRIDGE-1 …)*), so a unit from the donation is not missed while recipients are traced. Whether to recall is your decision under your lookback procedure.

## How long a notification has waited

The notifications still due show when each was opened and how long it has been open. Each also shows its **Notify by** date, from Facility Policies › Lookback › **Lookback: notify the physician within (days)** (84 days, OCD-089), marked **Past** once it has gone; if the policy is set to 0 the column says *not set*. One value applies to every kind of lookback (a recipient, a facility the unit was shipped to, a recall), and the notifications listed by a DIN search show the same date. It is a reminder and blocks nothing.

## A unit with no DIN on record

Recipient traceback finds other components and other recipients through each unit's DIN. A unit with no DIN recorded (entered by hand, or received before DINs were captured) is marked *no DIN recorded*, and the page names those units above the results: their donations cannot be traced here. Read the DIN from the unit label or the supplier's records and search it under **DIN lookback**. When no unit has a DIN, the results say *No DIN to search* rather than that nothing was found.
