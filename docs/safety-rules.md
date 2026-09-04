# Blood Bank LIS — Safety-Critical Business Rules

Status: Phase 0 (design). These rules are implemented as **pure functions** in `BloodBankLIS.Domain`, each returning a `RuleResult` with a stable `Code`, a `Severity`, and a human-readable message. The Application layer aggregates them into a single `RuleEvaluation`.

## Severity model

| Severity | Behavior |
|---|---|
| **HardStop** | Operation is blocked. Cannot be overridden by any user. |
| **Warning** | Operation is blocked unless overridden with reason + authorization + electronic signature + audit. |
| **Pass** | No objection. |

Rules:
- The engine **never** auto-downgrades a HardStop to a Warning or auto-corrects data.
- An evaluation with any HardStop is a HardStop overall, regardless of other results.
- An evaluation with one or more Warnings (and no HardStop) is overridable.
- Each rule is individually unit-tested with positive and negative cases (see `validation-plan.md`).

---

## 1. Issue gate (unit must not be issued unless ALL pass)

These run in `IssueUnitCommand` before a unit leaves inventory. Reference: `workflows.md` sections 4 and 5.

| Code | Rule | Severity if violated |
|---|---|---|
| `ISS-IDENTITY` | Patient identity confirmed and matches specimen + unit tag | HardStop |
| `PAT-MERGED-INACTIVE` | Patient record is not a merged (losing) identity — accession/order/result/immuno/issue/allocate/XM continue on the survivor | HardStop |
| `ISS-SPEC-EXISTS` | A current specimen exists for the patient | HardStop |
| `ISS-SPEC-PATIENT` | Specimen belongs to the same patient | HardStop |
| `ISS-SPEC-EXPIRED` | Specimen is not past its expiration | HardStop |
| `ISS-PT-ABORH` | Patient ABO/Rh is known (current `PatientBloodTypeHistory`) | HardStop |
| `ISS-UNIT-ABORH` | Unit ABO/Rh is present | HardStop |
| `ISS-ABO-COMPAT` | Unit ABO is compatible with patient via antigen/antibody conflict check (section 3) | HardStop |
| `ISS-PRODUCT-TYPE` | Unit product type matches what the order/clinical need requires | HardStop |
| `ISS-UNIT-STATUS` | Unit status is Available/Allocated/Assigned/Crossmatched/Selected (not Quarantine/OnHold/Discarded/Issued/Transfused/Expired/ReturnedToSupplier) | HardStop |
| `ISS-UNIT-EXPIRED` | Unit is not past expiration date/time | HardStop |
| `ISS-ALLOCATION` | Unit is allocated/reserved to THIS patient | HardStop |
| `ISS-AUTO-DIR` | Autologous/directed unit is issued or allocated only to the reserved patient — evaluated inside `IssueGate` (not only the issuing service) | HardStop |
| `ISS-XM-REQUIRED` | If product requires crossmatch, a compatible, unexpired crossmatch exists (unless emergency release) | HardStop |
| `ISS-SPECIAL-REQ` | All active special requirements met (irradiated/CMV-neg/leukoreduced/washed/antigen-negative) — computer-evaluated from persisted patient requirements | HardStop |
| `ISS-ANTIGEN-NEG` | For RBC/WB: unit typed antigen-negative for each clinically significant patient antibody (current or historical) | Warning (supervisor+ override via ExceptionDefinitions, MinSecurityLevel 2) |
| `ALLOC-XM-AB-HISTORY` | Positive antibody screen (current/historical) or antibody history requires complex crossmatch (simple XM needs override) | Warning |
| `ISS-ABORH-DISCREPANCY` | Current ABO/Rh determination agrees with historical record (computed from history, not an operator flag) | Warning (HardStop if unresolved on a crossmatch-required product) |
| `ISS-VISUAL` | Unit passed visual inspection at issue | HardStop |
| `ISS-APPEAR` | Coded appearance at issue is Acceptable (not Clots/Hemolysis/Leaking/…) | HardStop |
| `ISS-SPEC-NEAR-EXPIRY` | Specimen expires within configurable warning window | Warning |
| `ISS-UNIT-NEAR-EXPIRY` | Unit expires within configurable warning window | Warning (issue gate); worklist `GET /api/inventory/units/near-expiry` (`Inventory.NearExpiryWarningHours`, default 24) |
| `TX-DUAL-ID` | Distinct second verifier, or validated electronic identification of recipient + unit | HardStop when facility policy requires it |
| `TX-SECOND-USER` | Named second verifier is an active, unlocked application user (not free-text initials) | HardStop when a second verifier is supplied |
| `TX-WARD-RECEIPT` | Receiving location acknowledged the issued unit (`WardReceivedUtc`) | HardStop when facility policy `Transfusion.RequireWardReceipt` is true (default) |
| `TX-WARD-APPEAR` | Coded appearance at ward receipt is Acceptable (not Clots/Hemolysis/Leaking/…) | HardStop |
| `ISS-RETRO-XM-PENDING` | Emergency/MTP issue released without a compatible crossmatch remains on the retrospective XM worklist until a post-issue compatible XM is recorded | Worklist / Warning when overdue (`Issue.RetrospectiveCrossmatchDueHours`, default 24) |
| `ISS-IN-TRANSIT` | Issued unit has not been acknowledged at the receiving location (cooler / remote-issue custody) | Worklist / Warning when overdue (`Issue.InTransitDueHours`, default 4) |
| `INV-Q-RELEASE-2ND` | Distinct directory user as second verifier to release a unit from quality quarantine | HardStop when `Inventory.RequireQuarantineReleaseVerifier` is true (default) |
| `INV-DISC-2ND` | Distinct directory user as second verifier to discard a unit | HardStop when `Inventory.RequireDiscardVerifier` is true (default) |
| `INV-RCV-VISUAL` | Unit passed visual inspection at receipt (no clots, hemolysis, or container defects) | HardStop when `Inventory.RequireReceiveVisualInspection` is true (default) |
| `INV-RCV-APPEAR` | Coded appearance at receipt is Acceptable (not Clots/Hemolysis/Leaking/…) | HardStop when visual inspection is required (default) |
| `INV-RCV-TEMP` | Shipping-container temperature at receipt is recorded and within 1–10 °C | HardStop when `Inventory.RequireReceiveTemperature` is true (default); out-of-range units are not received |
| `INV-AUTO-DIR` | Autologous/directed unit names the intended recipient at receive (or packing-list expect) | HardStop |
| `INV-EXPECT-OVERDUE` | Packing-list / ASN unit has not arrived by `ExpectedArrivalDueUtc` | Worklist / Warning when overdue (`Inventory.ExpectedArrivalDueHours`, default 24) |
| `INV-Q-REASON` | Quality quarantine uses a coded catalog reason (not Unspecified; Other requires notes) | HardStop |
| `INV-DISCREPANCY` | Missing or damaged unit is awaiting locate or inspect | Worklist `GET /api/inventory/units/discrepancy` |
| `INV-DIR-ALLO` | Unused directed unit may be converted to allogeneic inventory; autologous cannot; reserved/issued statuses must be released first | HardStop |
| `INV-DIR-CONV-2ND` | Distinct directory user as second verifier to convert a directed unit to allogeneic | HardStop when `Inventory.RequireDirectedConversionVerifier` is true (default) |
| `INV-RCV-2ND` | Distinct directory user as second verifier when receiving a unit (walk-in, expected arrival, ISBT) | HardStop when `Inventory.RequireReceiveVerifier` is true (default) |
| `RET-REISSUE` | Returned unit may re-enter Available only when temperature, seal, visual, and time-out-of-storage checks pass | HardStop / Warning |

