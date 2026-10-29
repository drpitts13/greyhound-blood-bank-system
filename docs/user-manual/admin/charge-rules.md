---
slug: charge-rules
title: Charge rules
order: 320
chapter: Rules and safety
summary: Map result verification and unit issue events to charge codes, optionally limited to one test or product.
routes:
  - /admin/charge-rules
---

![Charge rules](figures/admin-charge-rules.png)

**Charge Rules** map a clinical event to a charge code. The event is result verification or unit issue. An optional key limits the rule to one test code or one product code. A product choice keeps that product code. When the product name is blank, the choice says Product and the id. The code is not shown as the name. A rule with no key matches every event of that type, which will double-charge if a more specific map also exists.

Prefer the specific maps on [Test and service billing](test-service-billing), [Product billing](product-billing), and [Action billing](action-billing) for those cases. Use a charge rule when the event does not fit those three screens. Read the queue after a test event and confirm you captured one charge, not two.

Deactivate a rule rather than deleting it once it has captured charges. The captured rows keep the code they were given.
