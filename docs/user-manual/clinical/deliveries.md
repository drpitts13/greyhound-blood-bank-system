---
slug: deliveries
title: Deliveries
order: 200
chapter: Inventory
summary: Create a delivery from a supplier and track the packing list before units are received into stock.
routes:
  - /inventory/deliveries/new
  - /inventory/deliveries/{id}
---

![New delivery](figures/delivery-new.png)

**New delivery** records a shipment you are expecting or have in hand. Choose the blood supplier. Suppliers are an administrator catalog. If the shipper is not listed, stop and have them added. Do not file the units under the wrong supplier to save the step.

The delivery page shows the packing list and the status of each expected unit. Units that were announced in advance (an advanced shipping notice) appear as expected inbound on the inventory screen as well.

![Delivery](figures/delivery.png)

Open **Enter units** when you are ready to receive the physical units onto this delivery. Receiving checks temperature, appearance, and a second verifier when facility policy requires them. That work is on [Enter units on a delivery](delivery-receive). Scanning ISBT labels one unit at a time, outside a packing list, is [ISBT receive](isbt-receive).

Do not mark a delivery complete while units are still sitting on the counter unreceived. The expected-inbound queue is how the next shift knows what has not been booked in.

When the units entered do not match the expected counts, the delivery page names each product short or over (for example *RBC 2 short*) and **Complete delivery** stays disabled until you say why: supplier short and credit requested, a unit refused on arrival, an extra unit kept. The note is kept on the delivery, shown once it is completed, and written to the audit trail with the difference. A delivery that matches its packing list completes without one. **Complete delivery** on Enter units appears only when every count matches.
