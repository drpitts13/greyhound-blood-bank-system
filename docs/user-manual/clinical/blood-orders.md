---
slug: blood-orders
title: Blood orders to prepare
order: 245
chapter: Compatibility and issue
summary: See every open blood order whose units are not all reserved yet, rush orders first, with the next step for each.
routes:
  - /blood-orders
---

**Blood orders to prepare** (Transfusion) lists, across patients, every open blood (product) order whose units are not all reserved or issued yet. Rush orders (STAT, urgent, emergency, massive transfusion) come first, then by when they are **needed by** (a surgery start or scheduled transfusion, from the order), then the oldest. A needed-by time that has passed is marked, and the home card counts those. When your facility sets **Warn before a blood order's needed-by time** (Facility Policies; off until the medical director sets it), an order needed within that many minutes is marked in amber with the minutes left (for example "(in 25 min)"), and the home card counts it as needed soon. When a surgery moves, change the time on the patient's Orders tab (**Set needed by** or **Change** on the order row; leave it empty to clear it); the earlier time stays in the audit trail. You need the allocate permission.

Each row shows the patient with the current ABO/Rh, any active antibodies, and what the units must be (the patient's active special transfusion requirements, such as irradiated or antigen negative; informational notes are on the chart), the order number and priority, the products ordered (each product line is one unit), and how many units are **still needed** out of those ordered, counting units reserved on the order and units already issued on it. A reservation that was released, or a unit that came back, does not count. When a unit reserved on the order expired, the expiry sweep ended its reservation; the row then says **Reserved unit expired** with the unit and when, so you know why the order needs units again. The home card counts these orders too.

**Specimen** says whether the patient has a usable specimen (accepted or received, not expired, not discarded) and until when. The button on the row is the next step:

- **Collect a specimen**: there is no usable specimen, so units cannot be crossmatched yet. It opens the patient's specimens with the accession form open; the form links the tube to the orders waiting for one.
- **Finish type and screen**: a usable specimen is on file but its ABO/Rh and antibody screen are not both verified yet (the row says **Type and screen not verified**). Units are reserved on a specimen with a completed type and screen, so it opens the patient's tests.
- **Reserve N units**: it opens the patient's products with the reserve form open, the order chosen, and a specimen with a completed type and screen filled in (the order's own linked specimen if it has one). When the usable specimen expires before the order's needed-by time, the row says **Expires before needed-by: draw a new specimen**: units crossmatched on it could not be issued at the surgery time. The row's next step is then **Collect a new specimen**, opening the patient's accession form (you can still reserve from the chart if your procedure calls for it). This applies only when no usable specimen lasts until the needed-by time. Choose an order number to see that order's history on the chart. Pick a unit and reserve; the usual compatibility checks run. The form says how many more units the order needs and updates after each reservation; once every unit is reserved or issued it says so, with **Back to Blood orders to prepare**.

An order drops off the list once its units are all reserved or issued, or when it is completed or cancelled. The home screen's **Blood orders to prepare** card counts the orders and says how many are rush and how many need a specimen.

> Demo data. In a development database, ORD-PREP-DEMO is a STAT order for two red cell units for MRN0001 with nothing reserved yet. ORD-SURG-DEMO is a routine order for one red cell unit for MRN0022, needed a day after that patient's current specimen expires: it shows **Expires before needed-by** and **Collect a new specimen**.
