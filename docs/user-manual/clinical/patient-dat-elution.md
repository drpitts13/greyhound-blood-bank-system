---
slug: patient-dat-elution
title: DAT and elution
order: 120
chapter: Patients and orders
summary: Record direct antiglobulin testing and elution work on the chart, separate from the antibody-identification workup.
routes:
  - /patients/{id}?tab=dat-elution
---

![DAT and elution](figures/patient-dat-elution.png)

The **DAT / Elution** tab holds direct antiglobulin test and elution records for this patient. Use it when the workup is a DAT or an elution rather than a panel identification. Panel identification has its own workup under [Antibody identification](antibody-identification).

Enter the specimen, the method your procedure names, and the reactions you observed. Verify according to the same result rules as other blood bank tests: the specimen must be acceptable, the order must not be on hold, and you cannot verify your own patient ABO or Rh when that rule applies to the test.

A positive DAT does not by itself add an antibody to the chart. Do not type a specificity into a comment and treat that comment as antibody history.

## Working a DAT/elution workup

1. **New workup**: choose the specimen, the DAT result and method, and the elution result. If you did not perform an elution, give the reason. Add a comment if needed.
2. **Save** keeps the workup open. **Complete** records you as the person who completed it.
3. **Eluate findings**: add one row for each thing the eluate showed:
   - **Alloantibody**: pick the antibody from the list. It must be a catalog antibody so it can protect the patient once posted. A name that is not in the catalog is refused.
   - **Autoantibody**, **Panreactive**, or **Nonspecific**: an optional description. These are recorded on the workup and never added to antibody history.

   **Save findings** replaces the list. The earlier rows are kept in the record, not deleted.
4. **Verify**: someone other than the person who completed the workup, with supervisor review permission, presses **Verify workup**. Each alloantibody that is not already on the patient's antibody history is then added to it as "Identified in eluate". From then on it drives antigen-negative unit selection like any other antibody. The workup shows **Posted to antibody history** or **Already on file** beside each finding.
5. After verification the findings cannot change and the workup cannot be voided. If a posted antibody was wrong, inactivate it in the patient's antibody history with a reason.

**Void** (before verification) needs a reason.

Panel identification still has its own workup under [Antibody identification](antibody-identification), which posts antibodies when that workup is reviewed.
