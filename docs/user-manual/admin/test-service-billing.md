---
slug: test-service-billing
title: Test and service billing
order: 240
chapter: Catalog
summary: Map a test code to a charge code captured when the result is verified.
routes:
  - /admin/test-service-billing
---

![Test and service billing](figures/admin-test-service-billing.png)

**Test/Service Billing** maps a test code to a charge code. The charge is captured when the result is verified, not when it is entered. Optional price overrides the charge master.

A test with no row produces no charge. Groupers bill through their member tests, so map the members. Mapping only the grouper code will not fire if the verified rows are the members.

Invalidating a result does not automatically cancel the charge. The billing queue is where a charge is cancelled, with a reason. Build that step into the procedure if a corrected test should not remain billed.
