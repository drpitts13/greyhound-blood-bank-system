---
slug: products
title: Products
order: 120
chapter: Catalog
summary: Define product types, attributes such as irradiated or leukoreduced, and the issue and return rules for each.
routes:
  - /admin/products
---

![Products](figures/admin-products.png)

**Products** describes what a unit is allowed to do: which attributes it can carry (irradiated, leukoreduced, and the others you define) and the rules for issue and return. The ISBT product code catalog is separate. This page is the local product behavior those codes map onto. The list keeps the product code. When the name is blank, the list says Product and the id. That wording is not saved, and the edit form still shows the stored name.

Return rules decide whether a unit that comes back unused can be returned to available stock, and under what temperature and seal checks. Loosening a return rule lets the issue desk restock a unit your procedure says must be discarded. Tighten a rule only when the medical director has agreed, and say so in the reason.

Attributes you add here become available on modification rules and on the unit chart. An attribute that is not defined cannot be recorded, so the special-requirement check cannot see it.

## Attributes the requirements check

A patient's requirement for irradiated (or leukoreduced, washed, volume-reduced) units is met by the product's **Attributes**, not by its name. The starter products carry the attributes their names state; check them for any product you add. CMV-negative and HbS-negative are met by the unit's own test (Unit › Attributes › Unit tests) or its product code.