If `IssueType = EmergencyRelease`, `ISS-XM-REQUIRED` is evaluated as a Warning within that workflow (see section 5) rather than a HardStop, and an `Override` + signature is mandatory.

---

## 2. Specimen expiration logic

- When the patient was transfused or had a documented pregnancy in the lookback window (default 90 days), specimen validity is the alloimmunization-risk window (default **72 hours / 3 days** from collection). Otherwise a longer configured standard window applies (default 168 hours). Keys: `Specimen.ValidityHours.AlloimmunizationRisk`, `Specimen.ValidityHours.Standard`, `Specimen.LookbackDays`.
- Accessioning stores two independent identifiers (typically MRN + DOB) that must match the patient record.
- `Specimens.ExpiresUtc` is computed at accessioning and recomputed (with audit) when alloimmunization-risk status changes.
- Rule `SPEC-EXPIRED` (HardStop on issue) and `SPEC-NEAR-EXPIRY` (Warning) both read `IClock` for deterministic testing.

---

## 3. ABO/Rh compatibility (antigen/antibody)

- ABO compatibility is evaluated by deriving antigens and naturally occurring isoagglutinins from each side’s ABO type, then detecting antigen/antibody conflicts. Example: type A expresses A antigen and is assumed to have anti-B.
- Conflict rule: whenever either side has an antigen, the other side must not carry the corresponding antibody (and symmetrically).
- Component direction:
  - **RBC / granulocytes:** patient antibodies vs unit antigens.
  - **Whole blood:** bidirectional (cellular + plasma), because the unit carries both red cells and plasma.
  - **Plasma / cryoprecipitate / platelets:** unit antibodies vs patient antigens (inverse).
