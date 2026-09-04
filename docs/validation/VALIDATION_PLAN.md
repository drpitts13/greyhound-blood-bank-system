# Validation plan

Goal: **compliance-supporting, validation-ready software**. Not automatic
certification.

Traceability path:

USER NEED (`URS-BB-*`) → REQUIREMENT (`FRS-BB-*`) → RISK (`RISK-BB-*`) →
DESIGN (`SRS-BB-*`) → CODE → TEST (`TEST-BB-*` / named test class) → EVIDENCE.

Definition of done for a safety rule:

1. Pure Domain rule with stable code, severity, and explainable message.
2. Positive and negative automated tests.
3. Application wiring + audit.
4. Database constraint when the hazard is a race or uniqueness failure.
5. Traceability and risk rows updated.
6. Build and tests green.

The Phase 0 narrative remains in `docs/validation-plan.md`.
