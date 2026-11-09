---
slug: dashboard
title: Dashboard
order: 30
chapter: Getting started
summary: The home screen shows the queues you can work, shortcuts into the day's work, and the permissions on your session.
routes:
  - /
---

![Transfusion service dashboard](figures/dashboard.png)

The dashboard is the first screen after you sign in. It is titled **Transfusion Service Dashboard**.

## What the counts mean

**Your work** is the first row after you sign in. Each card is a queue you can already open, and the card opens that queue:

- Near expiry, when you handle inventory or issue: units close to their expiration. The card notes how many are reserved for a patient; issue those first or reserve a replacement. It opens the Near expiry board in Inventory.
- Expired on shelf, when you can discard units: expired units still recorded on a shelf. It opens Discard expired, where you scan each one to discard it. Above zero it needs attention.
- Below minimum stock, when you handle inventory or issue: products with fewer available units than their minimum, the largest shortfalls named with what is on hand (for example "RBC O: 2 of 6"). It opens Minimum stock. Any shortage needs attention.
- Ready to issue, second ABO/Rh, retrospective crossmatch, and in transit, when you can issue. The Ready to issue card names how many reserved units need a new crossmatch because theirs has expired, how many expire soon (issue them first or reserve replacements), and how many are past their reservation (issue or release them). The In transit card names units past the arrival window and coolers out that were not checked before they left or were checked with differences. The Second ABO/Rh card names patients with no ABO/Rh ordered yet; those need a separate draw before their reserved units can be issued routinely.
- Outstanding issued units and final disposition, when you can issue, return, or document transfusion. The Outstanding issued card names transfusions started and not completed within your facility's reminder hours. The Final disposition card names how long its oldest unit has been past the window (for example "oldest 3 d past due"); every unit on it is overdue, so a card with any unit sits under Needs attention.
- Pending tests, when you can enter results. The card names how many results must be verified before their specimen expires, and how many tests need a new specimen because their tube was rejected. It also names STAT or urgent orders still waiting for a specimen. Results to verify, when you can verify: only the results you can verify now (not your own ABO/Rh entries), naming how many are due before their specimen expires; it opens the worklist on **To verify**. Reagent QC, when you can enter results: the reagent QC worklist, naming failed and overdue daily QC (which puts it under Needs attention); QC waiting for review is in the count. Pending specimens, when you can accession. The card names how many waiting tubes have expired and need rejecting, which puts it under Needs attention.
- Reaction investigations, when you can investigate reactions. A closed case is not in that count. A case that still needs fatality follow-up is named on the card, including after the investigation is closed. The card also names open cases still waiting for their post-transfusion specimen and open cases whose workup tests are not all verified.
- Supervisor queue, when you can review antibody identification, verify results, or release quarantine. The card names results to verify before their specimen expires, missing or damaged units, and overrides to review.
- Instrument failures, when you manage instrument interfaces: failed instrument messages nobody has replayed successfully or resolved. HL7 errors, when you manage HL7: interface errors still open.
- Open deviations, when you manage deviations: every deviation not yet closed. The card names critical ones, which puts it under Needs attention.
- Open RhIG evaluations, when you record immunohematology results or verify results: RhIG evaluations not yet completed, and completed candidates with no RhIG given recorded. The card says how many are ready to complete and how many are to give.
- Blood orders to prepare, when you can reserve units: open blood orders whose units are not all reserved yet; the card says how many are rush and how many need a specimen.
- Tubes due for discard, when you store tubes: stored tubes past your facility's keep-until date, which opens Store tubes with the list. It stays at zero until your facility sets a retention period.
- Unidentified patients, when you can merge charts: emergency charts registered without identification and not yet merged into the identified patient's chart. Charts open past the facility reminder (24 hours by default) are named with the oldest age, which puts the card under Needs attention; newer charts stay in the count only.
- Possible duplicates, when you can merge charts: pairs of charts that may be one person. Pairs that carry the same identifier from another system (for example an enterprise id sent by the EHR) are named, which puts the card under Needs attention.
- Shift handoff, when you can hand off a shift. An open packet says it needs acknowledgment.

A retrospective count names how many items are past their stored due time. An in-transit count names how many units are past the arrival window. Those notes appear only when the number is above zero.

A queue you cannot open is left off the row. A queue that does not load shows an em dash. That is not a zero. Opening a card does not issue, receive, verify, or close anything.

## Focus and what comes first

**Focus** sets which desk the page leads with: **Bench**, **Issue desk**, **Supervisor**, or **Everything**. The first focus comes from your permissions. A user who can work only one desk starts on that desk, and anyone else starts on Everything. Change it with the buttons. The choice is remembered for you in this browser only.

| Focus | Its queues |
|---|---|
| Bench | Pending specimens, pending tests, results to verify, reagent QC, second ABO/Rh, supervisor queue |
| Issue desk | Ready to issue, second ABO/Rh, in transit, outstanding issued, retrospective crossmatch, final disposition, near expiry, expired on shelf, below minimum stock |
| Supervisor | Supervisor queue, results to verify, reaction investigations, temperature excursions, retrospective crossmatch, final disposition, shift handoff, open deviations, near expiry, expired on shelf, below minimum stock |

The page is arranged in this order:

1. **Start here**: one link to the most urgent queue. When nothing is urgent, it links to the first focus queue that has work.
2. **Needs attention**: any queue with an overdue or follow-up note, plus temperature excursions, reaction investigations, instrument failures, HL7 errors, expired units on a shelf, products below minimum stock, and an open shift handoff whenever they are above zero. These show **whatever the focus is**, so changing focus never hides them. The most urgent comes first: temperature excursions, reactions, retrospective crossmatch, in transit, second ABO/Rh with nothing ordered, instrument failures, HL7 errors, shift handoff, then near expiry with reserved units.
3. **Queues with work**: focus queues above zero, in workflow order.
4. **Could not load**: any queue whose count failed. It is never counted as clear. Open it to check.
5. **Clear**: focus queues at zero, as small links.
6. **Other desks**: queues outside the focus, folded. The heading counts their items.

Every queue you can open is still on the page. Focus changes only the order and grouping.

Patients on file, available units, and units in quarantine stay on the next row. Charges waiting for review appear when you can see billing. Use a card or the sidebar to open the queue. The sidebar is the full map, grouped under headers: Patients and Testing, Inventory, Transfusion, Quality and Review, Reports and Billing, Interfaces, and Administration. Click a header to open or fold it. The header for the screen you are on opens by itself. **Help** is always present at the bottom.

## Your access

**Your access** lists every permission code on this session. The names are the codes the system checks, such as `result.enter` or `issue.create`. Roles are bundles of those codes. If a button on another screen is missing, compare this list with the task. An administrator changes what you can do on **Users** and **Roles**. You will not see Administration at all without an administration permission.

The top bar shows the workstation stored at sign-in and **Help for this page**, which opens the topic for the screen you are on.
