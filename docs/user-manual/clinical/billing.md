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

Open the queue if you have billing review permission. Confirm the patient, the code, and the triggering event. Export sends the accepted batch. Export can stay blocked until the outbound DFT is accepted. If a row will not export, read the status rather than entering the charge in another system and exporting it here as well.

## Cancel

Cancel requires a reason and the cancel permission. Cancel when the clinical event was reversed or charged in error, for example a result that was invalidated or an issue that was returned under your billing rules. Do not cancel a charge only because the dollar amount looks high. The amount comes from the charge master. An administrator changes the master. You document the reason for a cancellation.

A charge you cannot see means you lack review permission, not that the capture failed. Ask a billing analyst to open the queue.
