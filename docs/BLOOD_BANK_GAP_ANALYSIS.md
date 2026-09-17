# Blood bank gap analysis

Residual gaps after existing Domain/Application controls. Ranked by
patient-safety impact among items that are still open. Cycle 1 implemented
**gap 1**. Cycle 2 implemented **gap 6**. Cycle 3 implemented **gap 7**.
Cycle 4 implemented **gap 8**. Cycle 6 implemented the bedside identity-token
slice of **gap 15**. Cycle 7 added the licensed ISBT catalog **import path**
for **gap 2**; Cycle 14 added CSV/TSV/JSON extract import if files are
available (extract not yet loaded). Cycle 8 recorded **gap 5** as
accepted transport-trust (OCD-034). Cycle 9 closed **gap 9** (OCD-033
live row) and added security headers for **gap 18**. Cycle 10 closed
**gap 10** (OCD-007 no purge) and packaged **gap 19** (`TEST-BB-*`).
Cycle 11 closed **gap 12** (OCD-035: keep order-rule hooks) and implemented
**gap 16** (read-only downtime reconciliation snapshot).

SME-blocked items must not be “fixed” by inventing a clinical rule. Record
them in [`docs/OPEN_CLINICAL_DECISIONS.md`](OPEN_CLINICAL_DECISIONS.md).
Keep them in the continuous-improvement loop: ask the decision in the
active conversation, wait for the answer, then implement. Do not skip
them silently and do not invent ICCBBA tables.

| Rank | Priority | Gap | Residual | Cycle 1 action |
|---|---|---|---|---|
| 1 | P0 via P6 | API identity spoofing: `X-User` trusted after login; empty password hash signs in | Low (Bearer session; RISK-BB-254) | **Implemented Cycle 1** |
| 2 | P1 | Unlicensed ISBT / ICCBBA catalogs (RISK-BB-014, OCD-004) | Medium (JSON + extract-file import; licensed extract not yet loaded) | **Import path Cycle 7/14**; do not invent tables |
| 3 | P3 | Antigen phenotype updated in place (OCD-022) | Medium | Do not change default |
| 4 | P1 | Patient merge has no second authorizer (OCD-010) | Low | **Closed 2026-09-14** — keep `patient.merge` + reason only |
| 5 | P6 / P8 | HL7 MLLP/file-drop inbound is transport-trust; HTTP inbound is session + `hl7.manage` | Low (accepted residual) | **Closed 2026-09-14** — keep transport-trust; do not invent a credential (OCD-034) |
| 6 | P6 | Blazor circuit permissions stale after role change | Low (sessions revoked; `/api/me` refresh) | **Implemented Cycle 2** |
| 7 | P3 | Concurrent antibody-history edit vs electronic XM | Low (re-read before save; RISK-BB-256) | **Implemented Cycle 3** |
| 8 | P4 | Audit is append-only but has no hash chain | Low (hashed tip; RISK-BB-257) | **Implemented Cycle 4** |
| 9 | P5 | Configuration effective-dating incomplete (`FRS-BB-070`) | Low | **Closed 2026-09-14** — keep single live row (OCD-033); do not invent as-of dates |
| 10 | P5 | Record retention / purge not implemented (OCD-007) | Low | **Closed 2026-09-14** — do not purge; retention years stay metadata |
| 11 | P7 | RhIG workflow missing | Medium | SME for indications |
| 12 | P2 | Neonatal irradiation / CMV product-selection defaults | Low | **Closed 2026-09-14** — keep order-rule hooks; do not invent product defaults (OCD-035) |
| 13 | P7 / P9 | Quality metrics / reporting | Low | Later |
| 14 | P8 | FHIR | Low | HL7 v2 exists |
| 15 | P1 | Bedside dual-ID / administration device path incomplete | Low (PPID tokens + ISBT scan; RISK-BB-258). Residual: no dedicated administration-device protocol or required vitals (do not invent) | **Implemented Cycle 6** (identity tokens; device path still thin) |
| 16 | P5 | Downtime reconciliation tooling thin | Low (snapshot; no paper OCR / failover) | **Implemented Cycle 11** |
| 17 | P2 | Electronic XM policy defaults (OCD-001, OCD-006) | Low (policy off) | SME verification |
| 18 | P6 | Broader CSRF / XSS / secrets review | Low (headers + no wildcard CORS; Blazor CSP still allows inline/eval) | **Implemented Cycle 9** |
| 19 | P5 | Validation packaging: no `TEST-BB-*` IDs; stale “Phase 0” headers; missing iteration log | Low (core safety IDs through TEST-BB-030) | **Implemented Cycle 10**; Cycles 12–13 assigned leftover high-safety class-name citations |
| 20 | P6 | DevMode auto-admin | Low | Keep Development-only |

Later (not ranked into Cycle 1): no Blazor E2E suite; open antibody-identification
workup still allows allocate / serologic XM / issue as Warning (OCD-023).

Cycle 15 (workflow-audit loop): pending test worklist now surfaces ABO/Rh,
antibody history, and specimen expiration; `/compatibility` uses MRN/name and
accession pickers (RISK-BB-267). This is not a new AABB default.

Cycle 16: expected inbound arrival can be confirmed on the packing-list
worklist (RISK-BB-268). Receive gates are unchanged.

Cycle 17: in-transit and retrospective boards show unit number, ABO/Rh,
antibody history, and specimen expiration; Receive prefills ward receipt;
generic issue identifies the recipient by MRN/name (RISK-BB-269). Issue
gates are unchanged.

