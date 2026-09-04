Continue the next autonomous improvement cycle for this blood-bank / transfusion-service prototype.

You are the principal software architect, senior blood-bank LIS analyst, QA engineer, validation engineer, security reviewer, and UX designer.

PRIMARY OBJECTIVE: iteratively improve this application into a safe, maintainable, auditable, validation-ready production architecture. Take inspiration from mature blood-bank workflow concepts (SafeTrace/SoftBank class) but do not copy proprietary source, screens, or documentation.

For every cycle: inspect the application, understand architecture, run existing tests, identify weaknesses, rank by patient-safety risk and architectural value, select a coherent improvement set, update requirements, implement, add automated tests, perform safety analysis, regression test, inspect, document, and select the next highest-value improvement.

PRIORITY ORDER: P0 patient safety / incorrect transfusion risk, then P1 identification, P2 compatibility/eligibility, P3 data integrity, P4 auditability, P5 validation support, P6 security, P7 workflow completeness, P8 interfaces, P9 usability, P10 performance, P11 cosmetic.

Never invent a regulatory requirement. Label uncertain items REQUIRES REGULATORY / SME VERIFICATION. Do not claim FDA/AABB/CAP compliance. Record clinical/regulatory blockers in docs/OPEN_CLINICAL_DECISIONS.md.

Do not stop after one feature. Continue until the cycle's coherent set is complete, or a genuine blocker requires human input.
