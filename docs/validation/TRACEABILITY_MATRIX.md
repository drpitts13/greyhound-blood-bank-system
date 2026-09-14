# Traceability matrix

The living requirement-to-test matrix is
[`docs/traceability-matrix.md`](../traceability-matrix.md).

Update that file whenever a URS / FRS / SRS / RISK / test class is added.

Traceability path (from [`VALIDATION_PLAN.md`](VALIDATION_PLAN.md)):

```
URS-BB-*  →  FRS-BB-*  →  RISK-BB-*  →  SRS-BB-*  →  CODE  →  TEST (class or TEST-BB-*)  →  EVIDENCE
```

Latest Cycle 6 row: URS-BB-151 / FRS-BB-185 / SRS-BB-146 / RISK-BB-258 →
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

Formal `TEST-BB-*` identifiers are not yet assigned. Until they are, the
matrix cites named test classes. That is a documentation gap (gap 19), not a
missing control.

Citations support validation evidence. They are **not** a claim that this
software is AABB-accredited or FDA-cleared.
