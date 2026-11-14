---
slug: patient-timeline
title: Patient timeline
order: 61
chapter: Patients and orders
summary: One read-only list of everything recorded for the patient, newest first, with safety events marked.
routes:
  - /patients/{id}?tab=timeline
---

The **Timeline** tab lists everything recorded for the patient in one place, newest first and grouped by day. Use it to see what happened before you act, instead of opening each tab.

It shows:

- **Visits**: started and ended.
- **Orders**: test and product orders, with their current status. A cancelled order shows the cancellation reason.
- **Specimens**: collection and receipt. A rejected specimen shows the rejection reason; an infant's specimen valid for the admission says so.
- **Testing**: results entered and verified. A correction shows its reason, and the original result is kept and marked as replaced. An invalidated result shows who invalidated it and why. Blood type records are listed too.
- **Antibodies and requirements**: each recorded antibody and each special transfusion requirement. An antibody reported by another facility has a second event when this laboratory confirms it, with how (for example the panel or the antibody identification workup) and who.
- **Products and transfusion**: reservations (each saying what became of it: still reserved, issued, released with the reason, or ended because the unit expired, naming the unit and when), crossmatches with their result, issues, receipt on the ward, returns, transfusions, and reaction reports. A transfusion still running shows as "transfusion started, in progress", not as transfused. An emergency or overridden issue shows the override rule, reason, and who authorized it.
  A transfusion the EHR reported while the blood bank had the unit recalled, quarantined, damaged or missing says so, and stays a warning until a supervisor has reviewed it (then it names the reviewer and what was found).
  An issue also says how it was entered when that was not at the time at the issue desk: a late entry, a paper record entered after downtime, a time outside every recorded outage (with the reason), or an infant given group O without a crossmatch.

**Markings**

- **Critical** (red edge): an antibody, an incompatible crossmatch, a suspected reaction, or a reaction report.
- **Attention** (amber edge): a correction, an invalidation, a cancelled order, a rejected specimen, a special requirement, or an emergency or overridden issue.
- Everything else is informational.

Use the filter buttons to show one kind of event. **Warnings and critical only** hides the informational rows. Click an event to open its tab or the unit.

**Times.** Each event uses the time stored on that record, shown in this workstation's local time. When a change has no time of its own, such as a cancellation or a rejection, it is shown on the order or specimen it changed. The timeline does not guess when it happened. The audit trail has the exact time of every change.

The timeline only reads. Nothing on it can be edited.
