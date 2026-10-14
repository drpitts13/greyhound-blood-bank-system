---
slug: tests
title: Test definitions
order: 50
chapter: Catalog
summary: Define each test's result type, panel, interpretation, and whether it contributes to blood-type or antibody history.
routes:
  - /admin/tests
---

![Test definitions](figures/admin-tests.png)

**Test Definitions** is the catalog the order entry and the worklist use. A test that is not here cannot be ordered. Each definition says how the result is recorded (a choice list, a graded reaction, free text), which panel subtests it includes, and how an interpretation is derived.

## History

Mark the tests that write patient ABO, Rh, antibody screen, or antibody identification into history. Those are the tests the issue gate and electronic crossmatch read. A test you forget to mark will verify on the worklist and still leave the chart without a blood type. A test you mark by mistake can overwrite history with a value that was never meant to be a type.

## Interpretation

Panel interpretation uses the subtests and the logic table on the definition. Changing the logic changes what "positive" means for every future verification. It does not rewrite verified history. Coordinate the change with the medical director and record the reason.

Inactivating a test stops new orders. It does not remove historical results. Prefer inactive over deleting a code that already appears on patient records.

Billing for a verified test is mapped on [Test and service billing](test-service-billing), not on this page.
