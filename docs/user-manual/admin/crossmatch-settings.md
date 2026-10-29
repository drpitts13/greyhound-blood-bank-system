---
slug: crossmatch-settings
title: Crossmatch settings
order: 260
chapter: Rules and safety
summary: Choose the default crossmatch tests and the number of negative screens required before electronic crossmatch can be considered.
routes:
  - /admin/crossmatch-settings
---

![Crossmatch settings](figures/admin-crossmatch-settings.png)

**Crossmatch Settings** has two jobs.

First, it selects the default crossmatch and computer-crossmatch tests the compatibility screen offers, including which test is used when the patient has antibody history. Point these at real [test definitions](tests). A default that names a retired test leaves the bench with an empty choice.

Second, it sets the minimum number of negative antibody-screen visits, specimens, and tests required before electronic crossmatch is eligible. These minimums apply only after [Facility policy](facility-policies) has allowed electronic crossmatch at all. Raising a minimum makes fewer patients eligible. Lowering it makes more patients eligible. That is a medical-director decision. Record it as such.

> Facility policy. Any antibody history row, including one that was later deactivated, still blocks electronic crossmatch no matter what these minimums say. The minimums do not override history.
