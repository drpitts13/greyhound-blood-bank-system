---
slug: patient-specimens
title: Specimens
order: 90
chapter: Patients and orders
summary: Accession the specimen, track collection and receipt, and reject it when it cannot be tested.
routes:
  - /patients/{id}?tab=specimens
---

![Specimens](figures/patient-specimens.png)

The **Specimens** tab lists every specimen on the chart and lets you accession a new one when you are placing or completing an order. The pending-specimen queue on the test worklist is the bench view of specimens that still need to be received or accepted. Use whichever screen you are standing at. Both write the same record.

## Accession

Record the specimen type, collection time, and the collector when your procedure requires it. The testing window is calculated from facility policy and the specimen type. You do not type an expiration that contradicts that rule.

Print the specimen label from **Printing** or the print action on the specimen. The label layout (what sits on the 2×1 inch label) is an administrator setting. You choose the printer and confirm the identifiers.

## Receive and accept

A collected specimen is received into the laboratory, then accepted for testing. Until it is accepted, the worklist shows it as not ready. Reject the specimen when it is hemolyzed, mislabeled, or otherwise unsuitable. Rejection stops testing on that specimen. If results were already recorded on it, rejection is refused and names them: invalidate each result first (on the Tests tab), then reject. That way nothing from the bad specimen, such as its ABO/Rh, stays in use. If units are still reserved against the specimen, the rejection lists them so you can release them on Compatibility.

> Hard stop. Results cannot be verified on a rejected specimen or on a specimen past its testing window. Collect a new specimen rather than overriding the window for routine work.

## Relabel

Relabeling changes the barcode. It requires a reason. Use it when the printed label is wrong or damaged, not to move a specimen onto a different patient. A specimen belongs to one patient. If it was accessioned to the wrong chart, reject it and accession a new specimen on the correct chart.

## Identity at the bench

The accession number is what you will select when you record a crossmatch. At issue, the system checks that a current specimen exists for this patient and that it has not expired. A missing or expired specimen is not offered for routine issue.

## Specimen history

Click **History** on a specimen row to see everything that happened to that tube, newest first:

- When it was collected, by whom, and where.
- Each recorded change from the audit trail: accession, receipt, acceptance, rejection, relabel, and edits. Each one shows who made it, at which workstation, and what changed (for example `Barcode: OLD → NEW`).
- When it expires. Until then it shows as scheduled at the top. After that time it is marked **Attention**.
- Any override on the specimen, such as an expiry extension, with the rule, reason, and who authorized it.
- The orders it serves, the results on it (corrections and invalidations included), and the crossmatches and reservations that used it. An incompatible crossmatch is marked **Critical**.

Scanning a tube's accession in the top-bar find box opens this history directly.

Relabels and rejections are marked **Attention**. **Warnings and critical only** hides the rest. The panel only reads, and nothing in it can be edited.
