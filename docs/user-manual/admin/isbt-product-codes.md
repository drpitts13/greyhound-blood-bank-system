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

## Licensed-code readiness

The **Licensed-code readiness** card at the top of the page answers one question: is the facility running on a licensed ICCBBA catalog yet? It only reads. It never changes a code.

- **Catalog**: how many product description, ABO/RhD, and collection-type rows exist, and how many came from a licensed import versus a placeholder or public-subset seed. It also lists the licensed versions loaded.
- **In use without a licensed code**: each active product whose ISBT product code is missing, a placeholder, or not in the catalog, and each on-hand unit whose product or ABO/RhD code is a placeholder or not in the catalog. Each row links to the product or unit.
- Units with no ISBT codes at all, from non-ISBT manual intake, are counted but do not block readiness.

The card says **Ready** only when at least one licensed version has been imported and nothing in use relies on a placeholder or unknown code. Until then it says **Not ready** and why. The decision is recorded as OCD-004: this software does not invent codes, and the facility supplies them from its ICCBBA license.
