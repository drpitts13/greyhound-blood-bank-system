---
slug: reactions
title: Transfusion reactions
order: 270
chapter: Quality and operations
summary: Investigate a suspected reaction, quarantine remaining units, and close the investigation only when the checks are done.
routes:
  - /reactions
  - /reactions/{id}/report
---

![Reaction investigations](figures/reactions.png)

**Reactions** is the investigation board. You need permission to investigate reactions. Each row has a **Next step**, and the board lists the most urgent first: fatality reporting that is due, a unit remainder that is not held, an ABO/Rh discrepancy to review, the next missing workup item, then cases ready to close and sign. The first three are shown in red. Work the board from the top. Open an existing investigation or start one from the transfusion it belongs to.

A reaction noticed after the transfusion was documented, such as a fever hours later or a delayed hemolytic reaction, is reported from the patient's chart: on **Product history**, choose **Report reaction** on the transfused row, describe what was seen and who reported it, and give the onset if known. That opens the investigation here, holds any remainder of the unit, and marks the transfusion as reaction suspected so the chart and the patient's next issue show it. The onset cannot be before the transfusion started or in the future, and a transfusion that already has an investigation is refused with its number.

Before closing, the provider records the **Reaction concluded** and the **Treatment for future transfusions**, each from the lists kept in Administration › Reactions and Treatments. **No reaction** and **No treatment** are always available. You can choose more than one treatment, but No treatment cannot be combined with another. The case cannot be closed until both are chosen, and what is recorded keeps the wording of the list at the time.

If the investigation leads to a requirement for future products, choose **Add special requirement** on it. The patient's chart opens with the requirement form naming the investigation, and the requirement is linked to it. Choosing a treatment does not by itself add a special requirement to the chart; add the requirement there so it is enforced at selection and issue.

## Workup specimens

The workup tests two specimens, each with the tests of its Test Group (chosen in Administration › Reaction Workup):

1. **Pre-transfusion specimen.** Choose it from the patient's specimens collected before the unit was issued; the tube the unit was crossmatched on is suggested. A pre-transfusion tube that has since expired can still be used for the workup (shown as "Expired: workup allowed"); it cannot be used for anything else.
2. **Post-transfusion specimen.** Accession the tube drawn after the reaction (scan or type its accession, type, and collection time), or choose one already accessioned since the transfusion began.

Each specimen's tests are ordered as a Stat reaction workup order and appear on the test worklist. **Result on bench** opens the worklist on that tube; result and verify the tests there as usual. The investigation shows each test with its result and whether it is verified. A specimen is set once; it cannot be swapped after its tests are ordered. If its workup order is cancelled (for example, the tube was mislabeled), the investigation asks for that specimen again and you can choose or accession a new one; the replaced tube and order stay in the audit trail.

The repeat patient ABO/Rh is the verified ABO/Rh on the post-transfusion specimen. If it disagrees with the patient's current type or with the pre-transfusion specimen, the board shows an ABO/Rh discrepancy to review. Elution is required when the verified post-transfusion DAT is positive.

## Bedside vital signs

The case shows the vital signs taken at the bedside before and after the transfusion, when the nurse entered them on the transfusion desk, and the temperature change between them (for example *Temperature +1.7 °C from before the transfusion*). The printable report shows the same line. The change is stated, not interpreted: the reaction concluded is chosen by the reviewer.

## What you record

Record the clerical check and the visual check of the unit, the repeat unit ABO/Rh, and the elution when it applies, along with the symptoms and the timeline the ward reported. The investigation stays open while those items, or any workup test, are incomplete. Closing is a separate step with its own gate and a signature when the close action requires one.

Investigations recorded before the specimen workup show their typed repeat ABO/Rh and DAT read-only. If one is reopened, it needs the specimen workup before it can close again.

Remaining product from the implicated unit is quarantined so it cannot be issued to someone else while you are investigating. Do not release that quarantine to "put the unit back" until the investigation says the unit can be released. Discard, if that is the outcome, uses the inventory discard permission and a reason.

## Fatality and lookback

A reaction that your procedure treats as a fatality or as a product-safety signal stays open for the follow-up your medical director requires. The screen keeps that follow-up visible. It does not file a regulatory report for you. Filing outside this system is a facility procedure.

If the investigation implicates a donor unit, start or continue the [Lookback](lookback) from the donation identification number. The reaction and the lookback are separate records that reference the same unit.

> Hard stop. Closing is refused while the clerical or visual check is unrecorded, either workup specimen is missing, or any of their tests is not verified. Do not close an investigation to clear the board.

## The report

**Report** on the investigation opens the whole case on one sheet for the medical director's review and the record: the patient, unit, issue, and the bedside transfusion record (start and stop, volume, what was seen and done), the workup checklist, each workup specimen with its tests and results, the conclusion, and the close. A case that is not closed is marked **Draft**. **Print** records who printed it, and whether it was a draft, in the audit trail before the print dialog opens. The sheet ends with lines for the medical director's review signature.

> Units issued meanwhile. An investigation lists the units issued to the patient while it was open or under review, with when they were issued and where each unit is now, so you see them without opening the chart. The printed report has the same list under **Units issued while the investigation was open**, and says *None.* when there were none.

## After closing

A closed investigation is signed, so it is read-only. The screen shows who closed it and when. To change it, enter the **Reason for reopening** and choose **Reopen…**. The case goes back to Under review, the reason and your name are recorded, and the earlier close stays in the audit trail. Make the changes, then close it again with your signature.

Recording CBER notification and the written report for a fatality does not need a reopen.

> Demo data. In a development database Marcus Febrile (MRN0006) has a febrile reaction under review, and another red cell unit has been ordered (ORD-RXN-NEXT) and reserved for him. It shows the reaction warnings: **Reaction under investigation** on Ready to issue and the Issue form, the unit count on this list, *with units out or reserved* on the home card and the shift handoff. Releasing the unit does not bring it back.

## Bag culture, the supplier, and the donation's other units

The workup checklist records whether the bag was sent for **Gram stain and culture**, with the result, and whether the **blood supplier was notified** (the time is stamped when you tick it and save) with who was told and the reference. When the implicated unit has a donation number, **Other units from this donation** lists its other components with their product and status, on the page and the report, so they can be found and held if the supplier asks. Nothing is held automatically.

To accession the post-transfusion tube from the investigation, enter both patient identifiers as they appear on its label (MRN, then date of birth), as for any accession. The workup ordered on the pre-transfusion tube stays open when that tube is past its usual expiration: the specimen expiry sweep does not cancel or unlink a reaction workup.
