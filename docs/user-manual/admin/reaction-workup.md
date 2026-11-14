---
slug: reaction-workup
title: Reaction workup
order: 186
chapter: Catalog
summary: Choose the Test Group ordered on the pre-transfusion and post-transfusion specimens of a transfusion reaction workup.
routes:
  - /admin/reaction-workup
---

**Reaction Workup** sets which tests a transfusion reaction workup orders on each of its two specimens:

- the **pre-transfusion specimen**, collected before the unit was issued (usually the tube the unit was crossmatched on)
- the **post-transfusion specimen**, drawn after the reaction

Each is mapped to a **Test Group** from Administration › Test Groups. When the investigator adds a specimen to an investigation, that specimen's group is ordered on it as a Stat reaction workup order. The tests are then resulted and verified on the bench like any other, and the investigation cannot close until every test on both specimens is verified.

The starter groups are examples to review with your medical director (OCD-059), not policy:

- **TRX-PRE**: ABO/Rh and DAT
- **TRX-POST**: ABO/Rh, DAT, and antibody screen

To change the tests, edit the group in Test Groups, or create a new group and choose it here. Only active, released groups are offered. Saving needs a change reason; the settings are versioned, and the change is recorded in the configuration history and the audit trail. A change applies to specimens added afterwards; orders already placed keep their tests.

If a specimen has no group chosen, its workup cannot be ordered, and investigations cannot be closed until one is chosen.

You need the *admin.config.edit* permission to change the groups; *admin.config.view* shows them.
