# Design: mixed-ABO pooling

Status: phase 1 built (2026-11-21). OCD-094 decided 2026-11-21: cryoprecipitate only; Rh(D) may mix; for cryo a mixed pool ranks the same as single-group compatible units; no ABO/Rh type is sent on HL7 for a mixed pool.

## Problem

Modify Products refuses a pool whose source units differ in ABO or Rh(D) (`MOD-POOL-ABO-MISMATCH`, `UnitModificationEligibilityRule.EvaluatePool`). Many transfusion services routinely pool across ABO, chiefly cryoprecipitate and, at some sites, whole-blood-derived platelets. Those pools cannot be made in the system today.

The block can't simply be lifted. A pool's result unit takes the ABO/Rh of its first source (`BloodProductModificationService`, `Abo = primarySource.Abo`). A mixed pool would then be labelled and checked as one group it is not. For example, an A + O cryo pool would be checked as A, and its anti-B-bearing O plasma would never be considered. The block stays until the pool can say what it contains.

## Principle

**A mixed pool is compatible with a patient only if every ABO/Rh in it is.** Nothing else changes. The engine already reasons in antigens and antibodies (`AboCompatibilityRule.DeriveAboProfile`), and per-product ABO/Rh tables already allow or deny each patient/unit pair. A pool of A and O is checked against the patient once as A and once as O, under the same rule or table that applies to the product today. The worst result wins.

This needs no new compatibility rule and no new table. A facility's existing product tables keep deciding what is allowed.

## Data model

1. **`AboGroup.Mixed` (new value, stored as 5).** Used only on a pooled unit whose sources differ in ABO. It means "see the pool's components". It never stands for a group in its own right.
2. **`BloodUnit.PoolComponentsJson` (new, nullable).** The ABO/Rh of every source in the pool, for example `[{"abo":"A","rh":"Positive"},{"abo":"O","rh":"Positive"}]`. It is written when the pool is made, for every pool, matched or not, so the record is complete. It is never edited afterwards. A correction goes through the existing identity correction, with a reason.
3. **The pool's Rh(D)** is Positive if any source is Positive, otherwise Negative (or Unknown if any is Unknown). This is the conservative reading of what the bag contains, and it is fixed, not a setting.
4. **ISBT 128 ABO/RhD code.** ICCBBA defines codes for pooled products. Until a licensed table supplies the right one (OCD-004), a mixed pool's `AboRhdCode` stays empty. The label prints the groups in words (see Labels).

Migration: one nullable column plus the dev bootstrap column. `AboGroup` is stored as an int, so the new value needs no data change.

## Making the pool (Modify Products)

- A new per-product setting, **Allow mixed-ABO pools** (Admin › Products, beside the ABO/Rh tables), is off by default. With it off, behavior is exactly as today: `MOD-POOL-ABO-MISMATCH` stays a hard stop.
- With it on for the pool's target product, sources of different ABO/Rh pool without a stop. The form says before confirming: *Mixed pool: A, O. The pool will be labelled Mixed ABO and checked against each group at selection and issue.*
- Mixed Rh(D) follows the same setting. The confirmation names the Rh mix (*Rh: positive and negative; labelled Rh positive*).
- Dating and the expiration ceiling are unchanged (earliest source).

## Compatibility (selection, crossmatch, allocation, issue)

- A new `AboCompatibilityRule.EvaluateComponents(recipient, components, componentClass, sex, policy)` runs the existing `Evaluate` once per distinct component ABO/Rh and returns the most severe result. Its message names the component that failed: *Donor ABO O in this pool is not compatible with recipient ABO A for Cryoprecipitate.*
- Every call site that passes a unit's ABO/Rh routes through a single `UnitAboRh.For(unit)` helper. It returns the component list for a pooled unit and the unit's own ABO/Rh otherwise. The call sites are:
  - `CompatibilityService`
  - `PatientAllocationService` (two places)
  - `IssueGate`
  - `ReactionInvestigationService`
  - `CompatibilityRulesEngine`
