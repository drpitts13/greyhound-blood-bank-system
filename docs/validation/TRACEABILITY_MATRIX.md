# Traceability matrix

The living requirement-to-test matrix is
[`docs/traceability-matrix.md`](../traceability-matrix.md).

Update that file whenever a URS / FRS / SRS / RISK / test class is added.

Traceability path (from [`VALIDATION_PLAN.md`](VALIDATION_PLAN.md)):

```
URS-BB-*  →  FRS-BB-*  →  RISK-BB-*  →  SRS-BB-*  →  CODE  →  TEST (class or TEST-BB-*)  →  EVIDENCE
```

Latest Cycle 15 row: seed-backed FDA/AABB scenario suite → TEST-BB-036–046
(`FdaAabbScenarioTests`). Maps URS-BB-003 / 005 / 006 / 008 / 009 / 011 /
020 / 024 / 036 / 113 onto seeded patients MRN0001 and MRN0004–0008.
Citations are validation evidence, not certification.

Cycle 14: URS-BB-159 / FRS-BB-193 / SRS-BB-154 / RISK-BB-266 →
TEST-BB-031–033 (ICCBBA extract parser, drop-folder/upload import, seeder
does not revert licensed rows). Extract not loaded (OCD-004). Remaining
tests may stay class-name cited.

Cycle 13: URS-BB-158 / FRS-BB-192 / SRS-BB-153 / RISK-BB-265 →
TEST-BB-026–030 (uniqueness, merge, retype, emergency-issue, immuno
privilege). Remaining tests may stay class-name cited.

Cycle 12: URS-BB-157 / FRS-BB-191 / SRS-BB-152 / RISK-BB-264 →
TEST-BB-020–025 (issue-gate, merged patient, open-workup eXM, lookback,
catalog source-file check). Remaining tests may stay class-name cited.

Cycle 11: URS-BB-156 / FRS-BB-190 / SRS-BB-151 / RISK-BB-263 →
TEST-BB-018 (`DowntimeReconciliationAuthorizationRuleTests`),
TEST-BB-019 (`DowntimeReconciliationTests`). OCD-035 closed (keep order-rule
hooks). Gap 16 snapshot is read-only.

Cycle 10: URS-BB-155 / FRS-BB-189 / SRS-BB-150 / RISK-BB-262 →
TEST-BB-017 (`TestCatalogPackagingTests`). Formal IDs: `docs/validation/TEST_CATALOG.md`.
OCD-007 closed (no purge).

Cycle 9: URS-BB-154 / FRS-BB-188 / SRS-BB-149 / RISK-BB-261 →
TEST-BB-016 (`HttpSecurityHeaderPolicyTests`). OCD-033 closed (single live catalog row).

Cycle 8: URS-BB-153 / FRS-BB-187 / SRS-BB-148 / RISK-BB-260 →
`Hl7EndpointAuthorizationTests`; `docs/hl7-design.md` transport authentication.

Cycle 7: URS-BB-152 / FRS-BB-186 / SRS-BB-147 / RISK-BB-259 →
`IsbtLicensedCatalogImportRuleTests`, `IsbtLicensedCatalogImportTests`.

Cycle 6: URS-BB-151 / FRS-BB-185 / SRS-BB-146 / RISK-BB-258 →
`Phase4IssuingTests.Transfusion_MissingPatientIdentifiers_IsHardStopped`,
`Phase4IssuingTests.Transfusion_MismatchedPatientIdentifiers_IsHardStopped`,
`Phase4IssuingTests.Transfusion_IsbtUnit_RequiresIdentityAndMatchingScan`,
`Phase4IssuingTests.Transfusion_RequireSecondVerifier_WithoutElectronicId_NeedsDirectoryUser`.

Cycle 4: URS-BB-150 / FRS-BB-184 / SRS-BB-145 / RISK-BB-257 →
`AuditHashChainRuleTests`, `AuditHashChainTests`.

Cycle 3: URS-BB-149 / FRS-BB-183 / SRS-BB-144 / RISK-BB-256 →
`ElectronicXmHistoryRegressionTests.AntibodyAddedAfterEligibleAssess_BlocksElectronicXmRecord`.

Cycle 2: URS-BB-148 / FRS-BB-182 / SRS-BB-143 / RISK-BB-255 →
`AuthSessionServiceTests.AssignRoles_RevokesOutstandingSessions`,
`AuthSessionServiceTests.UpdateRolePermissions_RevokesSessionsForRoleMembers`.

Cycle 1: URS-BB-147 / FRS-BB-181 / SRS-BB-142 / RISK-BB-254 →
`IdentitySpoofingRegressionTests`, `AuthSessionServiceTests`,
`AuthSessionValidityRuleTests`.

Formal `TEST-BB-*` identifiers for Cycles 1–15 are assigned in
[`TEST_CATALOG.md`](TEST_CATALOG.md). Other tests remain citable by class
name. That remaining gap is packaging, not a missing clinical control.

Citations support validation evidence. They are **not** a claim that this
software is AABB-accredited or FDA-cleared.
