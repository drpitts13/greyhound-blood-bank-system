# Formal test identifiers (`TEST-BB-*`)

These IDs are **validation evidence handles**. They are not a claim that the
software is AABB-accredited or FDA-cleared. Remaining automated tests stay
citable by class or method name until an ID is assigned.

Numbering: `TEST-BB-NNN` is assigned once and is not reused. Next free ID:
**TEST-BB-091**.

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
| TEST-BB-064 | I | `ReactionInvestigationServiceTests.ListDtos_SurfacesAboIncompatibilityAndEmergencyIssueContext` | URS-BB-178 / FRS-BB-212 / RISK-BB-284 |
| TEST-BB-065 | I | `Phase5Hl7Tests.OutboundSender_Aa_ResolvesErrorQueue` | URS-BB-179 / FRS-BB-213 / RISK-BB-285 |
| TEST-BB-066 | I | `SeederTests.Seed_HelenHl7Order_SurfacesPostedInterfaceValueAndVerifyAction` | URS-BB-180 / FRS-BB-214 / RISK-BB-286 |
| TEST-BB-067 | I | `SeederTests.Seed_ChargeRulesDoNotOverlapBillingCatalogs` | URS-BB-181 / FRS-BB-215 / RISK-BB-287 |
| TEST-BB-068 | I | `TestWorklistTests.PendingWorklist_SurfacesAboRhDeltaHold_OnEnteredAborh` | URS-BB-182 / FRS-BB-216 / RISK-BB-288 |
| TEST-BB-069 | I | `SeederTests.Seed_PendingRetypeWorklist_IncludesRetDemoUnits` | URS-BB-183 / FRS-BB-217 / RISK-BB-289 |
| TEST-BB-070 | I | `Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate` | URS-BB-184 / FRS-BB-218 / RISK-BB-290 |
| TEST-BB-071 | I | `ReactionInvestigationServiceTests.ListDtos_SurfacesRepeatAboRhDiscrepancy` | URS-BB-185 / FRS-BB-219 / RISK-BB-291 |
| TEST-BB-072 | I | `SeederTests.Seed_HelenHl7Messages_SurfacePlacerTestAndUnit` | URS-BB-186 / FRS-BB-220 / RISK-BB-292 |
| TEST-BB-073 | I | `SeederTests.Seed_HelenHl7Bpam_SurfacesVolumeLocationAndTransfusionist` | URS-BB-187 / FRS-BB-221 / RISK-BB-293 |
| TEST-BB-074 | I | `FdaAabbScenarioTests.PatriciaDemo_ElectronicXm_IsOffUntilFacilityAllows` | URS-BB-189 / FRS-BB-223 / RISK-BB-295 |
| TEST-BB-075 | I | `Phase3ServicesTests.ResultSourcedAntigen_ChangeWithoutOverride_IsRejected` | URS-BB-190 / FRS-BB-224 / RISK-BB-296 |
| TEST-BB-076 | D | `ImmunoAuthorizationRuleTests.ResultSourcedAntigenChange_WithoutOverride_IsHardStop` | URS-BB-190 / FRS-BB-224 / RISK-BB-296 |
| TEST-BB-077 | D | `FacilityPolicyValidatorTests.SecondVerifierFlags_RemainOptional` | URS-BB-191 / FRS-BB-225 / RISK-BB-297 |
| TEST-BB-078 | D | `ActionCatalogValidatorTests` | URS-BB-192 / FRS-BB-226 |
| TEST-BB-079 | D | `ActionDefinitionAuthorizationRuleTests`; `ActionBillingAuthorizationRuleTests` | URS-BB-194 / FRS-BB-228 |
| TEST-BB-080 | I | `OrderActionTests.AddAction_ToProductOrder_ListsPendingAction` | URS-BB-192 / FRS-BB-226 |
| TEST-BB-081 | I | `OrderActionTests.OrderLinkedIrradiate_TransfersAllocation_BillsAction_AndLeavesResultAssigned` | URS-BB-193 / URS-BB-194 / FRS-BB-227 / FRS-BB-228 |
| TEST-BB-082 | I | `OrderActionTests.StandaloneModify_StillQuarantines_AndDoesNotBill` | URS-BB-193 / FRS-BB-062 / FRS-BB-227 |
| TEST-BB-083 | I | `ActionDefinitionAuthorizationTests`; `ActionBillingAuthorizationTests` | URS-BB-194 / FRS-BB-228 |
| TEST-BB-084 | I | `OrderActionTests.Issue_BlockedWhileActionPending_AndAllowedAfterComplete` | URS-BB-193 / FRS-BB-229 |
| TEST-BB-085 | I | `PatientAllocationTests.Allocate_AntiK_Level1Override_IsBlocked`; `PatientAllocationTests.AllocateUnit_AntiK_RequiresPrivilegedOverrideRow`; `PatientAllocationTests.ListCompatibleUnits_FlagsUntypedAndAntigenPositive` | URS-BB-196 / FRS-BB-231 / RISK-BB-298 |
| TEST-BB-086 | I | `Phase7BillingTests.Return_CancelsOpenIssueCharge_AndLeavesExported` | URS-BB-197 / FRS-BB-232 / RISK-BB-299 |
| TEST-BB-087 | I | `ReactionInvestigationServiceTests.Update_DiscardedUnit_SavesWorkupWhenRemainderCannotBeHeld` | URS-BB-198 / FRS-BB-233 / RISK-BB-300 |
| TEST-BB-088 | I | `ComplianceWorkflowTests.RecallByDin_IssuedUnit_OpensOneNotification_AndSecondRecallDoesNotDuplicate` | URS-BB-199 / FRS-BB-234 / RISK-BB-301 |
| TEST-BB-089 | I | `Phase5Hl7Tests.Replay_WhenEndpointDisallows_DoesNotReprocess` | URS-BB-200 / FRS-BB-235 / RISK-BB-302 |
| TEST-BB-090 | I | `Phase5Hl7Tests.InboundOru_LinksNamedAcceptedSpecimen_AndPosts`; `Phase5Hl7Tests.InboundOru_RejectsNamedSpecimenForAnotherPatient` | URS-BB-201 / FRS-BB-236 / RISK-BB-303 |
| TEST-BB-091 | I | `Phase7BillingTests.Invalidate_CancelsOpenTestCharge_AndLeavesExported` | URS-BB-202 / FRS-BB-237 / RISK-BB-304 |
| TEST-BB-092 | D | `AboRhDeltaRuleTests.OpensOverride_OnlyForBlockedOverridableDelta` | URS-BB-203 / FRS-BB-238 / RISK-BB-305 |
| TEST-BB-093 | D | `ReceiveAppearanceRuleTests.SuggestedSupplierReturnReason_UsesDefect_AndKeepsTypedReason` | URS-BB-204 / FRS-BB-239 / RISK-BB-306 |
| TEST-BB-094 | I | `Phase4IssuingTests.ListReadyToIssue_FlagsCellularUnitWhenSecondAboIsMissing` | URS-BB-205 / FRS-BB-240 / RISK-BB-307 |
| TEST-BB-095 | D | `ReactionFatalityFollowUpRuleTests.ClosedFatality_StaysDueUntilCberAndWrittenReport` | URS-BB-206 / FRS-BB-241 / RISK-BB-308 |
| TEST-BB-096 | I | `ComplianceWorkflowTests.ListOpenNotifications_IncludesPending_AndOmitsCompleted` | URS-BB-207 / FRS-BB-242 / RISK-BB-309 |
| TEST-BB-097 | I | `Phase5Hl7Tests.ResolveQueueItem_ClearsTheRow_WithoutReprocessing` | URS-BB-208 / FRS-BB-243 / RISK-BB-310 |
| TEST-BB-098 | I | `Phase5Hl7Tests.InboundOrm_StatPriority_CreatesStatOrder`; `Hl7MapperTests.OrmMapper_MapsPriorityCodes` | URS-BB-209 / FRS-BB-244 / RISK-BB-311 |
| TEST-BB-099 | I | `Phase7BillingTests.ReviewQueueDtos_ShowActionCodeAndUnit` | URS-BB-210 / FRS-BB-245 / RISK-BB-312 |
| TEST-BB-100 | I | `TestWorklistTests.PendingWorklist_ListsRushPriorityAheadOfNewerRoutine` | URS-BB-211 / FRS-BB-246 / RISK-BB-313 |
| TEST-BB-101 | I | `InventoryServiceTests.Discard_NearExpiryUnit_LeavesTheBoard` | URS-BB-212 / FRS-BB-247 / RISK-BB-314 |
| TEST-BB-102 | I | `Phase4IssuingTests.Return_FailedVisual_DisposesAndLeavesOutstanding` | URS-BB-213 / FRS-BB-248 / RISK-BB-315 |
| TEST-BB-103 | I | `ReactionInvestigationServiceTests.Update_IncompleteWorkup_DoesNotClose` | URS-BB-214 / FRS-BB-249 / RISK-BB-316 |
| TEST-BB-104 | I | `LookbackSearchAuthorizationTests.RecordAttempt_CompletedWithoutPhysician_StaysOnTheBoard` | URS-BB-215 / FRS-BB-250 / RISK-BB-317 |
| TEST-BB-105 | I | `Phase5Hl7Tests.OutboundSender_ExhaustedRetries_DoesNotSend` | URS-BB-216 / FRS-BB-251 / RISK-BB-318 |
| TEST-BB-106 | I | `Phase5Hl7Tests.InboundAdt_SparseA08_KeepsNameSexAndDateOfBirth` | URS-BB-217 / FRS-BB-252 / RISK-BB-319 |
| TEST-BB-107 | I | `Phase7BillingTests.Cancel_UnsentDft_IsWithheldFromSend` | URS-BB-218 / FRS-BB-253 / RISK-BB-320 |
| TEST-BB-108 | I | `TestWorklistTests.PendingWorklist_ExpiredSpecimen_DoesNotOfferVerify` | URS-BB-219 / FRS-BB-254 / RISK-BB-321 |
| TEST-BB-109 | I | `ProductRetypeServiceTests.MatchingRetype_OutdatedUnit_DoesNotBecomeAvailable` | URS-BB-220 / FRS-BB-255 / RISK-BB-322 |
| TEST-BB-110 | I | `Phase4IssuingTests.ListReadyToIssue_ExpiredSpecimen_DoesNotOfferIssue` | URS-BB-221 / FRS-BB-256 / RISK-BB-323 |
| TEST-BB-111 | I | `ReactionInvestigationServiceTests.RecordFatalityFollowUp_SecondStamp_DoesNotRewrite` | URS-BB-222 / FRS-BB-257 / RISK-BB-324 |
| TEST-BB-112 | I | `LookbackSearchAuthorizationTests.Recall_ReturnedThenReissued_NotifiesTheCurrentRecipient` | URS-BB-223 / FRS-BB-258 / RISK-BB-325 |
| TEST-BB-113 | I | `Phase5Hl7Tests.Replay_AlreadyApplied_DoesNotRunAgain` | URS-BB-224 / FRS-BB-259 / RISK-BB-326 |
| TEST-BB-114 | I | `Phase5Hl7Tests.InboundOru_CancelledOrder_DoesNotPost`; `InterfaceResultAcceptanceRuleTests.ClosedOrder_HardStop` | URS-BB-225 / FRS-BB-260 / RISK-BB-327 |
| TEST-BB-115 | I | `Phase7BillingTests.Export_UnacceptedDft_StaysOnTheQueue`; `BillingDftExportRuleTests.UnacceptedDft_HardStops` | URS-BB-226 / FRS-BB-261 / RISK-BB-328 |
| TEST-BB-116 | I | `TestWorklistTests.PendingWorklist_HeldOrder_DoesNotOfferEnter` | URS-BB-227 / FRS-BB-262 / RISK-BB-329 |
| TEST-BB-117 | I | `InventoryServiceTests.ReceiveExpected_OutdatedUnit_DoesNotEnterStock` | URS-BB-228 / FRS-BB-263 / RISK-BB-330 |
| TEST-BB-118 | I | `Phase4IssuingTests.ListReadyToIssue_OutdatedUnit_DoesNotOfferIssue` | URS-BB-229 / FRS-BB-264 / RISK-BB-331 |
| TEST-BB-119 | I | `ReactionInvestigationServiceTests.Update_FatalSeverity_StartsFatalityFollowUp` | URS-BB-230 / FRS-BB-265 / RISK-BB-332 |
| TEST-BB-120 | I | `LookbackSearchAuthorizationTests.RecordAttempt_FinishedNotification_DoesNotChange` | URS-BB-231 / FRS-BB-266 / RISK-BB-333 |
| TEST-BB-121 | I | `Phase5Hl7Tests.Replay_ErroredCopy_WhenSiblingAlreadyApplied_DoesNotRunAgain` | URS-BB-232 / FRS-BB-267 / RISK-BB-334 |
| TEST-BB-122 | D,I | `ProductSelectionOrderTests.OwnRestrictedUnit_PrecedesIdentical_WhichPrecedesSubstitute`; `PatientAllocationTests.ListCompatibleUnits_RanksOwnRestrictedAndIdenticalBeforeSubstitute` | URS-BB-233 / FRS-BB-268 / RISK-BB-335 |
| TEST-BB-123 | I | `PatientAllocationTests.ListCompatibleUnits_OmitsOutdatedUnit` | URS-BB-234 / FRS-BB-269 / RISK-BB-336 |
| TEST-BB-124 | I | `PatientAllocationTests.ListCompatibleUnits_MarksUnitInsideNearExpiryWindow` | URS-BB-235 / FRS-BB-270 / RISK-BB-337 |
| TEST-BB-125 | D,I | `SpecialTransfusionRequirementRuleTests.UnitSelection_OmitsMissingProductAttribute_KeepsAcknowledgmentOnly`; `SpecialTransfusionRequirementRuleTests.UnitSelection_OmitsUnitThatIsNotAntigenNegative`; `PatientAllocationTests.ListCompatibleUnits_OmitsUnitThatFailsSpecialRequirement`; `PatientAllocationTests.ListCompatibleUnits_KeepsUnitForAcknowledgmentOnlyRequirement` | URS-BB-236 / FRS-BB-271 / RISK-BB-338 |
| TEST-BB-126 | D | `CurrentSpecimenBannerTests` | URS-BB-237 / FRS-BB-272 / RISK-BB-339 |
| TEST-BB-127 | D,I | `CrossmatchRuleTests.CurrentCompatible_MatchesTheIssuePredicate`; `PatientAllocationTests.ListCompatibleUnits_ListsCurrentCrossmatchAheadOfShorterDatedUnit` | URS-BB-238 / FRS-BB-273 / RISK-BB-340 |
| TEST-BB-128 | D | `SecondAboDeterminationRuleTests.ChartNotice_OnlyWhenRequiredAndCurrentTypeLacksAMatch` | URS-BB-239 / FRS-BB-274 / RISK-BB-341 |
| TEST-BB-129 | D,I | `CrossmatchRuleTests.CountsAsIncompatible_MatchesTheIssuePredicate`; `PatientAllocationTests.ListCompatibleUnits_ListsIncompatibleCrossmatchAfterUnmatchedUnit` | URS-BB-240 / FRS-BB-275 / RISK-BB-342 |
| TEST-BB-130 | D | `ElectronicCrossmatchBannerTests.Notice_ShowsBlockingReasonOnlyWhenNotEligible` | URS-BB-241 / FRS-BB-276 / RISK-BB-343 |
| TEST-BB-131 | D,I | `AntibodyIdentificationHistoryPostRuleTests.ReadyToIssueNotice_UsesTheIssueWarning`; `Phase4IssuingTests.ListReadyToIssue_FlagsOpenAntibodyWorkupWithoutRemovingTheRow` | URS-BB-242 / FRS-BB-277 / RISK-BB-344 |
| TEST-BB-132 | I | `Phase4IssuingTests.ListReadyToIssue_MarksUnitInsideTheIssueNearExpiryWindow` | URS-BB-243 / FRS-BB-278 / RISK-BB-345 |
| TEST-BB-133 | I | `Phase4IssuingTests.ListReadyToIssue_ShowsElectronicCrossmatchBlockingReason` | URS-BB-244 / FRS-BB-279 / RISK-BB-346 |
| TEST-BB-134 | D, I | `CrossmatchRuleTests.DescribeReadyToIssue_MatchesTheStandardIssueEvaluation`; `Phase4IssuingTests.ListReadyToIssue_MarksMissingCrossmatchAndKeepsIncompatibleIssuable` | URS-BB-245 / FRS-BB-280 / RISK-BB-347 |
| TEST-BB-135 | D, I | `SpecialTransfusionRequirementRuleTests.ReadyToIssueNotice_UsesTheUnitFactHardStop`; `Phase4IssuingTests.ListReadyToIssue_MarksUnmetSpecialRequirementWithoutOfferingIssue` | URS-BB-246 / FRS-BB-281 / RISK-BB-348 |
| TEST-BB-136 | D, I | `BloodAttributeCompatibilityRuleTests.ReadyToIssueNotice_UsesTheIssueWarning`; `Phase4IssuingTests.ListReadyToIssue_MarksAntigenWarningWithoutRemovingTheRow` | URS-BB-247 / FRS-BB-282 / RISK-BB-349 |
| TEST-BB-137 | D, I | `IssueGateTests.DescribeAboRhDiscrepancy_MatchesTheIssueGate`; `Phase4IssuingTests.ListReadyToIssue_MarksUnresolvedAboRhDiscrepancy` | URS-BB-248 / FRS-BB-283 / RISK-BB-350 |
| TEST-BB-138 | D | `IssueGateTests.PatientBannerNotice_UsesTheDisagreementWarning` | URS-BB-249 / FRS-BB-284 / RISK-BB-351 |
| TEST-BB-139 | D | `ReactionHistoryBannerTests.Notice_NamesARecordedReactionAndStaysQuietOtherwise` | URS-BB-250 / FRS-BB-285 / RISK-BB-352 |
| TEST-BB-140 | I | `Phase4IssuingTests.ListReadyToIssue_MarksRecordedReactionWithoutRemovingTheRow` | URS-BB-251 / FRS-BB-286 / RISK-BB-353 |
| TEST-BB-141 | A | `ReadyToIssueFormCueTests.Notices_RepeatTheReadyToIssueMarks` | URS-BB-252 / FRS-BB-287 / RISK-BB-354 |
| TEST-BB-142 | I | `Phase4IssuingTests.ListReadyToIssue_MarksThisPatientsAutologousAndDirectedUnits` | URS-BB-253 / FRS-BB-288 / RISK-BB-355 |
| TEST-BB-143 | I | `Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate` | URS-BB-254 / FRS-BB-289 / RISK-BB-356 |
| TEST-BB-144 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-255 / FRS-BB-290 / RISK-BB-357 |
| TEST-BB-145 | I | `Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate` | URS-BB-256 / FRS-BB-291 / RISK-BB-358 |
| TEST-BB-146 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-257 / FRS-BB-292 / RISK-BB-359 |
| TEST-BB-147 | I | `PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved` | URS-BB-258 / FRS-BB-293 / RISK-BB-360 |
| TEST-BB-148 | I | `Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate` | URS-BB-259 / FRS-BB-294 / RISK-BB-361 |
| TEST-BB-149 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-260 / FRS-BB-295 / RISK-BB-362 |
| TEST-BB-150 | I | `PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved` | URS-BB-261 / FRS-BB-296 / RISK-BB-363 |
| TEST-BB-151 | I | `Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate` | URS-BB-262 / FRS-BB-297 / RISK-BB-364 |
| TEST-BB-152 | I | `Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate` | URS-BB-263 / FRS-BB-298 / RISK-BB-365 |
| TEST-BB-153 | I | `PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved` | URS-BB-264 / FRS-BB-299 / RISK-BB-366 |
| TEST-BB-154 | I | `Phase4IssuingTests.ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate` | URS-BB-265 / FRS-BB-300 / RISK-BB-367 |
| TEST-BB-155 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-266 / FRS-BB-301 / RISK-BB-368 |
| TEST-BB-156 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-267 / FRS-BB-302 / RISK-BB-369 |
| TEST-BB-157 | I | `PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved` | URS-BB-268 / FRS-BB-303 / RISK-BB-370 |
| TEST-BB-158 | I | `PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved` | URS-BB-269 / FRS-BB-304 / RISK-BB-371 |
| TEST-BB-159 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-270 / FRS-BB-305 / RISK-BB-372 |
| TEST-BB-160 | I | `PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved` | URS-BB-271 / FRS-BB-306 / RISK-BB-373 |
| TEST-BB-161 | I | `ReadyToIssueFormCueTests.Notices_RepeatTheReadyToIssueMarks` | URS-BB-272 / FRS-BB-307 / RISK-BB-374 |
| TEST-BB-162 | I | `PatientAllocationTests.Allocate_Rbc_OrdersCxm_AndListsReserved` | URS-BB-273 / FRS-BB-308 / RISK-BB-375 |
| TEST-BB-163 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-274 / FRS-BB-309 / RISK-BB-376 |
| TEST-BB-164 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-275 / FRS-BB-310 / RISK-BB-377 |
| TEST-BB-165 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-276 / FRS-BB-311 / RISK-BB-378 |
| TEST-BB-166 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-277 / FRS-BB-312 / RISK-BB-379 |
| TEST-BB-167 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-278 / FRS-BB-313 / RISK-BB-380 |
| TEST-BB-168 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-279 / FRS-BB-314 / RISK-BB-381 |
| TEST-BB-169 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-280 / FRS-BB-315 / RISK-BB-382 |
| TEST-BB-170 | U | `IssuedToNoticeTests.IssuedToNotice_NamesThePerson` | URS-BB-281 / FRS-BB-316 / RISK-BB-383 |
| TEST-BB-171 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-282 / FRS-BB-317 / RISK-BB-384 |
| TEST-BB-172 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-283 / FRS-BB-318 / RISK-BB-385 |
| TEST-BB-173 | U | `CoolerNoticeTests.CoolerNotice_NamesTheContainer` | URS-BB-284 / FRS-BB-319 / RISK-BB-386 |
| TEST-BB-174 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-285 / FRS-BB-320 / RISK-BB-387 |
| TEST-BB-175 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-286 / FRS-BB-321 / RISK-BB-388 |
| TEST-BB-176 | U | `RetrospectiveFollowUpNoticeTests.RetrospectiveFollowUpNotice_NamesCustody` | URS-BB-287 / FRS-BB-322 / RISK-BB-389 |
| TEST-BB-177 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-288 / FRS-BB-323 / RISK-BB-390 |
| TEST-BB-178 | U | `IssuedToLocationNoticeTests.IssuedToLocationNotice_NamesTheWard` | URS-BB-289 / FRS-BB-324 / RISK-BB-391 |
| TEST-BB-179 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-290 / FRS-BB-325 / RISK-BB-392 |
| TEST-BB-180 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-291 / FRS-BB-326 / RISK-BB-393 |
| TEST-BB-181 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-292 / FRS-BB-327 / RISK-BB-394 |
| TEST-BB-182 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-293 / FRS-BB-328 / RISK-BB-395 |
| TEST-BB-183 | U | `IssuedByNoticeTests.IssuedByNotice_NamesTheTechnologist` | URS-BB-294 / FRS-BB-329 / RISK-BB-396 |
| TEST-BB-184 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-295 / FRS-BB-330 / RISK-BB-397 |
| TEST-BB-185 | U | `RetrospectiveFollowUpNoticeTests.RetrospectiveFollowUpNotice_NamesCustody` | URS-BB-296 / FRS-BB-331 / RISK-BB-398 |
| TEST-BB-186 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-297 / FRS-BB-332 / RISK-BB-399 |
| TEST-BB-187 | U | `EmergencyReleaseReasonNoticeTests.EmergencyReleaseReasonNotice_NamesTheReason` | URS-BB-298 / FRS-BB-333 / RISK-BB-400 |
| TEST-BB-188 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-299 / FRS-BB-334 / RISK-BB-401 |
| TEST-BB-189 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-300 / FRS-BB-335 / RISK-BB-402 |
| TEST-BB-190 | U | `EmergencyReleaseReasonNoticeTests.EmergencyReleaseReasonNotice_NamesTheReason` | URS-BB-301 / FRS-BB-336 / RISK-BB-403 |
| TEST-BB-191 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-302 / FRS-BB-337 / RISK-BB-404 |
| TEST-BB-192 | U | `SecondVerifierNoticeTests.SecondVerifierNotice_NamesTheWitness` | URS-BB-303 / FRS-BB-338 / RISK-BB-405 |
| TEST-BB-193 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-304 / FRS-BB-339 / RISK-BB-406 |
| TEST-BB-194 | U | `RetrospectiveFollowUpNoticeTests.RetrospectiveFollowUpNotice_NamesCustody` | URS-BB-305 / FRS-BB-340 / RISK-BB-407 |
| TEST-BB-195 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-306 / FRS-BB-341 / RISK-BB-408 |
| TEST-BB-196 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-307 / FRS-BB-342 / RISK-BB-409 |
| TEST-BB-197 | U | `EmergencyReleaseAuthorizerNoticeTests.EmergencyReleaseAuthorizerNotice_NamesTheAuthorizer` | URS-BB-308 / FRS-BB-343 / RISK-BB-410 |
| TEST-BB-198 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-309 / FRS-BB-344 / RISK-BB-411 |
| TEST-BB-199 | U | `EmergencyReleaseAuthorizerNoticeTests.EmergencyReleaseAuthorizerNotice_NamesTheAuthorizer` | URS-BB-310 / FRS-BB-345 / RISK-BB-412 |
| TEST-BB-200 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-311 / FRS-BB-346 / RISK-BB-413 |
| TEST-BB-201 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-312 / FRS-BB-347 / RISK-BB-414 |
| TEST-BB-202 | U | `IssueAppearanceNoticeTests.IssueAppearanceNotice_NamesTheCode` | URS-BB-313 / FRS-BB-348 / RISK-BB-415 |
| TEST-BB-203 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-314 / FRS-BB-349 / RISK-BB-416 |
| TEST-BB-204 | U | `IssueAppearanceNoticeTests.IssueAppearanceNotice_NamesTheCode` | URS-BB-315 / FRS-BB-350 / RISK-BB-417 |
| TEST-BB-205 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-316 / FRS-BB-351 / RISK-BB-418 |
| TEST-BB-206 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-317 / FRS-BB-352 / RISK-BB-419 |
| TEST-BB-207 | U | `WardAppearanceNoticeTests.WardAppearanceNotice_NamesTheCode` | URS-BB-318 / FRS-BB-353 / RISK-BB-420 |
| TEST-BB-208 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-319 / FRS-BB-354 / RISK-BB-421 |
| TEST-BB-209 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-320 / FRS-BB-355 / RISK-BB-422 |
| TEST-BB-210 | U | `PtagNoticeTests.PtagNotice_NamesTheCode` | URS-BB-321 / FRS-BB-356 / RISK-BB-423 |
| TEST-BB-211 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-322 / FRS-BB-357 / RISK-BB-424 |
| TEST-BB-212 | U | `PtagNoticeTests.PtagNotice_NamesTheCode` | URS-BB-323 / FRS-BB-358 / RISK-BB-425 |
| TEST-BB-213 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-324 / FRS-BB-359 / RISK-BB-426 |
| TEST-BB-214 | I | `Phase4IssuingTests.ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt` | URS-BB-325 / FRS-BB-360 / RISK-BB-427 |
| TEST-BB-215 | U | `CrossmatchStatusNoticeTests.CrossmatchStatusNotice_NamesTheStatus` | URS-BB-326 / FRS-BB-361 / RISK-BB-428 |
| TEST-BB-216 | I | `Phase4IssuingTests.EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm` | URS-BB-327 / FRS-BB-362 / RISK-BB-429 |
| TEST-BB-217 | U | `CrossmatchStatusNoticeTests.CrossmatchStatusNotice_NamesTheStatus` | URS-BB-328 / FRS-BB-363 / RISK-BB-430 |
| TEST-BB-218 | I | `Phase10PatientWorkspaceTests.ProductHistory_IsPatientScoped` | URS-BB-329 / FRS-BB-364 / RISK-BB-431 |
| TEST-BB-219 | I | `ElectronicXmHistoryRegressionTests.DeactivatedAntibody_BlocksElectronicXm_WithExplainableReason` | URS-BB-330 / FRS-BB-365 / RISK-BB-432 |
| TEST-BB-220 | A | `OpenReactionInvestigationRuleTests.Count_IncludesOpenAndUnderReview_AndOmitsClosed` | URS-BB-331 / FRS-BB-366 / RISK-BB-433 |
| TEST-BB-221 | A | `InTransitAttentionRuleTests.CountOverdue_IncludesOnlyFlagsAlreadyPastTheArrivalWindow` | URS-BB-332 / FRS-BB-367 / RISK-BB-434 |
| TEST-BB-222 | A | `DashboardDueCountTests.RetrospectiveCountOverdue_IncludesOnlyItemsPastTheStoredDueTime` | URS-BB-333 / FRS-BB-368 / RISK-BB-435 |
| TEST-BB-223 | A | `DashboardDueCountTests.FatalityFollowUpDue_CountsClosedCasesThatStillNeedFollowUp` | URS-BB-333 / FRS-BB-368 / RISK-BB-435 |
| TEST-BB-224 | A | `SpecimenReceiptRuleTests`; `Phase3ServicesTests.ReceiveThenAccept_MovesCollectedToAccepted_AndLeavesTheBoard`; `Phase3ServicesTests.Receive_ExpiredCollected_IsHardStopped` | URS-BB-334 / FRS-BB-369 / RISK-BB-436 |
| TEST-BB-225 | A | `ShiftHandoffRuleTests`; `ShiftHandoffServiceTests.Create_WritesSnapshotAndAudit`; `ShiftHandoffServiceTests.Acknowledge_SameUser_IsHardStopped` | URS-BB-335 / FRS-BB-370 / RISK-BB-437 |
| TEST-BB-226 | A | `SpecimenRelabelRuleTests`; `Phase3ServicesTests.Relabel_WritesBarcode_WithReason`; `Phase3ServicesTests.Update_BarcodeChange_IsHardStopped`; `Phase3ServicesTests.Relabel_RejectedSpecimen_IsHardStopped` | URS-BB-336 / FRS-BB-371 / RISK-BB-438 |
| TEST-BB-227 | A | `SupervisorQueueRuleTests`; `SupervisorQueueServiceTests.List_WithInventoryRelease_IncludesQuarantineUnit`; `SupervisorQueueServiceTests.List_WithResultVerifyOnly_OmitsQuarantine`; `SupervisorQueueServiceTests.List_WithoutSupervisorPermissions_IsHardStopped` | URS-BB-337 / FRS-BB-372 / RISK-BB-439 |
| TEST-BB-228 | A | `PatientHistoricalAliasRuleTests`; `PatientServiceTests.Update_WritesDemographics_AndLeavesMrnUnchanged`; `PatientServiceTests.Update_NameChangeWithoutReason_IsHardStopped`; `PatientServiceTests.ListDtos_SearchMatchesPriorNameAlias` | URS-BB-338 / FRS-BB-373 / RISK-BB-440 |
| TEST-BB-229 | A | `Phase5Hl7Tests.InboundAdt_Update_ChangesDemographicsWithoutDuplicating`; `Phase5Hl7Tests.InboundAdt_SparseA08_DoesNotArchiveWhenNameUnchanged` | URS-BB-339 / FRS-BB-374 / RISK-BB-441 |
| TEST-BB-230 | I | `PatientUnmergeRuleTests`; `PatientMergeServiceTests.Unmerge_RestoresOrdersSpecimensAntibodiesAndElution`; `PatientMergeServiceTests.Unmerge_BlankReason_IsHardStopped`; `PatientMergeServiceTests.Unmerge_WithoutPatientMerge_IsRejected`; `PatientMergeServiceTests.Unmerge_SecondTime_IsHardStopped`; `PatientMergeServiceTests.Unmerge_OrderCreatedAfterMerge_IsHardStopped`; `PatientMergeServiceTests.Unmerge_WithoutLedger_IsHardStopped`; `PatientMergeServiceTests.Unmerge_SkippedDuplicateAntibody_StaysOnTheDuplicate` | URS-BB-340 / FRS-BB-375 / RISK-BB-442 |
| TEST-BB-231 | I | `UnitModificationAttributeRuleTests`; `BloodProductModificationServiceTests.Divide_CopiesSourceAntigenTypingOntoEachChild`; `BloodProductModificationServiceTests.Pool_DiscordantAntigen_IsHardStoppedAndSourcesStayAvailable`; `BloodProductModificationServiceTests.Pool_PositiveAndUntyped_CopiesPositive_AndDoesNotClaimAPartialNegative` | URS-BB-341 / FRS-BB-376 / RISK-BB-443 |
| TEST-BB-232 | I | `UnitModificationRestrictionRuleTests`; `BloodProductModificationServiceTests.Divide_CopiesAutologousReservationOntoEachChild`; `BloodProductModificationServiceTests.Pool_AutologousWithAllogeneic_IsHardStoppedAndSourcesStayAvailable`; `BloodProductModificationServiceTests.Pool_SameDirectedPatient_CopiesReservation` | URS-BB-342 / FRS-BB-377 / RISK-BB-444 |
| TEST-BB-233 | I | `UnitModificationEligibilityRuleTests.EvaluateAliquot_VolumeLessThanParent_Passes`; `UnitModificationEligibilityRuleTests.EvaluateAliquot_VolumeNotLessThanParent_HardStops`; `UnitModificationEligibilityRuleTests.EvaluateAliquot_MissingVolume_HardStops`; `BloodProductModificationServiceTests.Aliquot_KeepsParentAvailable_ReducesVolume_AndCopiesTyping`; `BloodProductModificationServiceTests.Aliquot_VolumeNotLessThanParent_IsHardStopped` | URS-BB-343 / FRS-BB-378 / RISK-BB-445 |
| TEST-BB-234 | I | `UnitModificationEligibilityRuleTests.EvaluateDivide_VolumesShortOfSource_HardStops`; `UnitModificationEligibilityRuleTests.EvaluateResultVolume_ExceedsSource_HardStops`; `UnitModificationEligibilityRuleTests.EvaluateResultVolume_NonPositive_HardStops`; `BloodProductModificationServiceTests.Divide_VolumesShortOfSource_IsBlocked`; `BloodProductModificationServiceTests.ApplySingle_ResultVolumeExceedsSource_IsBlocked`; `BloodProductModificationServiceTests.ApplySingle_ResultVolumeWithinSource_Succeeds` | URS-BB-344 / FRS-BB-379 / RISK-BB-446 |
| TEST-BB-235 | I | `UnitModificationEligibilityRuleTests.EvaluatePoolVolumes_MissingAmongKnown_HardStops`; `UnitModificationEligibilityRuleTests.EvaluatePoolVolumes_NonPositive_HardStops`; `UnitModificationEligibilityRuleTests.EvaluatePoolVolumes_NoneRecorded_Passes`; `BloodProductModificationServiceTests.Pool_MissingVolumeAmongKnownVolumes_IsBlocked`; `BloodProductModificationServiceTests.Pool_RecordedVolumes_SumOntoTheResult_AndAuditKeepsTheSources` | URS-BB-345 / FRS-BB-380 / RISK-BB-447 |
| TEST-BB-236 | I | `UnitModificationEligibilityRuleTests.EvaluatePoolLocations_DifferentLocations_HardStops`; `UnitModificationEligibilityRuleTests.EvaluatePoolLocations_AssignedAndUnassigned_HardStops`; `UnitModificationEligibilityRuleTests.EvaluatePoolLocations_AllUnassigned_Passes`; `BloodProductModificationServiceTests.Pool_DifferentLocations_IsBlocked`; `BloodProductModificationServiceTests.Pool_SameLocation_KeepsThatLocationOnTheResult` | URS-BB-346 / FRS-BB-381 / RISK-BB-448 |
| TEST-BB-237 | I | `BloodProductModificationServiceTests.GetHistory_ShowsWhereTheResultWasMade_AfterItIsMoved`; `BloodProductModificationServiceTests.GetHistory_WithoutALocation_ShowsNone` | URS-BB-347 / FRS-BB-382 / RISK-BB-449 |
| TEST-BB-238 | I | `BloodProductModificationServiceTests.GetHistory_ShowsWhereTheResultWasMade_AfterItIsMoved`; `BloodProductModificationServiceTests.GetHistory_StillAtTheCreationLocation_DoesNotMarkAMove`; `BloodProductModificationServiceTests.GetHistory_WithoutALocation_ShowsNone` | URS-BB-348 / FRS-BB-383 / RISK-BB-450 |
| TEST-BB-239 | I | `BloodProductModificationServiceTests.GetHistory_ShowsWhereTheResultWasMade_AfterItIsMoved`; `BloodProductModificationServiceTests.GetHistory_MoveWithoutAStatusRow_ShowsTheNewPlaceWithoutATimeOrReason`; `BloodProductModificationServiceTests.GetHistory_StillAtTheCreationLocation_DoesNotMarkAMove` | URS-BB-349 / FRS-BB-384 / RISK-BB-451 |
| TEST-BB-240 | I | `BloodProductModificationServiceTests.GetHistory_ShowsWhereTheResultWasMade_AfterItIsMoved`; `BloodProductModificationServiceTests.GetHistory_MoveWithoutAPerson_ShowsTimeAndReasonWithoutAName`; `BloodProductModificationServiceTests.GetHistory_MoveWithoutAStatusRow_ShowsTheNewPlaceWithoutATimeOrReason` | URS-BB-350 / FRS-BB-385 / RISK-BB-452 |
| TEST-BB-241 | D | `StorageLocationLabelTests.Format_NamedLocation_UsesTheName`; `StorageLocationLabelTests.Format_BlankOrUnknownName_KeepsTheLocationId`; `StorageLocationLabelTests.Format_NoLocation_DoesNotInventOne` | URS-BB-351 / FRS-BB-386 / RISK-BB-453 |
| TEST-BB-242 | D | `ModificationResultNextStepTests.ReleaseCue_Quarantine_NamesThePlaceAndTheRelease`; `ModificationResultNextStepTests.ReleaseCue_QuarantineWithoutALocation_DoesNotInventAPlace`; `ModificationResultNextStepTests.ReleaseCue_NotQuarantine_HasNoReleaseStep`; `ModificationResultNextStepTests.ReleaseHref_WithoutPermission_DoesNotOfferRelease`; `ModificationResultNextStepTests.QuarantineReleaseTarget_OpensOnlyAUnitOnTheBoard` | URS-BB-352 / FRS-BB-387 / RISK-BB-454 |
| TEST-BB-243 | D | `ModificationResultNextStepTests.AliquotParentCue_ConfirmedParent_NamesStatusAndRemainingVolume`; `ModificationResultNextStepTests.AliquotParentCue_UnrecordedVolume_DoesNotInventOne`; `ModificationResultNextStepTests.AliquotParentCue_UnconfirmedParent_DoesNotInventStatusOrVolume`; `ModificationResultNextStepTests.AliquotParentCue_ReportsTheParentStatusItWasGiven` | URS-BB-353 / FRS-BB-388 / RISK-BB-455 |
| TEST-BB-244 | D | `ModificationResultNextStepTests.RetiredSourceCue_ConfirmedSources_NamesEachStatus`; `ModificationResultNextStepTests.RetiredSourceCue_UnconfirmedSource_DoesNotInventModified`; `ModificationResultNextStepTests.RetiredSourceCue_ReportsTheStatusItWasGiven` | URS-BB-354 / FRS-BB-389 / RISK-BB-456 |
| TEST-BB-245 | I | `UnitModificationEligibilityRuleTests.OfferModification_Available_IsOffered`; `UnitModificationEligibilityRuleTests.OfferModification_Assigned_OnlyWhileCompletingAnOrder`; `UnitModificationEligibilityRuleTests.OfferModification_RetiredOrHeld_IsNotOffered`; `BloodProductModificationServiceTests.GetEligibleModifications_ModifiedUnit_ReturnsNone`; `BloodProductModificationServiceTests.GetEligibleModifications_AssignedUnit_StillListsRules` | URS-BB-355 / FRS-BB-390 / RISK-BB-457 |
| TEST-BB-246 | I | `InventoryServiceTests.ListQuarantine_NamesTheStorageLocation` | URS-BB-356 / FRS-BB-391 / RISK-BB-458 |
| TEST-BB-247 | I | `InventoryServiceTests.ListQuarantine_ShowsRecordedVolume_AndDoesNotInventOne` | URS-BB-357 / FRS-BB-392 / RISK-BB-459 |
| TEST-BB-248 | I | `InventoryServiceTests.ListDiscrepancy_NamesTheStorageLocation` | URS-BB-358 / FRS-BB-393 / RISK-BB-460 |
| TEST-BB-249 | I | `InventoryServiceTests.ListOnHold_NamesTheStorageLocation` | URS-BB-359 / FRS-BB-394 / RISK-BB-461 |
| TEST-BB-250 | D | `StorageLocationLabelTests.ForInventoryList_NamedLocation_UsesTheNameNotTheCode`; `StorageLocationLabelTests.ForInventoryList_BlankName_DoesNotFallBackToTheCode`; `StorageLocationLabelTests.ForInventoryList_NoLocation_DoesNotInventOne` | URS-BB-360 / FRS-BB-395 / RISK-BB-462 |
| TEST-BB-251 | I | `InventoryServiceTests.ListNearExpiry_NamesTheStorageLocation` | URS-BB-361 / FRS-BB-396 / RISK-BB-463 |
| TEST-BB-252 | D | `StorageLocationLabelTests.ForUnitChart_NamedLocation_UsesTheNameNotTheCode`; `StorageLocationLabelTests.ForUnitChart_BlankName_DoesNotFallBackToTheCode`; `StorageLocationLabelTests.ForUnitChart_NoLocation_DoesNotInventOne` | URS-BB-362 / FRS-BB-397 / RISK-BB-464 |
| TEST-BB-253 | I | `StatusHistoryLocationCueTests`; `InventoryServiceTests.UnitActivity_NamesTheStorageLocationsOnAMove` | URS-BB-363 / FRS-BB-398 / RISK-BB-465 |
| TEST-BB-254 | I | `PatientActivityLabelTests`; `InventoryServiceTests.UnitActivity_NamesThePatientOnAnAllocation` | URS-BB-364 / FRS-BB-399 / RISK-BB-466 |
| TEST-BB-255 | I | `InventoryServiceTests.UnitActivity_NamesThePatientOnACrossmatch` | URS-BB-365 / FRS-BB-400 / RISK-BB-467 |
| TEST-BB-256 | I | `InventoryServiceTests.UnitActivity_NamesThePatientOnATransfusion` | URS-BB-366 / FRS-BB-401 / RISK-BB-468 |
| TEST-BB-257 | I | `ReturnActivityCueTests`; `InventoryServiceTests.UnitActivity_NamesThePatientOnAReturn` | URS-BB-367 / FRS-BB-402 / RISK-BB-469 |
| TEST-BB-258 | I | `ModificationActivityCueTests`; `InventoryServiceTests.UnitActivity_NamesThePatientOnAnOrderModification` | URS-BB-368 / FRS-BB-403 / RISK-BB-470 |
| TEST-BB-259 | I | `BenchModificationActivityCueTests`; `InventoryServiceTests.UnitActivity_SaysWhenABenchModificationHasNoPatientOrder` | URS-BB-369 / FRS-BB-404 / RISK-BB-471 |

Layers: D = Domain.Tests, A = Application.Tests, I = Integration.Tests (including `tests/safety_regression`).
