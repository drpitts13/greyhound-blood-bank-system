---
slug: patient-orders
title: Orders
order: 80
chapter: Patients and orders
summary: Place, update, and cancel orders, attach specimens, and add modification actions such as irradiate or thaw.
routes:
  - /patients/{id}?tab=orders
---

![Orders](figures/patient-orders.png)

The **Orders** tab is where you place the work and see what is already ordered. Interface orders (ORM messages) appear here as well. You do not re-type an order that the interface has already filed unless your procedure says the electronic order is wrong.

## Place an order

Select the visit, the ordering provider, and the test, test grouper, or product. The ordering location follows the visit: when the visit's location is one ordering location (its code, name, or the HL7 mapping code an administrator set on it), it is filled in and marked **From the visit**; when it is not, the form says so and you choose. While the provider is empty, the visit's attending is offered (**Use**). Use it only if that provider ordered the test.

If a test you order is already ordered and still open for this patient, the order is created with a warning naming the open order (for example "Already ordered and still open: ABSC on ORD0012"). One result can serve the open order, so cancel the new one if it repeats by mistake; keep it when the repeat is intended, such as a second ABO/Rh determination. Editing an order to add a test that is already open on another order gives the same warning. A test choice keeps the test code. When the name is blank, the choice says Test and the id. The code is not shown as the name, and the stored test name is not changed. A product choice keeps the product code. When the name is blank, the choice says Product and the id. The code is not shown as the name, and the stored product name is not changed. A grouper such as a type and screen expands into the member tests. Priority from an interface order, including STAT and ASAP, is stored on the order and shown on the worklist.

Product orders can carry modification actions: irradiate, wash, thaw, volume reduce, or leukoreduce. Those actions are defined in Administration. Completing the action later is done from inventory modification or the product workflow, and it can queue a charge when action billing is mapped.

## Link a specimen

An order that needs a specimen stays incomplete until a specimen is linked. From the order you can open accessioning and collect the identifiers, collection time, and specimen type. See [Specimens](patient-specimens).

## Hold and cancel

An order on hold does not accept bench work. Results cannot be verified while the order is on hold. Take the order off hold only when the reason for the hold is resolved.

Cancel requires a reason from the cancellation catalog. Cancellation does not delete the order. It records who cancelled it and why. Do not cancel an order to hide a verified result. Correct or invalidate the result instead.

> Hard stop. Verification is blocked while the order is on hold, the specimen is rejected, or the specimen is past its testing window.

## Needed-by time

An open order's **needed by** time (a surgery start or scheduled transfusion) is shown under its date. It turns red with "(passed)" once the time has gone, and amber with the minutes left when it is inside your facility's warning window (Facility Policies; off until the medical director sets it), as on Blood orders to prepare and the issue desk. Use **Set needed by** or **Change** on the row when the surgery moves.

## Order history

**History** on an order's row opens what happened to it, newest first: when it was placed (priority, source, provider, needed-by), each recorded change such as a moved needed-by time or a cancellation (who, at which workstation, what changed), its specimens' collection and receipt, its results, and for a product order each unit reserved, crossmatched, issued, returned, and transfused. While the order is open, a needed-by time that has passed is shown as a warning. **Warnings and critical only** narrows the list; **Full audit trail** (audit readers) opens every audited change to the order. Scanning or typing an order number in the find box, or choosing an order number on **Blood orders to prepare**, opens the chart with that order's history already open.

## From the order to the unit

Product orders are what you allocate against on the Products tab and in Inventory. If the order is missing, allocation will not offer the patient. Place or release the order before you reserve a unit.

## Transfusion criteria

When the facility turns on **Evaluate product orders against transfusion criteria**, each product on a new order is compared with the facility's criteria when you save. If **Require a reason for orders outside criteria** is also on and a product is outside the criteria, the form shows what the check found and asks for an indication or a reason before anything is saved, including any specimen being accessioned. The check never decides whether the patient is transfused; it records the order for patient-blood-management review.
