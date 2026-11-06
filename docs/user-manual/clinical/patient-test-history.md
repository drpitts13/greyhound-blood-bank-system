---
slug: patient-test-history
title: Test history
order: 110
chapter: Patients and orders
summary: Review verified, corrected, and invalidated results in the order they were recorded.
routes:
  - /patients/{id}?tab=test-history
---

![Test history](figures/patient-test-history.png)

**Test History** is the longitudinal record. It shows what was verified, what was later corrected, and what was invalidated. Current blood type and antibody history on the overview are derived from this record. They are not a separate notepad.

Use this tab when a result on the overview surprises you, when you are investigating a discrepancy, or when a clinician asks what the previous type and screen showed.

Each version keeps who entered it, who verified it, and when. **Entered / verified** shows the person who entered the result (hover for the time), how it arrived (**Manual**, **Instrument**, **Interface**, or **Calculated**), who verified it, and who invalidated it. A correction does not remove the earlier version. To explain a change, tick **Corrected or invalidated only**: every version of each changed result is listed together, by tube and test, newest first, with the reason on each row. The accession opens that tube's history on Specimens, and the order number opens Orders showing that order.

> Warning. An ABO or Rh discrepancy between the current result and history blocks routine issue until it is resolved with Retain or Replace. Test History is where you see both values.
