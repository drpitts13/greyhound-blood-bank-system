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
  - /inventory/move
  - /inventory/count
  - /inventory/discard-expired
  - /inventory/return-to-supplier
  - /inventory/return-to-supplier/list
  - /inventory/ship-out
  - /inventory/ship-out/packing-list
---

![Inventory](figures/inventory.png)

**Inventory** is the stock list. One board is open at a time: Search, Expected inbound, Retype, Near expiry, Quarantine, Discrepancy, Operational hold, Recalled, and Open deliveries. Search opens first. Search by unit number, product, blood type, status, or location. Without a status or unit number it lists the units still on a shelf (expired ones included, to discard); tick **Include units no longer in stock** to see issued, transfused, discarded, modified, and shipped units too. A unit number you type or scan, or a status you choose, finds any unit. Available units are the ones you can reserve. Quarantine, hold, missing, and damaged units stay out of routine selection until someone with release permission clears them.

The row shows the storage location by name. A missing name falls back to the location code so you can still see where the unit was filed.

## Boards

**Retype** (`board=retype`, including a unit id) opens the front-type desk. **Scan a unit to retype**: scan the unit label (or type the number) and press Enter to open that unit's retype entry, so a box of new units is worked label by label. A unit not waiting for a retype says so. After you record a retype, the cursor goes back to the scan field for the next label. Received units need a type confirmation before they become available. A match releases the unit. A mismatch quarantines it. An outdated unit that matches is marked expired instead of available. The test worklist lists the same units and opens this board.

**Quarantine** (`board=quarantine`) opens units held for a coded quality reason. The row shows product, location, volume, any ABO or Rh retype mismatch, and the **Next step** for the unit. A unit cannot be released, and the row says what to do instead, when it has expired, when its verified retype disagrees with the label (discard it or return it to the supplier), when it is the remainder of a unit whose reaction investigation is still open (release it after the investigation is closed), or when it was returned with disposal requested. Release uses the same second-verifier gate as the unit's manage actions when policy requires two people. A unit created by a modification also asks for the unit number on the new label. That scan has to match the result. Other quarantine releases do not ask for it. A unit whose product needs a retype that has not been confirmed goes back to **Received** and onto the Retype board, not to available stock. Releasing a unit from **Operational hold** asks how the hold was resolved (for example that the paperwork arrived); the record keeps both why it was held and why it was released. It does not need a second verifier. An expired unit on hold says **Expired: discard it.** and cannot be released. A unit whose retype is not confirmed goes back to **Received**, as above.

After a storage temperature excursion, the units it quarantined are listed with the reason *Storage temperature excursion*. When more than one is waiting, the board offers **Release all after review**: once the excursion has been reviewed (see its deviation), pick the second verifier once and release them together. Each unit is released and audited as it would be from its own row; a unit that cannot be released (expired, a label scan needed, or refused) stays on the board and is named in the message. The **Retype** column says *Retype not interpreted* when a retype was recorded without an interpretation.