- Equivalent clinical outcomes for RBC: O→{O}; A→{A,O}; B→{B,O}; AB→{AB,A,B,O}.
- Rh(D): Rh-negative recipients must not receive Rh-positive RBC or whole blood (HardStop). Anti-D is not assumed from Rh-negative typing alone; immunized anti-D is handled by antigen-negative rules (`ISS-ANTIGEN-NEG`).
- Exhaustively unit-tested across recipient/donor combinations and component directions.

---

## 4. Inventory status transitions (guarded)

Allowed transitions are enforced by a transition guard; anything not listed is a HardStop.

```
# Authoritative allow-list is InventoryStatusTransition (expanded for ISBT 128).
# See docs/isbt128-module.md. Legacy core paths remain:
Expected   -> Received | Quarantine | CancelledAssignment | Missing | Discarded | ReturnedToSupplier
Quarantine -> Available | Discarded | Expired | Recalled | Damaged | Missing | ReturnedToSupplier
OnHold     -> Available | Quarantine | Discarded | Expired | Recalled | Damaged | Missing | ReturnedToSupplier
Available  -> Allocated | Assigned | Selected | Crossmatched | Quarantine | OnHold | Discarded | Expired | Recalled | Transferred | Modified | Missing | Damaged | ReturnedToSupplier
Allocated  -> Issued | Available (release) | Assigned | Crossmatched | Discarded | Expired
Assigned   -> Issued | Available | Crossmatched | CancelledAssignment | ...
Issued     -> Transfused | TransfusionStarted | Returned | ReturnPending | Recalled | Missing
Missing    -> Quarantine | Available | Discarded | Damaged
Damaged    -> Quarantine | Discarded | ReturnedToSupplier

Returned   -> Available | Quarantine | Discarded | Expired
Transfused -> (terminal)
Discarded  -> (terminal)
ReturnedToSupplier -> (terminal)
Expired    -> Discarded
Modified   -> (terminal)
```

- `Expected` is packing-list / ASN inventory that is not yet in house. It is not transferable or issuable. Arrival confirmation applies `INV-RCV-VISUAL` and lands in `Received` or `Quarantine`.
- `OnHold` is an operational hold (paperwork, pending review). It is not a quality quarantine: quarantine cannot move to hold, and a held unit cannot be issued until released to Available or escalated to Quarantine.
- `Missing` is a physical-inventory discrepancy (SoftBank/SafeTrace). It is not issuable. Locating a missing unit lands in `Quarantine` for inspection, not Available. Missing and damaged units appear on the discrepancy worklist (`GET /api/inventory/units/discrepancy`).
- `Damaged` is container integrity failure found after the unit is already in inventory. It is not issuable. Inspection lands in `Quarantine`; discard is the terminal alternative.
- `ReturnedToSupplier` is the SoftBank/SafeTrace consignee reject / unused-stock return to the vendor. Distinct from ward `Returned` and from packing-list `CancelledAssignment`. Terminal; not issuable.
- Releasing a unit from quality quarantine requires a distinct directory second verifier (`INV-Q-RELEASE-2ND`) when `Inventory.RequireQuarantineReleaseVerifier` is true (default).
- Placing a unit in quality quarantine requires a coded catalog reason (`INV-Q-REASON`). Intake, locate, inspect, retype discrepancy, failed return, reaction remainder, and modification results store the matching code automatically.
- Discarding a unit requires a distinct directory second verifier (`INV-DISC-2ND`) when `Inventory.RequireDiscardVerifier` is true (default).
- Any transition writes `InventoryStatusHistory` + `AuditEvent`.
- Expiration is enforced automatically: a unit past `ExpiresUtc` cannot move to Allocated/Issued (HardStop) and is eligible to be marked Expired.
- `Modified` is the terminal state for a source unit consumed into a product modification (divide/pool/irradiate/thaw/volume-reduce/leukoreduce); the resulting unit(s) are new `BloodProducts` rows in `Quarantine` (see section 4a).

---

## 4a. Product modification rules

Enforced by `UnitModificationEligibilityRule` and `ModificationExpirationRule` in `BloodProductModificationService`. Reference: `workflows.md` §8a.

| Code | Rule | Severity if violated |
|---|---|---|
| `MOD-STATUS-INVALID` | Source unit status is `Available` | HardStop |
| `MOD-EXPIRED` | Source unit is not past its expiration date/time | HardStop |
| `MOD-PRODUCT-MISMATCH` | Source unit's product type matches the modification rule's source product | HardStop |
| `MOD-POOL-MIN-SOURCES` | Pool has at least two source units | HardStop |
| `MOD-POOL-ABO-MISMATCH` | All pooled source units share the same product type, ABO, and Rh(D) | HardStop |
| `MOD-DIVIDE-MIN-TARGETS` | Divide requests at least two result units | HardStop |
| `MOD-VOLUME-EXCEEDS-SOURCE` | Divide's requested child volumes (when supplied) do not exceed the source unit's volume | HardStop |
| `MOD-COLLECTION-REQUIRED` | Collection-relative expiration codes require every source unit to have a collection date/time | HardStop |

