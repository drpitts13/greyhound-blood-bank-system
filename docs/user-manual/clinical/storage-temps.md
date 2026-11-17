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

## Readings due

When Facility Policies › Inventory › *Storage temperature reading interval (hours)* is set, each location with a temperature range whose last reading is that old or older is marked **Reading due** (*Due since …*), and one never read says *No reading recorded*. The home card **Temperature readings due** counts them. A location without a range, such as an issue window, is never marked. While the interval is not set (0, the default) nothing is marked; the interval is your procedure's (for example every 4 hours when readings are taken by hand).

Whatever the interval, **Time** says how old the last reading is (*5 d 3 h ago*), and a location holding units with no reading ever recorded says *No reading on record; 6 units stored here*.

## Ranges and excursions

The range is the minimum and maximum set on the device in Admin › Refrigerators & Freezers (or Admin › Inventory locations). Each reading keeps the range that was in force when it was taken, so a later range change does not rewrite history. A location with no range records readings but never raises an excursion.

A reading below the minimum or above the maximum is an excursion. The system opens a Major deviation that names the location, the reading, the range, and the number of units stored there. The excursion stays under **Open excursions**, and on the dashboard count, until that deviation is closed on the Deviations page.

Consecutive out-of-range readings belong to the same excursion and share one deviation and one alert.

**Automatic quarantine.** When an excursion has lasted longer than **Quarantine after excursion minutes** in Facility Policies (default 30), every unit still stored at that location is quarantined with the reason "storage temperature excursion". The time runs from the first out-of-range reading while readings stay out of range; an in-range reading ends the excursion. The check runs on each reading and every five minutes, so an excursion with no further reading is still caught. Each unit's history and the audit trail record the change, the deviation lists the units, and a critical alert is raised. Release a unit only after review. Setting the minutes to 0 turns automatic quarantine off.

**History** shows the last seven days of readings for one location. **Report** opens the temperature history report for that location, with any period, summary statistics, a trend chart, CSV export, and print. Add or retire devices in Admin › Refrigerators & Freezers.

## After an excursion

A reading outside the location's range opens a deviation and shows **Review the units stored at this location**, which opens Inventory filtered to that location. Inventory's search has a **Location** filter for the same list at any time.

The excursion message also says how long you have: if no in-range reading is recorded within the facility's time (Facility Policies › *Inventory.ExcursionQuarantineMinutes*, 30 minutes unless changed), the units stored at the location are quarantined automatically (OCD-036). Record the in-range reading as soon as the temperature is back.

Times on this page and on the temperature history report are local time. The report's CSV gives the reading time in local time too ("Reading time (local)").

An open excursion whose location has had an in-range reading since is marked **Back in range: close the deviation**. The dashboard's *Temperature excursions* counts only excursions still out of range; recovered ones are left to their deviations (counted under *Open deviations*).