**Recalled** (`board=recalled`) lists units recalled (from Lookback or a unit page) and still recorded as Recalled: they cannot be issued, but they are still in the building. The row shows the location, the recall reason, when and by whom it was recalled, and the expiry. A unit that was issued when it was recalled is marked *Was issued: on the ward*, with a link to return it on Issue & Transfuse: get it back and return it there, which closes its issue and quarantines it. The scan here refuses it. Find each unit and scan it in **Scan a recalled unit you have found** (permission *inventory.release*): it is quarantined for the recall (reason *Lookback recall*, noting the recall's reason) and leaves the board, and the scan records that it was found. A scan of a unit that is not recalled is refused with its status. Discard it, or return it to the supplier, from quarantine. You can still quarantine or discard from the unit page. The button shows how many are waiting.

The other boards cover expected inbound units, units near expiry, units on hold, discrepancy units (missing or damaged), and open deliveries. On Near expiry, a unit reserved for a patient says **Reserved for** and the patient, with a link to their products: issue it first or reserve a replacement. When it can be issued now, **Issue** opens the Issue form with the unit and patient filled. An unreserved unit whose product has open orders shows how many; that opens the unit's Actions to allocate it before it expires, and compatibility is checked when you allocate. Open the row rather than changing status from memory. **Discard** comes last on the row: use a unit first. When one cannot be used, Discard asks for a coded reason (Administration › Discard reasons), a comment when that reason needs one, and a second verifier, the same as on the unit page.

## Move units by scan

**Move units** (permission *inventory.transfer*) moves a batch of units to one location, for example to stock a satellite or operating-room refrigerator. Choose **Move to**, then scan each unit number; each scan moves that unit at once and is listed under **This run**, newest first. The checks are the same as a transfer from the unit page: the unit's status must allow a move, and the location must be active, take that kind of component, and have a storage range. A unit already in the location, an unreadable scan, or an unknown number is listed as **Not moved** with the reason. A unit number that was divided matches several units and moves none; scan its product code label next and the division with that product code moves. Every move is in the unit's history and the audit trail.

## Count a location

**Stock count** (permission *inventory.transfer*) checks one refrigerator, freezer, or incubator against the record. Choose the location, then scan every unit on its shelves; the lists update with each scan:

- **Found**: recorded here and scanned.
- **Missing**: recorded here but not scanned. Look for it, then check the unit's history.
- **Recorded elsewhere**: on the shelf, but recorded in another location. **Move here** corrects the record with the transfer checks and audit.
- **Not on hand**: on the shelf, but its record says issued, transfused, discarded, or similar. Take it out and hold it, and find out why before anything is issued.
- **Wrong storage**: a found or missing unit whose component this location does not take (for example platelets in a red-cell refrigerator) is marked *Wrong storage*. Move it to a location that takes it and check whether it is still fit for use. Receiving a unit into such a location is refused, as a move there is.
- **Unresolved scans**: an unreadable or unknown number, or a divided number matching several units. Scan a divided unit's number and then its product code label; the two are read as one scan and count that division. The cooler check reads them the same way.

Expired units still on a shelf are expected and marked **Expired**: discard them. Scanning changes nothing. **Finish count** records the outcome (counts, and the missing, misplaced, and not-on-hand units) in the audit trail; **Count again** starts over. Choosing a location shows when it was last counted and whether everything was found; **Recent counts** lists the latest finished counts across locations. After finishing, search for anything missing first; a unit that cannot be found can be marked missing from the Missing list (the reason names the count). A scanned unit whose record says Missing offers **Found here**: it goes to quarantine for inspection, and the count is redone.

## Expiration

The system moves units that have reached their expiration date to Expired by itself, every five minutes by default. A reservation on such a unit ends at the same time, with the reason recorded, so the patient no longer shows blood held that cannot be issued. The history and audit trail record the change under the system account. **Run expiration sweep** still does the same thing on demand for staff with discard permission. A unit is blocked at issue once it expires, even before the sweep runs.

**Discard expired** lists the expired units still recorded on a shelf, oldest first, with where each one is. Pull each unit and scan it: the scan discards it with the reason EXPIRED. If your facility requires a second verifier for discards, choose them first; they stay chosen for the run. A unit that is not expired is refused; discard it from its unit page with its own reason. A stock count's Expired badge opens this page.

**Return to supplier** packs a return shipment, such as a recall or short-dated stock. Give the reason once, then scan each unit; each scan returns that unit, recorded and audited as from the unit page. A reserved or issued unit is refused with its status: release the reservation first. **Print return list** opens, in a new tab, the return list for the return the last scan went into: the reason, when and by whom the units were returned, the suppliers, and each unit (number, product code, ABO/Rh, expiry, supplier) with a column for the receiver's initials, and spaces for the courier or supplier, the container and temperature, and a signature. Units returned for the same reason on the same day are one return. **Recent returns** lists the last 30 days, each with **Print return list** to print it again. A unit already returned, or already marked missing, cannot be returned or marked missing again, so the reason recorded the first time stays.

**Ship to facility** packs an outbound shipment to another hospital (stock balancing, or a patient transferred with blood). Name the receiving facility and, if you have one, the shipment reference (packing list or courier number) once. Then record the **container** (pick a transport container or type its ID), the **packing temperature** in °C, who **packed** it (it starts as you), and the **receiving contact** (name and phone at the other blood bank). Scanning stays unavailable until all of these are filled in, and they stay filled for every unit of the shipment (OCD-072). A temperature outside -50 to 50 °C is refused as a typing slip. Then scan each unit. Each scan ships that unit: it leaves your inventory as **ShippedOut**, and the unit page says where, when, and the reference, and below that how it was packed and the receiving contact (*Packing details: not recorded* for a unit shipped before these were kept). Only an unreserved, unexpired unit in stock ships; a reserved, quarantined, held, expired, or already shipped unit is refused with the reason. **Print packing list** opens the packing list for the shipment the last scan went into, in a new tab: a sheet of its own with the facility, reference, shipped time, container, packing temperature, packer and receiving contact, each unit (number, product code, ABO/Rh, expiry) with a column for the receiver's initials, and spaces for who received it, when, the temperature on arrival and a signature. Print it and put it in the container. A shipment recorded before packing details were kept says to write them by hand. The facility field offers the facilities you have shipped to before, and **Recent shipments** lists the last 30 days by facility and reference (or day, without one): **Units** shows what went in each, with **Print packing list** to open that shipment's packing list again. Scanning or typing a shipment reference into the top-bar lookup opens it here (searched over the past year), and a shipped unit found in the lookup says where it went. If the donation is later recalled, Lookback does not try to recall a shipped unit here; it lists a notification naming the facility to notify and the receiving contact recorded on the shipment. Each shipment in Recent shipments shows its container, temperature, packer and receiving contact; shipments recorded before these were required say *Not recorded*.

## Start the next task

**New delivery** starts a shipment from a supplier. **ISBT Receive** and **Modify Products** are in the sidebar under Inventory. Open a unit number to see its full chart: status, attributes, history, and the actions that are legal for the state it is in.

> Facility policy. Which statuses can move to which other statuses is fixed. If an action is missing, the unit is not in a state that allows it. Do not expect a button for discard, transfer, or release on every row.

> Demo data. In a development database, shipment SHIP-DEMO-01 sent two group O red cells (W000123SHP0001 and W000123SHP0002) to St. Mary's Hospital yesterday.
