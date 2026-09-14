# Formal test identifiers (`TEST-BB-*`)

These IDs are **validation evidence handles**. They are not a claim that the
software is AABB-accredited or FDA-cleared. Remaining automated tests stay
citable by class or method name until an ID is assigned.

Numbering: `TEST-BB-NNN` is assigned once and is not reused. Next free ID:
**TEST-BB-026**.

| ID | Layer | Test | Traces to |
|---|---|---|---|
| TEST-BB-001 | I | `IdentitySpoofingRegressionTests` | URS-BB-147 / FRS-BB-181 / RISK-BB-254 |
| TEST-BB-002 | I | `AuthSessionServiceTests` | URS-BB-147 / FRS-BB-181 / RISK-BB-254 |
| TEST-BB-003 | D | `AuthSessionValidityRuleTests` | URS-BB-147 / FRS-BB-181 / RISK-BB-254 |
| TEST-BB-004 | I | `AuthSessionServiceTests.AssignRoles_RevokesOutstandingSessions` | URS-BB-148 / FRS-BB-182 / RISK-BB-255 |
| TEST-BB-005 | I | `AuthSessionServiceTests.UpdateRolePermissions_RevokesSessionsForRoleMembers` | URS-BB-148 / FRS-BB-182 / RISK-BB-255 |
| TEST-BB-006 | I | `ElectronicXmHistoryRegressionTests.AntibodyAddedAfterEligibleAssess_BlocksElectronicXmRecord` | URS-BB-149 / FRS-BB-183 / RISK-BB-256 |
| TEST-BB-007 | D | `AuditHashChainRuleTests` | URS-BB-150 / FRS-BB-184 / RISK-BB-257 |
| TEST-BB-008 | I | `AuditHashChainTests` | URS-BB-150 / FRS-BB-184 / RISK-BB-257 |
| TEST-BB-009 | I | `Phase4IssuingTests.Transfusion_MissingPatientIdentifiers_IsHardStopped` | URS-BB-151 / FRS-BB-185 / RISK-BB-258 |
| TEST-BB-010 | I | `Phase4IssuingTests.Transfusion_MismatchedPatientIdentifiers_IsHardStopped` | URS-BB-151 / FRS-BB-185 / RISK-BB-258 |
| TEST-BB-011 | I | `Phase4IssuingTests.Transfusion_IsbtUnit_RequiresIdentityAndMatchingScan` | URS-BB-151 / FRS-BB-185 / RISK-BB-258 |
| TEST-BB-012 | I | `Phase4IssuingTests.Transfusion_RequireSecondVerifier_WithoutElectronicId_NeedsDirectoryUser` | URS-BB-151 / FRS-BB-185 / RISK-BB-258 |
| TEST-BB-013 | D | `IsbtLicensedCatalogImportRuleTests` | URS-BB-152 / FRS-BB-186 / RISK-BB-259 |
| TEST-BB-014 | I | `IsbtLicensedCatalogImportTests` | URS-BB-152 / FRS-BB-186 / RISK-BB-259 |
| TEST-BB-015 | I | `Hl7EndpointAuthorizationTests` | URS-BB-153 / FRS-BB-187 / RISK-BB-260 |
| TEST-BB-016 | D | `HttpSecurityHeaderPolicyTests` | URS-BB-154 / FRS-BB-188 / RISK-BB-261 |
| TEST-BB-017 | I | `TestCatalogPackagingTests.FormalIds_AreUnique` | URS-BB-155 / FRS-BB-189 / RISK-BB-262 |
| TEST-BB-018 | D | `DowntimeReconciliationAuthorizationRuleTests` | URS-BB-156 / FRS-BB-190 / RISK-BB-263 |
| TEST-BB-019 | I | `DowntimeReconciliationTests` | URS-BB-156 / FRS-BB-190 / RISK-BB-263 |
| TEST-BB-020 | I | `IssueGateSafetyRegressionTests` | URS-BB-008 / FRS-BB-032 / RISK-BB-007 |
| TEST-BB-021 | I | `MergedPatientClinicalUseTests` | URS-BB-015 / FRS-BB-003 / RISK-BB-015 |
| TEST-BB-022 | I | `ElectronicCrossmatchEligibilityTests.Assess_OpenAntibodyIdWorkup_BlocksEligibility` | URS-BB-009 / FRS-BB-033 / RISK-BB-168 |
| TEST-BB-023 | D | `LookbackAuthorizationRuleTests` | URS-BB-113 / FRS-BB-147 / RISK-BB-120 |
| TEST-BB-024 | I | `LookbackSearchAuthorizationTests` | URS-BB-113 / FRS-BB-147 / RISK-BB-120 |
| TEST-BB-025 | I | `TestCatalogPackagingTests.CitedClasses_HaveSourceFiles` | URS-BB-157 / FRS-BB-191 / RISK-BB-264 |

Layers: D = Domain.Tests, I = Integration.Tests (including `tests/safety_regression`).
