---
slug: utilization
title: Blood utilization
order: 321
chapter: Interfaces and reports
summary: Crossmatch-to-transfusion ratio and transfused share by ordering location and provider.
routes:
  - /utilization
---

**Blood utilization** shows how many crossmatched units were issued and transfused, by ordering location and provider, for a date range. Staff with report run permission can open it. It opens on the last 30 days.

A crossmatched unit is one patient and one unit with a compatible crossmatch in the period, by any method, including electronic. Repeat crossmatches of the same unit for the same patient count once. Issued and transfused are counted for that patient and unit whenever they happened, so a unit crossmatched near the end of the period still counts when it was transfused the next day. A transfusion counts when it was documented as completed or as stopped after it started. A wasted or returned unit does not count as transfused.

The location and provider come from the order on the first crossmatch of that unit for that patient. A crossmatch with no order is listed as "(no order)".

**C:T ratio** is crossmatched units divided by transfused units. A dash means nothing was transfused, so there is no ratio. **Transfused / crossmatched** and **Transfused / issued** are percentages. The page reports the numbers only. Your facility sets any target and decides what to review.

**Orders outside transfusion criteria** lists product orders recorded outside the facility's transfusion criteria in the same dates, with the finding and any reason the orderer gave. It fills only when the transfusion-criteria policy is on and criteria are set up (Admin › Transfusion Criteria).
