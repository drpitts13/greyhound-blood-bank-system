---
slug: unit-detail
title: The unit chart
order: 230
chapter: Inventory
summary: Open one unit to see status, attributes, history, retype, and the actions that status allows.
routes:
  - /inventory/units/{unitId}
  - /inventory/units/{unitId}?tab=actions
  - /inventory/units/{unitId}?tab=attributes
  - /inventory/units/{unitId}?tab=history
  - /inventory/units/{unitId}?tab=retype
---

![Unit chart](figures/unit-detail.png)

Open a unit from Inventory, from a delivery, or from a patient's product row. The header shows the facility unit number, product, ABO and Rh, expiration, and status.

## Tabs

**Overview** shows where the unit is, why it is quarantined or on hold if it is, and the receipt inspection (appearance, temperature, visual notes).

**Actions** are the moves that are legal from the current status: quarantine, hold, release, transfer to another location, allocate to a patient order, discard, return to the supplier, or mark missing or damaged. Each action asks for the reason the rule requires. Discard and some releases need a higher permission than everyday receive and transfer. If the button is disabled, you either lack the permission or the unit status does not allow the move.

**Attributes** lists antigens and modifications (irradiated, leukoreduced, and the others recorded on the unit). Antigen typing you performed is recorded with [Unit antigen typing](unit-antigen-typing) or on the retype tab.

**History** is the status log. It is append-only. You cannot delete a prior status to tidy the story.

**Retype** records the front type. A mismatch quarantines the unit. Do not release a mismatch from this tab. Release, if your medical director allows it at all, is a quarantine-release on the supervisor queue with the second verifier.

## Allocate

Allocation from the unit asks for the patient order. The order must be an open product order for a patient who may receive this unit. Autologous and directed units only allocate to the intended patient. The screen warns when the unit is not typed for an antigen the patient must avoid, and when a red cell has not been retyped.

Printing a component label is available from the unit when you have print permission. Reprinting asks for a reason.
