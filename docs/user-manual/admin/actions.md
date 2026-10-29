---
slug: actions
title: Actions
order: 130
chapter: Catalog
summary: Define modification actions that can be attached to a product order, such as irradiate, wash, thaw, volume reduce, and leukoreduce.
routes:
  - /admin/actions
---

![Actions](figures/admin-actions.png)

**Actions** are the modifications a product order can request: irradiate, wash, thaw, volume reduce, leukoreduce. The order stores the request. [Modify products](/help/clinical/modifications) is where the bench completes it. The modification rule says which source products can become which result.

Add an action only when both the order form and a modification rule will use it. An action with no rule cannot be completed. An action with no billing map will complete without a charge. Map the charge on [Action billing](action-billing) if the completed action is billable.

Inactivate an action to stop new orders from requesting it. Open orders that already requested it still show the request.
