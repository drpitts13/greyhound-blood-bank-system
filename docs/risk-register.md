# Blood Bank LIS — Risk Register

Status: Phase 0 (design). This register identifies dangerous workflows and the controls that mitigate them. Severity reflects potential patient harm; likelihood assumes the listed controls are NOT yet in place. Each control maps to a rule/design reference and a planned test.

Scoring: Severity (Sev) and Likelihood (Lik) on Low / Medium / High / Critical.

| Risk ID | Hazard | Sev | Lik | Mitigating control | Reference | Verified by |
|---|---|---|---|---|---|---|
| RK-01 | ABO-incompatible unit issued | Critical | Medium | Mandatory ABO/Rh matrix HardStop in issue gate; cannot be overridden | safety-rules 1, 3 | D: matrix tests; A: issue gate |
| RK-02 | Unit issued against expired specimen | Critical | Medium | `ISS-SPEC-EXPIRED` HardStop; specimen expiry computed + clock-based | safety-rules 1, 2 | D: expiry boundary; A: issue gate |
| RK-03 | Expired blood unit issued | High | Medium | `ISS-UNIT-EXPIRED` HardStop; expired blocks allocate/issue | safety-rules 1, 4 | D: transitions |
| RK-04 | Unit issued without crossmatch when required | Critical | Medium | `ISS-XM-REQUIRED` HardStop unless audited emergency release | safety-rules 1; workflows 6 | A: issue gate; emergency path |
| RK-05 | Special requirement missed (e.g. non-irradiated to at-risk patient) | High | Medium | `ISS-SPECIAL-REQ` HardStop from active requirements | safety-rules 1 | D,A: special-req tests |
| RK-06 | Antigen-positive unit to patient with antibody | Critical | Medium | `ISS-ANTIGEN-NEG` + `ISS-ANTIBODY-HX` HardStop | safety-rules 1 | D: antigen-neg tests |
| RK-07 | Wrong patient (identity mismatch) | Critical | Medium | `ISS-IDENTITY` HardStop; specimen-patient binding | safety-rules 1 | A: identity tests |
| RK-08 | Verified result silently altered | High | Low | Verified results immutable; correction creates new version + e-sign | safety-rules 6, 7 | A,I: versioning tests |
| RK-09 | ABO/Rh history overwritten silently | Critical | Low | Append-only history; manual edit needs reason + e-sign | safety-rules 5, 7 | A: history tests |
| RK-10 | Emergency release abused / unaudited | High | Low | Override + authorizer + e-signature + retrospective-crossmatch flag | safety-rules 5; workflows 6 | A: override required |
| RK-11 | Discarded/returned unit re-issued unsafely | High | Low | Reissue eligibility re-check on return; discarded excluded from selection | workflows 7, 8; safety-rules 4 | A: return/discard tests |
| RK-12 | Duplicate billing charges | Medium | Medium | Deterministic `DedupeKey` unique constraint | printing-billing B.3 | A,I: dedupe tests |
| RK-13 | Compatibility tag reprinted without trace | Medium | Medium | Reprint requires reason + `Reprint` audit; full PrintJobs history | printing-billing A.3; safety-rules 5 | A: reprint tests |
| RK-14 | HL7 demographic update corrupts clinical history | High | Low | ADT maps demographics only; never touches immunohematology history | hl7-design 2.1 | H,A: ADT scope test |
| RK-15 | HL7 replay duplicates orders/patients | Medium | Medium | Idempotency by control id + business key | hl7-design 4 | A,I: replay idempotency |
| RK-16 | Lost audit trail | High | Low | Audit written in same transaction as change; failure rolls back | architecture 4.1 | A: audit-on-action |
| RK-17 | Unauthorized action via API or HL7 path | High | Medium | Permission checks enforced in Application layer (both entry points) | architecture 4.2 | A: permission tests |
| RK-18 | HardStop downgraded to overridable warning | Critical | Low | Engine never downgrades; HardStop excluded from override path | safety-rules severity model | D: aggregation tests |

## Review cadence
- Re-review this register at the start of each implementation phase and whenever a new dangerous workflow is introduced.
- Any risk scored Critical must have a passing Domain-layer test before the corresponding feature is enabled.
