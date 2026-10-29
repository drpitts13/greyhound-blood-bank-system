---
slug: antibody-panel-lots
title: Antibody panel lots
order: 160
chapter: Catalog
summary: Withdraw or restore antibody-identification panel lots. A withdrawn lot cannot be accepted or completed.
routes:
  - /admin/antibody-panel-lots
---

![Antibody panel lots](figures/admin-antibody-panel-lots.png)

**Antibody Panel Lots** tracks the panel lots the identification workup can use. Withdraw a lot when it is expired, contaminated, or recalled. A withdrawn lot cannot be accepted on a new workup and cannot be completed. Restore a lot only if the withdrawal was a mistake and the lot is still valid to result.

Withdrawing a lot does not identify antibodies and does not close workups that were already completed on that lot. Open workups on a withdrawn lot have to move to a valid lot or be closed without using the withdrawn one.

The lot's antigram is what **Assist** compares. A wrong antigram produces wrong suggestions. Assist still does not identify the antibody. Correct the antigram before the bench uses the lot, and record why.
