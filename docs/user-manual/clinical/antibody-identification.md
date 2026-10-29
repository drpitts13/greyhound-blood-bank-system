---
slug: antibody-identification
title: Antibody identification
order: 170
chapter: Testing
summary: Record panel reactions, use assist without letting it identify the antibody, and send the workup for supervisor review.
routes:
  - /patients/{patientId}/antibody-id
  - /patients/{patientId}/antibody-id/{workupId}
---

![Antibody identification](figures/antibody-identification.png)

Open a workup from the [worklist](antibody-worklist) or from the patient chart. The screen shows the panel, the phases, and the reactions you enter. The lot must be an active antibody-panel lot. A withdrawn lot cannot be accepted or completed.

## What you record

Select the specimen and the panel lot. Enter the graded reactions for each cell and phase. The phase list (immediate spin, 37°C, AHG, check cells, and any others your laboratory defined) comes from Administration.

**Assist** compares the pattern with the panel antigram and suggests specificities that fit. Assist does not identify the antibody. You still interpret. The identified specificity is posted to the patient's antibody history only when the workup is completed under your procedure, including supervisor review when policy requires it.

## While the workup is open

You cannot add or remove antibodies on the chart. That prevents the history and the workup from diverging. Serologic crossmatch can still be recorded. The issue desk shows an open-workup warning and still allows issue. Electronic crossmatch is a hard stop until the workup is no longer open.

> Hard stop. Electronic crossmatch is refused while an antibody-identification workup is open. Deactivating an antibody after the workup, or marking it currently undetectable, does not restore electronic-crossmatch eligibility.

## Supervisor review

When the workup needs review, it appears on the [Supervisor queue](supervisor). The reviewer confirms the interpretation. Completing the workup writes the identified specificities into antibody history as identified antibodies. Those rows drive antigen-negative selection and block electronic crossmatch from that point forward.

If the workup is closed without an identification, history is not given a new specificity. Say that in the workup comment so the next shift can see why the panel was closed.
