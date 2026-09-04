# Change control

1. Identify the hazard or user need.
2. Update requirements (`URS-BB-*` / `FRS-BB-*` / `SRS-BB-*`) when behavior changes.
3. Update `docs/risk/RISK_REGISTER.md` when a new hazard is introduced or a control changes.
4. Implement in Domain first for safety rules; Application orchestrates; Infrastructure constrains.
5. Add positive and negative automated tests. Safety defects also add a test under `tests/safety_regression/`.
6. Record open clinical questions in `docs/OPEN_CLINICAL_DECISIONS.md` instead of guessing.
7. Do not represent a change as FDA/AABB/CAP certified.
8. Commit after the vertical slice is green (see workspace GitHub-sync rule). Ask before pushing.
