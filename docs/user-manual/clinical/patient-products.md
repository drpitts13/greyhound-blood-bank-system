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

## Choosing an order and a unit

In the Allocate panel, each open product order is listed once for each product it asks for (an order for two units of red cells is one choice). Once you choose an order, the unit list offers only compatible units of that order's product, soonest to expire first, and a line under it says how many are in stock ("2 compatible units of Red Blood Cells, Leukoreduced in stock", or "No compatible units of … in stock"). Reserving still checks the unit against the order.

## Several units for one order

After you reserve a unit, the Allocate panel stays open on the same order, visit, and specimen and lists the units reserved now. Choose the next unit and **Reserve next unit**, or **Done** when you have enough. Each unit is checked on its own, exactly as the first.

## What each row shows

Each row shows the unit number, product, expiration, unit ABO and Rh, storage location, crossmatch result, and status. A row without a crossmatch result says so. A reserved unit that still needs a crossmatch offers **Crossmatch** when you have permission to record one.

A reserved unit that does not meet one of the patient's special requirements, for example one added after the unit was reserved, shows **Does not meet a special requirement** and what the patient requires. Issue will refuse it outside emergency release. Modify the unit if a modification meets the requirement, or release it and choose another unit. Adding a requirement on the Overview tab also lists any reserved units that do not meet it.

![Crossmatch dialog](figures/patient-crossmatch-dialog.png)

The crossmatch dialog asks for the specimen and the method. Electronic crossmatch is offered only when the patient is eligible and facility policy allows it. Otherwise record a serologic crossmatch and the reactions your test definition requires. Saving the crossmatch updates the row.

## Ask for a unit to be issued

On the ward, **Request issue** on a reserved unit tells the blood bank to send it now. Say where it should go (for example OR 3) and choose **Send request**. The row then shows **Requested → OR 3**, and the issue desk sees it first. To withdraw it, choose **Cancel request…** and give the reason. A request does not issue anything: the blood bank still issues the unit with every check. You need the transfusion documentation or issue permission.

## Issue from the row

**Issue** opens the issue panel for that reserved unit. The patient and the unit are already selected. **Location to issue** starts with the ward's requested destination; otherwise the panel offers the patient's visit location (**Use it**), which you confirm. You still complete identity, appearance, and the other issue checks. The issue gate runs here the same way it runs on the Issue and Transfuse desk.

![Issue unit](figures/patient-issue-dialog.png)

Enter two independent identifiers, read from the wristband or requisition. The fields start empty unless your facility has turned on pre-fill (OCD-039). Each one shows whether it matches the patient record. MRN, date of birth, and name are the tokens the gate compares. A single checkbox is not an identifier. Record appearance. If the unit is going out in a cooler, record the cooler or transport id and the destination. When facility policy requires a second verifier or a scanned P-tag, those fields are mandatory before **Issue unit** is available.

If the gate returns a warning, the panel asks for an override reason and an electronic signature. A hard stop has no override. Read the message, fix the condition (specimen, type, allocation, or special requirement), and try again.

> Hard stop. Emergency release is not available from a routine row that the gate has blocked for a missing specimen, an outdated unit, or an incompatible allocation. Emergency and massive transfusion use the issue type described in [Issue and transfuse](issuing), and they require the emergency-release permission.

## After issue

The row shows the issue. When the unit is in transit, ward receipt is recorded on the Issue and Transfuse desk. Transfusion documentation can be started from a row that is issued and has been received when your procedure requires receipt first.
