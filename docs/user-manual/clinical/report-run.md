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

Date filters count whole local days: a range from 2026-11-12 to 2026-11-12 includes everything that day, and **Today**, **Last 7 days**, **Last 30 days** and **Last month** are local days. Times in the results are local, and columns are headed by their names (for example "Occurred") in the page and in exports. A row with no date is left out when a date range is set.
