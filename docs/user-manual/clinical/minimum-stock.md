---
slug: minimum-stock
title: Minimum stock
order: 248
chapter: Inventory
summary: Compare available units with a minimum count by product and optional ABO. The board does not order blood.
routes:
  - /inventory/minimum-stock
  - /inventory/par
---

**Minimum stock** shows how many available units you have against a minimum you set. The minimum is for one product. You can limit it to one ABO group, or leave ABO as Any so the count includes every available unit of that product.

On hand is the available count. Short is how many units you are under the minimum. A shortfall of zero means the available count meets the minimum. The home screen card **Below minimum stock** counts the products that are short and names the largest shortfalls, so you see them without opening this page.

**Open orders** is how many product orders for that product are still waiting for a unit, whatever the patient's ABO group. Orders do not carry a quantity, so each one counts as at least one unit. When there are more open orders than units on hand, the row says **More orders than on hand**: order more, or check whether some of those orders can be filled another way. It is a count, not a forecast.

An administrator with configuration edit permission saves the minimum. Saving again for the same product and ABO updates that row.

The board does not place an order with a blood center. It does not change unit status.
