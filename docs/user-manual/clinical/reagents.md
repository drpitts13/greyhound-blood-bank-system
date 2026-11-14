---
slug: reagents
title: Reagent QC
order: 245
chapter: Inventory
summary: Record reagent lots, daily QC, and a review by a different person. The worklist warns. Patient resulting stays open unless facility policy is turned on.
routes:
  - /reagents
---

**Reagent QC** is the bench record for reagents you use in testing. Add the reagent, receive a lot, open it when you start using it, and close it when it is finished. A lot stores the received, opened, and closed times and the expiration. A lot that has already expired cannot be received, and a lot number that is already received or open for the same reagent is refused (check the vial). The lot list shows how many days each lot has left, amber within a week and red once expired; an expired lot cannot be opened or tested, only closed. Opening and closing a lot are recorded in the audit trail.

Each day, record the expected result, the observed result, and whether the row passed or failed. Scan the lot number into **Scan lot** to select the lot (it must be received or opened, and only one open lot may carry that number). While **Expected** is empty, the form offers the expected result last recorded for the same reagent; check it against the package insert before you press **Use it**. **Recent QC** lists the last 14 days: who performed and who reviewed each row, and whether it passed. The person signed in is the performer. Review is a separate sign-in. The same person cannot review their own row.

> Hard stop. Review is blocked when the reviewer is the person who performed the QC.

The worklist shows lots with no QC for the current day, lots whose latest QC failed, rows that have not been reviewed, and lots that have expired while still open. A failure stays on the list until the lot passes QC again or is closed. An expired lot asks to be closed and replaced rather than tested; a result entered for its test records no lot of that reagent and says so. Closing a lot takes it off the overdue list. When an open lot has a week or less left and no later lot of that reagent has been received, the worklist asks you to receive the next lot; **Receive next lot** selects the reagent in the receive form and puts the cursor on the lot number. Receiving a lot that expires later clears the item.

> Facility policy. Patient resulting stays available while this worklist has rows. An administrator can turn on **Block results when reagent QC is open**. That policy ships off. It is not a CLIA hold.

## Which results used a lot

Give each reagent the tests it is used for under **Used for tests** (for example Anti-A: ABORH; AHG: ABSC, DAT, XM). When a result for one of those tests is entered by hand, the open, unexpired lots of those reagents are recorded against it, with no extra step. If two lots of one reagent are open, both are recorded. Instrument results are tested with the instrument's own reagents and record none here. The chart and the test worklist show the recorded lots with each result (**Lots:**), so the verifier sees them. When you enter a result by hand you are told, without being stopped, if a reagent used for the test has no open lot (so none is recorded for it) or if a recorded lot has no passed QC today.

When a lot's QC fails, its worklist row and the critical alert say how many results were tested with it since it last passed QC; **N results to review** (or the alert) opens them, listed first and marked **To review**. For each one, write what you found or did (for example "Repeated with lot SC-24502: agreed", or "Corrected") and choose **Mark reviewed** (result verification permission); it leaves the count and the alert, and the note stays on the lot's list and in the result's audit. A repeat QC that passes ends the failure, but not the review: the results tested between the last passed QC and the passing repeat stay to review, and the lot stays on the worklist as **Review results** with the count until each is marked reviewed. Results are shown as staff read them (an ABO/Rh as *O+*). Marking reviewed does not change the result: correct or invalidate it on the chart first when needed. **Results** on a lot lists every result recorded with it: patient, specimen, test, value, status, and who entered it. Use it when a lot fails QC or is recalled, then review each result and correct or invalidate it as your procedure says. Keep only the lots actually in use open, so the record matches the bench.

This screen does not decide that a reagent is acceptable for a patient. It records what the bench did and who reviewed it.

## Today's QC on one table

**Today's QC, all open lots** lists every received or open lot in date, one row each. Type Expected and Observed for each lot you test; Result reads Passed when they match and Failed when they differ, and you can change it. Expected starts blank, showing the last value recorded for that reagent; **Use last expected for blank rows** copies it into every blank row, to be checked against the package insert. **Record** saves every row with both results in one step and says which passed and which failed; rows left blank stay for later. A lot already done today shows *Done today* (or *Failed today*).

## Correct a lot

**Correct** on a received or open lot fixes its lot number or expiry when it was entered wrongly. Give the reason; the change is audited with the values before and after. Another received or open lot of the reagent with the same number is refused, and a closed lot is not corrected. A corrected expiry already past makes the lot expired, so it can only be closed. Results already recorded with the lot keep the number as it was; the lot's **Results** still lists them.

## Keep the reagent list

A reagent code is used once: adding a code already in the list (in any case, retired or not) is refused. **Rename** corrects a reagent's name and asks why. **Retire** takes a reagent the laboratory no longer uses off the list: it asks why, and is refused while any of its lots is received or open, so close them first. A retired reagent takes no new lots and records no lots on results; its lots and QC stay on record. **Show retired reagents** lists them again, with **Restore**. Each change is in the audit trail.

"Today" for daily QC is the facility's day (configuration *Facility:TimeZone*, or the server's time zone): QC run in the evening counts for that day, not the next one, so the next morning's QC is still asked for.
