---
slug: storage-devices
title: Refrigerators and freezers
order: 341
chapter: Inventory and ISBT
summary: Add temperature-controlled storage devices, set their ranges, and deactivate retired ones.
routes:
  - /admin/storage-devices
---

**Refrigerators & Freezers** lists the temperature-controlled storage devices: refrigerators, freezers, platelet incubators, and satellite refrigerators. Each row shows the range, the last reading, and how many units are stored there now. The same devices appear under Inventory locations; this page is the short form for the temperature log.

## Add a device

Select **Add device** and enter a code, a name, and the type. The code is stored in upper case and cannot change later. Leave **Min °C** and **Max °C** blank to use the AABB storage default for the type, or enter the facility's range. Use **Notes** for the make, model, serial number, or asset tag.

A new device also gets the storage-class and issue settings for its type. Change those in Admin › Inventory locations if the facility uses the device differently.

Once saved and active, the device appears on Storage temperatures and can take readings.

## Edit a device

**Edit** changes the name, department, range, and notes. Storage class, issue, and time-out settings are kept as they are. A range change applies to new readings only; past readings keep the range that was in force when they were taken.

## Deactivate a device

**Deactivate** asks for confirmation, then removes the device from Storage temperatures and stops new readings. A device that still holds units cannot be deactivated: transfer the units first. **Activate** puts it back into service.

A deactivated device keeps its history. Select **History** on its row, or choose it on the temperature history report.
