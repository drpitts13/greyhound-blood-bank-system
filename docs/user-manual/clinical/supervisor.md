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

## Reported transfusion

When the EHR reports a unit transfused that the blood bank had recalled, quarantined, or recorded as damaged or missing after it was issued, the transfusion is recorded and the unit keeps its status (OCD-083). The row says which unit, which patient, when, and the unit's status and reason. Open the unit, find out what happened (for a recall, the recipient notification on Lookback is the next step), then **Review**: say what the review found and choose **Reviewed**. The review is recorded with your name and audited, and the row leaves the queue. You need inventory release or override approval permission. Such a unit keeps its status for good: it leaves the Quarantine and Discrepancy boards, and releasing, locating, inspecting or discarding it is refused, since it was transfused. The home Supervisor queue card counts them (*reported transfusions to review*), and the patient's Product History and timeline, and the unit's own Status history, show the transfusion as reported over the unit's status, with the review once it is done.

## Override review

Every override (an emergency release, a massive-transfusion release, or an issue or result that went ahead despite a warning) waits here for a second person. The title says which kind it was. The row shows what was overridden, the rule, the reason, who authorized it, who performed it, and when. **Open** goes to the unit when the override was on an issue.

Choose **Review**, then **Acceptable** or **Needs follow-up**. Follow-up needs a note saying what must happen. You cannot review an override you performed or authorized. The review is recorded on the override and in the audit trail; it does not undo anything. Who may review, and how soon, is your facility's policy.

> Warning. Releasing a unit from quarantine makes it selectable for patients. Read the quarantine reason before you release. A retype mismatch is not a routine release.

Quarantine rows give the reason in words and the quarantine board's next step; **Release** is offered only when the board would allow it (otherwise **Open**, for example a reaction remainder whose investigation is still open). An override row names the record as staff know it, for example "Emergency release — unit W0001230001004 for Trauma, John Doe (MRN0005)".
