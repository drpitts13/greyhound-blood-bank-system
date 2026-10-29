---
slug: special-requirements
title: Special requirements
order: 140
chapter: Catalog
summary: Define requirement types that can be applied to a patient, a unit, or an issue, and that the issue gate enforces while they are active.
routes:
  - /admin/special-requirements
---

![Special requirements](figures/admin-special-requirements.png)

**Special Requirements** is the catalog of requirement types: irradiated, antigen-negative specificities, and the other constraints your laboratory uses. The patient chart turns a type on or off for a person. An end date makes it inactive. While it is active, selection and issue enforce it. The list keeps the requirement code. When the name is blank, the list says Requirement and the id. That wording is not saved, and the edit form still shows the stored name.

Create the type here before the bench can apply it. Wording matters because the issue alert repeats it. Say what must be true of the unit, not a vague precaution.

> Facility policy. Which patients require irradiation or antigen-negative units is a medical-director policy. This catalog only makes the requirement enforceable. It does not decide who needs it.

Antigen-negative types depend on the blood-attribute catalog and on unit typing. A requirement for an antigen you cannot type will warn or stop at issue until a typed unit is chosen.
