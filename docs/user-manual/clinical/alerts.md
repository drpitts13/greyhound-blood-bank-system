---
slug: alerts
title: Alerts
order: 35
chapter: Getting started
summary: The in-app alert inbox for excursions, expired units, minimum stock shortfalls, near-expiry units, and reagent QC work.
routes:
  - /alerts
---

**Alerts** collects conditions that need someone to look. The **Alerts** button in the top bar shows how many are open. It turns red when there is at least one and refreshes when you move between pages and every minute.

Alerts come from nine places:

- **Temperature excursion**: a storage reading outside the location range. Critical.
- **Incompatible after release**: a retrospective crossmatch for a unit released as an emergency or under MTP came back incompatible. Critical. Tell the clinical team at once; the unit may already be transfusing. A critical deviation is opened for it too, and the alert gives its number.
- **Units expired**: the scheduled expiration sweep moved units to Expired. Remove them from storage. If any were reserved for patients, the alert says how many; those reservations have ended, so reserve replacement units.
- **Below minimum stock**: a product is under its minimum stock level.
- **Near expiry**: a unit is inside the near-expiry window. When the unit is reserved for a patient, the alert is a warning that names the patient: issue it first or reserve a replacement.
- **Reagent QC**: QC is overdue, failed (critical), or waiting for review.
- **Transfusion not completed**: a transfusion was started at the bedside and not finished within the facility reminder hours. Warning. Ask the ward to document the stop.
- **Backup failed** and **Backup overdue**: the database backup did not succeed, or has not run within its schedule. Critical. Tell your system administrator.

Below minimum stock, near expiry, reagent QC, transfusion not completed, and backup overdue alerts close on their own once the condition clears. The system checks every five minutes by default. Temperature excursion, incompatible after release, expired-unit, and backup failed alerts stay open until someone acknowledges them.

When more than one kind is open, the buttons above the list show one kind at a time, with a count each, most severe first. Under each raised time, **open** says how long the alert has waited.

**Acknowledge** records your name, the time, and an optional note, and moves the alert to the closed list. It does not change the unit, deviation, or QC row. Open the alert's link to do the work. An acknowledged condition alert is not raised again while the condition lasts.

Alerts appear in the app only. Nothing is emailed or texted.
