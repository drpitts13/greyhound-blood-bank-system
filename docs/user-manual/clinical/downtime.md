---
slug: downtime
title: Downtime snapshot
order: 400
chapter: Supervision
summary: After the system returns, review interface errors, open issues, and retrospective crossmatches that still need to be entered from paper.
routes:
  - /downtime
---

![Downtime snapshot](figures/downtime.png)

**Downtime Snapshot** is the recovery checklist inside the application. It counts unresolved interface errors, outbound HL7 that is still pending, open issues, retrospective crossmatches that are due, and the status of the audit chain. You need audit read permission. The screen does not import paper forms and it does not scan them.

## While the system is down

Downtime documentation is your facility's paper procedure. Capture the identifiers you will need to enter later: patient MRN and name, specimen accession and collection time, ABO and Rh, antibody screen, antibodies and special requirements, unit number or donation identification number, product, ABO and Rh, expiration, inspection, crossmatch or the emergency-release acknowledgment, and any transfusion or reaction. The software does not store those paper rows for you.

## After the system returns

1. Confirm you are on the recovered database, not a partial copy.
2. Replay interface messages that failed, and watch for duplicates. A message that already filed must not be entered by hand as well.
3. Enter the paper record into the chart, the specimen, the result, and the issue screens. Use the same identity checks you use for any issue. The first electronic issue after downtime is not a shortcut.
4. Complete retrospective crossmatches for emergency issues that were documented on paper.
5. Review overrides and interface errors before you call the recovery finished.

> Facility policy. The paper forms, the decision to transfuse during downtime, and the regulatory notice are your procedure. This screen only shows what is still open in the database.
