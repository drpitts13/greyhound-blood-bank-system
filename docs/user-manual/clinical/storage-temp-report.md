---
slug: storage-temp-report
title: Temperature history report
order: 250
chapter: Inventory
summary: Report storage readings, excursions, and missed readings for one device or all devices over a period.
routes:
  - /storage-temps/report
---

The **Temperature history report** reviews the storage temperature log over a period. Open it from Storage temperatures, or from **History** on Admin › Refrigerators & Freezers.

## Run the report

Choose a device or **All devices**, a From and To date, and whether to list all readings or excursions only, then select **Run**. Preset buttons set the dates and run in one click. Dates are UTC. The period runs from the start of the From date to the end of the To date and covers at most 366 days. Deactivated devices are listed and marked Inactive, so the history of a retired device stays available.

## What it shows

**Summary** has one row per device: the range, the number of readings, the minimum, maximum, and mean reading, the percentage of readings in range, the number of excursions, the number of out-of-range readings, the longest gap, and the last reading.

- **% in range** counts only readings taken while the device had a range.
- An **excursion** is an unbroken run of out-of-range readings, the same grouping that opens one deviation.
- **Longest gap** is the longest time with no reading, counted from the period start, between readings, and to the period end (or now). A long gap points to missed rounds.

With one device selected, **Trend** plots every reading against the range band. Out-of-range readings are marked; hover a point for its time and value.

**Readings** lists each reading with the range in force when it was taken, who recorded it, its deviation, and its comment.

## Export and print

**Export CSV** downloads the listed readings. **Print** prints the page without the menu and filters.
