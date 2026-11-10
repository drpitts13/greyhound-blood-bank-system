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

Open a workup from the [worklist](antibody-worklist) or from the patient chart. On the training data, Kellerman, Diane (MRN0004) has a workup in progress on tube ACC-DEMO-ABID1: IS is read, AHG is part read, and 37C is not read. The screen shows the panel, the phases, and the reactions you enter. The lot must be an active antibody-panel lot. A withdrawn lot cannot be accepted or completed.

## What you record

Select the specimen and the panel lot. When only one panel lot is active and in date, it is already selected and the form says so; with several, you pick. When none is, the form says a workup cannot be opened and links to **Antibody panel lots**. Enter the graded reactions for each cell and phase. The phase list (immediate spin, 37°C, AHG, check cells, and any others your laboratory defined) comes from Administration. A cell starts at **Not tested** until you grade it. When you have read a phase, **Rest 0** in that phase's heading sets the cells still at Not tested to 0. Those cells are outlined until you save: check each against its tube and change any that reacted (whether this aid is allowed is OCD-079). **Save reactions** saves only the cells you changed and says how many; with nothing changed it saves nothing. Changing a grade after the workup is interpreted or reviewed clears the interpretation and review for re-interpretation; saving unchanged grades does not. Each change is kept in the audit trail with the grade it replaced.

**Assist** compares the pattern with the panel antigram and suggests specificities that fit. Assist does not identify the antibody. You still interpret. Pick each antibody from the catalog, or type it: text that names one catalog antibody (for example "anti-k") is listed as that antibody, marked catalog, because that is how it will post. Text the catalog does not hold, or text naming more than one antibody, is listed as typed and marked as not in the catalog; it will not drive antigen-negative selection. The identified specificity is posted to the patient's antibody history only when the workup is completed under your procedure, including supervisor review when policy requires it.

## While the workup is open

You cannot add or remove antibodies on the chart. That prevents the history and the workup from diverging. Serologic crossmatch can still be recorded. The issue desk shows an open-workup warning and still allows issue. Electronic crossmatch is a hard stop until the workup is no longer open.

> Hard stop. Electronic crossmatch is refused while an antibody-identification workup is open. Deactivating an antibody after the workup, or marking it currently undetectable, does not restore electronic-crossmatch eligibility.

## Supervisor review

When the workup needs review, it appears on the [Supervisor queue](supervisor). The reviewer confirms the interpretation. Until the technologist records an interpretation, the review card says it is waiting. If you recorded the interpretation yourself and your facility blocks self-review, the card says another supervisor must accept it and Accept is unavailable; you can still reject it. Completing the workup writes the identified specificities into antibody history as identified antibodies. Those rows drive antigen-negative selection and block electronic crossmatch from that point forward.

If the workup is closed without an identification, history is not given a new specificity. Say that in the workup comment so the next shift can see why the panel was closed.
