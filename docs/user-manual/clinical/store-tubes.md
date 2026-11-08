---
slug: store-tubes
title: Store tubes
order: 118
chapter: Patients and orders
summary: Put tubes away by scan so the chart shows where each one is, with every earlier place kept, and discard them by scan when they are no longer needed.
routes:
  - /specimens/store
---

**Store tubes** (Patients & testing) records where each tube is kept, so it can be found again for a repeat test, an extra crossmatch, or a transfusion reaction workup.

Scan the rack or type the shelf into **Storage place** and press Enter: the page lists every tube recorded there now, so you can check the rack against the system or find a tube in it. Then scan each tube you put there (its accession number or label barcode); each scan stores the tube and waits for the next one. The place field offers the places already in use. A place typed or scanned another way ("rack3" or "RACK-3" for "Rack 3") is the same place and is stored under the spelling already in use, so tubes are not split across misspellings. Add a **Note** such as a position if your racks are not labelled by position. The list under the scanner shows what you stored this session, with where each tube was before.

The patient's **Specimens** tab shows each tube's current place under its status, and the tube's history shows every time it was stored or moved. Scanning a tube into the top-bar lookup also says where it is stored, and a transfusion reaction investigation shows where its pre- and post-transfusion tubes are. A move never erases the earlier place. Storing a tube where it already is, scanning a tube the system does not know, and storing a cancelled tube are refused with the reason. Each store is in the audit trail.

**Discard tubes.** When you throw a tube away, scan it under **Discard tubes** and press Enter. The tube leaves its place (the place stays on its history), the chart shows when it was discarded, and it cannot be stored again. A tube is kept, whatever the reason, while an order on it is not finished or a transfusion reaction investigation on it is still open; the message names the order. Each tube is kept until its expiration plus your facility's **Specimen retention days** (Admin › Facility policy); the place list shows each tube's **Keep until** date and a **Due** badge once it has passed, and **Due for discard** lists every stored tube past that date, oldest first, with its place. To discard a tube before its date, or when your facility has not set a retention period, type a **Reason** first; the reason is kept on the tube and in the audit trail.

How many days tubes are kept is set by your medical director (OCD-070). Until it is set, nothing is listed as due and each discard needs a reason.

> Storing and discarding tubes need the specimen accession permission.

> Demo data. In a development database the reaction demo's pre-transfusion tube ACC0014 is stored in Retention fridge R2, moved there from Bench rack 1.
