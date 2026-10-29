---
slug: safety
title: Safety checks
order: 410
chapter: Safety
summary: How hard stops differ from warnings, what the issue gate checks, and when the system asks for an electronic signature.
routes:
---

The bench does not decide compatibility by itself. The application runs the safety rules and returns a result: pass, warning, or hard stop. The alert on the screen is the message you act on. This topic is the map of the checks you will meet most often.

## Hard stop and warning

A **hard stop** ends the action. There is no reason field that lets you continue. Correct the record or choose a different patient, specimen, or unit.

A **warning** lets a person with the override permission continue after a reason and, for issue warnings, an electronic signature. The warning and the override are both stored. Overriding does not change the antibody, the crossmatch, or the blood type.

> Hard stop. Do not use another user's session to get past a check you cannot pass. The audit trail will show their name, and the check exists because the action is unsafe.

## Issue gate

Before a unit leaves the bank, the gate checks:

| Check | What it means |
| --- | --- |
| Identity | Two independent tokens, such as MRN and date of birth, at issue and again when transfusion is documented. |
| Specimen | A current specimen for this patient, not expired and not rejected. |
| ABO and Rh | The patient has a type, the unit has a type, and they are compatible for the component. |
| Unit status | The unit is in a status that can be issued, is not expired, and is the unit reserved to this patient. |
| Crossmatch | A current compatible crossmatch, unless this is an emergency or MTP issue. |
| Special requirements | Active requirements such as irradiation or antigen-negative are met. |
| Appearance | The unit passed visual inspection at issue. |
| Autologous and directed | The unit is issued only to the intended patient. |
| Merged chart | The MRN is the surviving chart, not the one that was merged away. |

Emergency and MTP do not skip identity, specimen, expiration, or appearance. They change the missing crossmatch from a hard stop into a warning, and only for a user with emergency-release permission. The issue is then due for a retrospective crossmatch.

## Electronic crossmatch

Electronic crossmatch is off until Facility Policy turns it on. Even then it is refused when the patient has any antibody history, including deactivated rows, or an open identification workup. It is also refused when the patient does not meet the minimum negative antibody screens configured in Crossmatch Settings. Serologic crossmatch remains available.

## Signatures

The system asks you to sign by entering your password again. The signature is stored with the action. You will be asked when you:

- Override an issue warning, including emergency release without a crossmatch.
- Correct a verified result.
- Override ABO or Rh history.
- Close a reaction investigation when the close action requires a signature.

Discard, return, reprint, and product modification ask for a reason. A signature is added when that action's policy requires one. Reprint specifically requires the reprint permission and a reason.

## Antibodies

Antibody history is append-only. Deactivate means "not currently detectable," not "never identified." Any historical row blocks electronic crossmatch. An open identification workup blocks chart edits to antibodies and blocks electronic crossmatch. Completing the workup posts the identified specificities. Assist on the workup never identifies the antibody for you.
