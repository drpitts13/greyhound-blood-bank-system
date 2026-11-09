---
slug: patient-chart
title: The patient chart
order: 60
chapter: Patients and orders
summary: The chart is the hub for identity, blood type, antibodies, special requirements, and every later step of the workup.
routes:
  - /patients/{id}
  - /patients/{id}/antibody-card
---

![Patient chart overview](figures/patient-overview.png)

Open a patient from **Patients**. The overview is the default tab. The ribbon across the chart shows where this patient is in the transfusion workflow and jumps to the tab for that step.

## What the overview shows

The header shows the current name, MRN, date of birth, and sex, and the current visit: its number (open it for that visit's orders), patient type, location, and attending. When more than one visit is open it says so ("+1 more open"), so check which visit the work belongs to; "No open visit" means the patient has none. Below that, the overview summarizes the facts the rest of the system will enforce:

- Current ABO and Rh, and whether history disagrees with it. A supervisor can record a type by hand with a reason. Say where it came from: **Previous blood bank system** or **Documented outside source** (name the record or report), or **Not documented**. Only a documented type counts as one of the two ABO/Rh determinations needed for red-cell issue; an undocumented one is recorded but does not count. The blood type history says where each type came from and who recorded it, crosses out a retired type (its result was invalidated or corrected) with the reason, and marks a type that does not count as a determination.
- Antibody history, including antibodies that are no longer detectable. A deactivated antibody still counts as history. The warning at the top of the chart names each one, marks one no longer detectable as "historical / currently undetectable", and one known only from another hospital's report as "reported by" that facility with the date identified. The **Source** column says where each came from: **Outside report** with the facility and date for an antibody another hospital reported, or recorded or identified here. To record an outside report (an antibody card, a transfer letter), choose the antibody under **Add antibody**, fill **Reported by another facility** and, if known, **Identified there on**. It counts for product selection like any other antibody; whether it must be confirmed here is your medical director's decision (OCD-073).
- Special requirements that are still active.
- Whether electronic crossmatch can be considered. Eligibility is calculated. It is not a checkbox you tick.

Use the tabs for the detail: Visits, Orders, Specimens, Tests, Test History, DAT / Elution, Products, and Product History. Each tab has its own topic in this guide. On Tests, a result waiting for verification whose specimen expires soon shows **Verify before** and the time, as on the test worklist.

## Mother and infant

On a newborn's chart (under one year old), **Link mother** takes the mother's MRN and links the two charts (you need patient registration permission). The infant's chart then shows **Mother** with her ABO/Rh and antibodies, and a note when she has antibodies on record, since maternal antibodies can be present in the infant's blood. The mother's chart lists her linked **Infants** with their birth dates and types, which helps with her RhIG decision. The mother must be another chart, born before the infant. **Remove link** asks why (for example, linked to the wrong chart); the earlier link stays in the audit trail. If the mother's chart is merged into another, her infants are linked to the surviving chart. When the EHR sends the mother's MRN with a newborn's registration, the charts are linked for you. The link is information only: it changes no testing or product rule. Neonatal policies are your medical director's (OCD-074).

> Demo data. In a development database, MRN0027 (newborn) is linked to the RhIG demo mother MRN0025.

## Antibody card

**Antibody card** in the chart header shows a wallet-size card for the patient to carry: name, date of birth, MRN, blood type, the active antibodies (one known only from another facility's report says "reported by" that facility), and the special requirements in effect, with the instruction to show it to the blood bank before any transfusion. **Print card** prints it; the print is recorded in the audit trail with what the card carried. A patient with no antibody and no special requirement on file has nothing to carry, so no card is printed. The card does not replace a pretransfusion sample.

## Changing identity

Update demographics from the chart when your permission allows it. Race and ethnicity are chosen from the lists on the edit form, or left *Not recorded*; they usually arrive from the EHR by ADT and go back on results, charges, and dispense messages. A name change asks for a reason. The previous name stays on the chart as an alias so old paperwork still finds the patient.

Merging is for a confirmed duplicate. It requires supervisor permission. You merge into the surviving chart. The losing MRN remains as a pointer and cannot be used for new work. Unmerge is available only when nothing has been recorded on the surviving chart because of the merge. If orders, results, or issues exist after the merge, do not unmerge. Correct the clinical record instead, and record a deviation if the merge was wrong.

> Hard stop. Work entered on a merged-away MRN is rejected. Reprint labels and requisitions so the surviving MRN is what the bench and the ward scan.

## Special requirements

Choose a requirement from the list, grouped by table: unit typings, unit modifications, informational, and issue procedures. Typings and modifications name the unit category they apply to, for example "Irradiated red cells – RBC". Give a **start date** (today unless you change it) and, if it is temporary, an **end date**. The requirement applies from the start of the start date and no longer applies on and after the end date. Dates are facility days, and the end date must come after the start. Typings and modifications are enforced for units of their category when a unit is selected and issued. Informational requirements are not enforced: each one shows as a blue banner at the top of the chart and on the issue desk. **End date** on an active requirement ends it now, with a reason. Adding or ending one is audited. A requirement added from a reaction investigation shows which investigation it came from. The catalog of requirement types is maintained in Administration.
