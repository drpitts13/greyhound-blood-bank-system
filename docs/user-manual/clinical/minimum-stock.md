---
slug: minimum-stock
title: Minimum stock
order: 248
chapter: Inventory
summary: Compare available units with a minimum count by product and optional ABO and Rh(D). The board does not order blood.
routes:
  - /inventory/minimum-stock
  - /inventory/par
---

**Minimum stock** shows how many available units you have against a minimum you set. The minimum is for one product. You can limit it to one ABO group, one Rh(D) type, or both, or leave them as Any so the count includes every available unit of that product. Set O negative red cells as their own row (ABO O, Rh(D) Negative): an O row counts O positive and O negative together, so it can look full while no O negative unit is on the shelf. The **Group** column names the row, for example *O Neg*, *O*, *Rh Neg* or *Any*, and the on-hand number opens those units in Inventory.

On hand is the available count, leaving out units reserved for a patient, since those cannot go to anyone else; they are shown under it (*+ 4 reserved for patients*). The alert, the home card and the shift handoff use the same count. Short is how many units you are under the minimum. A shortfall of zero means the available count meets the minimum. The home screen card **Below minimum stock** counts the products that are short and names the largest shortfalls, so you see them without opening this page.

**On order** is how many units of that product (and ABO and Rh(D), when the row names them) are on supplier orders that were submitted and have not arrived. Drafts and cancelled orders do not count. The row stays short until the units arrive, because they are not on the shelf yet, but it says **Covered by orders** when the orders cover the shortage, or **n still to order** when they cover part of it. **n overdue** in red means a submitted order is past its arrival time; it opens Supplier orders.

**Open orders** is how many product orders for that product are still waiting for a unit, whatever the patient's ABO group. Orders do not carry a quantity, so each one counts as at least one unit. When there are more open orders than units on hand, the row says **More orders than on hand**: order more, or check whether some of those orders can be filled another way. It is a count, not a forecast.

A product line already covered by a unit reserved or issued on its order is not counted, so the number matches the units still needed on **Blood orders to prepare**.

An administrator with configuration edit permission saves the minimum. Saving again for the same product, ABO and Rh(D) updates that row. **Order the shortfall** on Supplier orders orders each short row with its ABO and Rh(D).

The board does not place an order with a blood center. It does not change unit status.
