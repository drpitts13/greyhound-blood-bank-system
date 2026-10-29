---
slug: isbt-receive
title: ISBT receive
order: 220
chapter: Inventory
summary: Scan or type an ISBT 128 label to bring a unit into inventory without a packing-list delivery.
routes:
  - /inventory/isbt-receive
---

![ISBT receive](figures/isbt-receive.png)

**ISBT Receive** is for scanner or keyboard entry of an ISBT 128 label: the donation identification number, the product code, and the expiration. Use it when the unit is not already on a delivery packing list. Use a delivery when you are working a shipment.

Start a scan session and scan the bag. The product code must exist in the ISBT catalog. The catalog shipped with the system is a placeholder until your facility imports a licensed ICCBBA extract. If a real product code is rejected as unknown, do not invent a local code that looks similar. Ask an administrator to import or add the code your license allows.

Confirm ABO, Rh, and expiration against the label after the scan. The scan can misread a damaged barcode. Keyboard entry is there for that case. Treat keyboard entry with the same second check you would give a scan.

> Facility policy. Official ISBT codes are licensed. The software does not include the ICCBBA tables. A code that is missing is a catalog gap, not a reason to relabel the unit.

Received units follow the same retype and quarantine rules as units received on a delivery.
