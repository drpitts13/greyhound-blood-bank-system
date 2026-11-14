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

**Reflex Rules** add a test when a verified result matches a trigger value. The added test is placed on the same order and specimen. It shows up on the worklist with a **Reflex from** badge naming the result that added it (for example **Reflex from ABSC Positive**), and the line keeps that result for traceability. When a verify on the test worklist adds a reflex test, a note at the top says so ("Reflex added: ABID on ORD-1 (from ABSC Positive)."). If that result is later invalidated, or corrected to a value that would not have added the test, the badge turns amber and the row says so ("ABSC was corrected to Negative after this test was added. Check whether ABID is still needed."). The test is not cancelled for you: it may already be under way. The list keeps the rule code. When the name is blank, the list says Reflex and the id. That wording is not saved, and the edit form still shows the stored name.

Choose the trigger test, the value, and the reflex test. A trigger that is too broad (any result, or a value the test rarely uses) will add the reflex test on almost every verification. Confirm the value against the test definition's result codes, not against a nickname.

The reflex fires at verification, so an unverified entry does not add the test yet. Invalidating the original result does not automatically cancel the reflex order. Cancel that order with a reason if it should not be done.

Reflex rules are for testing. They do not allocate a unit.
