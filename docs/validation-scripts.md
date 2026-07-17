# Blood Bank LIS — Validation Scripts

Status: Implemented in Phase 8.

These are executable, step-by-step validation scripts for each primary workflow in
[`workflows.md`](workflows.md). Each script states a precondition, numbered operator
steps (as API calls), the expected system response, and an explicit pass/fail criterion.
They are written for the demo seed data (`DatabaseSeeder`) and the request-scoped,
header-based identity used by the API.

## Conventions

- The API enforces authorization at the boundary: every request carries an identity
  header `X-User: <username>` (and optionally `X-Workstation`). A request with no
  identity is **401**; an authenticated user lacking the required permission is **403**
  (see `architecture.md` 4.2, `PermissionCodes`).
- Seeded demo accounts and roles:
  | User | Role | Notable permissions |
  |---|---|---|
  | `admin` | Administrator | all |
  | `supervisor` | Supervisor | technologist + override/discard/correct/cancel |
  | `tech1` | Technologist | accession, result enter/verify, inventory, crossmatch/allocate, issue, print labels |
  | `viewer` | ReadOnly | `audit.read` only |
- A **HardStop** rule block returns **422** with `overridable:false`; an overridable
  **Warning** returns **422** with `overridable:true`.
- Dangerous/override actions require a reason and (for issue overrides) a valid
  electronic signature; signatures are recorded via `POST /api/signatures` and supplied
  back via the `X-Esignature-Id` header.

---

## S-01 — Unit intake (receive into quarantine)

Precondition: a product type and a refrigerator location exist (seeded).

1. `POST /api/inventory/units` as `tech1` with a new unit number, product type, ABO/Rh,
   expiration, and location.

Expected: **201 Created**; unit `Status = Quarantine`; an `InventoryStatusHistory` row
and a `Create` audit event exist.

Pass/fail: PASS if status is Quarantine and history+audit are present; FAIL otherwise.

Negative: same call as `viewer` → **403** (`inventory.receive` required).

---

## S-02 — Specimen accessioning

Precondition: patient `MRN0001` exists (seeded).

1. `POST /api/specimens` as `tech1` with patient id, type, collection time.

Expected: **201 Created**; unique accession number; specimen `Status = Accepted`;
`ExpiresUtc` computed.

Pass/fail: PASS if accepted with an expiration and a `Create` audit event.

Negative: accession a specimen whose collection time is beyond the type's stability →
expiration in the past → downstream issue gate will HardStop (see S-05).

---

## S-03 — Result entry, verification, and correction

Precondition: an accepted specimen and an order exist.

1. `POST /api/results/abo-rh` as `tech1` → **201**, result `Status = Entered`.
2. `POST /api/results/{id}/verify` as `tech1` → **200**, `Status = Verified`,
   verifier/UTC stamped; a billing event is captured (see S-10).
3. If the verified ABO/Rh differs from the patient's current historical type, verify
   returns **422** with `overridable=true` and `RES-ABORH-DELTA` until an authorized
   override (reason + e-signature + Retain or Replace) is supplied by a user whose
   max role `SecurityLevel` meets the exception definition's `MinSecurityLevel`
   (seeded as 2 for Supervisor+). **Replace** flips historical current; **Retain** keeps it.
4. `POST /api/results/{id}/correct` as `supervisor` with a reason → **200**; a new
   versioned result supersedes the original; original is preserved; explicit `Update`
   audit event with reason recorded.

Pass/fail: PASS if verification stamps the verifier, delta mismatches block until
override with Retain/Replace, and the correction is versioned (original retained).

Negative: `POST /api/results/{id}/correct` as `tech1` → **403** (`result.correct`).
Negative: delta override as `tech1` (security level 1) → HardStop `EXC-SECURITY-LEVEL`.

---

## S-04 — Crossmatch and allocation

1. `POST /api/crossmatches` as `tech1` for a recipient/unit pair.
   - ABO-incompatible pair → **422** HardStop (not overridable).
   - Compatible pair → **201**, crossmatch recorded with validity window.
2. `POST /api/allocations` as `tech1` for the crossmatched unit → **201**; unit becomes
   reserved; a second active allocation for the same unit is rejected.

Pass/fail: PASS if incompatible pairs HardStop and a unit holds at most one active
allocation.

---

## S-05 — Issue (full issue gate)

Precondition: a crossmatched, allocated, in-date, ABO-compatible unit for the patient.

1. `POST /api/issues` as `tech1` with `IdentityConfirmed:true` and the gate inputs.

Expected: **201 Created**; unit issued; billing capture fires (S-10).

Gate negatives (each **422**):
- Patient identity not confirmed → HardStop.
- Expired unit / expired specimen → HardStop.
- ABO/Rh incompatible → HardStop.
- Missing special requirement (e.g., irradiated) or required antigen-negative not
  confirmed → HardStop.

Pass/fail: PASS if a clean issue succeeds and each unsafe condition blocks with no unit
status change.

