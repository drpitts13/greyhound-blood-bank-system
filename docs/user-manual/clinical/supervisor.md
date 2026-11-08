---
slug: supervisor
title: Supervisor queue
order: 370
chapter: Supervision
summary: Review antibody identifications, verify results that are waiting, and release quarantined units that need a higher permission.
routes:
  - /supervisor
---

![Supervisor queue](figures/supervisor.png)

The **Supervisor queue** collects work that a technologist cannot finish alone:

- Antibody-identification workups waiting for review.
- Results waiting for verification when you are the verifier.
- Quarantine releases that require the release permission and, when policy says so, a second verifier.
- Missing units to locate and damaged units to inspect, with the reason recorded and where each was last recorded (release permission). A unit leaves the queue once it is located, inspected, or discarded.
- RhIG evaluations the bench has filled in and that are ready to complete, when you verify results. The row opens the patient's RhIG tab, where you complete it.
- Overrides waiting for review, when you hold the override approval permission (`override.approve`). The Supervisor role has it by default; your administrator can change that.

A result that must be verified before its specimen expires is marked **Due before specimen expiry** in red. A row about a patient links to the patient's chart from its title. A link to `/supervisor?kind=OverrideReview` (or any other kind) opens the queue on that kind of work.

Open the row, read the underlying record, and complete the review on that record. The queue is a list. The decision is recorded on the workup, the result, or the unit, not as a checkmark that skips the screen.

You see the sections your permissions allow. A supervisor who cannot release inventory will not see release rows. If a technologist says an item is "in the queue" and you do not see it, confirm you are signed in as yourself and that the item was actually submitted.

## Override review

Every override (an emergency release, a massive-transfusion release, or an issue or result that went ahead despite a warning) waits here for a second person. The title says which kind it was. The row shows what was overridden, the rule, the reason, who authorized it, who performed it, and when. **Open** goes to the unit when the override was on an issue.

Choose **Review**, then **Acceptable** or **Needs follow-up**. Follow-up needs a note saying what must happen. You cannot review an override you performed or authorized. The review is recorded on the override and in the audit trail; it does not undo anything. Who may review, and how soon, is your facility's policy.

> Warning. Releasing a unit from quarantine makes it selectable for patients. Read the quarantine reason before you release. A retype mismatch is not a routine release.