Cycle 18: reaction investigations list MRN, name, unit number, type,
antibodies, workup completeness, and remainder hold; documenting a
reaction or the patient product-history badge opens the case
(RISK-BB-270). Close and quarantine gates are unchanged.

Cycle 19: the HL7 error queue shows control id and message type and
replays from that row; an accepted replay resolves the original mapping
error (RISK-BB-271). MSH-10 idempotency is unchanged.

Cycle 20: seeded `MRN0009` is an ADT/ORM/ORU load onto an accepted
specimen; the test worklist verifies the posted interface value without
re-keying (RISK-BB-272). OBX-11 and specimen gates are unchanged.

Cycle 21: seeded Patricia Demo verify/issue/transfusion charges appear
on the review queue with MRN, unit/test, and queued DFT control id
(RISK-BB-273). Capture stays automatic; review-before-export is unchanged.

Cycle 22: the pending test worklist shows electronic-XM eligibility and
an open antibody-identification workup (RISK-BB-274). AABB 5.16 criteria
and OCD-006 remain the existing eligibility service.

Cycle 23: the quality-quarantine board shows product and a labeled-versus-
interpreted retype mismatch; Release is available on the row with the
existing second-verifier gate (RISK-BB-275). Retype auto-quarantine and
release privilege are unchanged.

Cycle 24: outstanding Issued units stay on `/issuing` after ward receipt
with unit number and patient context; Return and Document prefill from
the row (RISK-BB-276). Return and identity-token gates are unchanged.

Cycle 25: a completed transfusion with a suspected reaction now quarantines
the remainder; the reaction board shows labeled unit ABO/Rh and the first
workup hold (RISK-BB-277). Clerical/visual/DAT close gates are unchanged.

Cycle 26: the HL7 error queue and message log show PID MRN and name
(RISK-BB-278). Replay and MSH-10 idempotency are unchanged.

Cycle 27: seeded Helen Interface RAS documents transfusion of
`W000123BPAM001`; product history shows `HL7-BPAM` without a second
Issuing Document (RISK-BB-279). Bedside-scan and issue gates are unchanged.

Cycle 28: the billing queue keeps reviewed charges until export; inbound
RAS captures the completed-transfusion charge; Helen seed includes DFT
stubs (RISK-BB-280). Dedupe keys and export-after-review are unchanged.

Cycle 29: the pending test worklist and patient Tests tab show the posted
result and use Verify for interface/instrument PendingVerification; ABO
self-verify is hidden for the enterer (RISK-BB-281). Specimen and OCD-018
gates are unchanged.

Cycle 30: discrepancy and operational-hold boards show product and ABO/Rh;
Locate, Inspect, and Release from hold run from the row (RISK-BB-282).
Locate-to-quarantine and `inventory.release` gates are unchanged.

Cycle 31: the retrospective crossmatch board shows specimen accession and
opens Record crossmatch with patient, issued unit, and specimen prefilled
(RISK-BB-283). Compatible XM still closes the queue; emergency defaults
are unchanged.

Cycle 32: the reaction board badges labeled ABO/Rh incompatibility using
the existing issue-path rule and shows issue type / crossmatch status
(RISK-BB-284). Close and workup completeness gates are unchanged.

Cycle 33: the HL7 error queue shows placer/test/unit and direction;
inbound Replay and outbound Send run from the row; outbound AA resolves
the queue item (RISK-BB-285). ReplayAllowed stays unenforced.

Cycle 34: the patient Orders tab shows HL7 source and the posted
interface value, and Verifies without re-keying (RISK-BB-286). Specimen
and OCD-018/019 gates are unchanged.

Cycle 35: verify, issue, and transfusion capture one charge when a
catalog row matches, even if a ChargeRule also matches; demo seed
deactivates overlapping rules (RISK-BB-287). Review-before-export and
dedupe keys are unchanged.

Cycle 36: pending ABO/Rh worklist rows show a historical discrepancy
badge from `AboRhDeltaRule` before verify (RISK-BB-288). Override and
self-verify gates are unchanged.

Cycle 37: `/inventory` shows pending product ABO/Rh retype with inline
Enter (RISK-BB-289). Match/mismatch release and quarantine gates are
unchanged.

Cycle 38: `/issuing` lists reserved units as Ready to issue and prefills
the issue form from the row or `/compatibility` (RISK-BB-290). IssueGate
and emergency defaults are unchanged.

Cycle 39: the reaction board badges a recorded repeat ABO/Rh that
disagrees with the current patient type or labeled unit type
(RISK-BB-291). Close and workup completeness gates are unchanged.

Cycle 40: the HL7 message log shows placer, test, and unit and links
the patient (RISK-BB-292). ReplayAllowed stays unenforced.

## Scoring used for Cycle 1 selection

| Candidate | Safety | Validation | Integrity | Workflow | Architecture | Impl. risk | Notes |
|---|---|---|---|---|---|---|---|
| Session auth vs `X-User` | 5 | 4 | 5 | 3 | 5 | 3 | Impersonation can bypass every privilege HardStop at the HTTP boundary |
| Licensed ISBT tables | 5 | 5 | 5 | 4 | 3 | 5 | Blocked on ICCBBA license |
| Phenotype versioning | 3 | 4 | 5 | 2 | 3 | 3 | OCD-022 |
| HL7 inbound auth | 4 | 3 | 4 | 3 | 4 | 4 | Next after sessions |
