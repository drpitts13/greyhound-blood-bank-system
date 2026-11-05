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
- Ready to issue, second ABO/Rh, retrospective crossmatch, and in transit, when you can issue. The Ready to issue card names how many reserved units need a new crossmatch because theirs has expired, and how many expire soon (issue them first or reserve replacements).
- Outstanding issued units and final disposition, when you can issue, return, or document transfusion.
- Pending tests, when you can enter results. The card names how many results must be verified before their specimen expires. Pending specimens, when you can accession. The card names how many waiting tubes have expired and need rejecting, which puts it under Needs attention.
- Reaction investigations, when you can investigate reactions. A closed case is not in that count. A case that still needs fatality follow-up is named on the card, including after the investigation is closed.
- Supervisor queue, when you can review antibody identification, verify results, or release quarantine.
- Instrument failures, when you manage instrument interfaces: failed instrument messages nobody has replayed successfully or resolved. HL7 errors, when you manage HL7: interface errors still open.
- Shift handoff, when you can hand off a shift. An open packet says it needs acknowledgment.

A retrospective count names how many items are past their stored due time. An in-transit count names how many units are past the arrival window. Those notes appear only when the number is above zero.

A queue you cannot open is left off the row. A queue that does not load shows an em dash. That is not a zero. Opening a card does not issue, receive, verify, or close anything.

## Focus and what comes first

**Focus** sets which desk the page leads with: **Bench**, **Issue desk**, **Supervisor**, or **Everything**. The first focus comes from your permissions. A user who can work only one desk starts on that desk, and anyone else starts on Everything. Change it with the buttons. The choice is remembered for you in this browser only.

| Focus | Its queues |
|---|---|
| Bench | Pending specimens, pending tests, second ABO/Rh, supervisor queue |
| Issue desk | Ready to issue, second ABO/Rh, in transit, outstanding issued, retrospective crossmatch, final disposition, near expiry |
| Supervisor | Supervisor queue, reaction investigations, temperature excursions, retrospective crossmatch, final disposition, shift handoff, near expiry |

The page is arranged in this order:

1. **Start here**: one link to the most urgent queue. When nothing is urgent, it links to the first focus queue that has work.
2. **Needs attention**: any queue with an overdue or follow-up note, plus temperature excursions, reaction investigations, instrument failures, HL7 errors, and an open shift handoff whenever they are above zero. These show **whatever the focus is**, so changing focus never hides them. The most urgent comes first: temperature excursions, reactions, retrospective crossmatch, in transit, instrument failures, HL7 errors, shift handoff, then near expiry with reserved units.
3. **Queues with work**: focus queues above zero, in workflow order.
4. **Could not load**: any queue whose count failed. It is never counted as clear. Open it to check.
5. **Clear**: focus queues at zero, as small links.
6. **Other desks**: queues outside the focus, folded. The heading counts their items.

Every queue you can open is still on the page. Focus changes only the order and grouping.

Patients on file, available units, and units in quarantine stay on the next row. Charges waiting for review appear when you can see billing. Use a card or the sidebar to open the queue. The sidebar is the full map, grouped under headers: Patients and Testing, Inventory, Transfusion, Quality and Review, Reports and Billing, Interfaces, and Administration. Click a header to open or fold it. The header for the screen you are on opens by itself. **Help** is always present at the bottom.

## Your access

**Your access** lists every permission code on this session. The names are the codes the system checks, such as `result.enter` or `issue.create`. Roles are bundles of those codes. If a button on another screen is missing, compare this list with the task. An administrator changes what you can do on **Users** and **Roles**. You will not see Administration at all without an administration permission.

The top bar shows the workstation stored at sign-in and **Help for this page**, which opens the topic for the screen you are on.
