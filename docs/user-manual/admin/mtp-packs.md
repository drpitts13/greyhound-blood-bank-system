---
slug: mtp-packs
title: MTP packs
order: 183
chapter: Catalog
summary: Units of each component per round of the massive transfusion protocol, shown as guidance on the issue desk.
routes:
  - /admin/mtp-packs
---

**MTP Packs** holds your facility's massive transfusion protocol packs: how many units of red cells, plasma, platelets, cryo, and whole blood make up one round. **No pack is shipped.** Enter only packs your medical director has approved. This is open decision OCD-012, which **requires SME verification**.

- A new pack starts with every component at 0. Enter the units per round, from 0 to 50 for each component, with at least one above 0.
- When someone activates a protocol on Issue & Transfuse, they can choose an active pack. The red protocol banner then shows the current round and how many units of each component have gone in that round, for example "Red cells 2/4".
- Units count toward a round when they are issued and not returned.
- A pack is guidance only. It never blocks an issue, never adds a unit, and does not replace any safety check.
- Changing, activating, or deactivating a pack asks for the reason (at least 8 characters), and it is recorded in configuration history. Adding a new pack does not ask. A protocol already running keeps the pack it was activated with; its guidance follows the pack as it is now.
