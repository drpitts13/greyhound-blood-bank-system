---
slug: mtp-pack-report
title: MTP pack report
order: 262
chapter: Compatibility and issue
summary: Record each container sent during a massive transfusion protocol and print the sheet that travels with it.
routes:
  - /mtp/pack-records/{id}
---

During an active massive transfusion protocol, each container you send is recorded as a **pack**.

## Record a pack

1. Issue the units under the protocol as usual. Each unit passes every safety check.
2. On the red protocol banner, choose **Pack a cooler…**.
3. Pick the **Container** and, if you have several of the same kind, which one (for example "Cooler 3").
4. Read the thermometer and enter the **Temperature °C**. This is required. A reading outside the container's range is saved, flagged, and printed; it is not blocked.
5. Tick the units going in. Only units issued under this protocol, still issued, and not already in a pack are listed. **Select all** ticks every one shown.
6. Choose **Record pack**. The packed date and time and your name are saved with it.

## Print the report

Under **Packs sent** on the banner, open the pack and choose **Print**. Printing is recorded in the audit trail. The sheet shows:

- the patient, the protocol, who called it and why, the pack definition (if one was chosen), the pack number, when it was packed and by whom, the container, and the temperature at packing
- each unit: unit number, product type, product code, ABO/Rh, expiration, and whether it was crossmatched
- blank columns for the receiving clinicians to write the date and time they checked each unit and their initials
- blank lines for who received the pack, the date and time, and initials, and for the ordering provider's printed name, date, and signature
- the statements printed on every pack report

All times on the sheet are UTC, like the rest of the system.

The statements are general handling reminders and say how the units were released. They make no time limit of their own: a time limit appears only when your facility has entered the container's validated hours. Your transfusion committee should review the wording before go-live (OCD-045).
