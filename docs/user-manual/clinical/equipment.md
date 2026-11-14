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

## Coming up

**Coming up** lists the checks on equipment in service that are not due yet but fall due within the next 7, 30 or 90 days (30 by default), soonest first, so a calibration, preventive maintenance or vendor visit can be booked in time. Checks already due are in **Checks to do** instead.

## Out of service

**Take out of service** asks for the reason (for example *failed QC* or *broken lid latch*). Its checks leave the list while it is out, and the register shows since when, by whom and why. A check can still be recorded while it is out, such as the check after repair. **Return to service** asks why it can be used again (for example *repaired, QC passed* or the vendor's service report) and warns while a check's latest record failed.

## History and the log

**History** on a piece of equipment lists every check performed on it, newest first: when, which check (including checks no longer scheduled), passed or failed, who, and the reading or note. **Download log (CSV)** saves the same list with times in UTC and local time, for an inspection or the equipment file. The latest 500 records are shown and downloaded.

## On the home screen and in modifications

The **Equipment checks to do** home card (Bench and Supervisor desks) counts the checks on this list and names any failed or overdue; it opens this page. The shift handoff records the same count, with the failed or overdue among them, and the equipment out of service (*do not use*).

On **Modify Products**, the device field offers the registered equipment in service. A modification whose device is registered equipment out of service, or retired from the register, is refused and says since when and why; use other equipment, or return it to service here once it may be used. A device that is not in the register is recorded as typed.

## Register equipment and schedule checks

An administrator (*admin.config.edit*) selects **Add equipment** and enters the code (the asset tag, stored in upper case and unique), name, kind, and optionally the location and serial number. Then **Schedule a check**: name it (*Daily QC*, *Monthly timer check*, *Annual calibration*), say how often it is due in days, and copy the instructions or pass criteria from the SOP so they show when the check is recorded.

**Edit** corrects a piece of equipment's name, kind, location or serial number; its code (asset tag) does not change. **Change** on a check changes its name, interval or instructions, for example after an SOP revision; its records are kept, and it is next due its new interval after its last pass. Both ask for the reason, which is kept in the audit trail.

**Stop** on a check stops scheduling it (its records stay in the history). **Retire** takes equipment that has been replaced or sent back off the register: its checks stop being due, its records are kept, and it can no longer be named as the device of a modification. **Show retired** lists retired equipment with **Restore**, which brings it back with its checks. Each asks for the reason.

> The intervals and pass criteria are the facility's, from its SOPs and the manufacturer's instructions. None is built in; the demo equipment's intervals are examples only.

Every registration, scheduled check, record, and change of service is in the audit trail. Nothing is deleted.

Due dates count the facility's days (configuration *Facility:TimeZone*, or the server's time zone): a daily check done in the evening is due the next day, and overdue the day after.

## Equipment and the tests it is used for

**Used for tests** names the tests a piece of equipment is used for (for example ABSC, XM), when it is added or edited (an edit asks for a reason and is audited). The test worklist then warns, above the list, when equipment used for a test on the list is out of service or has a check failed or overdue, naming the equipment, the tests and the problem, with a link to Equipment. It is a warning only: whether such results may be entered is your facility's procedure.
