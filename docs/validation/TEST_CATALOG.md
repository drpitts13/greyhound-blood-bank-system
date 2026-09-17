# Formal test identifiers (`TEST-BB-*`)

These IDs are **validation evidence handles**. They are not a claim that the
software is AABB-accredited or FDA-cleared. Remaining automated tests stay
citable by class or method name until an ID is assigned.

Numbering: `TEST-BB-NNN` is assigned once and is not reused. Next free ID:
**TEST-BB-049**.

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
| TEST-BB-026 | I | `AllocationIssueConcurrencyTests` | URS-BB-007 / FRS-BB-041 / RISK-BB-006 |
| TEST-BB-027 | I | `PatientMergeServiceTests` | URS-BB-017 / FRS-BB-003 / RISK-BB-017 |
| TEST-BB-028 | I | `ProductRetypeServiceTests` | URS-BB-018 / FRS-BB-051 / RISK-BB-018 |
| TEST-BB-029 | D | `IssueAuthorizationRuleTests` | URS-BB-006 / FRS-BB-040 / RISK-BB-019 |
| TEST-BB-030 | D | `ImmunoAuthorizationRuleTests` | URS-BB-002 / FRS-BB-002 / RISK-BB-021 |
| TEST-BB-031 | A | `IccbbaExtractParserTests` | URS-BB-159 / FRS-BB-193 / RISK-BB-266 |
| TEST-BB-032 | I | `IccbbaExtractImportTests` | URS-BB-159 / FRS-BB-193 / RISK-BB-266 |
| TEST-BB-033 | I | `SeederTests.Seed_DoesNotRevertLicensedProductCode` | URS-BB-159 / FRS-BB-193 / RISK-BB-266 |
| TEST-BB-034 | D | `CrossmatchSettingsValidatorTests` | URS-BB-160 / FRS-BB-194 |
| TEST-BB-035 | I | `CrossmatchSettingsTests` | URS-BB-160 / FRS-BB-194 |
| TEST-BB-036 | I | `FdaAabbScenarioTests.PregnantPatientSpecimen_ExpiresAt72HourAlloimmunizationWindow` | URS-BB-003 / FRS-BB-010 |
| TEST-BB-037 | I | `FdaAabbScenarioTests.AutologousUnit_IssuesOnlyToReservedPatient` | URS-BB-008 / FRS-BB-032 |
| TEST-BB-038 | I | `FdaAabbScenarioTests.DirectedUnit_ConvertWithoutSecondVerifier_IsHardStopped` | URS-BB-024 / FRS-BB-053 |
| TEST-BB-039 | I | `FdaAabbScenarioTests.LookbackByDin_ListsSeededRecipientAndPendingNotification` | URS-BB-113 / FRS-BB-147 / FRS-BB-165 |
| TEST-BB-040 | I | `FdaAabbScenarioTests.EmergencyRelease_CarriesIncompleteTestingStatement` | URS-BB-006 / FRS-BB-040 |
| TEST-BB-041 | I | `FdaAabbScenarioTests.AntiKPatient_UntypedOrKPositiveUnit_RequiresAntigenNegOverride` | URS-BB-005 / FRS-BB-030 |
| TEST-BB-042 | I | `FdaAabbScenarioTests.QuarantineMissingAndExpiredUnits_AreNotIssuable` | URS-BB-006 / FRS-BB-031 |
| TEST-BB-043 | I | `FdaAabbScenarioTests.AboSelfVerify_IsBlockedBySeededFacilityPolicy` | URS-BB-020 / FRS-BB-022 |
| TEST-BB-044 | I | `FdaAabbScenarioTests.ReactionInvestigation_HasClericalCheckAndDat` | URS-BB-036 / FRS-BB-066 |
| TEST-BB-045 | I | `FdaAabbScenarioTests.SeededClinicalActions_ProduceAuditEvents` | URS-BB-011 / FRS-BB-060 |
| TEST-BB-046 | I | `FdaAabbScenarioTests.PatriciaDemo_IsElectronicXmEligible_WhenFacilityAllows` | URS-BB-009 / FRS-BB-033 |
| TEST-BB-047 | I | `TestWorklistTests.PendingWorklist_SurfacesBloodTypeAntibodyHistoryAndSpecimenExpiry` | URS-BB-161 / FRS-BB-195 / RISK-BB-267 |
| TEST-BB-048 | I | `InventoryServiceTests.ListExpected_FlagsOverdueWhenPastDue` | URS-BB-162 / FRS-BB-196 / RISK-BB-268 |
| TEST-BB-049 | I | `Phase4IssuingTests.Issue_SetsCoolerAndAppearsOnInTransitWorklist` | URS-BB-163 / FRS-BB-197 / RISK-BB-269 |
| TEST-BB-050 | I | `ReactionInvestigationServiceTests.ListDtos_SurfacesPatientUnitTypeAndWorkupIncomplete` | URS-BB-164 / FRS-BB-198 / RISK-BB-270 |
| TEST-BB-051 | I | `Phase5Hl7Tests.Replay_AfterPatientExists_ResolvesMappingError` | URS-BB-165 / FRS-BB-199 / RISK-BB-271 |
| TEST-BB-052 | I | `TestWorklistTests.PendingWorklist_InterfaceResult_CanVerifyWithoutReentry` | URS-BB-166 / FRS-BB-200 / RISK-BB-272 |
| TEST-BB-053 | I | `Phase7BillingTests.ReviewQueueDtos_SurfaceMrnUnitAndQueuedDft` | URS-BB-167 / FRS-BB-201 / RISK-BB-273 |
| TEST-BB-054 | I | `TestWorklistTests.PendingWorklist_SurfacesElectronicXmHoldAndOpenAntibodyId` | URS-BB-168 / FRS-BB-202 / RISK-BB-274 |
| TEST-BB-055 | I | `InventoryServiceTests.ListQuarantine_SurfacesRetypeMismatch` | URS-BB-169 / FRS-BB-203 / RISK-BB-275 |
| TEST-BB-056 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-170 / FRS-BB-204 / RISK-BB-276 |
| TEST-BB-057 | I | `Phase4IssuingTests.Transfusion_ReactionCompleted_QuarantinesRemainder` | URS-BB-171 / FRS-BB-205 / RISK-BB-277 |
| TEST-BB-058 | I | `Phase5Hl7Tests.InboundOrm_UnknownPatient_ProducesApplicationErrorAndQueuesIt` | URS-BB-172 / FRS-BB-206 / RISK-BB-278 |
| TEST-BB-059 | I | `Phase5Hl7Tests.InboundRas_DocumentsTransfusionOnIssuedUnitWithoutRekey` | URS-BB-173 / FRS-BB-207 / RISK-BB-279 |
| TEST-BB-060 | I | `Phase7BillingTests.ReviewQueueDtos_KeepReviewedUntilExported` | URS-BB-174 / FRS-BB-208 / RISK-BB-280 |
| TEST-BB-061 | I | `TestWorklistTests.PendingWorklist_PostedInterface_SurfacesResultAndVerifyAction` | URS-BB-175 / FRS-BB-209 / RISK-BB-281 |
| TEST-BB-062 | I | `InventoryServiceTests.ListOnHold_SurfacesProductAndHoldReason` | URS-BB-176 / FRS-BB-210 / RISK-BB-282 |
| TEST-BB-063 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-177 / FRS-BB-211 / RISK-BB-283 |

Layers: D = Domain.Tests, A = Application.Tests, I = Integration.Tests (including `tests/safety_regression`).
