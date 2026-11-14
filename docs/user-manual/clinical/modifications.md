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

**Modify Products** performs component processing. Each modification is defined by an administrator rule: which source products are allowed, what the resulting product code is, and how the expiration changes. You pick the modification and the source unit. You do not invent a new product code at the bench. Opened from the menu, the cursor is already in **Unit number**, so you can scan the source unit straight away; opened on a unit from its page or the patient chart, the unit is already chosen.

## What happens to the units

The source unit is consumed or reduced according to the rule. Resulting units are new stock. They start in quarantine until they are released, so a just-irradiated or just-pooled unit is not immediately available for issue. Release them from Inventory or the unit chart when the modification has been checked. Scan the unit number printed on the new label. The scan has to match that result. A blank or different number is a hard stop. A quarantine release that is not a modification result does not ask for this scan.

Thaw of a plasma product that your facility marked for auto-thaw can also happen at allocation. Those units expire after the thawed shelf life set for their product code (Admin › Auto thaw products), or at the frozen expiration if that is sooner. If the bag was entered, tick **Thawed in an open system** in the thaw prompt: the code's open-system shelf life applies, and a pooled unit uses its pooled shelf life (the shorter if both). The prompt's expiration sentence follows the box: it states the time that will apply, and says when the unit is a pool. When the time that applies is not set, the prompt says so in red and **Yes** stays unavailable; ask whoever edits configuration to enter it (Admin › Auto thaw products), or clear the box if the bag was not entered. The product row shows the new expiration. Do not issue on the old frozen expiration.

Pooling, irradiation, and similar changes may apply an expiration-modification code. The new date is calculated. If the date looks wrong, stop and check the modification rule and the expiration code with a supervisor. Do not overwrite the expiration by hand.

## Supply lot

You can record a supply description and lot with the modification. Both are optional. They are stored on the modification, written to the audit record, and shown on modification history. Facility policy can require the lot. That policy ships off.

> Facility policy. When require supply lot is on, a blank lot is a hard stop and the units are not modified.

## Device and dose indicator

Irradiation asks for the **Irradiator** (scan or type its id) and the **Dose indicator** reading: **Shows irradiated** or **Does not show irradiated**. Other one-to-one modifications (wash, thaw, volume reduction) take an optional **Device**. Both are stored on the modification, written to the audit record, and shown in the **Device** column of modification history. The irradiator stays filled for the next unit; the indicator is cleared, because it is read on each unit.

If the indicator does not show irradiated, the unit is not recorded as irradiated and nothing is changed: follow the irradiation SOP for that unit.

> Facility policy. When **Require irradiator and dose indicator** is on, an irradiation with no irradiator or an unchecked indicator is a hard stop. That policy ships off (OCD-067).

## Batch

For irradiate, thaw, volume reduction, leukoreduction, and wash, you can apply the selected rule and the same reason to the unit on the form plus the unit numbers you list. If any unit is ineligible, none of them are modified.

## Orders that asked for the modification

A product order can request irradiate, wash, thaw, volume reduce, or leukoreduce. Completing that action here satisfies the order and can queue the action charge. Complete the action only when the physical modification is done.

> Warning. Resulting units are quarantined on purpose. Issuing the source unit after it has been modified, or issuing the child unit before release, is blocked by unit status.

> Hard stop. A modification result stays in quarantine when the scanned unit number is blank or does not match the result.

## Where the result goes

**Result location** chooses where the new unit is recorded; *Same as the source* keeps the source's location. The location must be active and take the result's component. A thawed unit cannot stay in a freezer or go to one: a thaw from a freezer asks for the refrigerator it goes to. In a batch every unit must have a place before any is modified.
