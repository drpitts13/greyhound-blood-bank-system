---
slug: issuing
title: Issue and transfuse
order: 260
chapter: Compatibility and issue
summary: Issue reserved units, release blood in an emergency, acknowledge ward receipt, document transfusion, and take units back.
routes:
  - /issuing
---

![Issue and transfuse](figures/issuing.png)

**Issue and Transfuse** is the desk for blood that is leaving the bank and for blood that is coming back. One board is open at a time: Ready to issue, Second ABO/Rh, Retrospective, In transit, Issued units, Issue, Ward receipt, Return, and Document transfusion. The issue gate runs on every routine issue. It checks identity, the specimen, ABO and Rh, allocation, crossmatch, special requirements, appearance, autologous or directed restrictions, and whether the chart you are using was merged away. Storage-location rules and remote electronic issue are part of the same gate.

## Ready to issue

Ready to issue opens first. Reserved units are listed so you do not retype the patient and the unit. The row tells you when something is wrong before you start:

- A red cell that still needs a second concordant ABO or Rh is not offered for routine issue.
- A missing or expired specimen is not offered for routine or emergency issue.
- An outdated unit is not offered.
- A unit that still needs a compatible crossmatch is not offered for routine issue.
- An open antibody workup is marked and can still be issued.
- A unit inside the near-expiry window is marked and can still be issued.
- When electronic crossmatch is not eligible, the row shows the reason. Serologic issue is unchanged.

**Issue** on the row switches to the Issue board with the unit and patient filled in. A link that already names the patient or the unit opens that same board.

**Patient identifiers.** Scan the MRN from the wristband, or type it from the requisition, then the date of birth. The scanner's Enter moves you to the date of birth. Under each field a line says **Matches the patient record** or **Does not match this patient**. It never shows the value it expected. By default both fields start empty, so the check compares what you read with the record. A facility can choose to start them filled in (Facility Policies › *Pre-fill patient identifiers at issue and bedside*, OCD-039), and staff then confirm them visually. The server still refuses an issue whose two identifiers do not match.

Then complete appearance, location, and who is taking the unit.

**Scanning without the mouse.** On Issue, Ward receipt, and Document transfusion, Enter moves to the next field: patient ID 1, patient ID 2, then the unit label fields (DIN, product code, ABO/RhD, expiration, extended division). A scanner sends Enter after each scan, so you can scan wristband, requisition, and bag in a row. After the last field the form gets a green outline and keeps the focus. **Issue unit**, **Acknowledge receipt**, and **Document** are never pressed by Enter, so a stray scan cannot issue a unit. Click the button, or press Tab to reach it and then Enter. Press **?** (outside a text field) for the list of shortcuts. Print the compatibility tag when the issue requires a P-tag, then scan the P-tag id before you issue. Facility policy decides whether the tag is required.

> Hard stop. Two independent identity tokens are required. Confirming a single checkbox does not satisfy the check. A hard stop cannot be signed away.

> Warning. A warning (near expiry, an antigen-negative gap, a historical ABO discrepancy that is overridable, or a complex crossmatch that was not done) asks for a reason, the authorizer, and an electronic signature. The signature is re-authentication. The override is stored with the issue. It does not change the result that caused it.

## Emergency release and massive transfusion

Emergency and MTP are a different issue type. They are not an instruction to ignore every rule. The missing crossmatch becomes a warning only inside this issue type, and only for someone who has the emergency-release permission. Technologists who can issue routine units cannot release uncrossmatched blood unless that permission was granted.

You still need a current specimen for the patient, an in-date unit, acceptable appearance, and two identity tokens. You record the reason and sign. The issue is flagged for retrospective crossmatch. It stays on the retrospective queue until someone records the crossmatch, by default within the hours set in facility policy (the shipped default is 24). **Record crossmatch** opens Compatibility with the issue filled in.

## In transit and ward receipt

**In transit** lists units that left in a cooler or with a courier until the receiving location acknowledges custody. **Receive** on the row opens Ward receipt. Receipt repeats the product, expiration, type, specimen, crossmatch, who it was issued to, the cooler, and the destination so the person receiving can compare them with the unit in hand. Transfusion documentation waits until receipt is recorded when that is the path the unit took.

## Document the transfusion

**Issued units** lists outstanding issues so you can document or return them without looking up an internal issue number. **Document** opens Document transfusion and records the identifiers checked at the bedside, the start, and the disposition your procedure captures. Dual identification is required here the same way it is required at issue. Read both identifiers from the patient's wristband at the bedside. The fields start empty unless your facility has turned on pre-fill, and the same match line shows under each.

## Final disposition

**Final disposition** lists issued units that are past the facility window and still have no transfusion document, return, or discard. The window ships at 24 hours. Change it in Facility Policy if your procedure uses another operational interval. The board is a warning. It does not stop you from issuing another unit, documenting the transfusion, returning the unit, or discarding it.

> Warning. A unit on this board still needs a final fate. The list does not change the unit.

## Return

**Return** on an issued row opens the Return board for a unit that came back unused. Record temperature, seal, and appearance. A unit that fails those checks does not go back to available stock. The issued-units board shows whether the storage device is governed. A governed device cannot return to available stock after the facility window, which ships at 30 minutes. Saying the time out was acceptable does not change that. An exempt device does not fail the window. Reissue follows the return rules on the product. Do not document a transfusion for a unit you are returning, and do not return a unit that was transfused.

## What the gate will not do

The gate will not issue the losing chart of a merged patient, a unit allocated to someone else, an autologous or directed unit to the wrong patient, a unit that fails appearance, or a unit that does not meet an enforced special requirement. Fix the record or choose another unit. See [Safety checks](safety) for the list you will see most often.

## Return inspection

When you return a unit, answer **Temperature**, **Seal**, and **Visual inspection**. Each starts as **Not answered**. Until all three are answered, **Return unit** stays unavailable when the unit would go back to available stock. A unit you send to **Quarantine** or **Disposed** can be returned without them, and the record says they were not answered. Your facility can choose to start them as acceptable (Facility Policies › *Start unit inspections as acceptable*, OCD-040). Time out of storage is always worked out by the system.

## Appearance

**Appearance at issue** and **Appearance at receipt** start at **Choose appearance...** with *Not recorded. Look at the bag.* underneath. Look at the unit and pick what you see. The issue or receipt is refused until it is recorded. After each issue or receipt the field clears, so the next unit gets its own look. The same applies when units are received into inventory. If your facility chooses to start appearance as acceptable (OCD-040), the field starts at **Acceptable** and the record notes that the facility default supplied it.

## Who authorized an override

**Authorized by** starts empty. Type the name of the physician or supervisor who approved the override. If you approved it yourself, press **Me**. A blank field is never filled in for you. If you give a reason but no name, the message says so. Your electronic signature separately records who signed.
