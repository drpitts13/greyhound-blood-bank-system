---
slug: test-groupers
title: Test groupers
order: 70
chapter: Catalog
summary: Bundle member tests so ordering a grouper, such as a type and screen, expands into the individual test lines.
routes:
  - /admin/test-groupers
---

![Test groupers](figures/admin-test-groupers.png)

A **test grouper** is an orderable bundle. The technologist orders the grouper. The system expands it into the member tests on the same order and specimen. A type and screen is the usual example. The members are [test definitions](tests). The grouper does not have its own result. The list keeps the grouper code. When the name is blank, the list says Grouper and the id. That wording is not saved, and the edit form still shows the stored name.

Changing the members changes the next order. It does not add or remove tests from orders already placed. Retire a grouper by inactivating it when the code has been used, so old orders still display the name.

Do not create two groupers with the same clinical meaning and different codes unless the billing or the interface truly needs both. Interface maps often send one code. A second grouper will not receive those orders.
