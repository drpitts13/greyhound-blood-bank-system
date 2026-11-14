---
slug: patient-product-history
title: Product history
order: 140
chapter: Patients and orders
summary: Review units already issued or transfused to this patient, including returns and final disposition.
routes:
  - /patients/{id}?tab=history
---

![Product history](figures/patient-product-history.png)

**Product History** lists units that have already moved past the active reservation: issued, transfused, returned, or otherwise disposed. Use it when you are answering what this patient received, which unit numbers were involved, and whether a unit came back to inventory.

The active reservation list stays on the Products tab so the bench is not scrolling through old transfusions to find the unit in hand. Show issued and transfused on Products only when you turn that option on. History remains the place for the completed record.

A transfused row without a reaction has **Report reaction** for a reaction noticed afterwards; see [Reactions](reactions). A transfusion that has been started and not yet completed shows as **TransfusionStarted** with disposition **In progress**, highlighted, with **Complete transfusion**, which opens it on the transfusion desk. An issued row has **Document transfusion**. It opens the bedside documentation form on Issue and Transfuse with that unit already selected. Every bedside check still applies there: the two patient identifiers, the unit scan, the second verifier when your policy requires one, ward receipt, and the unit's expiration. There is no shortcut that marks a unit transfused without them.

Each line keeps the unit number, product, patient identifiers used at issue, and the disposition. This is also the record you will compare during a lookback or a reaction investigation. Do not delete a historical line. If something was documented in error, correct it with the appropriate return, reaction, or deviation workflow so the original entry stays visible.

> Issued during a reaction. A unit issued while the patient's transfusion reaction investigation was open or under review is marked **Issued during reaction #n** on its row. The issue records it at the time, so a later review finds the units given while a reaction was being investigated (OCD-078).

> How it was issued. An issue row also says how the issue was entered, when that was not at the issue desk at the time:
> - **Late entry**, with the reason given.
> - **Entered from a paper issue record after downtime**, with its reason.
> - **Outside every recorded outage**, with the reason given.
> - **Infant, negative screen: group O issued without a crossmatch** under the neonatal rules.
>
> These were recorded with the issue and in the audit trail; the row repeats them so a review does not need the audit trail to find them.
