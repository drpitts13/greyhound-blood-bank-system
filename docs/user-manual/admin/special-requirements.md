---
slug: special-requirements
title: Special requirements
order: 140
chapter: Catalog
summary: Define the unit typings, unit modifications, informational notes, and issue procedures that can be put on a patient chart.
routes:
  - /admin/special-requirements
---

![Special requirements](figures/admin-special-requirements.png)

**Special Requirements** is the catalog of requirements staff can put on a patient chart. It has four tables:

- **Unit typings**: what the unit must type as, for one unit category. An allowed ABO group (for example "Group O red cells only"), RhD negative, antigen negative, or a negative unit test such as CMV or HbS. A unit-test typing is met when the product code carries the product attribute you name, or when the unit's latest result for that test, recorded under **Unit tests** on the unit page, is Negative.
- **Unit modifications**: the product attribute the unit must carry, for one unit category: irradiated, washed, leukoreduced, volume reduced.
- **Informational**: a note shown as a banner on the patient chart and the issue desk. It is never enforced, and the banner text is required.
- **Issue procedures**: acknowledgments at issue (blood warmer, other), complex crossmatch, and A1/A2 subgroup typing.

A new typing or modification names the unit category it applies to (RBC, platelets, plasma...). A requirement for red cells does not apply to plasma or platelets. Entries made before unit categories existed show **All categories** and apply to every unit, as before. To make one apply to a single unit category, add a new entry and deactivate the old one.

Unit typings and modifications that a unit does not meet leave it out of selection, cannot be reserved, and stop issue. On emergency or MTP release they need an authorized override instead, which is recorded with the issue. Issue procedures are never overridden. While a requirement is active, selection and issue enforce it. The list keeps the requirement code. When the name is blank, the list says Requirement and the id. That wording is not saved, and the edit form still shows the stored name.

The starter entries (for example Group O red cells only, Irradiated red cells, CMV-negative red cells, HbS-negative red cells) are examples to edit or deactivate. Create an entry here before the bench can apply it. Wording matters because the issue alert repeats it. Say what must be true of the unit, not a vague precaution.

> Facility policy. Which patients require irradiation or antigen-negative units is a medical-director policy. This catalog only makes the requirement enforceable. It does not decide who needs it.

Antigen-negative types depend on the blood-attribute catalog and on unit typing. A requirement for an antigen you cannot type will warn or stop at issue until a typed unit is chosen.
