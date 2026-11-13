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

Alerts come from twelve places:

- **Temperature excursion**: a storage reading outside the location range. Critical.
- **Incompatible after release**: a retrospective crossmatch for a unit released as an emergency or under MTP came back incompatible. Critical. Tell the clinical team at once; the unit may already be transfusing. A critical deviation is opened for it too, and the alert gives its number.
- **Units expired**: the scheduled expiration sweep moved units to Expired. Remove them from storage. If any were reserved for patients, the alert is critical and names each unit and patient MRN (the first five, then how many more); those reservations have ended, so reserve replacement units. It then opens **Blood orders to prepare**, where those orders need units again.
- **Below minimum stock**: a product is under its minimum stock level.
- **Near expiry**: a unit is inside the near-expiry window. When the unit is reserved for a patient, the alert is a warning that names the patient: issue it first or reserve a replacement.
- **Reagent QC**: QC is overdue, failed (critical), or waiting for review.
- **Transfusion not completed**: a transfusion was started at the bedside and not finished within the facility reminder hours. Warning. Ask the ward to document the stop.
- **Massive transfusion protocol still open**: a protocol is open with no unit issued under it for the facility reminder minutes (Facility Policies › *Massive transfusion protocol open reminder*, 240 by default, OCD-076). While it is open, massive-transfusion issue stays available for the patient: close it from the banner if the bleeding is controlled. The alert clears when it is closed or another unit is issued under it. A reminder only: nothing is closed for you.
- **Duplicate test ordered by the EHR**: an order received from the EHR repeats a test the patient already has open on another order. Warning. Nothing is blocked or cancelled, because a repeat can be intended (a second ABO/Rh, a new specimen after a rejection); open the order from the alert and cancel it if it was a mistake, since one result can serve the open order. The acknowledgment sent back to the EHR says the same.
- **Date of birth or sex changed by the EHR**: an ADT update (or an edit on the chart, with its reason) changed the date of birth or sex of a patient the blood bank holds. Warning. The date of birth is the second identifier on every tube label and at issue, so the alert names the patient's current tubes, labelled before the change: check each label against the chart before testing or issue, and recollect if they disagree. Nothing is changed or blocked.
- **Backup failed** and **Backup overdue**: the database backup did not succeed, or has not run within its schedule. Critical. Tell your system administrator.

Three or more open alerts of one kind and severity raised together (for example seven reagent lots overdue for QC, or eight units near expiry) are shown as one row naming each lot or unit, each linked. **Acknowledge all** acknowledges every alert in the row with the note you type, each recorded with your name as if acknowledged alone; if one cannot be acknowledged the page names it. **Show each** lists them one per row. Critical alerts are always listed one per row.

Below minimum stock, near expiry, reagent QC, transfusion not completed, and backup overdue alerts close on their own once the condition clears. While the condition holds, the alert keeps saying what is true now: a below minimum stock alert names what is on order, and drops from a warning to information when orders that are still on time cover the shortage; it becomes a warning again if such an order is late. It stays one alert with its first raised time, and an acknowledgement stands, unless the condition becomes more severe: an alert you acknowledged as information comes back open as a warning (a new alert, raised now), and the one you acknowledged is closed with your acknowledgement kept. The system checks every five minutes by default. Temperature excursion, incompatible after release, expired-unit, and backup failed alerts stay open until someone acknowledges them.

When more than one kind is open, the buttons above the list show one kind at a time, with a count each, most severe first. Under each raised time, **open** says how long the alert has waited.

**Acknowledge** records your name, the time, and an optional note, and moves the alert to the closed list. It does not change the unit, deviation, or QC row. Open the alert's link to do the work. An acknowledged condition alert is not raised again while the condition lasts.

Alerts appear in the app only. Nothing is emailed or texted.
