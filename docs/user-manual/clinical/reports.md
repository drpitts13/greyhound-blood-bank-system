---
slug: reports
title: Reports
order: 320
chapter: Interfaces and reports
summary: Run a report from the catalog and export it to PDF, Excel, or CSV.
routes:
  - /reports
---

![Reports](figures/reports.png)

**Reports** lists the reports you are allowed to run. Designing a report is a separate administrator screen. Running one does not change clinical data.

Open a report, enter the parameters it asks for (a date range is typical), and run it. The result is a table. Export PDF, Excel, or CSV from the run screen when you need a file. Large results can be truncated at the export limit. The export says so when that happens. Narrow the dates rather than assuming the missing rows were blank.

**Test turnaround** lists each verified result with the minutes from order, and from tube receipt, to verification. **Test turnaround by priority** counts them by month, priority, and test, with the average and longest minutes from order. Compare them with your facility's targets; the report does not judge them. A corrected result counts at its latest verification.

**Blood order turnaround** lists each blood order (cancelled and discontinued orders left out) with the minutes from order to the first unit reserved and to the first unit issued, and, when the order has a needed-by time, whether a unit was ready by then (**Yes**, **No**, or blank while none is ready). A reservation later released still counts as the time a unit was ready. **Blood order turnaround by priority** counts orders by month and priority with the average and longest minutes to reserve and the average to issue. Compare them with your facility's targets; the report does not judge them.

Operational reports are facility reports. They are not a regulatory certificate. Read the title and the parameters you typed before you send the file to someone else.

If the report you need is not listed, you either lack report permission or nobody has published that definition. Report design is in the Administrator Guide.
