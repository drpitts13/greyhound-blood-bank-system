---
slug: inventory
title: Inventory
order: 190
chapter: Inventory
summary: Search units, work the retype and quarantine boards, and open a unit or a delivery.
routes:
  - /inventory
  - /inventory?board=retype
  - /inventory?board=quarantine
---

![Inventory](figures/inventory.png)

**Inventory** is the stock list. One board is open at a time: Search, Expected inbound, Retype, Near expiry, Quarantine, Discrepancy, Operational hold, and Open deliveries. Search opens first. Search by unit number, product, blood type, status, or location. Available units are the ones you can reserve. Quarantine, hold, missing, and damaged units stay out of routine selection until someone with release permission clears them.

The row shows the storage location by name. A missing name falls back to the location code so you can still see where the unit was filed.

## Boards

**Retype** (`board=retype`, including a unit id) opens the front-type desk. Received units need a type confirmation before they become available. A match releases the unit. A mismatch quarantines it. An outdated unit that matches is marked expired instead of available. The test worklist lists the same units and opens this board.

**Quarantine** (`board=quarantine`) opens units held for a coded quality reason. The row shows product, location, volume, and any ABO or Rh retype mismatch. Release uses the same second-verifier gate as the unit's manage actions when policy requires two people. A unit created by a modification also asks for the unit number on the new label. That scan has to match the result. Other quarantine releases do not ask for it.

The other boards cover expected inbound units, units near expiry, units on hold, discrepancy units (missing or damaged), and open deliveries. Open the row rather than changing status from memory.

## Start the next task

**New delivery** starts a shipment from a supplier. **ISBT Receive** and **Modify Products** are in the sidebar under Receive and modify. Open a unit number to see its full chart: status, attributes, history, and the actions that are legal for the state it is in.

> Facility policy. Which statuses can move to which other statuses is fixed. If an action is missing, the unit is not in a state that allows it. Do not expect a button for discard, transfer, or release on every row.
