---
slug: transport-containers
title: Transport containers
order: 184
chapter: Catalog
summary: Coolers, insulated bags, and boxes units are packed into, with your facility's validated temperature range and hours.
routes:
  - /admin/transport-containers
---

**Transport Containers** lists the coolers and other vessels your blood bank packs units into. When a massive transfusion pack is recorded on Issue & Transfuse, the tech picks one of these. **None are shipped.**

- **Code** and **Name** identify the container. **Type** is cooler, insulated bag, box, or other.
- **Lowest °C**, **Highest °C**, and **Validated hours** come from your facility's own container validation. Leave them blank until you have validated the container. The software does not supply them.
- When a range is set, a temperature recorded at packing that falls outside it is saved, flagged, and printed on the pack report. It is not blocked.
- When validated hours are set, the pack report says the container is validated for that many hours from the packed time.
- Changes go to configuration history. Deactivate a container that is out of service; past packs keep showing it.
