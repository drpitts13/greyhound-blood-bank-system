---
slug: admin-home
title: Administration home
order: 10
chapter: Getting started
summary: Configuration is versioned and audited. This page is the index of every catalog, rule, and interface setting.
routes:
  - /admin
---

![Administration](figures/admin-home.png)

**Administration** is the index of configuration. Each card opens one catalog or rule set. You only see cards your permissions allow. Viewing configuration is not the same as editing it. Edit permission is separate, and privilege changes to users require a reason.

## How a change is stored

Edits do not overwrite history. Saving records who changed the row, when, at which workstation, and why. **Configuration History** can compare versions. The clinical audit trail also receives the event. You cannot delete a historical version to undo a mistake. Enter a new version that corrects it, with a reason that says what was wrong.

> Facility policy. Turning on electronic crossmatch, changing a specimen window, or editing a compatibility table changes what the bench is allowed to do. Make those changes under your change-control procedure, not as a casual edit during a shift.

## Where to go next

The Administration menu groups pages by task:

- **Tests & specimens**: tests, subtests, phases, test groupers, reflex and order rules, specimen types, labels and rejection reasons, antibody panel lots, blood attributes, coded comments.
- **Products & inventory**: products, ISBT product codes, modification rules, expiration codes, actions, auto thaw, suppliers, inventory locations, refrigerators and freezers, transport containers, discard reasons.
- **Transfusion & safety**: facility policy, exceptions, compatibility tables, crossmatch settings, special requirements, transfusion criteria, MTP packs, reaction workup, reactions and treatments.
- **Ordering**: ordering locations, providers, order cancellation reasons.
- **Billing**: charge codes and rules, product, test/service and action billing.
- **Interfaces & printing**: HL7, instruments, printers, P-tag layout.
- **Directory & audit**: configuration history, backups, roles, users.

Report definitions are the report designer.

The Clinical User Guide describes what technologists see after you save. Read that topic before you change a rule that issues blood, verifies a result, or captures a charge.
