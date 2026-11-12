---
slug: equipment
title: Equipment
order: 246
chapter: Inventory
summary: Register bench equipment, schedule the checks each needs, record them, and take equipment out of service and back.
routes:
  - /equipment
---

**Equipment** is the register of bench equipment that needs scheduled checks: centrifuges, cell washers, heat blocks and water baths, irradiators, plasma thawers, platelet agitators, thermometers, timers and pipettes. Refrigerators and freezers keep their temperature log under **Storage temps** instead.

## Checks to do

The top of the page lists every check that needs doing now on equipment in service, failed first, then overdue, never done, and due today. Select **Record**, choose **Passed** or **Failed**, and add the reading (for example *3400 rpm* or *37.1 °C*). A failure needs a note saying what failed and what was done. The person signed in is recorded as the performer, with the time.

A check is due again its interval after it last **passed**, counted in days: a daily check done late yesterday is due today. A check never recorded is due now. A failed check stays failed, and on the list, until it is recorded as passed.

## Out of service

**Take out of service** asks for the reason (for example *failed QC* or *broken lid latch*). Its checks leave the list while it is out, and the register shows since when, by whom and why. A check can still be recorded while it is out, such as the check after repair. **Return to service** asks why it can be used again (for example *repaired, QC passed* or the vendor's service report) and warns while a check's latest record failed.

## History and the log

**History** on a piece of equipment lists every check performed on it, newest first: when, which check (including checks no longer scheduled), passed or failed, who, and the reading or note. **Download log (CSV)** saves the same list with times in UTC and local time, for an inspection or the equipment file. The latest 500 records are shown and downloaded.

## On the home screen and in modifications

The **Equipment checks to do** home card (Bench and Supervisor desks) counts the checks on this list and names any failed or overdue; it opens this page. The shift handoff records the same count, with the failed or overdue among them, and the equipment out of service (*do not use*).

On **Modify Products**, the device field offers the registered equipment in service. A modification whose device is registered equipment out of service, or retired from the register, is refused and says since when and why; use other equipment, or return it to service here once it may be used. A device that is not in the register is recorded as typed.

## Register equipment and schedule checks

An administrator (*admin.config.edit*) selects **Add equipment** and enters the code (the asset tag, stored in upper case and unique), name, kind, and optionally the location and serial number. Then **Schedule a check**: name it (*Daily QC*, *Monthly timer check*, *Annual calibration*), say how often it is due in days, and copy the instructions or pass criteria from the SOP so they show when the check is recorded.

> The intervals and pass criteria are the facility's, from its SOPs and the manufacturer's instructions. None is built in; the demo equipment's intervals are examples only.

Every registration, scheduled check, record, and change of service is in the audit trail. Nothing is deleted.
