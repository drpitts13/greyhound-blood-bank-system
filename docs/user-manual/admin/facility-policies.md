---
slug: facility-policies
title: Facility policy
order: 250
chapter: Rules and safety
summary: Set specimen windows, dual verification, electronic crossmatch, inventory receipt, and retention. Each edit requires a reason.
routes:
  - /admin/facility-policies
---

![Facility policy](figures/admin-facility-policies.png)

**Facility Policy** holds the parameters that drive clinical behavior: how long a specimen is valid, when a second person must verify, whether electronic crossmatch is allowed, how inventory receipt works, and how long records are retained. The page groups them by category. Filter by name, key, or citation.

Each row shows the current value and the citation the parameter was written against. Select the row to edit. Boolean parameters are true or false. Numeric parameters show the allowed range. A value outside the range is rejected. Every save requires a reason.

> Facility policy. Electronic crossmatch stays off until you set the policy that allows it. Turning it on does not by itself make any patient eligible. Antibody history, open workups, and the minimums in Crossmatch Settings still apply.

A parameter marked legal hold should not be edited to satisfy a convenience. The badge is there so you see the hold before you save.

Links on this page open [Crossmatch settings](crossmatch-settings) and [Compatibility tables](compatibility-rules). Those are separate catalogs. A change here does not edit the ABO table.

The bench feels a policy change on the next action. It does not reprint old issues. Say in the reason which procedure version you are implementing.
