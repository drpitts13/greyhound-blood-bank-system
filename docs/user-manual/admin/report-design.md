---
slug: report-design
title: Report designer
order: 430
chapter: Reports
summary: Build a visual report or a SQL report. SQL help on this screen lists the views and functions you can use.
routes:
  - /admin/reports/design
  - /admin/reports/design/{id}
---

![Report designer](figures/admin-report-design.png)

The designer builds one report. Visual mode places columns from the reporting views. SQL mode writes a query. **Help** in SQL mode lists the views and functions that are allowed. Use those. The application does not give the report a direct query against every table, and a query that does not use the published views will be rejected.

Parameters you add become fields on the run screen. Name them so a technologist knows what to type. A parameter called "date" with no indication of collection versus issue will be filled in differently by every shift.

Save, then run the report from the clinical Reports screen with a range you understand. Confirm the row count before you tell anyone the report is published. SQL that returns patient identifiers is still a patient report. Do not publish a query that selects more identifiers than the procedure allows.

> Warning. A SQL report runs with the application's reporting access. It can return every patient who matches. Treat the definition as carefully as a clinical extract.
