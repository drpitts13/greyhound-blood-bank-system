---
slug: storage-temps
title: Storage temperatures
order: 249
chapter: Inventory
summary: Record storage readings against each location's range. A reading outside the range opens a deviation.
routes:
  - /storage-temps
---

**Storage temperatures** is the temperature log for refrigerators, freezers, and platelet storage. Each location shows its range, its last reading, and how many units are stored there now.

## Record a reading

Staff with inventory receive permission pick the location, type the reading in °C, and add a comment if needed. The reading time is now. The system rejects a reading below -100 °C or above 60 °C as a typing error. It also rejects a time in the future.

Readings are never edited. If a reading is wrong, record the correct one and say why in the comment.

## Ranges and excursions

The range is the minimum and maximum set on the location in Admin › Inventory locations. Each reading keeps the range that was in force when it was taken, so a later range change does not rewrite history. A location with no range records readings but never raises an excursion.

A reading below the minimum or above the maximum is an excursion. The system opens a Major deviation that names the location, the reading, the range, and the number of units stored there. The excursion stays under **Open excursions**, and on the dashboard count, until that deviation is closed on the Deviations page.

Units are not quarantined automatically. Open Inventory, review the units at that location, and quarantine any that need it with a coded reason.

**History** shows the last seven days of readings for one location.
