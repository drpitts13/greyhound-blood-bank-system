---
slug: patient-tests
title: Tests on the chart
order: 100
chapter: Patients and orders
summary: See this patient's pending and completed tests, and open result entry without leaving the chart.
routes:
  - /patients/{id}?tab=tests
---

![Tests](figures/patient-tests.png)

The **Tests** tab is this patient's view of the same work that appears on the [Test worklist](test-worklist). Use it when you are already in the chart and need to see what is pending, what is blocked, and what has been verified.

Enter and verify from the row when your permissions allow it. The rules are the same as on the worklist:

- The person who entered a patient ABO or Rh result cannot verify that result.
- Verification is blocked on a rejected specimen, an expired specimen, or an order on hold.
- A delta between the new ABO or Rh and history stops verification until a supervisor records Retain or Replace and signs.

Correcting or invalidating a verified result is a separate action. Correction requires an electronic signature. Invalidation leaves the old result in history and marks it so later decisions do not treat it as current. Do not edit a verified value in place.

Interface results that are waiting for review show as such. Accepting an interface result is still verification. Read the transmitted values before you verify them.

The antibody chart is part of this patient's testing record. You can add an antibody or deactivate one when no antibody-identification workup is open. Deactivating an antibody does not erase it and does not make the patient eligible for electronic crossmatch.