Negative (authz): `POST /api/issues` as `viewer` → **403** (`issue.create`).

---

## S-06 — Emergency release / warning override (electronic signature)

Precondition: a condition producing an **overridable Warning** (or an emergency
release before full testing).

1. `POST /api/issues` as `supervisor` with an override reason but **no**
   `X-Esignature-Id` header → **403** "Electronic signature required".
2. `POST /api/signatures` as `supervisor` with `Action:"IssueOverride"` and a
   meaning-of-signature → **201**, returns a signature `id`.
3. `POST /api/issues` as `supervisor` with the override reason and header
   `X-Esignature-Id: <id>` → issue proceeds through the override path.

Expected: the signature is append-only and bound to `supervisor`; the override is
audited; a signature created by a different user is rejected.

Pass/fail: PASS if the override is blocked without a valid signature and succeeds with
one; FAIL if an override is ever accepted without reason + valid signature.

---

## S-07 — Return to inventory

Precondition: a unit currently issued (not yet transfused).

1. `POST /api/issues/{id}/return` as `tech1` with a reason and reissue eligibility.

Expected: **200**; unit re-evaluated for reissue (expiration/integrity re-checked);
status reflects return; audit event recorded.

Pass/fail: PASS if reissue eligibility is re-computed and the action is audited.

---

## S-08 — Discard (dangerous action)

1. `POST /api/inventory/units/{id}/discard` as `supervisor` with a reason → **200**;
   unit `Status = Discarded`; explicit `Discard` audit event with reason.

Pass/fail: PASS if discard requires a reason and writes an explicit audit event.

Negative: discard as `tech1` → **403** (`inventory.discard`).

---

## S-09 — HL7 inbound / outbound

1. `POST /api/hl7/inbound` as `admin` with a valid `ADT^A01` → **200** with an `AA` ACK;
   patient demographics upserted; immunohematology history untouched.
2. Re-send the identical message (same MSH-10) → no duplicate clinical effect
   (idempotent); message logged.
3. Malformed message → **422** with `AR` NAK; an application error → `AE` NAK + an
   interface error-queue entry.
4. `POST /api/hl7/outbound/results/{verifiedResultId}` as `admin` → an `ORU^R01` is
   queued for the verified result.
5. `POST /api/hl7/messages/{id}/replay` as `admin` → replay is idempotent and audited.

Pass/fail: PASS if ACK/NAK codes match outcomes, demographics-only updates apply, and
replays do not duplicate effects.

Negative (authz): any `/api/hl7/*` call as `viewer` → **403** (`hl7.manage`).

---

## S-10 — Billing capture and review

1. Verify a billable result (S-03 step 2) → exactly one **pending** `BillingEvent` is
   captured from the matching charge rule.
2. Repeat the verify/capture trigger → **no** second event (unique `DedupeKey`).
3. Issue a unit (S-05) → a unit-issued charge is captured (product-specific rule or
   catch-all).
4. `GET /api/billing/charges` as `tech1` → the pending queue.
5. `POST /api/billing/charges/{id}/cancel` as `supervisor` with a reason → **200**,
   `Status = Cancelled`, explicit audit event with reason.

Pass/fail: PASS if each trigger creates exactly one event, duplicates are prevented, and
cancellation requires a reason and is audited.

Negative: cancel as `tech1` → **403** (`billing.cancel`).

---

## S-11 — Label and P-tag printing + reprint

1. `POST /api/print/specimen-labels/{specimenId}` as `tech1` → **201**; a `PrintJob` with
   rendered ZPL (control characters in data are hex-escaped) and a preview proof.
2. `POST /api/print/compatibility-tags/{issueId}` as `tech1` → **201**; the P-tag model
   reflects the issued unit/patient; emergency banner appears for emergency releases.
3. `POST /api/print/jobs/{id}/reprint` as `supervisor` with a reason → **200**; explicit
   `Reprint` audit event with reason.

Pass/fail: PASS if labels render faithfully from audited records and reprints are
reason-gated and audited.

Negative: reprint as `tech1` → **403** (`print.reprint`).

---

## S-12 — Authorization (default-deny)

1. Any write with **no** `X-User` header → **401**.
2. `POST /api/results/{id}/correct` as `tech1` → **403** (lacks `result.correct`).
3. The same call as `supervisor` → permitted (subject to the workflow rules).
4. `GET /api/audit-events` as `viewer` → **200**; as an unknown user → **401**.

Pass/fail: PASS if unauthenticated requests are 401, under-privileged requests are 403,
and privileged requests proceed.

---

## S-13 — Audit completeness and immutability

1. After exercising S-01..S-11, `GET /api/audit-events?entityType=...&entityId=...` as
   `viewer` returns the chain of `Create/Update/Verify/Issue/Return/Discard/Override/
   Reprint` events for the affected entities.
2. There is **no** API path to update or delete an audit event.

Pass/fail: PASS if every clinical action produced an audit event and audit is read-only.
