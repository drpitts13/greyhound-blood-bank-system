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

**Quarantine** (`board=quarantine`) opens units held for a coded quality reason. The row shows product, location, volume, any ABO or Rh retype mismatch, and the **Next step** for the unit. A unit cannot be released, and the row says what to do instead, when it has expired, when its verified retype disagrees with the label (discard it or return it to the supplier), when it is the remainder of a unit whose reaction investigation is still open (release it after the investigation is closed), or when it was returned with disposal requested. Release uses the same second-verifier gate as the unit's manage actions when policy requires two people. A unit created by a modification also asks for the unit number on the new label. That scan has to match the result. Other quarantine releases do not ask for it. A unit whose product needs a retype that has not been confirmed goes back to **Received** and onto the Retype board, not to available stock. Releasing a unit from **Operational hold** asks how the hold was resolved (for example that the paperwork arrived); the record keeps both why it was held and why it was released. It does not need a second verifier. An expired unit on hold says **Expired: discard it.** and cannot be released. A unit whose retype is not confirmed goes back to **Received**, as above.

The other boards cover expected inbound units, units near expiry, units on hold, discrepancy units (missing or damaged), and open deliveries. On Near expiry, a unit reserved for a patient says **Reserved for** and the patient, with a link to their products: issue it first or reserve a replacement. When it can be issued now, **Issue** opens the Issue form with the unit and patient filled. An unreserved unit whose product has open orders shows how many; that opens the unit's Actions to allocate it before it expires, and compatibility is checked when you allocate. Open the row rather than changing status from memory.

## Expiration

The system moves units that have reached their expiration date to Expired by itself, every five minutes by default. A reservation on such a unit ends at the same time, with the reason recorded, so the patient no longer shows blood held that cannot be issued. The history and audit trail record the change under the system account. **Run expiration sweep** still does the same thing on demand for staff with discard permission. A unit is blocked at issue once it expires, even before the sweep runs.

## Start the next task

**New delivery** starts a shipment from a supplier. **ISBT Receive** and **Modify Products** are in the sidebar under Inventory. Open a unit number to see its full chart: status, attributes, history, and the actions that are legal for the state it is in.

> Facility policy. Which statuses can move to which other statuses is fixed. If an action is missing, the unit is not in a state that allows it. Do not expect a button for discard, transfer, or release on every row.