- **Fail closed.** `AboGroup.Mixed` reaching `DeriveAboProfile` throws, and a `Mixed` unit with no components is a hard stop (*Pool components are not recorded*). A missed call site therefore blocks the unit; it can never pass it. A source test lists every `AboCompatibilityRule.Evaluate(` call outside the helper and fails if one is added.
- Emergency release and MTP choose by group. A mixed pool is offered for a patient of unknown type only if every component would be offered alone. This uses the same per-component check against the facility's emergency choices.

## Inventory, stock and selection

- Inventory, unit detail, pick lists and the allocation board show **Mixed** with the groups (*Mixed: A, O*). They never show a single group.
- Minimum stock and supplier orders count mixed pools under their own row, *Mixed ABO*. They count toward no single group's minimum.
- Selection order (`ProductSelectionOrder`) ranks a mixed pool after same-group and compatible single-group units. This is an assumption to confirm in OCD-094.
- Retype does not apply to pools of non-red-cell products. If a red-cell product is ever pooled, retype stays required per source, as today.

## Labels, reports and interfaces

- **Component label and P-tag:** the ABO field prints *MIXED* with the groups beneath (*A, O*), and the Rh field prints the pool's Rh(D). The ISBT ABO/RhD bar code is left off until a licensed pooled code is available (OCD-004).
- **HL7 BPS/BTS** (BPX-11, blood product ABO/Rh): a mixed pool sends the pooled ABO/RhD code once one is licensed. Until then the field is empty and the components go in a note segment. Interfaces must be told before go-live, and this is listed as an interface change.
- **FHIR** (`FhirMapper`): the unit's ABO is reported as a mixed pool with one component entry per source.
- **Reports:** Antibodies and Special requirements are unaffected. Inventory and utilization reports show *Mixed ABO*.
- **Lookback and recall:** these already trace pools to every source unit, so this change adds nothing there.

## Phases

1. **Record components.** Write `PoolComponentsJson` for every pool and show it on unit detail. No behavior change. This is safe to ship alone.
2. **Fail-closed compatibility.** Add `AboGroup.Mixed`, the `UnitAboRh` helper, routing for every call site, and the source test. Ship it with the setting still unavailable, so no mixed unit can exist yet.
3. **Allow mixed pools.** Add the per-product setting, the Modify Products confirmation, labels and P-tags, inventory, stock and selection display.
4. **Interfaces.** HL7 BPS/BTS and FHIR, once OCD-004 supplies the pooled ABO/RhD code or the facility decides how to send it.

Each phase has its own tests. Phases 2 and 3 need integration tests for these cases:
- A + O cryo pool to an A patient: refused or allowed according to the cryo table.
- AB patient: allowed.
- O patient: refused for the A component.
- Mixed Rh platelet pool to an Rh-negative female: follows the product's Rh table.
- A `Mixed` unit without components: hard stop.

## Not in scope

- Pooling red cells across ABO. It stays blocked: a product whose ABO table makes cross-group red cells incompatible would be refused per component anyway, and the setting should not be offered for red-cell products.
- Choosing a pool's "representative" group. The design deliberately has none.

## Decisions (OCD-094, decided 2026-11-21)

1. Cryoprecipitate only: the per-product setting is offered only for cryoprecipitate products.
2. Yes: a cryo pool may mix Rh(D), labelled Rh positive if any source is, checked per component.
3. For cryo, a mixed pool ranks the same as single-group compatible units (no penalty in `ProductSelectionOrder`).
4. No ABO/Rh type is sent on HL7 (BPX-11 empty) for a mixed pool.

## Questions as raised

1. Which products may be pooled across ABO (expected: cryoprecipitate, possibly pooled platelets). The setting is per product and off by default.
2. Whether a mixed pool may also mix Rh(D). The design labels it Rh positive if any source is positive and checks it per component.
3. Where a mixed pool ranks in selection relative to single-group compatible units.
4. How a mixed pool is sent on HL7 until a licensed pooled ABO/RhD code is loaded: empty BPX-11 with a note, or hold it from the interface.
