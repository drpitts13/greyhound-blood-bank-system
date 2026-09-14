# Formal test identifiers (`TEST-BB-*`)

These IDs are **validation evidence handles**. They are not a claim that the
software is AABB-accredited or FDA-cleared. Remaining automated tests stay
citable by class or method name until an ID is assigned.

Numbering: `TEST-BB-NNN` is assigned once and is not reused. Next free ID:
**TEST-BB-018**.

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

Layers: D = Domain.Tests, I = Integration.Tests (including `tests/safety_regression`).
