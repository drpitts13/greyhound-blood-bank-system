---
slug: charge-codes
title: Charge codes
order: 210
chapter: Catalog
summary: Maintain the charge master: internal code, description, default amount, and optional CPT mapping.
routes:
  - /admin/charge-codes
---

![Charge codes](figures/admin-charge-codes.png)

**Charge Codes** is the charge master. Each row has an internal code, a description, a default amount, and an optional CPT mapping. Charge rules, product billing, test billing, and action billing all point at these codes. They do not invent amounts of their own except where a product or test map supplies an override.

Changing an amount changes the next capture. It does not reprice charges already captured. Cancel and recapture is a billing action with a reason, not an edit on this row that rewrites history.

Retire a code by inactivating it once rules no longer point at it. A rule that still points at an inactive code will fail at capture. Update the rule first.
