---
slug: training-data
title: Training data
order: 420
chapter: Appendix
summary: Demo accounts and example patients that exist only on a database seeded for training or development.
routes:
---

A development or training database can be seeded with demo accounts and example patients. A clinical database should not keep the demo password. If you can sign in as `admin` with the password `demo`, you are not on a production system.

## Demo accounts

The password for each of these accounts, on a seeded training database, is `demo`.

| Username | Typical role | Use it to see |
| --- | --- | --- |
| admin | Administrator | Every screen, including Administration |
| supervisor | Supervisor | Emergency release, overrides, merge, discard, reaction and lookback work |
| tech1 | Technologist | Everyday bench work without emergency release or user administration |
| viewer | Read only | Audit and reports, without entering results |
| analyst | Interface analyst | HL7 and instrument consoles |
| invmgr | Inventory manager | Inventory operations and product setup |
| biller | Billing analyst | The billing queue |
| auditor | Auditor | Audit, reports, and configuration history |

Signing in as one of these accounts shows the sidebar that role is allowed to see. The permission list on the dashboard is the exact set.

## Example patients

Seeded charts are fictitious. They are useful for walking through this guide. MRN0001 (Patricia Demo) is a general chart. Other seeded MRNs illustrate a newborn, an Rh-negative patient, antigen-negative selection, an emergency release with a retrospective crossmatch due, a reaction investigation, autologous and directed units, and an electronic-crossmatch example. Electronic crossmatch on that example still requires Facility Policy to enable it.

Do not document training steps against a real MRN. Do not copy a screenshot from this guide into a patient chart.
