---
slug: billing
title: Billing review
order: 310
chapter: Interfaces and reports
summary: Review charges captured from verified tests, issued units, and completed actions, then export or cancel them.
routes:
  - /billing
---

![Billing](figures/billing.png)

Charges are captured when a result is verified, a unit is issued or transfused (as the charge rule says), or a modification action is completed. You do not key a charge for a successful clinical action. **Billing** is the review queue for charges that were captured and for the outbound DFT messages waiting to go to the billing system.

## Review and export

Open the queue if you have billing review permission. Confirm the patient, the code, and the triggering event. Export sends the accepted batch. Export can stay blocked until the outbound DFT is accepted. A DFT that has not gone says so under its control id, how long it has been queued and why (*Not sent, queued 13 d: No enabled outbound interface is set up to send it…*); ask the interface administrator when the reason is the interface. If a row will not export, read the status rather than entering the charge in another system and exporting it here as well.

## Units returned unused

An issue charge whose unit came back to the blood bank without being transfused is marked **Returned unused** with the time it came back. The charge is not cancelled for you: whether a returned unit is still billed (an issue or handling fee) is your facility's policy. Check it before you export, and cancel with a reason if it should not be billed.

## Cancel

Cancel requires a reason and the cancel permission. Cancel when the clinical event was reversed or charged in error, for example a result that was invalidated or an issue that was returned under your billing rules. Do not cancel a charge only because the dollar amount looks high. The amount comes from the charge master. An administrator changes the master. You document the reason for a cancellation.

A charge you cannot see means you lack review permission, not that the capture failed. Ask a billing analyst to open the queue.

**Transfused-unit charges.** When your facility charges products on transfusion (Facility Policies › *Product charge on transfuse*), the charge's service date is when the transfusion happened (its stop time, else its start), not when it was charted. A unit's issue or transfusion is charged once per issue: documenting it again, or the EHR correcting its times, does not add a second charge.
