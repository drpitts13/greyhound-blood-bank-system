---
slug: instrument-setup
title: Instrument interface setup
order: 400
chapter: Interfaces and printers
summary: Configure ASTM instrument connections, field maps, test mappings, and if/then message rules. Open Help on this screen for the catalog.
routes:
  - /admin/instruments
---

![Instrument interface setup](figures/admin-instruments.png)

**Instrument Interfaces** in Administration is the configuration for each analyzer connection. The status log that shows whether messages are arriving is the operational [Instrument interfaces](/help/clinical/instruments) screen. The [simulator](/help/clinical/instrument-simulator) sends a test message.

On this page you set the connection, the field map, which instrument test code becomes which LIS test, and if/then rules that adjust a message before it files. **Help** on this screen lists the instrument catalog fields. Use that panel while you are editing. It stays current with the catalog.

A test code that is not mapped does not file a result. The message appears as an error on the operational log. Map the code, then decide whether to replay. Do not map two instrument codes to the same LIS test unless both codes really are that test. You will verify indistinguishable results.

> Warning. A mapping change affects the next message from that instrument, including a live analyzer. Prove the map with the simulator on a training accession first.
