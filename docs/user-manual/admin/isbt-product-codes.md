---
slug: isbt-product-codes
title: ISBT product codes
order: 350
chapter: Inventory and ISBT
summary: Review the product-code catalog and import a licensed ICCBBA extract when your facility has one.
routes:
  - /admin/isbt-product-codes
---

![ISBT product codes](figures/admin-isbt-product-codes.png)

**ISBT Product Codes** is the catalog ISBT receive, modification, and product billing match against. The rows shipped with the application are a placeholder subset, not the licensed ICCBBA tables. Official codes are not included.

If your facility holds an ICCBBA license and you have an extract in CSV, TSV, or JSON, import it from this page. Review the import before you rely on it for live receipts. A code that is missing causes receive and modification to reject the unit. Do not invent a look-alike local code for a licensed product.

> Facility policy. Using ISBT 128 product codes requires the facility's own ICCBBA license. Importing an extract you are not licensed to use is not a configuration shortcut.

After new codes are imported, check [Product billing](product-billing) and [Modification rules](modification-rules) so the new codes charge and modify the way you intend.
