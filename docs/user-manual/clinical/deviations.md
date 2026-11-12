---
slug: deviations
title: Deviations
order: 290
chapter: Quality and operations
summary: Record a nonconformance, link it to the patient, unit, or order it affects, and track it to closure.
routes:
  - /deviations
---

![Deviations](figures/deviations.png)

**Deviations** is the quality record for something that did not follow procedure: a wrong unit selected and caught, a labeling error, a temperature excursion, a missed retrospective crossmatch, an interface result filed on the wrong patient. You need permission to manage deviations.

## Write the record

Describe what happened, when it was discovered, and what the immediate risk is. Link the patient, specimen, unit, order, or issue that is involved so the next person can open the chart without searching. From a unit page or a patient chart, **Record deviation** in the **Deviations** panel opens this form already linked to that unit or patient, and the panel lists the deviations linked to it. On the form itself, scan or type the unit's DIN, the patient's MRN, the tube's accession, or the order number in **Related record** and press Enter: one exact match is linked and named; when several match, choose one. For an issue, a result, or an HL7 message, choose the type and enter its id. The link is a reference. It does not by itself quarantine a unit or invalidate a result. Do those actions in Inventory or on the result, and cite the deviation.

Status moves from open toward closure as the corrective action is done. Closure requires the outcome, not a blank completion: **Save** stays disabled for Closed until **Corrective action** says what was done, or why no action is needed. A corrective action once recorded can be edited but not emptied. Reopening a closed deviation clears who closed it and when; the audit trail keeps the earlier close.

The list shows open deviations first, most severe first, then the oldest. Each row shows the description, any corrective action, who reported it and when, and how long it has been open. The record stays in the audit trail. You cannot delete a deviation to remove an uncomfortable event.

Use a deviation in addition to an issue override, not instead of it. The override explains why the gate allowed the issue. The deviation explains the process failure if there was one.

## Raised by the system

A deviation opened by a temperature excursion shows its context as **Temperature reading**, linking to Storage temperatures. Status reads in words (Open, Under review, Closed).
