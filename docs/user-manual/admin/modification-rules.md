---
slug: modification-rules
title: Modification rules
order: 310
chapter: Rules and safety
summary: Define pool, divide, irradiate, wash, and the other modifications, including the product code of the resulting unit.
routes:
  - /admin/modification-rules
---

![Modification rules](figures/admin-modification-rules.png)

**Modification Rules** define component processing: which source products can be pooled, divided, irradiated, washed, or otherwise modified, and what product code the resulting unit receives. The bench screen [Modify products](/help/clinical/modifications) only offers rules you activate here.

Resulting units start in quarantine. Build the release into the procedure. A rule that produces an unknown ISBT code will fail at the moment of modification, not when you save the rule. Confirm the code exists in the ISBT catalog first.

Expiration changes come from the expiration-modification codes. If irradiation should shorten dating, the rule and the expiration code have to agree. A rule that keeps the original frozen expiration on a thawed product will offer an unsafe date at issue. Check a training modification and read the child unit's expiration before you activate the rule.
