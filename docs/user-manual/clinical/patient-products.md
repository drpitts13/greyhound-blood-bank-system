---
slug: patient-products
title: Products on the chart
order: 130
chapter: Patients and orders
summary: Reserve, crossmatch, and issue units from the patient's product list, including the issue and crossmatch dialogs.
routes:
  - /patients/{id}?tab=products
---

![Products](figures/patient-products.png)

The **Products** tab lists units reserved or issued to this patient. It is the chart-side view of allocation. The laboratory-wide queues live on [Compatibility](compatibility) and [Issue and transfuse](issuing).

## What each row shows

Each row shows the unit number, product, expiration, unit ABO and Rh, storage location, crossmatch result, and status. A row without a crossmatch result says so. A reserved unit that still needs a crossmatch offers **Crossmatch** when you have permission to record one.

![Crossmatch dialog](figures/patient-crossmatch-dialog.png)

The crossmatch dialog asks for the specimen and the method. Electronic crossmatch is offered only when the patient is eligible and facility policy allows it. Otherwise record a serologic crossmatch and the reactions your test definition requires. Saving the crossmatch updates the row.

## Issue from the row

**Issue** opens the issue panel for that reserved unit. The patient and the unit are already selected. You still complete identity, appearance, and the other issue checks. The issue gate runs here the same way it runs on the Issue and Transfuse desk.

![Issue unit](figures/patient-issue-dialog.png)

Enter two independent identifiers. MRN, date of birth, and name are the tokens the gate compares. A single checkbox is not an identifier. Record appearance. If the unit is going out in a cooler, record the cooler or transport id and the destination. When facility policy requires a second verifier or a scanned P-tag, those fields are mandatory before **Issue unit** is available.

If the gate returns a warning, the panel asks for an override reason and an electronic signature. A hard stop has no override. Read the message, fix the condition (specimen, type, allocation, or special requirement), and try again.

> Hard stop. Emergency release is not available from a routine row that the gate has blocked for a missing specimen, an outdated unit, or an incompatible allocation. Emergency and massive transfusion use the issue type described in [Issue and transfuse](issuing), and they require the emergency-release permission.

## After issue

The row shows the issue. When the unit is in transit, ward receipt is recorded on the Issue and Transfuse desk. Transfusion documentation can be started from a row that is issued and has been received when your procedure requires receipt first.
