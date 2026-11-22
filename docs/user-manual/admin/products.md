---
slug: products
title: Products
order: 120
chapter: Catalog
summary: Define product types, their attributes such as irradiated or leukoreduced, and their ABO/Rh issue tables.
routes:
  - /admin/products
---

![Products](figures/admin-products.png)

**Products** describes what a unit is allowed to do: which attributes it can carry (irradiated, leukoreduced, and the others you define) and the ABO/Rh tables issue and allocation use. The ISBT product code catalog is separate. This page is the local product behavior those codes map onto. The list keeps the product code. When the name is blank, the list says Product and the id. That wording is not saved, and the edit form still shows the stored name. Each row also shows the product's **ISBT code** (a dash when none is set), its class, whether it needs a crossmatch, and whether it needs a retype, with the Rh-positive and Rh-negative retype tests beneath (*not set* when one is missing). **Copy** on a row opens a new product filled from it, with its attributes and ABO/Rh tables, the code ending *-COPY* and the ISBT code blank; change the code, name and ISBT code, then save to create a draft. Nothing is saved until you do.

**Notes (reference only, not enforced)** holds issue, return and modification notes kept with the product. The software does not act on them. Whether a returned unit goes back to stock is decided by the return inspection (temperature, seal, visual check, time out of storage and expiration), with the time limit set in Facility Policies. Modifications follow Modification Rules. The notes section opens by itself when a product has notes.

Attributes you add here become available on modification rules and on the unit chart. An attribute that is not defined cannot be recorded, so the special-requirement check cannot see it.

## Attributes the requirements check

A patient's requirement for irradiated (or leukoreduced, washed, volume-reduced) units is met by the product's **Attributes**, not by its name. The starter products carry the attributes their names state; check them for any product you add. CMV-negative and HbS-negative are met by the unit's own test (Unit › Attributes › Unit tests) or its product code.

**Deactivating a product** still goes ahead, but the confirmation names what still uses it: active modification rules that start from it or make it, units of it still in inventory (including quarantined, reserved and crossmatched units), and minimum stock levels set for it.

**Allow mixed-ABO pools** appears for cryoprecipitate products only (OCD-094). When ticked, units of the product may be pooled across ABO and Rh(D) on Modify products; the pool is labelled Mixed ABO and checked against every unit in it at selection and issue. Leave it off to keep pools to one group.
