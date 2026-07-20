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
| `ISS-UNIT-STATUS` | Unit status is Available/Allocated (not Quarantine/Discarded/Issued/Transfused/Expired) | HardStop |
| `ISS-UNIT-EXPIRED` | Unit is not past expiration date/time | HardStop |
| `ISS-ALLOCATION` | Unit is allocated/reserved to THIS patient | HardStop |
| `ISS-XM-REQUIRED` | If product requires crossmatch, a compatible, unexpired crossmatch exists (unless emergency release) | HardStop |
| `ISS-SPECIAL-REQ` | All active special requirements met (irradiated/CMV-neg/leukoreduced/washed) | HardStop |
| `ISS-ANTIGEN-NEG` | For RBC/WB: unit typed antigen-negative for each clinically significant patient antibody (current or historical) | Warning (supervisor+ override via ExceptionDefinitions, MinSecurityLevel 2) |
| `ALLOC-XM-AB-HISTORY` | Positive antibody screen (current/historical) or antibody history requires complex crossmatch (simple XM needs override) | Warning |
| `ISS-ABORH-DISCREPANCY` | Current ABO/Rh determination agrees with historical record | Warning (HardStop if unresolved on a crossmatch-required product) |
| `ISS-SPEC-NEAR-EXPIRY` | Specimen expires within configurable warning window | Warning |
| `ISS-UNIT-NEAR-EXPIRY` | Unit expires within configurable warning window | Warning |

If `IssueType = EmergencyRelease`, `ISS-XM-REQUIRED` is evaluated as a Warning within that workflow (see section 5) rather than a HardStop, and an `Override` + signature is mandatory.

---

## 2. Specimen expiration logic

- Default specimen validity is policy-driven via `SystemConfiguration` (e.g. 3 calendar days for patients potentially alloimmunized — transfused or pregnant within the preceding 3 months — else a longer interval). The exact policy values are configuration, never hard-coded constants in the engine.
- `Specimens.ExpiresUtc` is computed at accessioning from `CollectedUtc` + the applicable window and recomputed if relevant inputs change (with audit).
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
Quarantine -> Available | Discarded | Expired
Available  -> Allocated | Quarantine | Discarded | Expired
Allocated  -> Issued | Available (release) | Discarded | Expired
Issued     -> Transfused | Returned
Returned   -> Available | Quarantine | Discarded | Expired
Transfused -> (terminal)
Discarded  -> (terminal)
Expired    -> Discarded
```

- Any transition writes `InventoryStatusHistory` + `AuditEvent`.
- Expiration is enforced automatically: a unit past `ExpiresUtc` cannot move to Allocated/Issued (HardStop) and is eligible to be marked Expired.

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
