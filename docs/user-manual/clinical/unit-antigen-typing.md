---
slug: unit-antigen-typing
title: Unit antigen typing
order: 180
chapter: Testing
summary: Record antigen typing performed on a specific unit for this patient.
routes:
  - /patients/{patientId}/unit-antigen-typing/{bloodUnitId}
---

![Unit antigen typing](figures/unit-antigen-typing.png)

Open unit antigen typing from the patient and the unit you are typing. This is a result on that unit, not a change to the patient's antibody history. Use it when you type a unit for an antigen the patient needs to avoid, or when you are confirming a labeled antigen.

Enter the antigen, the method, and the reaction your procedure uses. The unit's antigen record is what allocation and issue compare with the patient's antigen-negative requirements. A unit that was not typed for a required antigen is not treated as antigen-negative.

> Warning. An antigen-negative requirement that is not met is a warning at allocation and can block or warn at issue, depending on the rule. Typing the unit is what clears it. A comment on the chart does not.

The unit chart in Inventory shows the same attributes. You can open the unit from here to see location, status, and the rest of the unit record.