Admin `ExpirationModificationCodes` catalog validation (`ExpirationModificationCodeValidator`), all HardStop: `EXPCODE.CODE.REQUIRED`, `EXPCODE.AMOUNT.INVALID`, `EXPCODE.UNIT.INVALID`, `EXPCODE.RELATIVE.INVALID`, `EXPCODE.CODE.DUPLICATE`.

Admin `ModificationRules` catalog validation (`ModificationRuleValidator`), all HardStop: `MODRULE.CODE.REQUIRED`, `MODRULE.CODE.DUPLICATE`, `MODRULE.SOURCE.REQUIRED`, `MODRULE.TARGET.REQUIRED`, `MODRULE.EXPCODE.REQUIRED`, `MODRULE.EXPCODE.INACTIVE`, `MODRULE.TRIPLE.DUPLICATE` (another active rule already maps the same source product + type + target product), `MODRULE.SOURCE.INACTIVE`/`MODRULE.TARGET.INACTIVE`. `MODRULE.SAMEPRODUCT` is a non-blocking Warning.

Expiration: `ResultExpiresUtc = min(anchor + offset, earliest source ExpiresUtc)` — the anchor is `PerformedUtc` when the expiration code is relative to modification, or the earliest source collection timestamp when it is relative to collection. A result unit can never outlive the shortest-lived unit consumed to produce it.

---

## 5. Dangerous actions (confirmation + reason + e-signature + audit)

| Action | Confirmation | Reason | E-signature | Audit event |
|---|---|---|---|---|
| Emergency release (uncrossmatched) | Yes | Yes | Yes (authorizer) | `Issue` + `Override` |
| Override a compatibility/issue Warning | Yes | Yes | Yes | `Override` |
| Discard a unit | Yes | Yes | No (reason required) | `Discard` |
| Change a verified result | Yes | Yes | Yes | `Correct` |
| Reprint a compatibility tag | Yes | Yes | No (reason required) | `Reprint` |
| Return an issued unit to inventory | Yes | Yes | No (reason required) | `Return` |
| Manually alter ABO/Rh history | Yes | Yes | Yes | `Update` (blood type history) |
| Deactivate an antibody record | Yes | Yes | No (reason required) | `Update` (antibody history) |
| Modify a product (divide/pool/irradiate/thaw/volume-reduce/leukoreduce) | No | Yes | No (reason required) | `Modify` |

- HardStops are never part of an override path — only Warnings can be overridden.
- All override and signature records are append-only (`Overrides`, `ElectronicSignatures`).

---

## 5a. Transfusion reaction workup (close gate)

Recorded on `ReactionInvestigation` and evaluated by `ReactionWorkupCompletenessRule` before close.

| Code | Rule | Severity if violated |
|---|---|---|
| `RXN-WORKUP-INCOMPLETE` | Clerical check, visual inspection, and DAT are recorded; DAT-positive requires elution notes | HardStop |

Opening a reaction investigation quarantines the implicated unit when `InventoryStatusTransition` allows (typically `TransfusionStopped` remainder). Fully transfused units stay terminal; the checklist still records segment/bag retention.

---

## 6. Result integrity rules

- Verified results are immutable; corrections create a new `TestResults` version and supersede (never overwrite) the prior row.
- Delta check: a new ABO/Rh result that disagrees with the current historical record raises `RES-ABORH-DELTA` (**Warning**). At **verify**, the Warning **blocks** until an authorized override supplies reason + electronic signature + **Retain** (keep historical `IsCurrent`) or **Replace** (append and flip `IsCurrent` to the verified type). Override eligibility is gated by the admin `ExceptionDefinitions` catalog (`MinSecurityLevel` vs the user's max role `SecurityLevel`). Unresolved discrepancy still contributes a HardStop to the issue gate on crossmatch-required products.
- Critical/special flags on results are surfaced to the verifier and carried into compatibility evaluation.
- Verifying a free-text or coded test with `ContributesToAntibodyHistory` (typically ABID) resolves catalog specificities (`anti-K`, `anti-E`) and posts them to `AntibodyHistory`, which then drives antigen-negative selection and the complex-crossmatch gate. Unmatched `anti-*` tokens post as free-text history and raise `RES-ABID-UNMATCHED` (Warning). Historical antibodies are never removed by a later negative or different identification.

---

## 7. No-silent-change guarantees

- No clinical row is hard-deleted; status/void columns and history tables preserve prior state.
- The system never auto-resolves an ABO/Rh discrepancy, antibody conflict, or compatibility failure. Resolution is always an explicit, audited user action.
- Audit writes occur in the same transaction as the change; a failed audit rolls back the operation.
