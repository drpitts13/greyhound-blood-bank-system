---
slug: audit
title: Audit trail
order: 390
chapter: Supervision
summary: Filter the append-only audit log by user, patient, unit, or time.
routes:
  - /audit
---

![Audit trail](figures/audit.png)

The **Audit Trail** records who did what, at which workstation, and when. It includes clinical actions and the reasons captured with them: overrides, corrections, reprints, discards, and configuration changes. You need audit read permission. You do not need to be an administrator to read it, if that permission was granted to your role.

Filter by time, user, patient, or unit. Open the row to see the detail. The log is append-only. There is no delete and no edit. A new hash chain covers rows written after the chain was turned on. Older rows were not rewritten. If a chain check fails, treat that as a quality event and do not "fix" it by changing data.

Use the audit trail to answer a specific question. It is not a report designer. For a table you need to export on a schedule, use Reports.

Configuration changes also appear in **Configuration History** under Administration, which compares versions. The audit trail is the broader record, including bench actions.
