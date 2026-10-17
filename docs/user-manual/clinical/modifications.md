---
slug: modifications
title: Modify products
order: 240
chapter: Inventory
summary: Divide, pool, irradiate, thaw, wash, aliquot, volume-reduce, or leukoreduce a unit under the modification rules.
routes:
  - /inventory/modify
---

![Modify products](figures/modifications.png)

**Modify Products** performs component processing. Each modification is defined by an administrator rule: which source products are allowed, what the resulting product code is, and how the expiration changes. You pick the modification and the source unit. You do not invent a new product code at the bench.

## What happens to the units

The source unit is consumed or reduced according to the rule. Resulting units are new stock. They start in quarantine until they are released, so a just-irradiated or just-pooled unit is not immediately available for issue. Release them from Inventory or the unit chart when the modification has been checked. Scan the unit number printed on the new label. The scan has to match that result. A blank or different number is a hard stop. A quarantine release that is not a modification result does not ask for this scan.

Thaw of a plasma product that your facility marked for auto-thaw can also happen at allocation. Those units expire 24 hours from the thaw time. The product row shows the new expiration. Do not issue on the old frozen expiration.

Pooling, irradiation, and similar changes may apply an expiration-modification code. The new date is calculated. If the date looks wrong, stop and check the modification rule and the expiration code with a supervisor. Do not overwrite the expiration by hand.

## Supply lot

You can record a supply description and lot with the modification. Both are optional. They are stored on the modification, written to the audit record, and shown on modification history. Facility policy can require the lot. That policy ships off.

> Facility policy. When require supply lot is on, a blank lot is a hard stop and the units are not modified.

## Batch

For irradiate, thaw, volume reduction, leukoreduction, and wash, you can apply the selected rule and the same reason to the unit on the form plus the unit numbers you list. If any unit is ineligible, none of them are modified.

## Orders that asked for the modification

A product order can request irradiate, wash, thaw, volume reduce, or leukoreduce. Completing that action here satisfies the order and can queue the action charge. Complete the action only when the physical modification is done.

> Warning. Resulting units are quarantined on purpose. Issuing the source unit after it has been modified, or issuing the child unit before release, is blocked by unit status.

> Hard stop. A modification result stays in quarantine when the scanned unit number is blank or does not match the result.
