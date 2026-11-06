---
slug: reagents
title: Reagent QC
order: 245
chapter: Inventory
summary: Record reagent lots, daily QC, and a review by a different person. The worklist warns. Patient resulting stays open unless facility policy is turned on.
routes:
  - /reagents
---

**Reagent QC** is the bench record for reagents you use in testing. Add the reagent, receive a lot, open it when you start using it, and close it when it is finished. A lot stores the received, opened, and closed times and the expiration.

Each day, record the expected result, the observed result, and whether the row passed or failed. Scan the lot number into **Scan lot** to select the lot (it must be received or opened, and only one open lot may carry that number). While **Expected** is empty, the form offers the expected result last recorded for the same reagent; check it against the package insert before you press **Use it**. **Recent QC** lists the last 14 days: who performed and who reviewed each row, and whether it passed. The person signed in is the performer. Review is a separate sign-in. The same person cannot review their own row.

> Hard stop. Review is blocked when the reviewer is the person who performed the QC.

The worklist shows lots with no QC for the current day, rows that failed, and rows that have not been reviewed. Closing a lot takes it off the overdue list.

> Facility policy. Patient resulting stays available while this worklist has rows. An administrator can turn on **Block results when reagent QC is open**. That policy ships off. It is not a CLIA hold.

This screen does not decide that a reagent is acceptable for a patient. It records what the bench did and who reviewed it.
