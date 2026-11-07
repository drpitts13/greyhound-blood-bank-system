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

## Which results used a lot

Give each reagent the tests it is used for under **Used for tests** (for example Anti-A: ABORH; AHG: ABSC, DAT, XM). When a result for one of those tests is entered by hand, the open, unexpired lots of those reagents are recorded against it, with no extra step. If two lots of one reagent are open, both are recorded. Instrument results are tested with the instrument's own reagents and record none here. The chart shows the recorded lots under each result (**Lots:**). When you enter a result by hand you are told, without being stopped, if a reagent used for the test has no open lot (so none is recorded for it) or if a recorded lot has no passed QC today.

**Results** on a lot lists every result recorded with it: patient, specimen, test, value, status, and who entered it. Use it when a lot fails QC or is recalled, then review each result and correct or invalidate it as your procedure says. Keep only the lots actually in use open, so the record matches the bench.

This screen does not decide that a reagent is acceptable for a patient. It records what the bench did and who reviewed it.
