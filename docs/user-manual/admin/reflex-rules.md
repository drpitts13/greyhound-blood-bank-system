---
slug: reflex-rules
title: Reflex rules
order: 300
chapter: Rules and safety
summary: When a verified result matches a trigger, add another test on the same order and specimen.
routes:
  - /admin/reflex-rules
---

![Reflex rules](figures/admin-reflex-rules.png)

**Reflex Rules** add a test when a verified result matches a trigger value. The added test is placed on the same order and specimen. It shows up on the worklist like any other unordered-until-now test.

Choose the trigger test, the value, and the reflex test. A trigger that is too broad (any result, or a value the test rarely uses) will add the reflex test on almost every verification. Confirm the value against the test definition's result codes, not against a nickname.

The reflex fires at verification, so an unverified entry does not add the test yet. Invalidating the original result does not automatically cancel the reflex order. Cancel that order with a reason if it should not be done.

Reflex rules are for testing. They do not allocate a unit.
