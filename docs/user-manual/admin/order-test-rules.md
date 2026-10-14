---
slug: order-test-rules
title: Order and test rules
order: 290
chapter: Rules and safety
summary: Write versioned rules that decide when an order may be placed or a result may be verified. Open Help on that screen for the attribute catalog.
routes:
  - /admin/rules
---

![Order and test rules](figures/admin-rules.png)

**Order and Test Rules** are conditions you write. When the condition is true, the action runs. Order rules see the patient and the order and run when an order is placed or changed. Test rules see those plus the result and run when a result is verified. An empty condition never fires.

Use **Help** on this screen for the attribute list, the operators, and the functions. That panel is generated from the rule catalog, so it matches the engine. This topic does not duplicate that reference.

Rules are versioned like the other catalogs. A rule that blocks an order will block it for every user, including an administrator, unless the action itself is a warning with an override. Test the rule on a training patient before you activate it. An active rule with a typo can stop all ordering of a test.

> Warning. A rule is not a substitute for the issue gate. You cannot write an order rule that issues an incompatible unit, and you should not write one that tries to skip a hard stop. The gate still runs at issue.
