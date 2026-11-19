---
slug: report-run
title: Running a report
order: 330
chapter: Interfaces and reports
summary: Enter parameters, read the rows, and export the run to PDF, Excel, or CSV.
routes:
  - /reports/{id}
---

![Run report](figures/report-run.png)

The run screen is one report. The title is the report name. Parameters are the filters the designer defined. A date range that is backwards or empty returns no rows. That is a parameter result, not a failure of the clinical record.

Run, then read the table before you export. Export PDF for a paginated copy, Excel when you need to sort or filter further, and CSV for another system or a statistics tool. A CSV cell that starts with =, +, -, or @ gets a leading apostrophe so a spreadsheet does not run it as a formula. Signed numbers such as -18.5 are left alone. The PDF is marked as a facility operational report. It is not an accreditation certificate.

If the report was changed while you had it open, run it again. The file you export is the run you just executed, not a later edit to the design.

To change columns or the query, you need report-design permission and the designer. Running permission is not design permission.

Date filters count whole local days: a range from 2026-11-12 to 2026-11-12 includes everything that day, and **Today**, **Last 7 days**, **Last 30 days** and **Last month** are local days. Times and months (for example "Discard month") in the results are local, and columns are headed by their names (for example "Occurred") in the page and in exports. A row with no date is left out when a date range is set.

Status, type, severity and similar columns read in words (*On hold*, *Under review*, *Transfusion started*) rather than as stored codes; a unit status reads as on the screens (a unit awaiting its retype is *Pending retype*). Filters and grouping still use the stored value.

In *Emergency releases and overrides*, an override names its unit and patient, as the supervisor queue does: through the issue, reservation or crossmatch it was recorded on. **Authorized by** says who authorized each row: an override's authorizer; for an emergency or massive-transfusion issue, the authorizer of its override, or, for a massive-transfusion issue without one, the physician who called its protocol (*Dr. Demo Trauma (called MTP #1)*). **Actor** is still who recorded it.

In *Test turnaround*, **Minutes, receipt to verified** counts from the tube's receipt, or from the order when the tube was received before the test was ordered (for example a reaction workup ordered on a stored tube), so a stored tube does not count days it sat waiting for an order.

In *Blood order turnaround*, **Ready by needed-by** is *No* for an order whose needed-by time has passed with no unit reserved or issued yet, as well as for one first ready after it; it is blank only when the order has no needed-by time or is not yet due.

In *Units issued and transfused*, **Issue status** says whether each issued unit is still out (*Issued*), came back (*Returned*) or was transfused, so a returned unit is not read as one still waiting for its transfusion record.

In *Lookback and recall notifications*, each row names the unit transfused and the date the notification was opened, as the Lookback page does.

In *Open deviations*, each row names the related record (the unit, patient, specimen or temperature reading) as the Deviations page does, with who it is assigned to, when it is due and whether it is FDA reportable.

SQL reports read the same reporting views; their month columns (for example *discarded_month*) are local months too. Other times in a view are stored UTC.
