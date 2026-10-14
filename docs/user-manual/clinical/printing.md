---
slug: printing
title: Printing
order: 300
chapter: Interfaces and reports
summary: Print specimen labels, component labels, and compatibility tags, and reprint only with a reason.
routes:
  - /printing
---

![Printing](figures/printing.png)

**Printing** produces specimen labels, component labels, and compatibility tags (P-tags). You can also open it from a specimen, a unit, or an issue, with that record already selected.

## What you are printing

Confirm the identifiers on the preview before you send the job. The preview is a PDF. The printer may be a PDF queue or a Zebra printer that takes ZPL. Which printers exist, and which layout is active for the specimen label and the P-tag, is set in Administration. You pick the printer. You do not redesign the label here.

A compatibility tag printed at issue carries the patient, the unit, and the crossmatch information the layout includes. When facility policy requires the P-tag id before issue, scan that id on the issue form. Printing the tag and not scanning it does not satisfy the check.

## Reprint

Reprint requires a reason and the reprint permission. The reason is audited. Reprint when the label is damaged or lost. Do not reprint onto a different specimen or unit. If the identifiers on the label are wrong, fix the record first, then print again, or reject the specimen and start over.

Jobs stay in the queue on this screen so you can see what was sent and open the preview again.
