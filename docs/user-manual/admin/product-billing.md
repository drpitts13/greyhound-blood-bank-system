---
slug: product-billing
title: Product billing
order: 220
chapter: Catalog
summary: Map an ISBT product code to a billing code and to the event that captures the charge, either issue or transfusion.
routes:
  - /admin/product-billing
---

![Product billing](figures/admin-product-billing.png)

**Product Billing** maps an ISBT product code to a charge code. You choose whether the charge is captured when the unit is issued or when transfusion is documented. Optional price overrides the charge master's default for this product.

A product with no row is not charged. That may be correct for a product you do not bill, and it is a silent gap if you simply forgot. After adding a code to the ISBT catalog, check this map before the first issue.

The bench does not see this page. They see the captured row on the billing queue after the triggering event. Wrong trigger (issue versus transfused) is the usual reason a charge appears too early or not at all. Change the trigger here. Do not ask the bench to key the charge by hand.
