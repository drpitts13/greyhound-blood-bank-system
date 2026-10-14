---
slug: patient-visits
title: Visits
order: 70
chapter: Patients and orders
summary: Record the encounter the orders belong to, including location, attending provider, and pregnancy history when your procedure requires it.
routes:
  - /patients/{id}?tab=visits
---

![Visits](figures/patient-visits.png)

The **Visits** tab lists encounters for this patient. An order is placed on a visit. Create the visit before you place the order when the patient has just arrived and no interface message has created one.

## What to enter

Enter the visit identifiers your procedure uses, the ordering location, and the attending or ordering provider when you have them. Providers and locations come from the administrator catalogs. If the ward or the clinician is missing from the list, ask an administrator to add them. Do not invent a free-text location that will not match later issue documentation.

Pregnancy history, when the form shows it, is part of the clinical record used with Rh and antibody workups. Record what the history actually says. Do not leave a default that contradicts the interview.

## Interface visits

An admission or registration message can create or update the visit before you open the chart. If the location or provider looks wrong, correct it here when you have permission. The correction is audited. The original interface message remains in the HL7 log for the interface analyst.
