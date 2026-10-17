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

Select the visit, the ordering provider, and the test, test grouper, or product. A product choice keeps the product code. When the name is blank, the choice says Product and the id. The code is not shown as the name, and the stored product name is not changed. A grouper such as a type and screen expands into the member tests. Priority from an interface order, including STAT and ASAP, is stored on the order and shown on the worklist.

Product orders can carry modification actions: irradiate, wash, thaw, volume reduce, or leukoreduce. Those actions are defined in Administration. Completing the action later is done from inventory modification or the product workflow, and it can queue a charge when action billing is mapped.

## Link a specimen

An order that needs a specimen stays incomplete until a specimen is linked. From the order you can open accessioning and collect the identifiers, collection time, and specimen type. See [Specimens](patient-specimens).

## Hold and cancel

An order on hold does not accept bench work. Results cannot be verified while the order is on hold. Take the order off hold only when the reason for the hold is resolved.

Cancel requires a reason from the cancellation catalog. Cancellation does not delete the order. It records who cancelled it and why. Do not cancel an order to hide a verified result. Correct or invalidate the result instead.

> Hard stop. Verification is blocked while the order is on hold, the specimen is rejected, or the specimen is past its testing window.

## From the order to the unit

Product orders are what you allocate against on the Products tab and in Inventory. If the order is missing, allocation will not offer the patient. Place or release the order before you reserve a unit.
