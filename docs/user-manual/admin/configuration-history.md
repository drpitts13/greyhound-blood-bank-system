---
slug: configuration-history
title: Configuration history
order: 40
chapter: Directory and audit
summary: Compare versions of configuration changes and see who saved them.
routes:
  - /admin/history
---

![Configuration history](figures/admin-history.png)

**Configuration History** lists changes to catalogs and rules. Open a change to compare the previous version with the new one. You need permission to review the configuration audit.

Use this screen when the bench reports that a rule "changed overnight." The history shows the parameter, the old value, the new value, the user, and the reason. It does not offer a one-click revert that hides the bad version. If the new value is wrong, open the catalog it came from and save a corrected value with a reason that points at this history row.

Clinical actions (issues, results, logins) are on the [Audit trail](/help/clinical/audit) in the clinical guide. Configuration history is the narrower record of setup changes.
