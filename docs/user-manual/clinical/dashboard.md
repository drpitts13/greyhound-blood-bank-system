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

- Ready to issue, second ABO/Rh, retrospective crossmatch, and in transit, when you can issue.
- Outstanding issued units and final disposition, when you can issue, return, or document transfusion.
- Pending tests, when you can enter results. Pending specimens, when you can accession.
- Reaction investigations, when you can investigate reactions. A closed case is not in that count. A case that still needs fatality follow-up is named on the card, including after the investigation is closed.
- Supervisor queue, when you can review antibody identification, verify results, or release quarantine.
- Shift handoff, when you can read the audit trail. An open packet says it needs acknowledgment.

A retrospective count names how many items are past their stored due time. An in-transit count names how many units are past the arrival window. Those notes appear only when the number is above zero.

A queue you cannot open is left off the row. A queue that does not load shows an em dash. That is not a zero. Opening a card does not issue, receive, verify, or close anything.

Patients on file, available units, and units in quarantine stay on the next row. Charges waiting for review appear when you can see billing. Use a card or the sidebar to open the queue. The sidebar is the full map: Patient and Lab, Transfusion, Operations, and Administration. **Help** is always present.

## Your access

**Your access** lists every permission code on this session. The names are the codes the system checks, such as `result.enter` or `issue.create`. Roles are bundles of those codes. If a button on another screen is missing, compare this list with the task. An administrator changes what you can do on **Users** and **Roles**. You will not see Administration at all without an administration permission.

The top bar shows the workstation stored at sign-in and **Help for this page**, which opens the topic for the screen you are on.
