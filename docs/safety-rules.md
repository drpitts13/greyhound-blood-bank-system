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
| `ISS-SPEC-EXISTS` | A current specimen exists for the patient | HardStop |
| `ISS-SPEC-PATIENT` | Specimen belongs to the same patient | HardStop |
| `ISS-SPEC-EXPIRED` | Specimen is not past its expiration | HardStop |
| `ISS-PT-ABORH` | Patient ABO/Rh is known (current `PatientBloodTypeHistory`) | HardStop |
| `ISS-UNIT-ABORH` | Unit ABO/Rh is present | HardStop |
| `ISS-ABO-COMPAT` | Unit ABO is compatible with patient via antigen/antibody conflict check (section 3) | HardStop |
| `ISS-PRODUCT-TYPE` | Unit product type matches what the order/clinical need requires | HardStop |
| `ISS-UNIT-STATUS` | Unit status is Available/Allocated/Assigned/Crossmatched/Selected (not Quarantine/OnHold/Discarded/Issued/Transfused/Expired) | HardStop |
| `ISS-UNIT-EXPIRED` | Unit is not past expiration date/time | HardStop |
| `ISS-ALLOCATION` | Unit is allocated/reserved to THIS patient | HardStop |
| `ISS-XM-REQUIRED` | If product requires crossmatch, a compatible, unexpired crossmatch exists (unless emergency release) | HardStop |
| `ISS-SPECIAL-REQ` | All active special requirements met (irradiated/CMV-neg/leukoreduced/washed/antigen-negative) — computer-evaluated from persisted patient requirements | HardStop |
| `ISS-ANTIGEN-NEG` | For RBC/WB: unit typed antigen-negative for each clinically significant patient antibody (current or historical) | Warning (supervisor+ override via ExceptionDefinitions, MinSecurityLevel 2) |
| `ALLOC-XM-AB-HISTORY` | Positive antibody screen (current/historical) or antibody history requires complex crossmatch (simple XM needs override) | Warning |
| `ISS-ABORH-DISCREPANCY` | Current ABO/Rh determination agrees with historical record (computed from history, not an operator flag) | Warning (HardStop if unresolved on a crossmatch-required product) |
| `ISS-VISUAL` | Unit passed visual inspection at issue | HardStop |
| `ISS-SPEC-NEAR-EXPIRY` | Specimen expires within configurable warning window | Warning |
| `ISS-UNIT-NEAR-EXPIRY` | Unit expires within configurable warning window | Warning |
| `TX-DUAL-ID` | Distinct second verifier, or validated electronic identification of recipient + unit | HardStop when facility policy requires it |
| `TX-SECOND-USER` | Named second verifier is an active, unlocked application user (not free-text initials) | HardStop when a second verifier is supplied |
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
Quarantine -> Available | Discarded | Expired | Recalled | Damaged | Missing
OnHold     -> Available | Quarantine | Discarded | Expired | Recalled | Damaged | Missing
Available  -> Allocated | Assigned | Selected | Crossmatched | Quarantine | OnHold | Discarded | Expired | Recalled | Transferred | Modified
Allocated  -> Issued | Available (release) | Assigned | Crossmatched | Discarded | Expired
Assigned   -> Issued | Available | Crossmatched | CancelledAssignment | ...
Issued     -> Transfused | TransfusionStarted | Returned | ReturnPending | Recalled

Returned   -> Available | Quarantine | Discarded | Expired
Transfused -> (terminal)
Discarded  -> (terminal)
Expired    -> Discarded
Modified   -> (terminal)
```

- `OnHold` is an operational hold (paperwork, pending review). It is not a quality quarantine: quarantine cannot move to hold, and a held unit cannot be issued until released to Available or escalated to Quarantine.
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

## 6. Result integrity rules

- Verified results are immutable; corrections create a new `TestResults` version and supersede (never overwrite) the prior row.
- Delta check: a new ABO/Rh result that disagrees with the current historical record raises `RES-ABORH-DELTA` (**Warning**). At **verify**, the Warning **blocks** until an authorized override supplies reason + electronic signature + **Retain** (keep historical `IsCurrent`) or **Replace** (append and flip `IsCurrent` to the verified type). Override eligibility is gated by the admin `ExceptionDefinitions` catalog (`MinSecurityLevel` vs the user's max role `SecurityLevel`). Unresolved discrepancy still contributes a HardStop to the issue gate on crossmatch-required products.
- Critical/special flags on results are surfaced to the verifier and carried into compatibility evaluation.

---

## 7. No-silent-change guarantees

- No clinical row is hard-deleted; status/void columns and history tables preserve prior state.
- The system never auto-resolves an ABO/Rh discrepancy, antibody conflict, or compatibility failure. Resolution is always an explicit, audited user action.
- Audit writes occur in the same transaction as the change; a failed audit rolls back the operation.
