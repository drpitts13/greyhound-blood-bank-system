---
slug: downtime
title: Downtime snapshot
order: 400
chapter: Supervision
summary: After the system returns, review interface errors, open issues, and retrospective crossmatches that still need to be entered from paper.
routes:
  - /downtime
  - /downtime/patients
---

![Downtime snapshot](figures/downtime.png)

**Downtime Snapshot** is the recovery checklist inside the application. It counts unresolved interface errors, failed instrument messages not yet replayed or resolved, outbound HL7 that is still pending, open issues, paper issues you have back-entered whose transfusion (and ward receipt) are still to be entered, retrospective crossmatches that are due, and the status of the audit chain. You need audit read permission. The screen does not import paper forms and it does not scan them.

## Before an outage: print the patient summary

Open **Downtime patient summary** from the link on Downtime Snapshot (or go to /downtime/patients) before a planned outage, or as soon as an unplanned one starts, and keep the printout at the issue bench. You need the *issue.create* permission. It lists every patient the blood bank is working for now: anyone with a specimen that has not expired, a unit reserved, or a unit issued and still out. For each patient it shows:

- name, MRN, and date of birth
- ABO/Rh on file, and **No second determination on file** when the second ABO/Rh that issue requires is missing
- active antibodies
- special requirements in effect, with informational notes below them
- current specimens with their expiration
- reserved and issued units: unit number, product, ABO/Rh, unit expiration, and when the reservation ends

**Print** records who printed it and which patients it carried in the audit trail before the print dialog opens. The sheet is accurate only as of the time printed on it. It does not replace your facility's downtime procedure; check identity and unit labels at issue as usual.

## While the system is down

Downtime documentation is your facility's paper procedure. Capture the identifiers you will need to enter later: patient MRN and name, specimen accession and collection time, ABO and Rh, antibody screen, antibodies and special requirements, unit number or donation identification number, product, ABO and Rh, expiration, inspection, crossmatch or the emergency-release acknowledgment, and any transfusion or reaction. The software does not store those paper rows for you.

## After the system returns

1. Confirm you are on the recovered database, not a partial copy.
2. Replay interface messages that failed, and watch for duplicates. A message that already filed must not be entered by hand as well.
3. Enter the paper record into the chart, the specimen, the result, and the issue screens. Use the same identity checks you use for any issue. The first electronic issue after downtime is not a shortcut. On the chart's issue form, set **Date/time of issue** to the time on the paper form; the form then asks for a **Late entry reason** (for example "Issued on paper during downtime"). The record keeps the paper time, the time you entered it, and your reason. The safety checks still run when you enter it.

   If the unit or the specimen has expired since it was issued on paper, use **Downtime back-entry from a paper issue record** on the Issue desk instead. It needs the *issue.downtime-backentry* permission, which is given to supervisors by default. Enter the time on the paper record, a reason, and the unit number as written on the record; no scan is needed, because the bag is usually gone. Expiration is checked as of the paper time, so a unit or specimen that was in date then is accepted, and one that had already expired then is still refused. Every other check still runs, and warnings that applied at the paper time (for example a unit near expiry) still need an override. The issue is marked as a downtime back-entry. To record its ward receipt and transfusion afterwards, open **Downtime back-entry: no bag to scan** under the scan panel on Ward receipt or the transfusion desk and type the same unit number. Ward receipt also asks for the receipt time written on the paper record; it cannot be before the issue or in the future, and it is the time the receipt is recorded at. Until then, In transit shows the issue as **Back-enter receipt** rather than overdue. This works only for an issue entered as a downtime back-entry, and only with the same permission. It does not count as electronic identification, so a second verifier is still needed where your facility requires one.
4. Complete retrospective crossmatches for emergency issues that were documented on paper.
5. Review overrides and interface errors before you call the recovery finished.

> Facility policy. The paper forms, the decision to transfuse during downtime, and the regulatory notice are your procedure. This screen only shows what is still open in the database.
