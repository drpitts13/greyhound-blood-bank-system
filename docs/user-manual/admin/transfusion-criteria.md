---
slug: transfusion-criteria
title: Transfusion criteria
order: 182
chapter: Catalog
summary: Facility-approved indications for ordering each blood component, used for patient blood management review.
routes:
  - /admin/transfusion-criteria
---

**Transfusion Criteria** holds your facility's indications for ordering each component class, for patient blood management (PBM). **The software ships no criteria and no thresholds.** Enter only criteria your medical director has approved. This is open decision OCD-038, which **requires SME verification**.

## Two kinds of criterion

- **Laboratory**: a test code, a comparison (<, ≤, >, ≥), a threshold with a unit, and a look-back in hours. It is met automatically when the patient's latest verified result for that test, within the look-back before the order, satisfies the comparison. Results come from the test results the system holds, including interface results. Only numeric, verified, current (not superseded) results count.
- **Selected by the orderer**: leave the test code blank. Use it for indications without a lab value, such as active bleeding. The orderer selects it on the order form.

An order meets the criteria when any active criterion for that component is met.

## Facility policies

- **Evaluate product orders against transfusion criteria** (default off): when on, each product line is checked when the order is saved, and the result is stored on the line.
- **Require a reason for orders outside criteria** (default off): when on, a manual product order outside the criteria needs a selected indication or a written reason before it is saved. Orders received over the interface are never stopped; they are recorded as outside criteria for review.

Neither policy blocks a transfusion on clinical grounds. The order form shows what the check found, and the decision stays with the ordering provider.

## Review

Utilization › **Orders outside transfusion criteria** lists, for the chosen dates, every product order recorded outside the criteria: patient, product, location, provider, what the check found, and the reason given.

Changes to criteria are kept in configuration history. Deactivate a criterion rather than editing an approved threshold in place, so the history shows what applied when.
