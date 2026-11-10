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

**Shift Handoff** shows the counts that matter at change of shift: work still open on the benches you can see, including pending results (how many are already entered and waiting for someone to verify them, and how many must be verified before their specimen expires), open issues, reservations past their end time, units near expiry (and how many of those are reserved), coolers still out (and how many were not checked before they left or were checked with differences), expired units still on a shelf (to discard), quality deviations not yet closed (a critical one leads the packet as CRITICAL DEVIATION OPEN), and retrospective crossmatches that are due. You need the shift handoff permission (`shift.handoff`) to open it. Technologists and supervisors have it; read-only and auditor roles do not, because acknowledging a handoff accepts responsibility for the open work.

**What you are handing over**, above the note, shows the counts the handoff will carry right now as links, the urgent ones first in red (**Refresh counts** updates them); the handoff waiting for acknowledgment is shown the same way, so the incoming technologist can open each queue from it. The home screen's **Shift handoff** card names the open handoff's urgent items too, so you can add to the note whatever the counts do not show. The handoff counts again when you record it.

The packet also counts products below their minimum stock level (**BELOW MINIMUM STOCK n products**), so the next shift knows to check Minimum stock and order or move blood. It leads with **REAGENT QC FAILED** when a reagent lot's latest QC failed, with the number of results tested with it still to review (Reagent QC), and counts open **instrument failures** and how many are results held for review (Instruments), open **HL7 errors** (orders, results, or admissions that did not file or send), and **unmapped interface values** waiting for a translation.

The counts are a snapshot, not a sign-out of each patient. Write the handoff note for anything the count cannot say: a unit in a cooler that has not been received, a patient whose specimen is unacceptable, an interface that has been down, a retrospective crossmatch that is overdue. The note is required. An empty handoff is not a completed handoff.

If you name the incoming technologist, pick them from the list; the name must be an active user other than you. Leave it blank to hand off to whoever is on the next shift. An active massive transfusion protocol is listed first on the packet (with how many are idle, to close: open with nothing issued for the facility reminder minutes), then any emergency release whose retrospective crossmatch came back incompatible, then transfusions still running at the bedside (and how many are overdue for completion).

**Reaction investigations** turns urgent and says how many are *with units out or reserved* when a reaction is still under investigation for a patient who has units issued or waiting to issue, so the next shift checks with the physician before another unit goes (the same warning as on Issue & Transfuse, OCD-078).

The next shift should read the note before relying on the dashboard alone. The note does not assign tasks inside the software. It is the record of what you said you left.

The handoff counts **HL7 outbound not sent**: results and charges for the EHR or billing that have waited more than 15 minutes without going out; the item opens the HL7 page, which says why each has not gone. The handoff counts **Antibody workups** not yet completed or voided and says how many wait for supervisor review; the item opens the antibody ID worklist. The handoff also counts **RhIG evaluations** still needing work (not completed, or a candidate with no RhIG given recorded) and says how many are waiting to be given; the item opens the RhIG list. **Unidentified patients** not yet merged into an identified chart lead the list (their history is split until the merge) and open Patients; **Tubes due for discard** (once your facility sets a retention period) opens Store tubes. **Blood orders to prepare** counts open blood orders whose units are not all reserved yet and opens that list; it is urgent when an order's needed-by time has passed, or is within the warning window your facility sets on Facility Policies (named as needed soon).
