---
slug: blood-attributes
title: Blood attributes
order: 150
chapter: Catalog
summary: Maintain the antigen and antibody catalog used for patient history, unit typing, and compatibility.
routes:
  - /admin/blood-attributes
---

![Blood attributes](figures/admin-blood-attributes.png)

**Blood Attributes** is the antigen and antibody list. Patient antibody history, unit antigen typing, and compatibility rules reference these codes. A specificity that is not in the catalog cannot be posted from an identification workup and cannot be required as antigen-negative.

Mark clinical significance the way your medical director defines it. That flag is what compatibility uses when it decides an antibody must be honored. Changing significance changes future crossmatch and issue behavior. It is a rule change, not a spelling fix.

Do not create a second code for an antibody you already have because a panel used a synonym. Add the synonym if the screen allows one, or map it in the interface translation table. Two codes split the history and can make electronic crossmatch look at only one of them.
