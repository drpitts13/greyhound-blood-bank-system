---
slug: shift-handoff
title: Shift handoff
order: 380
chapter: Supervision
summary: Read the queue counts at shift change and write the note the next shift must see.
routes:
  - /handoff
---

![Shift handoff](figures/handoff.png)

**Shift Handoff** shows the counts that matter at change of shift: work still open on the benches you can see, including pending results, open issues, and retrospective crossmatches that are due. You need the shift handoff permission (`shift.handoff`) to open it. Technologists and supervisors have it; read-only and auditor roles do not, because acknowledging a handoff accepts responsibility for the open work.

The counts are a snapshot, not a sign-out of each patient. Write the handoff note for anything the count cannot say: a unit in a cooler that has not been received, a patient whose specimen is unacceptable, an interface that has been down, a retrospective crossmatch that is overdue. The note is required. An empty handoff is not a completed handoff.

If you name the incoming technologist, pick them from the list; the name must be an active user other than you. Leave it blank to hand off to whoever is on the next shift. An active massive transfusion protocol is listed first on the packet.

The next shift should read the note before relying on the dashboard alone. The note does not assign tasks inside the software. It is the record of what you said you left.
