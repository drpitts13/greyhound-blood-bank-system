---
slug: supplier-orders
title: Supplier orders
order: 249
chapter: Inventory
summary: Build a blood order from the minimum stock shortfall, sign it, send it, and link the delivery that fills it.
routes:
  - /inventory/orders
---

**Supplier orders** turns the minimum stock shortfall into an order for your blood supplier.

## Draft

Staff with inventory receive permission choose a supplier and select **Create draft**. The draft has one line for each minimum stock row that is short, for the number of units short. If nothing is below its minimum stock, no order is created. Change any quantity before you submit; a zero removes the line. Add notes for the supplier if needed. **Save changes** keeps the draft.

## Submit

Enter your password or PIN and select **Sign and submit**. Your edits are saved first, then the order is signed and fixed. The signature is recorded with the order and the audit trail.

## Send

**Export PDF** or **Export CSV** and send the file to the supplier by your usual route. The system does not send anything to the supplier. Each export is recorded in the audit trail.

## Receive

When the supplier's delivery arrives and is entered under Deliveries, open the order and pick that delivery under **Delivery received**. Only open deliveries from the same supplier are offered. Linking the delivery marks the order received. It does not receive or release any unit; deliveries and units follow their own steps.

## Cancel

A draft or submitted order can be cancelled with a reason. A received order cannot be cancelled.
