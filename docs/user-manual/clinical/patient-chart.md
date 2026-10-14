---
slug: patient-chart
title: The patient chart
order: 60
chapter: Patients and orders
summary: The chart is the hub for identity, blood type, antibodies, special requirements, and every later step of the workup.
routes:
  - /patients/{id}
---

![Patient chart overview](figures/patient-overview.png)

Open a patient from **Patients**. The overview is the default tab. The ribbon across the chart shows where this patient is in the transfusion workflow and jumps to the tab for that step.

## What the overview shows

The header shows the current name, MRN, date of birth, and sex. Below that, the overview summarizes the facts the rest of the system will enforce:

- Current ABO and Rh, and whether history disagrees with it.
- Antibody history, including antibodies that are no longer detectable. A deactivated antibody still counts as history.
- Special requirements that are still active.
- Whether electronic crossmatch can be considered. Eligibility is calculated. It is not a checkbox you tick.

Use the tabs for the detail: Visits, Orders, Specimens, Tests, Test History, DAT / Elution, Products, and Product History. Each tab has its own topic in this guide.

## Changing identity

Update demographics from the chart when your permission allows it. A name change asks for a reason. The previous name stays on the chart as an alias so old paperwork still finds the patient.

Merging is for a confirmed duplicate. It requires supervisor permission. You merge into the surviving chart. The losing MRN remains as a pointer and cannot be used for new work. Unmerge is available only when nothing has been recorded on the surviving chart because of the merge. If orders, results, or issues exist after the merge, do not unmerge. Correct the clinical record instead, and record a deviation if the merge was wrong.

> Hard stop. Work entered on a merged-away MRN is rejected. Reprint labels and requisitions so the surviving MRN is what the bench and the ward scan.

## Special requirements

Active special requirements (irradiated, antigen-negative, and the others your laboratory defined) are enforced when a unit is selected and issued. An end-dated requirement is inactive. Adding or ending one is audited. The catalog of requirement types is maintained in Administration.
