---
slug: supplier-orders
title: Supplier orders
order: 249
chapter: Inventory
summary: Build a blood order from the minimum stock shortfall, sign it, send it, and link the delivery that fills it.
routes:
  - /inventory/orders
---

**Supplier orders** turns the minimum stock shortfall into an order for your blood supplier. A new order asks only for what is still to order: units already on a submitted order that has not arrived are taken off. When every shortage is already on order, it says so and creates nothing; receive those orders, or cancel one to order again.

## Draft

Staff with inventory receive permission choose a supplier and select **Create draft**. The draft has one line for each minimum stock row that is short, for the number of units short. If nothing is below its minimum stock, no order is created. Change any quantity before you submit; a zero removes the line. Add notes for the supplier if needed. **Save changes** keeps the draft.

## Submit

Enter your password or PIN and select **Sign and submit**. Your edits are saved first, then the order is signed and fixed. The signature is recorded with the order and the audit trail.

## Send

**Export PDF** or **Export CSV** and send the file to the supplier by your usual route. The system does not send anything to the supplier. Each export is recorded in the audit trail.

## Receive

A submitted order is due within the facility's expected arrival window (**Expected arrival due hours**, 24 by default, the same window as an expected packing list). The order shows when it is due; once that passes without a delivery it is marked **Arrival overdue**, and Minimum stock and the home card say so too. Chase the supplier, or plan around the late units.

When the supplier's delivery arrives and is entered under Deliveries, open the order and pick that delivery under **Delivery received**. Only open deliveries from the same supplier are offered. Linking the delivery marks the order received. It does not receive or release any unit; deliveries and units follow their own steps. Once received, each line shows how many of the delivery's units count against it and how many are **Short**; the order list marks a short order, and the shortfall is recorded with the receipt. Units of a group you did not order are not counted. To replace what was short, create a new order from minimum stock.

## Cancel

A draft or submitted order can be cancelled with a reason. A received order cannot be cancelled.

## Special orders for a patient

When no unit in stock meets a patient's needs (for example irradiated, CMV-negative or antigen-negative red cells), **New special order** starts an empty draft. **Add line** takes the product, ABO and Rh (or any), the **Requirements** as the supplier is to read them, the **For patient** MRN, and the quantity. A line for a patient is kept apart from a stock line of the same product. The exported PDF and CSV list the Rh, requirements and patient for each line. Sign and submit as for any order.

## Following up an order

On a submitted order, **Record follow-up** keeps who you spoke to at the supplier and what they said, and, when they give one, the **promised arrival**. The order is then due at the promised time instead of the usual arrival window, so it is not marked overdue (here, on Minimum stock, or in the alert) until then. The latest follow-up shows on the order with who recorded it and when; each is in the audit trail. Times on the order are local.
