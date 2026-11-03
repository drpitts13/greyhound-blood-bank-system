---
slug: compatibility-rules
title: Compatibility tables
order: 270
chapter: Rules and safety
summary: Versioned ABO, Rh, antigen, and electronic-crossmatch rules by component class. Medical-director review is required.
routes:
  - /admin/compatibility-rules
---

![Compatibility tables](figures/admin-compatibility-rules.png)

**Compatibility Tables** record the approved ABO, Rh, antigen, and electronic-crossmatch rules for each component class, with their codes and severities, for medical-director review. The issue gate reads the active table, but only to make a rule stricter (OCD-052). A row set to Hard stop where the built-in rule is a warning (for example platelet ABO mismatch) makes that a hard stop at issue for that component class. A row can never make a rule weaker: a softer severity than the built-in one cannot be saved, and deactivating a row returns that rule to its built-in behavior. Which ABO and Rh pairs are allowed still comes from each product's ABO/Rh matrix (Admin › Products). A unit the issue gate calls ABO-incompatible is a hard stop, not a warning you can sign.

Edit a table only under medical-director review. The screen versions the change. The previous version remains available in history so you can see what was in force when an earlier unit was issued. Do not "fix" a historical issue by editing the table that was active that day. The issue stored the decision it made at the time.

Component class matters. A rule written for red cells is not automatically the rule for plasma. Check the class on the row before you save.

Electronic-crossmatch rows in this table do not turn electronic crossmatch on. Facility policy does that. These rows say which ABO and Rh pairs are acceptable when electronic crossmatch is already allowed.

Rules in the table in use can still be added, edited, activated, or deactivated, but each change asks for a reason (at least 8 characters) in **Reason for changing the active table**, kept with the change history. A draft table can be built without one; its reason is given when you activate it.

The **At issue** column says what each row does when its table is in use: **Raises to hard stop** (a built-in rule made stricter), **Hard stop if the gate reports this code** (your own code, which applies only if an issue rule reports it), **Same as built-in**, **No effect at issue**, or **Off – built-in rule applies**.
