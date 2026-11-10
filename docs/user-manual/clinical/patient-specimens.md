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

Record the specimen type, collection time, who collected it, and where. Under the type, the form names the orders on the chart still waiting for a specimen and the tube types their tests accept; it starts on a type that suits them all and warns when the type you choose does not. With **Accept now** ticked, **Link to waiting orders** lists the test orders on that visit the tube suits, all ticked; untick any that should wait for another tube. When you accession, each ticked order is linked (and audited) and its tests appear on the worklist on the new tube. An order that cannot be linked is named with the reason; link it from the Orders tab. Orders with blood products are not offered: they need a completed type and screen first. A tube collected without Accept now gets the same offer when it is accepted later, here or on the pending-specimens worklist (**Link them**). Scan or type the two patient identifiers **from the tube label**: they start empty, a line under each says whether it matches the patient record, and accession is refused without both or when they do not match. They are not copied from the record unless your facility has turned on identifier pre-fill. The testing window is calculated from facility policy and the specimen type, and is shorter for a patient transfused or pregnant recently. The accession form shows the expiration that will be saved and says when the shorter window applies. You do not type an expiration that contradicts that rule. When you edit a specimen, the expiration offered is the one the policy gives for this patient; a different expiration needs an override with a comment and the name of whoever authorized it (choose **Me** if that is you).

Print the specimen label from **Printing** or the print action on the specimen. The label layout (what sits on the 2×1 inch label) is an administrator setting. You choose the printer and confirm the identifiers.

A new tube is linked to the visit it was drawn on. Accessioned with a new order, it takes the order's visit. Accessioned on its own, choose **Visit drawn on**: it starts on the patient's open visit when there is only one; with more than one open visit it asks you to choose. If none is chosen, the patient's only open visit is used, and with several none is guessed. The Specimens list shows each tube's **Visit**.

## Receive and accept

A collected specimen is received into the laboratory, then accepted for testing. Until it is accepted, the worklist shows it as not ready. Reject the specimen when it is hemolyzed, mislabeled, or otherwise unsuitable.

If your facility requires the collector (**Require the collector before accepting a tube** in Facility policies), a tube is accepted only once **Collected by** is recorded; the accession form says "required to accept". You can still collect and receive the tube without it. Under Pending specimens on the test worklist, a received tube with no collector shows a box for the collector on the label and **Save collector** instead of Accept; save it, then accept. If the label does not show a collector, follow your SOP (usually reject and recollect). Choose the reason from the list, and add a comment; some reasons, such as Other, require one. The list is kept by your administrator under Specimen Rejection Reasons. Rejection stops testing on that specimen. If results were already recorded on it, rejection is refused and names them: invalidate each result first (on the Tests tab), then reject. That way nothing from the bad specimen, such as its ABO/Rh, stays in use. If units are still reserved against the specimen, the rejection lists them so you can release them on Compatibility.

When your laboratory sends results to the EHR, rejecting a tube also tells the EHR that each open test on it will have no result: the message gives the tube, your reason, and "Recollect", and the order stays open for the new specimen. If that message cannot be queued, the rejection still stands and a warning tells you to phone the ward; check the HL7 page.

> Hard stop. Results cannot be verified on a rejected specimen or on a specimen past its testing window. The one exception is a correction of a result that was already verified: it can be verified after the specimen expires, because it fixes the record of a test already done (OCD-051). Collect a new specimen rather than overriding the window for routine work.

## Relabel

Relabeling changes the barcode. It requires a reason. Use it when the printed label is wrong or damaged, not to move a specimen onto a different patient. A specimen belongs to one patient. If it was accessioned to the wrong chart, reject it and accession a new specimen on the correct chart.

## Identity at the bench

**Infants (OCD-074).** When your facility turns on the neonatal rules (Facility Policies › *Neonatal pretransfusion testing rules*, off by default), a specimen collected from an infant during a visit stays valid for the admission. The infant must be younger than the neonatal age limit, which is 120 days by default.
- The specimen runs until the visit is discharged or the infant reaches the age limit, whichever comes first. It never ends earlier than the usual rules would make it.
- The Expires column says **For the admission (infant)**.
- When the visit is discharged (on the chart or by an ADT message), the specimen ends at discharge, or at its usual expiration if that is later. The change is recorded in the audit trail.
- A specimen with no visit, or with its own validity hours, keeps the usual rules.
- ABO/Rh is not repeated during the admission. When the infant's current ABO/Rh was tested on that admission specimen, it stands for routine red cell issue without a second determination while the specimen is valid.
  - The chart header says **Infant: stands for the admission** in place of the second-ABO/Rh notice.
  - The infant does not appear on the Second ABO/Rh desk.
  - Ready to issue does not flag the infant's units.
  - Electronic crossmatch still needs two determinations.
  - After discharge or the age limit, the usual rule returns.

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

**After a rejection.** Rejecting a tube that open orders still use says which orders now need a new specimen. On the test worklist and the chart Tests tab their lines read **Recollect**, with the rejection reason; collect a new tube, accession it, and link it to the order (the line offers **Accession**). The lines can then be resulted.

Under a tube's status, **Stored:** shows where it is kept (recorded on [Store tubes](store-tubes)); its history shows every time it was stored or moved.
