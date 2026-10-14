---
slug: action-billing
title: Action billing
order: 230
chapter: Catalog
summary: Map a completed modification action to a charge code. Completing the action queues the charge.
routes:
  - /admin/action-billing
---

![Action billing](figures/admin-action-billing.png)

**Action Billing** maps an [action](actions) to a charge code. The charge is captured when the action is completed, and a DFT is queued with the other outbound billing messages.

Map an action only if completing it is a separate billable event from issuing the product. Mapping both, when your payer considers the modification part of the product, double-charges. Leave the map empty when the product charge is enough.

The action must exist before you can map it. The charge code must exist on the charge master. An incomplete map does not warn the technologist at the moment they irradiate a unit. Check a test completion on a training database before you rely on the map.
