---
slug: alerts
title: Alerts
order: 35
chapter: Getting started
summary: The in-app alert inbox for excursions, expired units, par shortfalls, near-expiry units, and reagent QC work.
routes:
  - /alerts
---

**Alerts** collects conditions that need someone to look. The **Alerts** button in the top bar shows how many are open. It turns red when there is at least one and refreshes when you move between pages and every minute.

Alerts come from five places:

- **Temperature excursion**: a storage reading outside the location range. Critical.
- **Units expired**: the scheduled expiration sweep moved units to Expired. Remove them from storage.
- **Below par**: a product is under its par minimum.
- **Near expiry**: a unit is inside the near-expiry window.
- **Reagent QC**: QC is overdue, failed (critical), or waiting for review.

Below par, near expiry, and reagent QC alerts close on their own once the worklist no longer shows the condition. The system checks every five minutes by default. Temperature excursion and expired-unit alerts stay open until someone acknowledges them.

**Acknowledge** records your name, the time, and an optional note, and moves the alert to the closed list. It does not change the unit, deviation, or QC row. Open the alert's link to do the work. An acknowledged condition alert is not raised again while the condition lasts.

Alerts appear in the app only. Nothing is emailed or texted.
