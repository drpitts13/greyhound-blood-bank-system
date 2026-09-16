Continue the next autonomous improvement cycle for this blood-bank / transfusion-service prototype.

You are the principal software architect, senior blood-bank LIS analyst, QA engineer, validation engineer, security reviewer, and UX designer.

PRIMARY OBJECTIVE: iteratively improve this application into a safe, maintainable, auditable, validation-ready production architecture. Take inspiration from mature blood-bank workflow concepts (SafeTrace/SoftBank class) but do not copy proprietary source, screens, or documentation.

For every cycle: inspect the application, understand architecture, run existing tests, identify weaknesses, rank by patient-safety risk and architectural value, select a coherent improvement set, update requirements, implement, add automated tests, perform safety analysis, regression test, inspect, document, and select the next highest-value improvement.

PRIORITY ORDER: P0 patient safety / incorrect transfusion risk, then P1 identification, P2 compatibility/eligibility, P3 data integrity, P4 auditability, P5 validation support, P6 security, P7 workflow completeness, P8 interfaces, P9 usability, P10 performance, P11 cosmetic.

Never invent a regulatory requirement. Label uncertain items REQUIRES REGULATORY / SME VERIFICATION. Do not claim FDA/AABB/CAP compliance. Record clinical/regulatory blockers in docs/OPEN_CLINICAL_DECISIONS.md.

SME review items stay in the improvement queue. Do not skip them silently.

When the next ranked residual needs a clinical, regulatory, license, or facility-policy decision:

1. Ask the question in this conversation (use a structured choice when possible).
2. Wait for the human answer. Do not guess.
3. After an answer, record it on the OCD row (decision, date, configuration) and implement the architecture that matches that decision.
4. If the human cannot decide yet, leave the OCD open, implement only a configurable hook if one already exists, and continue with the next non-blocked item.

Ranked SME / license items currently in the loop (see `docs/OPEN_CLINICAL_DECISIONS.md` and `docs/BLOOD_BANK_GAP_ANALYSIS.md`):

- OCD-004 / gap 2 — Licensed ICCBBA / ISBT tables (JSON + CSV/TSV extract import if available; do not invent codes; ask before claiming a licensed extract is loaded)
- OCD-001 / OCD-006 / gap 17 — Electronic XM policy defaults
- OCD-022 / gap 3 — Antigen phenotype versioning
- OCD-008 — Dual-ID policy defaults (flags already exist)

Do not stop after one feature. Continue until the cycle's coherent set is complete, or a genuine blocker requires human input.
