# Open clinical and regulatory decisions

Do not guess. When a rule depends on an external standard or facility policy
that is not available in this repository, implement the control as configurable
and record the question here.

These items are labeled **REQUIRES REGULATORY / SME VERIFICATION**.
This software is compliance-supporting and validation-ready. It is not
automatically FDA, AABB, CAP, or CLIA certified.

| ID | Topic | Question | Current software behavior | Suggested owner |
|---|---|---|---|---|
| OCD-001 | Electronic XM after undetectable antibody | Does a historically identified, currently undetectable antibody permanently preclude computer XM? | Any antibody-history row (active or deactivated) blocks electronic XM (`XM-EC-HISTORY`). Antigen-negative requirements still evaluate all history rows. | Transfusion medicine medical director |
| OCD-002 | Specimen validity windows | Confirm alloimmunization-risk vs standard specimen windows and lookback days against current AABB language and facility SOP. | Defaults: 72 hours when recent transfusion/pregnancy; 168 hours otherwise; 90-day lookback. Facility policy keys exist. | Blood bank supervisor + medical director |
| OCD-003 | Uncrossmatched ABO/Rh | Confirm whether uncrossmatched RBC/WB must be group O, and whether childbearing-age Rh-negative recipients require O-negative, including age cutoff. | Defaults on (`UncrossmatchedCellularMustBeGroupO`, `UncrossmatchedONegForChildbearing`, age 50). Emergency path is Warning + override, not "ignore all rules." | Medical director |
| OCD-004 | ISBT 128 licensed tables | Confirm DIN check, ABO/RhD codes, product codes, and data structures against current ICCBBA documentation. | Parsers and placeholder catalogs are present. Seeded codes are not a licensed ICCBBA extract. | Interface / ISBT administrator + ICCBBA licensee |
| OCD-005 | Receipt temperature range | Confirm 1–10 °C (and frozen-product ranges) against current product circulars and facility SOP. | Default receive-temperature HardStop uses 1–10 °C when the policy is enabled. | Inventory supervisor |
| OCD-006 | Computer XM AABB 5.16 validation | When may the facility enable electronic XM in production? | `AllowElectronicCrossmatch` is a facility policy flag. Disabled policy is a HardStop (`XM-EC-POLICY`). | Quality + medical director |
| OCD-007 | Record retention | Confirm retention years against current 21 CFR 606.160 interpretation and state law. | Default retention metadata is 10 years; clinical/audit purge is not implemented. | Quality / compliance |
| OCD-008 | Second-verifier policy | Which workflows require a distinct directory second user vs validated electronic identification? | Configurable flags exist for issue, receive, discard, quarantine release, directed conversion, and transfusion dual ID. | Medical director + nursing |
| OCD-009 | Inactive patient clinical use | May an Inactive (not Merged) patient receive testing, allocation, or issue? | Merged records are HardStop `PAT-MERGED-INACTIVE`. Inactive records remain usable. | Medical director + registration |
| OCD-010 | Manual merge authorization | Does merging two patient records require a second authorizer in addition to `patient.merge`? | Manual merge requires `patient.merge` (not `patient.write`). Seeded to Supervisor and Administrator, not Technologist. Reason is required. Discordant ABO/Rh remains a HardStop. A second authorizer is not implemented. | Medical director + registration + quality |
| OCD-011 | Unit retype self-verify | May the same user who entered a unit ABO/Rh retype verify it and release the unit? | Default `Inventory.BlockRetypeSelfVerify` is on. Record leaves the unit Received. Verify applies `RES-SELF-VERIFY` and only then moves to Available or Quarantine. | Blood bank supervisor + medical director |
| OCD-012 | MTP vs emergency privilege | Should massive transfusion use a distinct privilege from emergency release? | Both require `issue.emergency-release`. Seeded to Supervisor and Administrator, not Technologist. | Medical director + blood bank supervisor |
| OCD-013 | Patient ABO/Rh self-verify | May the same user who entered a patient ABO/Rh verify it and establish the current type? | Default `Result.BlockAboSelfVerify` is on. Save/complete leaves the result Entered. Verify applies `RES-SELF-VERIFY` and only then writes current `PatientBloodTypeHistory`. | Blood bank supervisor + medical director |
| OCD-014 | Lookback vs inventory recall privilege | Must a lookback recall also require `inventory.recall`, or is `lookback.manage` sufficient? | DIN recall requires `lookback.manage` in the Application service. It does not also require `inventory.recall`. | Medical director + inventory supervisor + quality |

When a decision is closed, move the row to the bottom with the effective date,
the chosen configuration values, and the validation evidence ID. Do not delete
the history.
