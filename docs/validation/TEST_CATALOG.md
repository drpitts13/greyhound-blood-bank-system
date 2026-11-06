# Formal test identifiers (`TEST-BB-*`)

These IDs are **validation evidence handles**. They are not a claim that the
software is AABB-accredited or FDA-cleared. Remaining automated tests stay
citable by class or method name until an ID is assigned.

Numbering: `TEST-BB-NNN` is assigned once and is not reused. Next free ID:
**TEST-BB-286**.

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
| TEST-BB-260 | I | `BenchModificationActivityCueTests`; `InventoryServiceTests.UnitActivity_SaysWhenABenchModificationHasNoPatientOrder` | URS-BB-370 / FRS-BB-405 / RISK-BB-472 |
| TEST-BB-261 | I | `ModificationHistoryPatientCueTests`; `BloodProductModificationServiceTests.GetHistory_NamesThePatientWhenAnOrderPointsAtTheModification` | URS-BB-371 / FRS-BB-406 / RISK-BB-473 |
| TEST-BB-262 | I | `InventoryServiceTests.UnitActivity_NamesThePatientOnAModificationStatusRow` | URS-BB-372 / FRS-BB-407 / RISK-BB-474 |
| TEST-BB-263 | I | `IssueDestinationCueTests`; `InventoryServiceTests.UnitActivity_NamesTheStorageLocationOnAnIssue` | URS-BB-373 / FRS-BB-408 / RISK-BB-475 |
| TEST-BB-264 | I | `Phase4IssuingTests.IssuedBoards_NameTheStorageLocationWhenThePlaceIsALocationCode` | URS-BB-374 / FRS-BB-409 / RISK-BB-476 |
| TEST-BB-265 | I | `ComplianceWorkflowTests.Lookback_NamesTheStorageLocationWhenThePlaceIsALocationCode` | URS-BB-375 / FRS-BB-410 / RISK-BB-477 |
| TEST-BB-266 | U | `IssuedToLocationNoticeTests.IssuedToLocationNotice_NamesTheWard`; `RetrospectiveFollowUpNoticeTests.RetrospectiveFollowUpNotice_NamesCustody` | URS-BB-376 / FRS-BB-411 / RISK-BB-478 |
| TEST-BB-267 | I | `StorageLocationCueTests.StorageLocationLabel_KeepsTheLocationIdWhenTheNameIsBlank`; `Phase4IssuingTests.ListReadyToIssue_KeepsTheLocationIdWhenTheNameIsBlank`; `PatientAllocationTests.CompatibleAndReserved_KeepTheLocationIdWhenTheNameIsBlank` | URS-BB-377 / FRS-BB-412 / RISK-BB-479 |
| TEST-BB-268 | D | `StorageLocationLabelTests.ForPicker_KeepsTheCodeAndTheLocationIdWhenTheNameIsBlank` | URS-BB-378 / FRS-BB-413 / RISK-BB-480 |
| TEST-BB-269 | A | `InventoryLocationAdminDtoTests.NameLabel_KeepsTheLocationIdWhenTheNameIsBlank` | URS-BB-379 / FRS-BB-414 / RISK-BB-481 |
| TEST-BB-270 | I | `OrderingLocationAuthorizationTests.Create_BlankName_DoesNotStoreTheCodeAsTheName`; `OrderingLocationAuthorizationTests.Hl7_BlankName_DoesNotStoreTheCodeAsTheName`; `AdminCatalogTests.CreateLocation_CodeOnly_DoesNotStoreTheCodeAsTheName` | URS-BB-380 / FRS-BB-415 / RISK-BB-482 |
| TEST-BB-271 | A | `BloodSupplierLabelTests.ForChoice_KeepsTheCodeAndTheSupplierIdWhenTheNameIsBlank`; `BloodSupplierChoiceTests.DisplayName_KeepsTheCodeAndTheSupplierIdWhenTheNameIsBlank` | URS-BB-381 / FRS-BB-416 / RISK-BB-483 |
| TEST-BB-272 | A | `BloodSupplierLabelTests.Name_KeepsTheSupplierIdWhenTheNameIsBlank`; `BloodSupplierChoiceTests.NameLabel_KeepsTheSupplierIdWhenTheNameIsBlank` | URS-BB-382 / FRS-BB-417 / RISK-BB-484 |
| TEST-BB-273 | I | `DeliverySupplierLabelTests.SupplierNameLabel_KeepsTheSupplierIdWhenTheNameIsBlank`; `DeliveryServiceTests.BlankSupplierName_ShowsTheSupplierIdAndDoesNotWriteItOnTheUnit` | URS-BB-383 / FRS-BB-418 / RISK-BB-485 |
| TEST-BB-274 | I | `BloodUnitSupplierLabelTests.SupplierLabel_KeepsTheSupplierIdWhenTheDeliveryNamesABlankSupplier`; `DeliveryServiceTests.UnitChart_ShowsTheSupplierIdAndDoesNotRewriteTheStoredName` | URS-BB-384 / FRS-BB-419 / RISK-BB-486 |
| TEST-BB-275 | I | `ExpectedInboundSupplierLabelTests.SupplierLabel_KeepsTheSupplierIdWhenTheDeliveryNamesABlankSupplier`; `DeliveryServiceTests.ExpectedInbound_ShowsTheSupplierIdAndDoesNotRewriteTheStoredName` | URS-BB-385 / FRS-BB-420 / RISK-BB-487 |
| TEST-BB-276 | A | `ProductTypeLabelTests.ForChoice_KeepsTheCodeAndTheProductIdWhenTheNameIsBlank`; `ProductTypeLabelTests.Name_KeepsTheProductIdWhenTheNameIsBlank`; `ProductTypeChoiceTests.ChoiceLabel_KeepsTheCodeAndTheProductIdWhenTheNameIsBlank`; `ProductTypeChoiceTests.ChargeRuleChoice_KeepsTheProductCodeAndDoesNotUseTheIsbtCodeAsTheName` | URS-BB-386 / FRS-BB-421 / RISK-BB-488 |
| TEST-BB-277 | A | `ProductTypeLabelTests.Name_KeepsTheProductIdWhenTheNameIsBlank`; `ProductTypeChoiceTests.NameLabel_KeepsTheProductIdWhenTheNameIsBlank` | URS-BB-387 / FRS-BB-422 / RISK-BB-489 |
| TEST-BB-278 | A | `ProductTypeChoiceTests.OrderChoice_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-388 / FRS-BB-423 / RISK-BB-490 |
| TEST-BB-279 | I | `ReturnReissueRuleTests.ExemptDevice_DoesNotFailTheWindow`; `Phase4IssuingTests.Return_CallerTimeOutFlag_DoesNotOverrideAGovernedWindow`; `Phase4IssuingTests.Return_ExemptLocation_AfterThirtyMinutes_CanReturnToAvailable` | URS-BB-389 / FRS-BB-424 / RISK-BB-491 |
| TEST-BB-280 | I | `ModificationLabelVerificationRuleTests`; `BloodProductModificationServiceTests.ReleaseModificationResult_RequiresAMatchingUnitNumberScan` | URS-BB-390 / FRS-BB-425 / RISK-BB-492 |
| TEST-BB-281 | I | `CompetitorGapRuleTests`; `CompetitorGapWorkflowTests.ReagentQc_ReviewerMustDiffer_AndWorklistClearsAfterReview`; `CompetitorGapWorkflowTests.PatientResult_StaysOpenWhenReagentQcPolicyIsOff` | URS-BB-391 / FRS-BB-426 / RISK-BB-493 |
| TEST-BB-282 | I | `CompetitorGapRuleTests`; `CompetitorGapWorkflowTests.OverdueDisposition_ListsOnlyUndocumentedIssuedUnits` | URS-BB-392 / FRS-BB-427 / RISK-BB-494 |
| TEST-BB-283 | I | `BloodProductModificationServiceTests.ApplySingle_RecordsOptionalSupplyLot_OnHistory`; `BloodProductModificationServiceTests.ApplySingle_RequiredSupplyLot_BlocksWhenMissing` | URS-BB-393 / FRS-BB-428 / RISK-BB-495 |
| TEST-BB-284 | I | `BloodProductModificationServiceTests.ApplyBatch_IneligibleUnit_ModifiesNone`; `BloodProductModificationServiceTests.ApplyBatch_AllEligible_ModifiesEach` | URS-BB-394 / FRS-BB-429 / RISK-BB-496 |
| TEST-BB-285 | I | `CompetitorGapRuleTests`; `CompetitorGapWorkflowTests.MinimumStock_ShowsOnHandAgainstTheMinimum` | URS-BB-395 / FRS-BB-430 / RISK-BB-497 |
| TEST-BB-286 | A | `DashboardQueueCatalogTests` | URS-BB-396 / FRS-BB-431 / RISK-BB-498 |
| TEST-BB-287 | A | `TestDefinitionChoiceTests.OrderChoice_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-397 / FRS-BB-432 / RISK-BB-499 |
| TEST-BB-288 | A | `TestDefinitionChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-398 / FRS-BB-433 / RISK-BB-500 |
| TEST-BB-289 | A | `SubtestDefinitionChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-399 / FRS-BB-434 / RISK-BB-501 |
| TEST-BB-290 | A | `TestGrouperChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-400 / FRS-BB-435 / RISK-BB-502 |
| TEST-BB-291 | A | `PhaseDefinitionChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-401 / FRS-BB-436 / RISK-BB-503 |
| TEST-BB-292 | A | `BloodAttributeChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-402 / FRS-BB-437 / RISK-BB-504 |
| TEST-BB-293 | A | `SpecialRequirementChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-403 / FRS-BB-438 / RISK-BB-505 |
| TEST-BB-294 | A | `ReflexRuleChoiceTests.CatalogList_KeepsTheCodeAndDoesNotUseItAsTheName` | URS-BB-404 / FRS-BB-439 / RISK-BB-506 |
| TEST-BB-295 | I | `InventoryServiceTests.ScheduledExpirationSweep_ExpiresDueUnits_WithoutUserPermission`; `InventoryServiceTests.ScheduledExpirationSweep_SkipsTerminalStatuses` | URS-BB-405 / FRS-BB-440 / RISK-BB-507 |
| TEST-BB-296 | D / A / I | `StorageTemperatureRuleTests`; `DashboardQueueCatalogTests.Select_TemperatureExcursions_FollowsReceiveOrDeviationPermission`; `StorageTemperatureServiceTests` | URS-BB-406 / FRS-BB-441 / RISK-BB-508 |
| TEST-BB-297 | D / I | `DiscardReasonRuleTests`; `InventoryServiceTests.Discard_StoresTheCode_AndBlocksUnknownInactiveOrMissingComment`; `DiscardReasonWorkflowTests` | URS-BB-407 / FRS-BB-442 / RISK-BB-509 |
| TEST-BB-298 | D / I | `UtilizationRuleTests`; `UtilizationServiceTests` | URS-BB-408 / FRS-BB-443 / RISK-BB-510 |
| TEST-BB-299 | D / I | `AlertRuleTests`; `AlertWorkflowTests` | URS-BB-409 / FRS-BB-444 / RISK-BB-511 |
| TEST-BB-300 | D / I | `CsvFormatTests`; `CsvExportTests` | URS-BB-410 / FRS-BB-445 / RISK-BB-512 |
| TEST-BB-301 | D / I | `SupplierOrderRuleTests`; `SupplierOrderTests` | URS-BB-411 / FRS-BB-446 / RISK-BB-513 |
| TEST-BB-302 | D / I | `DatabaseBackupRuleTests`; `DatabaseBackupServiceTests` | URS-BB-412 / FRS-BB-447 / RISK-BB-514 |
| TEST-BB-303 | D / I | `TotpTests`; `MfaTests` | URS-BB-413 / FRS-BB-448 / RISK-BB-515 |
| TEST-BB-304 | D / I | `StorageExcursionQuarantineRuleTests`; `StorageTemperatureServiceTests.SustainedExcursion_*`, `ShortExcursion_*`, `QuarantineMinutesZero_*` | URS-BB-414 / FRS-BB-449 / RISK-BB-516 |
| TEST-BB-305 | D / I | `TotpTests.MfaRule_*`, `TotpTests.IsRequired_*`; `MfaTests` | URS-BB-415 / FRS-BB-450 / RISK-BB-517 |
| TEST-BB-306 | D / I | `TransfusionIndicationRuleTests`; `TransfusionIndicationTests` | URS-BB-416 / FRS-BB-451 / RISK-BB-518 |
| TEST-BB-307 | D / I | `UniversalLookupTests` | URS-BB-417 / FRS-BB-452 / RISK-BB-519 |
| TEST-BB-308 | D / I | `PatientTimelineTests` | URS-BB-418 / FRS-BB-453 / RISK-BB-520 |
| TEST-BB-309 | D / I | `SpecimenTimelineTests` | URS-BB-419 / FRS-BB-454 / RISK-BB-521 |
| TEST-BB-310 | D / I | `DashboardHomeTests` | URS-BB-420 / FRS-BB-455 / RISK-BB-522 |
| TEST-BB-311 | D / I | `PatientIdentifierEntryRuleTests`; `RazorFormBindingTests.Patient_identifier_fields_are_not_filled_from_the_record_directly` | URS-BB-421 / FRS-BB-456 / RISK-BB-523 |
| TEST-BB-312 | D / I | `ComboBoxDisplayTests` | URS-BB-422 / FRS-BB-457 / RISK-BB-524 |
| TEST-BB-313 | D / I | `EnterFlowTests` | URS-BB-423 / FRS-BB-458 / RISK-BB-525 |
| TEST-BB-314 | D / I | `ReturnInspectionRuleTests`; `Phase4IssuingTests.Return_WithUnansweredInspection_*`, `Return_WithFacilityDefault_*`, `Return_ExplicitQuarantine_*`, `Return_ExplicitAvailable_*`; `AttestationDefaultTests` | URS-BB-424 / FRS-BB-459 / RISK-BB-526 |
| TEST-BB-315 | D / I | `AppearanceAttestationRuleTests`; `Phase4IssuingTests.Issue_WithoutAppearance_*`, `Issue_WithFacilityDefault_*`, `WardReceipt_WithoutAppearance_*`; `InventoryServiceTests.ReceiveUnit_WithoutAppearance_*`; `AttestationDefaultTests.Appearance_*` | URS-BB-425 / FRS-BB-460 / RISK-BB-527 |
| TEST-BB-316 | D / I | `CrossmatchSettingsTests.UpdateOrder_OverrideReasonWithoutAuthorizer_SaysWhoIsMissing`; `Phase3ServicesTests.Update_ExpiryChange_ReasonWithoutAuthorizer_SaysWhoIsMissing` | URS-BB-424 / FRS-BB-459 / RISK-BB-526 |
| TEST-BB-317 | D / I | `EluateVerificationRuleTests`; `EluateFindingTests` | URS-BB-426 / FRS-BB-461 / RISK-BB-528 |
| TEST-BB-318 | D / I | `IsbtReadinessRuleTests`; `IsbtReadinessTests` | URS-BB-427 / FRS-BB-462 / RISK-BB-529 |
| TEST-BB-319 | D / I | `PbmDraftCriteriaTests`; `TransfusionIndicationTests.LoadDrafts_*`, `Draft_CannotBeActivated_UntilRenamed` | URS-BB-428 / FRS-BB-463 / RISK-BB-530 |
| TEST-BB-320 | I | `FhirReadServiceTests` | URS-BB-429 / FRS-BB-464 / RISK-BB-531 |
| TEST-BB-321 | D / I | `IssueGateTests.Emergency_*`, `NonEmergency_AboConflict_UsesTheOrdinaryHardStop`; `Phase4IssuingTests.EmergencyRelease_RedCellsConflictingWithEstablishedType_AreRefused_EvenWithOverride`, `EmergencyRelease_GroupORedCells_ForEstablishedGroupA_StillIssueWithOverride`; `IssueGateSafetyRegressionTests.Emergency_GroupARedCells_ForEstablishedGroupO_IsHardStopped` | URS-BB-430 / FRS-BB-465 / RISK-BB-532 |
| TEST-BB-322 | D | `RequestReadErrorTests` | URS-BB-431 / FRS-BB-466 / RISK-BB-533 |
| TEST-BB-323 | D / I | `RetypeStockStatusTests`; `IssueGateTests.VerifiedRetypeMismatch_IsHardStopped_OnAnyIssueType`, `NoRetypeMismatch_Passes`; `Phase4IssuingTests.Return_OfUnretypedEmergencyUnit_GoesBackToReceived_ForRetype`, `Return_OfRetypedUnit_StillGoesToAvailable`, `EmergencyRelease_OfUnitWithVerifiedRetypeMismatch_IsHardStopped`; `InventoryServiceTests.QuarantineRelease_RespectsPendingRetype`, `HoldRelease_RespectsPendingRetype` | URS-BB-432 / FRS-BB-467 / RISK-BB-534 |
| TEST-BB-324 | D / I | `MtpActivationRuleTests`; `Phase4IssuingTests.MassiveTransfusionIssue_*`, `MtpActivation_*`, `EmergencyRelease_DoesNotNeedOrLinkAnActivation` | URS-BB-433 / FRS-BB-468 / RISK-BB-535 |
| TEST-BB-325 | D / I | `MtpPackRuleTests`; `Phase4IssuingTests.MtpPack_ChosenAtActivation_ShowsRoundProgress_AndIgnoresReturnedUnits`, `MtpActivation_WithInactiveOrUnknownPack_IsRefused`, `MtpPack_InvalidContents_AreRefused_AndNoneAreShipped` | URS-BB-434 / FRS-BB-469 / RISK-BB-536 |
| TEST-BB-326 | D / I | `TransportContainerRuleTests`; `MtpPackReportTextTests`; `MtpPackCrossmatchTextTests`; `Phase4IssuingTests.MtpPack_IsRecorded_Audited_AndReported`, `MtpPack_OutOfRangeTemperature_IsRecordedAndFlagged`, `MtpPack_Refusals_WriteNothing`; `SeederTests.Seed_PopulatesReferenceAndDemoData_AndIsIdempotent` | URS-BB-435 / FRS-BB-470 / RISK-BB-537 |
| TEST-BB-327 | D / I | `ReactionInvestigationLockRuleTests`; `ReactionInvestigationServiceTests.ClosedInvestigation_RefusesChanges_ButAcceptsAnUnchangedResave`, `Reopen_NeedsReason_KeepsTheCloseInTheAudit_AndNeedsANewSignatureToClose`, `ClosedFatality_StillTakesItsFollowUpReporting` | URS-BB-436 / FRS-BB-471 / RISK-BB-538 |
| TEST-BB-328 | I | `FacilityTimeZoneTests`; `Phase4IssuingTests.MtpPackReport_PrintsLocalTime_AndNamesTheZone` | URS-BB-435 / FRS-BB-472 / RISK-BB-537 |
| TEST-BB-329 | D / I | `TransfusionDocumentationRuleTests`; `Phase4IssuingTests.DocumentTransfusion_InconsistentTimesOrVolume_AreRefused` | URS-BB-437 / FRS-BB-473 / RISK-BB-539 |
| TEST-BB-330 | D / I | `ShiftHandoffRuleTests`; `ShiftHandoffServiceTests`; `DashboardQueueCatalogTests.Select_AuditReadOnly_OmitsHandoff` | URS-BB-438 / FRS-BB-474 / RISK-BB-540 |
| TEST-BB-331 | D / I | `IssueTimeRuleTests`; `Phase4IssuingTests.Issue_LateEntry_NeedsReason_AndKeepsBothTimes`, `Issue_WithoutStatedTime_IsNotALateEntry` | URS-BB-439 / FRS-BB-475 / RISK-BB-541 |
| TEST-BB-332 | D / I | `OverrideReviewRuleTests`; `OverrideReviewTests` | URS-BB-440 / FRS-BB-476 / RISK-BB-542 |
| TEST-BB-333 | D / I | `DerivedFromSourceRuleTests`; `ResultLifecycleTests.InvalidateVerifiedAboRh_RetiresTheDerivedType_AndWarns`, `InvalidatedType_IsNotASecondConcordantDetermination`, `RejectSpecimen_WithActiveResults_IsRefused_ThenAllowedAfterInvalidation` | URS-BB-441 / FRS-BB-477 / RISK-BB-543 |
| TEST-BB-334 | D / I | `SpecimenRejectionReasonRuleTests`; `SpecimenRejectionReasonTests.CodedRejection_StoresCodeAndText_AndAudits`, `CommentRequiredReason_WithoutComment_IsRefused_ThenStoresComment`, `UnknownInactiveOrBlankReason_IsRefused_AndSpecimenUnchanged` | URS-BB-442 / FRS-BB-478 / RISK-BB-544 |
| TEST-BB-335 | I | `ReportServiceTests.RejectionsByCause_GroupsRejectedSpecimens_AndSkipsOthers`, `SeededReports_Exist_Run_AndCannotBeDeleted`; `SpecimenRejectionReasonTests.CodedRejection_StoresCodeAndText_AndAudits` (rejection time and user) | URS-BB-443 / FRS-BB-479 / RISK-BB-545 |
| TEST-BB-336 | D / I | `RetrospectiveCrossmatchIncompatibleRuleTests`; `Phase4IssuingTests.EmergencyRelease_IncompatibleXm_RaisesCriticalAlert_AndLeadsTheBoard`, `EmergencyRelease_CompatibleXm_RaisesNoAlert`, `EmergencyRelease_IncompatibleXm_StaysOnWorklist` | URS-BB-444 / FRS-BB-480 / RISK-BB-546 |
| TEST-BB-337 | D / I | `AuthorizerDirectoryRuleTests`; `OverrideAuthorizerMatchTests.Provider_User_AndUnknown_AreStamped`, `InactiveProvider_IsNotInDirectory`, `ExistingMatch_IsNotRecomputed_AndLaterEditsDoNotRestamp` | URS-BB-445 / FRS-BB-481 / RISK-BB-547 |
| TEST-BB-338 | D / I | `OverrideActionRuleTests`; `Phase4IssuingTests.MassiveTransfusionIssue_RecordsAMassiveTransfusionOverride_AndQueueNamesIt` | URS-BB-446 / FRS-BB-482 / RISK-BB-548 |
| TEST-BB-339 | I | `OverrideApproveGrantSeederTests.FreshInstall_GrantsSupervisor_AndRecordsTheMarker`, `OlderDatabaseWithoutTheMarker_IsGrantedOnce`, `AdministratorRemoval_AfterTheGrant_IsNotUndone` | OCD-047 |
| TEST-BB-340 | D / I | `ReturnDisposalRuleTests`; `Phase4IssuingTests.Return_AfterThirtyMinutes_DisposalRequested_QuarantinesInstead`, `Return_FailedVisual_QuarantinesAndLeavesOutstanding` | URS-BB-447 / FRS-BB-483 / RISK-BB-549 |
| TEST-BB-341 | I | `Phase4IssuingTests.Return_ToStock_OffersReserveAgain_ThenReissues`, `Return_ToQuarantine_OffersNoReserveAgain` | URS-BB-448 / FRS-BB-484 / RISK-BB-550 |
| TEST-BB-342 | D / I | `TransfusionUnitLifeRuleTests`; `Phase4IssuingTests.DocumentTransfusion_UnitExpiredAfterItStarted_CanBeDocumented`, `DocumentTransfusion_StartedAfterExpiry_IsRefused`, `DocumentTransfusion_StartBeforeIssue_IsRefused`, `DocumentTransfusion_InconsistentTimesOrVolume_AreRefused` (now issued before its documented start) | URS-BB-449 / FRS-BB-485 / RISK-BB-551 |
| TEST-BB-343 | I | `Phase4IssuingTests.StartThenComplete_ShowsTransfusing_ThenFinishesTheSameRecord`, `Start_WithoutIdentifiers_IsRefused_AndRecordsNothing`, `Complete_WithoutStart_OrWithStopBeforeStart_IsRefused`, `FinishedRecordOnAnIssuedIssue_IsNotShownAsStarted_AndCannotBeStartedAgain`, `StartOnlyPermission_CanStart_ButNotComplete` | URS-BB-450 / FRS-BB-486 / RISK-BB-552 |
| TEST-BB-344 | I | `DemoTransfusionConsistencyTests.EveryTransfusionOnAnIssuedIssue_IsARunningTransfusion`; `FdaAabbScenarioTests.LookbackByDin_ListsSeededRecipientAndPendingNotification` (running transfusion counts as reaching the patient) | FRS-BB-486 |
| TEST-BB-345 | I | `DemoDataInvariantTests.SeededUnitsIssuesAndRecords_AreConsistent` | Demo data |
| TEST-BB-346 | D / I | `TransfusionCompletionRuleTests`; `AlertWorkflowTests.Scanner_RaisesTransfusionNotCompleted_AfterReminderHours_AndResolvesWhenCompleted`, `Scanner_TransfusionReminder_OffWhenHoursAreZero` | URS-BB-451 / FRS-BB-487 / RISK-BB-553 |
| TEST-BB-347 | I | `DemoTransfusionConsistencyTests.RunningTransfusion_IsShownAsStarted_OnProductHistoryAndTimeline` | URS-BB-452 / FRS-BB-488 / RISK-BB-554 |
| TEST-BB-348 | D / I | `TransfusionRecordStateTests`; `FdaAabbScenarioTests.RunningTransfusion_HasNoDispositionInLookback_AndReportsInProgress` | URS-BB-453 / FRS-BB-489 / RISK-BB-555 |
| TEST-BB-349 | D / I | `SpecimenLabelIdentityRuleTests`; `SpecimenLabelIdentityTests.WithoutLabelIdentifiers_IsRefused_AndNothingIsAccessioned`, `LabelIdentifiersThatDoNotMatch_AreRefused`, `MatchingLabelIdentifiers_AreRecorded_WithCollectorAndDrawLocation`, `WhenTheFacilityPrefills_TheRecordIsUsed` | URS-BB-454 / FRS-BB-490 / RISK-BB-556 |
| TEST-BB-350 | D / I | `SpecimenReceiptScanRuleTests`; `SpecimenReceiptScanTests.ScanOfTheAccession_ReceivesThatTube_AndRecordsTheMethod`, `ScanOfTheLabelBarcode_ReceivesThatTube`, `UnknownTube_IsRefused_AndNothingChanges`, `ScanningAReceivedTubeAgain_IsRefused_NamingTheAccession` | URS-BB-455 / FRS-BB-491 / RISK-BB-557 |
| TEST-BB-351 | D / I | `SpecimenTransitRuleTests`; `SpecimenReceiptScanTests.AcceptByScan_AfterReceipt_AcceptsThatTube_AndRecordsTheMethod`, `AcceptByScan_BeforeReceipt_IsRefused_NamingTheTube`, `PendingList_ShowsTimeSinceDraw_ThenTimeToReceipt` | URS-BB-456 / FRS-BB-492 |
| TEST-BB-352 | D / I | `DerivedFromSourceRuleTests.TypeRetiredByCorrection_IsAWarningSayingWhenTheTypeReturns`; `ResultLifecycleTests.CorrectVerifiedAboRh_RetiresTheDerivedTypeImmediately_AndWarns` | URS-BB-457 / FRS-BB-493 / RISK-BB-558 |
| TEST-BB-353 | D / I | `SecondAboSeparateSpecimenTests`; `ResultLifecycleTests.SameSpecimenRetest_IsNotASecondDetermination_ButASecondTubeIs`, `Backfill_FillsTheSpecimenFromTheSourceResult` | URS-BB-458 / FRS-BB-494 / RISK-BB-559 |
| TEST-BB-354 | I | `Phase4IssuingTests.ShiftHandoff_CountsRunningTransfusions_AndIncompatibleRetrospectiveCrossmatches` | URS-BB-459 / FRS-BB-495 |
| TEST-BB-355 | D / I | `ManualBloodTypeDocumentationRuleTests`; `Phase3ServicesTests.ManualBloodType_Documented_NeedsAReference_AndIsStored`, `ManualBloodType_Undocumented_StoresNoDocumentation` | URS-BB-460 / FRS-BB-496 / RISK-BB-560 |
| TEST-BB-356 | D / A | `BloodTypeHistoryLabelTests`; `BloodTypeDtoTests.RetiredTestedType_IsMarked_AndDoesNotCount`, `DocumentedManualType_NamesItsRecord_AndCounts` | URS-BB-461 / FRS-BB-497 |
| TEST-BB-357 | D / I | `ResultReleaseRuleTests`; `Hl7OruBuilderTests.FirstVerifiedResult_IsFinal`, `VerifiedCorrection_IsCorrected`, `InvalidatedAfterVerification_IsPostedAsWrong`; `ResultLifecycleTests.Outbound_InvalidatedVerifiedResult_IsSentAsWrong_UnverifiedIsRefused` | URS-BB-462 / FRS-BB-498 / RISK-BB-561 |
| TEST-BB-358 | D / I | `ResultReleaseRuleTests.OnlyCorrectionsAndWithdrawals_AreSentAutomatically`; `ResultEhrUpdateTests.InvalidatingAVerifiedResult_QueuesAWithdrawal_WhenAResultsInterfaceIsEnabled`, `VerifyingACorrection_QueuesACorrection`, `WithoutAnEnabledResultsInterface_NothingIsQueued`, `WhenQueueingFails_TheInvalidationStands_AndAWarningSaysToSendByHand` | URS-BB-463 / FRS-BB-499 / RISK-BB-562 |
| TEST-BB-359 | I | `InventoryServiceTests.ExpireDueUnits_EndsTheReservationOnAnExpiredUnit_AndLeavesOthers`; `AlertWorkflowTests.UnitsExpiredAndExcursion_RaiseEventAlerts` | URS-BB-464 / FRS-BB-500 / RISK-BB-563 |
| TEST-BB-360 | D / I | `ProductAllocationDisplayStatusRuleTests.Evaluate_AnExpiredCrossmatch_IsNotReadyForIssue`; `Phase4IssuingTests.ListReadyToIssue_AnExpiredCrossmatch_ShowsCrossmatchExpired_NotReady` | URS-BB-465 / FRS-BB-501 / RISK-BB-564 |
| TEST-BB-361 | D | `DashboardDueCountTests.ReadyToIssue_CountsOnlyReservationsWhoseCrossmatchExpired` | URS-BB-466 / FRS-BB-502 / RISK-BB-565 |
| TEST-BB-362 | D | `DashboardDueCountTests.ReadyToIssue_IssuableRowsComeFirst_AndKeepTheirOrder`; `DashboardDueCountTests.ReadyToIssue_EachBlockingMark_StopsRoutineIssue` | URS-BB-467 / FRS-BB-503 / RISK-BB-566 |
| TEST-BB-363 | D / I | `CrossmatchSpecimenRuleTests.ExpiredSpecimen_IsAHardStop_NamingTheSpecimenAndTheNextStep`, `AtTheExpirationTime_TheSpecimenHasExpired`, `InDateOrUndatedSpecimen_Passes`, `ExpiredCompatibleCrossmatch_NeedsRepeating_UnlessElectronicOrIssued`; `Phase4IssuingTests.RecordCrossmatch_OnAnExpiredSpecimen_IsRefused_AndNothingIsSaved` | URS-BB-468 / FRS-BB-504 / RISK-BB-567 |
| TEST-BB-364 | D / I | `CrossmatchSpecimenRuleTests.RetrospectiveOnPreTransfusionSpecimen_IsAWarningToOverride`, `PreTransfusionSpecimen_IsCollectedAtOrBeforeIssue`; `Phase4IssuingTests.RetrospectiveXm_OnExpiredPreTransfusionSpecimen_AsksForAnException_ThenRecordsIt`, `RetrospectiveXm_OnSpecimenCollectedAfterIssue_ThatExpired_IsStillAHardStop`, `RetrospectiveXm_Exception_WithoutAnActiveDefinition_IsRefused` | URS-BB-469 / FRS-BB-505 / RISK-BB-568 / OCD-050 |
| TEST-BB-365 | I | `ResultLifecycleTests.InterfaceResend_OnAnExpiredSpecimen_IsRefused_AndThePendingValueStands` | URS-BB-470 / FRS-BB-506 / RISK-BB-569 |
| TEST-BB-366 | I | `ResultCorrectionAfterExpiryTests.CorrectionOfAVerifiedResult_CanBeVerified_AfterTheSpecimenExpires`, `APendingFirstResult_IsStillNotVerified_OnAnExpiredSpecimen`, `CorrectionOnARejectedSpecimen_IsStillRefusedAtVerification` | URS-BB-471 / FRS-BB-507 / RISK-BB-570 / OCD-051 |
| TEST-BB-367 | I | `TestWorklistTests.WorklistCorrection_AfterTheSpecimenExpires_CreatesTheCorrectedVersion`, `CorrectionReason_DoesNotLetAFirstResultPost_OnAnExpiredSpecimen`; `ResultCorrectionAfterExpiryTests.VerifiedResult_CanBeInvalidated_AfterTheSpecimenExpires` | URS-BB-471 / FRS-BB-507 / RISK-BB-570 / OCD-051 |
| TEST-BB-368 | D / I | `VerificationDeadlineRuleTests.PendingResult_WithSpecimenExpiringInsideTheWindow_GetsTheDeadline`, `NoDeadline_OutsideTheWindow_AfterExpiry_OrWithoutAnExpiration`, `NoDeadline_ForCorrectionsOrFinishedResults`, `OnlyAPendingCorrection_IsAllowedAnExpiredSpecimen`; `TestWorklistTests.PendingResult_WhoseSpecimenExpiresSoon_NamesTheDeadline_AndComesFirst`, `PendingCorrection_OnAnExpiredSpecimen_CanStillBeWorkedFromTheWorklist` | URS-BB-472 / FRS-BB-508 / RISK-BB-571 |
| TEST-BB-369 | D | `DashboardDueCountTests.PendingTests_CountsOnlyRowsWithAVerificationDeadline` | URS-BB-473 / FRS-BB-509 / RISK-BB-572 |
| TEST-BB-370 | I | `TestWorklistTests.ShiftHandoff_CountsResultsToVerifyBeforeSpecimenExpiry` | URS-BB-474 / FRS-BB-510 / RISK-BB-573 |
| TEST-BB-371 | I | `TestWorklistTests.PatientTestsTab_CarriesTheVerificationDeadline` | URS-BB-475 / FRS-BB-511 / RISK-BB-571 |
| TEST-BB-372 | D | `BloodTypeDtoDeterminationTests.ChartDeterminations_MatchTheServer`, `TheDto_CarriesTheSpecimen` | URS-BB-476 / FRS-BB-512 / RISK-BB-574 |
| TEST-BB-373 | D | `BloodTypeDtoDeterminationTests.ChartDiscrepancy_MatchesTheIssueGate` | URS-BB-476 / FRS-BB-513 / RISK-BB-574 |
| TEST-BB-374 | I | `Phase4IssuingTests.OutstandingIssue_TimeOutDecision_FollowsTheFacilityWindow` | URS-BB-477 / FRS-BB-514 / RISK-BB-575 |
| TEST-BB-375 | I | `Phase3ServicesTests.ExpirationPreview_MatchesTheSave_ForAnAlloRiskPatient`, `ExpirationPreview_WithoutRisk_IsTheCatalogWindow_AndUnknownPatientFails` | URS-BB-478 / FRS-BB-515 / RISK-BB-576 |
| TEST-BB-376 | I | `Phase3ServicesTests.ExpirationPreview_WithValidityHours_MatchesAccession` | URS-BB-478 / FRS-BB-516 / RISK-BB-576 |
| TEST-BB-377 | I | `SeederTests.Seed_DemoPatientSpecimens_ExpireAsPolicySays` | URS-BB-478 / RISK-BB-576 |
| TEST-BB-378 | I | `Phase4IssuingTests.EmergencyRelease_Override_StoresTrimmedFields_AndNeedsAnAuthorizer` | FRS-BB-517 / OCD-041 |
| TEST-BB-379 | I | `InventoryServiceTests.NearExpiry_NamesThePatientAReservedUnitIsHeldFor`; `AlertWorkflowTests.NearExpiryAlert_OnAReservedUnit_NamesThePatient_AndIsAWarning` | URS-BB-479 / FRS-BB-518 / RISK-BB-577 |
| TEST-BB-380 | D | `DashboardDueCountTests.ReadyToIssueCard_NamesExpiringSoonAndExpiredCrossmatches` | URS-BB-479 / FRS-BB-519 / RISK-BB-577 |
| TEST-BB-381 | D / I | `ExceptionCatalogChangeReasonRuleTests.MissingOrShortReason_IsAHardStop`, `EightCharactersAfterTrimming_Passes`; `AdminCatalogTests.ExceptionDefinition_CreateAndUpdate_WriteConfigure`; `ExceptionDefinitionAuthorizationTests.Deactivate_WithoutAdminConfigActivate_IsRejected` | URS-BB-480 / FRS-BB-520 / RISK-BB-578 |
| TEST-BB-382 | D / I | `CompatibilityTableChangeReasonRuleTests.ActiveTable_WithoutAReason_IsAHardStop`, `ActiveTableWithReason_OrDraftTable_Passes`; `CompatibilityRuleAdminTests.RuleInTheActiveTable_ChangesOnlyWithAReason` | URS-BB-481 / FRS-BB-521 / RISK-BB-579 |
| TEST-BB-383 | D / I | `CompatibilityTablePolicyTests.ActiveHardStopRow_MakesAMatchingWarningAHardStop`, `SeededRowsEqualToTheBuiltInDefault_ChangeNothing`, `Table_NeverSoftensAHardStop`, `InactiveRow_OtherComponentClass_OtherCode_OrPass_LeaveTheResultAlone`, `Admin_CannotSaveARowSofterThanTheBuiltInRule`; `CompatibilityRuleValidatorTests.Rule_ValidObject_Passes`; `Phase4IssuingTests.Issue_ActiveCompatibilityTable_MakesAWarningAHardStop_AndNeverSoftens` | URS-BB-482 / FRS-BB-522 / RISK-BB-580 / OCD-052 |
| TEST-BB-384 | D | `CompatibilityTablePolicyTests.DescribeAtIssue_MatchesWhatTheGateDoes` | URS-BB-482 / FRS-BB-523 / RISK-BB-580 |
| TEST-BB-385 | I | `UserAdminAuthorizationTests.AccessChanges_RequireAReason_AndKeepTheBeforeState`, `RolePermissionOrLevelChange_NeedsAReason_DescriptionDoesNot` | URS-BB-483 / FRS-BB-524 / RISK-BB-581 |
| TEST-BB-386 | D / I | `EmergencyPreSpecimenRuleTests.OnlyGroupORedCellsAndAbOrAPlasma`, `Applies_OnlyToEmergency_WithNoUsableSpecimen`, `Gate_EmergencyGroupORedCells_WithoutSpecimen_IsAnOverridableWarning_ThatSaysToCollectOne`, `Gate_EmergencyGroupARedCells_WithoutSpecimen_IsAHardStop`, `Gate_RoutineIssue_WithoutSpecimen_StaysAHardStop`, `Gate_Emergency_WithAnotherPatientsSpecimen_StaysAHardStop`, `Gate_Emergency_WithAnExpiredSpecimen_UsesThePreSpecimenRule`; `Phase4IssuingTests.EmergencyRelease_WithAnExpiredSpecimen_GroupORedCells_IssuesUnderOverride` | URS-BB-484 / FRS-BB-525 / RISK-BB-582 / OCD-053 |
| TEST-BB-387 | I | `Phase4IssuingTests.ReadyToIssue_ExpiredSpecimen_OffersEmergencyOnlyForGroupORedCells` | URS-BB-484 / FRS-BB-526 / RISK-BB-582 |
| TEST-BB-388 | D | `AlertHelpCoverageTests.EverySource_HasAHelpName_AndIsDescribedOnTheAlertsPage`, `ThePageCount_MatchesTheSources` | Alerts help / `AlertRule.HelpName` |
| TEST-BB-389 | I | `ExceptionDefinitionCoverageTests.TheScan_FindsTheKnownOverridePaths`, `EveryCheckedCode_HasAnActiveOverridableSeededDefinition` | Exception catalog / `ExceptionOverridePolicy` |
| TEST-BB-390 | D / I | `RetrospectiveCrossmatchIncompatibleRuleTests.Deviation_IsCritical_AndItsNumberIsNamedInTheWarningAndAlert`; `Phase4IssuingTests.EmergencyRelease_IncompatibleXm_RaisesCriticalAlert_AndLeadsTheBoard`, `EmergencyRelease_CompatibleXm_RaisesNoAlert` | URS-BB-444 / FRS-BB-527 / RISK-BB-546 / OCD-046 |
| TEST-BB-391 | D / I | `OutsideProviderRuleTests`; `OutsideProviderTests.Add_WithPermissionAndReason_CreatesAnOutsideProvider_WithAnAssignedId_AndAuditsTheReason`, `Add_WithoutPermission_IsRefused_AndNothingIsAdded`, `Add_WithoutReason_WithoutName_OrAlreadyListed_IsRefused`, `AnOverrideAuthorizedByTheAddedProvider_IsRecordedAsOutsideProvider` | URS-BB-485 / FRS-BB-528 / RISK-BB-583 / OCD-055 |
| TEST-BB-392 | D / I | `DowntimeBackEntryRuleTests`; `Phase4IssuingTests.DowntimeBackEntry_JudgesExpiryAsOfThePaperTime_AndIsControlledByRole` | URS-BB-486 / FRS-BB-529 / RISK-BB-584 / OCD-054 |
| TEST-BB-393 | D / I | `SpecimenReceiptScanRuleListTests`; `SpecimenReceiptScanTests.ScanRequired_RefusesReceiptFromARow_ButAcceptsTheScan`, `ScanNotRequired_ByDefault_ReceiptFromARowIsAllowed`, `ScanRequired_DuplicateLabel_DoesNotSendTheUserToTheRow` | URS-BB-487 / FRS-BB-530 / OCD-056 |
| TEST-BB-394 | D / I | `DowntimeBackEntryRuleTests.FollowOn_OnlyForABackEnteredIssue_WithThePermission_AndTheRightDin`; `Phase4IssuingTests.DowntimeBackEntry_WardReceiptAndTransfusion_UseThePaperDin_InPlaceOfTheScan` | URS-BB-486 / FRS-BB-531 / RISK-BB-584 / OCD-054 |
| TEST-BB-395 | D / I | `DowntimeBackEntryRuleTests.ReceiptTime_IsRequired_NotBeforeTheIssue_AndNotInTheFuture`; `Phase4IssuingTests.DowntimeBackEntry_WardReceiptAndTransfusion_UseThePaperDin_InPlaceOfTheScan` (In transit row, receipt time checks, stamped time, audit) | URS-BB-486 / FRS-BB-532 / RISK-BB-584 / OCD-054 |
| TEST-BB-396 | I | `DowntimeReconciliationTests.GetSnapshot_CountsOpenWorkAndReportsChain` (back-entered issues: open without receipt, open with receipt, transfused) | URS-BB-486 / FRS-BB-533 / OCD-054 |
| TEST-BB-397 | A | `FacilityPolicyDefaultsCoverageTests.EveryKey_IsInTheCatalog_UnlessRetired`, `EveryGetter_ReadsACatalogKey_AndUsesTheCatalogDefault` | URS-BB-488 / FRS-BB-534 / RISK-BB-585 / OCD-008 |
| TEST-BB-398 | I | `FacilityPolicyStorageTests.Seeding_AnUpgradedDatabase_StoresEveryMissingPolicy_AtItsDefault_WithHistory`, `Seeding_Twice_AddsNothingMore` | URS-BB-488 / FRS-BB-535 / RISK-BB-585 |
| TEST-BB-399 | I | `Phase5Hl7Tests.ResolveQueueItem_ClearsTheRow_WithoutReprocessing`, `Replay_AfterPatientExists_ResolvesMappingError`, `OutboundSender_Aa_ResolvesErrorQueue` | URS-BB-489 / FRS-BB-536 |
| TEST-BB-400 | D / I | `InstrumentMessageFollowUpRuleTests`; `InstrumentInterfaceTests.FailedMessage_ClosesOnASuccessfulReplay_AndProcessedMessages_AreNotReplayed`, `FailedMessage_ResolvedByHand_NeedsAReason_AndIsAudited` | URS-BB-489 / FRS-BB-537 / RISK-BB-586 |
| TEST-BB-401 | A / I | `DashboardQueueCatalogTests.InterfaceCards_FollowTheInterfacePermissions`; `DashboardHomeTests.InterfaceFailures_AreAttention_WhenNonZero_AfterPatientSafetyItems`; `DowntimeReconciliationTests.GetSnapshot_CountsOpenWorkAndReportsChain` | URS-BB-489 / FRS-BB-538 |
| TEST-BB-402 | D / I | `LateReactionReportRuleTests.AValidReport_Passes_WithOrWithoutAnOnset`, `Refusals_SayWhy`; `Phase4IssuingTests.LateReactionReport_OpensAnInvestigation_AndMarksTheTransfusion` | URS-BB-490 / FRS-BB-539 / RISK-BB-587 |
| TEST-BB-403 | I | `SpecialRequirementReactionLinkTests.Requirement_AddedFromAReaction_IsLinked_AndAudited`, `Requirement_CannotLinkAnotherPatientsReaction_OrOneThatDoesNotExist` | URS-BB-490 / FRS-BB-540 / OCD-057 |
| TEST-BB-404 | D | `ReactionNextStepRuleTests.FatalityFollowUp_LeadsEverything_EvenWhenClosed`, `Steps_FollowTheUrgencyOrder`, `ARemainderThatCannotBeHeld_IsNotAskedToBeHeld` | URS-BB-490 / FRS-BB-541 |
| TEST-BB-405 | A | `PendingSpecimenAttentionRuleTests.ExpiredTubes_AreCounted_AndMakeTheCardNeedAttention`, `NoExpiredTubes_KeepsAPlainCount` | URS-BB-487 / FRS-BB-542 |
| TEST-BB-406 | D / I | `QuarantineReleaseRuleTests.OrdinaryHolds_CanBeReleased`, `ReactionRemainder_WaitsForTheInvestigation`, `Expired_RetypeMismatch_AndDisposal_AreNeverReleased_AndSayWhatToDo`; `InventoryServiceTests.QuarantineRelease_FollowsWhyTheUnitIsHeld` | URS-BB-491 / FRS-BB-543 / RISK-BB-588 |
| TEST-BB-407 | D / I | `HoldReleaseRuleTests.Release_NeedsAReason_AndAnInDateUnit`; `InventoryServiceTests.ReleaseFromQuarantineAndHold_WriteProductStatus` | URS-BB-491 / FRS-BB-544 |
| TEST-BB-408 | I | `InventoryServiceTests.MarkMissing_ThenLocate_EntersQuarantine`, `MarkDamaged_ThenInspect_EntersQuarantine`, `LocateAndInspect_WriteProductStatus` | URS-BB-491 / FRS-BB-545 |
| TEST-BB-409 | D / I | `ReactionConclusionRuleTests.Closing_NeedsAReactionAndATreatment_NoneCountsForEach`, `CodesMustBeOnTheActiveList_OfTheRightKind_AndNoTreatmentStandsAlone`, `TheNoneEntries_CannotBeDeactivatedOrRecoded`; `ReactionNextStepRuleTests.AWorkedUpCase_WithoutAConclusion_AsksForIt`; `ReactionCatalogTests.Seeding_GivesNoneOnBothLists_AndTheyCannotBeRemovedOrRecoded`, `Closing_RecordsTheConclusion_FromTheLists_AsTheyReadThen`, `NoReaction_AndNoTreatment_CloseAnInvestigation` | URS-BB-492 / FRS-BB-546 / OCD-057 |
| TEST-BB-410 | D / I | `StorageLocationChangeReasonRuleTests.AReasonOfAtLeastEightCharacters_IsRequired`; `InventoryLocationAuthorizationTests.CreateAndUpdate_WriteConfigure`, `Deactivate_WithoutAdminConfigActivate_IsRejected`, `Deactivate_WhileUnitsAreStored_IsRefused` | URS-BB-483 / FRS-BB-547 |
| TEST-BB-411 | D / I | `ValidatedConfigChangeReasonRuleTests.AReasonOfAtLeastEightCharacters_IsRequired_AndNamesWhatChanged`; `Phase4IssuingTests.TransportContainer_Update_And_Deactivate_NeedAReason`, `MtpActivation_WithInactiveOrUnknownPack_IsRefused` | URS-BB-483 / FRS-BB-548 |
| TEST-BB-412 | D / I | `SpecialRequirementCategoryRuleTests.ARequirementForOneUnitCategory_IgnoresUnitsOfAnother_AndAllCategoriesAppliesToEvery`, `UnitAbo_AcceptsOnlyTheAllowedGroups_AndRhNegative_OnlyRhNegativeUnits`, `UnitTestNegative_IsMetByTheProductCode_OrANegativeTestOnTheUnit_AndNotOtherwise`, `Informational_NeverBlocks_SelectionOrIssue`, `ARequirement_AppliesFromItsStart_AndNoLongerOnOrAfterItsEnd`, `AtIssue_AnUnmetTypingStops_ButOnEmergencyRelease_NeedsTheOverride_WhileProceduresStillStop`, `Validator_PutsEachKindInItsTable_AndAsksForWhatTheKindNeeds`, `ChoiceLabels_NameTheUnitCategory`; `Phase4IssuingTests.Issue_GroupORedCellsOnly_RefusesAGroupARedCell_ButNotGroupAPlasma`, `Issue_UnmetModification_OnEmergencyRelease_IssuesUnderTheRecordedOverride`, `Issue_CmvNegativeTyping_IsMetByANegativeTestRecordedOnTheUnit`; `PatientAllocationTests.ListCompatibleUnits_GroupORedCellsOnly_AppliesToRedCellsOnly_UntilItsEndDate`; `SpecialRequirementAdminTests.Create_InEachTable_AsksForWhatThatTableNeeds`, `Backfill_PlacesEarlierDefinitions_AndKeepsAllCategories`, `ChartDates_AreFacilityMidnight_AndTheEndMustFollowTheStart` | URS-BB-493 / FRS-BB-549, FRS-BB-550 / RISK-BB-589 |
| TEST-BB-413 | D / I | `UnitSpecialTestRuleTests.ACodeAndANegativeOrPositiveResult_AreRequired`, `TheSameResultTwice_IsRefused_AndAChange_NeedsAReason`; `UnitSpecialTestTests.Record_KeepsTheResult_AuditsIt_AndRefusesTheSameResultAgain`, `Correction_NeedsAReason_KeepsHistory_AndTheLatestResultCounts`, `Record_WithoutInventoryReceive_IsRefused` | URS-BB-493 / FRS-BB-551 / RISK-BB-589 |
| TEST-BB-414 | I | `PatientAllocationTests.ListActive_ReservedUnitThatFailsALaterRequirement_SaysWhatItLacks` | URS-BB-493 / FRS-BB-552 / RISK-BB-589 |
| TEST-BB-415 | D | `IssueRunTests.NextForPatient_OffersThatPatientsOtherReadyUnits_OldestReservationFirst`, `UnitsTheBoardWouldNotIssueRoutinely_AreNotOffered_ButCounted` | URS-BB-494 / FRS-BB-553 |
| TEST-BB-416 | D | `IssueRunTests.NextToReceive_OffersTheRestOfTheSameCooler_ForThatPatient_EarliestFirst`, `NextToReceive_WithoutACooler_MatchesTheDestination_AndOnlyUncooledUnits` | URS-BB-494 / FRS-BB-554 |
| TEST-BB-417 | D / I | `Hl7BpsBuilderTests.Dispensed_SendsBpsO29_WithTheUnitTheOrderAndWhereItWent`, `Returned_SendsReturnedUnused_WithoutADestination_AndWithoutAnOrderUsesTheIssue`, `TheDispenseStatus_CanBeTranslatedForASite`; `Phase4IssuingTests.IssueAndReturn_QueueADispenseMessage_OnlyWithAnEnabledBloodProductInterface` | URS-BB-495 / FRS-BB-555 |
| TEST-BB-418 | D | `IssueRunTests.NextAtBedside_PutsUnitsStillTransfusingFirst_ThenReceivedOnes_ForThatPatientOnly` | URS-BB-494 / FRS-BB-556 |
| TEST-BB-419 | D | `IssueRunTests.APickedUnit_FillsItsReservedPatient_OrSaysWhoseItIs` | URS-BB-494 / FRS-BB-557 |
| TEST-BB-420 | D | `IssueRunTests.RoutineIssueBlocks_NameWhatStopsRoutineIssue_AndAreEmptyWhenReady` | URS-BB-494 / FRS-BB-558 |
| TEST-BB-421 | D | `IssueRunTests.AnIssuedUnitsNextStep_FollowsReceiptThenStartThenComplete_AndReturnStopsOnceStarted` | URS-BB-494 / FRS-BB-559 |
| TEST-BB-422 | D / I | `CrossmatchShownStatusTests.ARecordedResult_IsShown_OtherwiseTheClinicalStatus`; `PatientTimelineTests.RecordedCrossmatchResults_AndWardReceipt_AreShown`; `Phase4IssuingTests.Issue_CmvNegativeTyping_IsMetByANegativeTestRecordedOnTheUnit` (records the crossmatch and asserts its clinical status) | URS-BB-494 / FRS-BB-560 / RISK-BB-590 |
| TEST-BB-423 | D | `DashboardQueueCatalogTests.Select_NearExpiry_ForInventoryOrIssue_OpensTheNearExpiryBoard`; `DashboardHomeTests.NearExpiry_IsAnIssueDeskAndSupervisorQueue_AndReservedOnesNeedAttention` | URS-BB-496 / FRS-BB-561 |
| TEST-BB-424 | D | `IssueRunTests.NearExpiry_OffersIssueForAReadyReservedUnit_AndAllocateWhenOrdersAreOpen` | URS-BB-496 / FRS-BB-562 |
| TEST-BB-425 | D / I | `DashboardDueCountTests.LapsedReservations_AreCountedOnTheReadyCard`; `Phase4IssuingTests.LapsedReservation_StaysOnReadyToIssue_UntilReleasedWithAReason` | URS-BB-497 / FRS-BB-563 |
| TEST-BB-426 | D / I | `MinimumStockRuleOpenOrdersTests.OpenOrdersExceedOnHand_OnlyWhenThereAreMoreOrdersThanUnits`; `MinimumStockOpenOrdersTests.OpenOrders_CountOnlyActiveProductLines_OnOpenOrders` | URS-BB-498 / FRS-BB-564 |
| TEST-BB-427 | D | `TestWorkItemBenchActionTests.AnEnteredResult_SaysVerify_WhateverItsSource`, `TheEntererOfAnAboRh_SeesReview_OthersSeeVerify_AndThePromptNamesTheOrigin` | URS-BB-499 / FRS-BB-566 |
| TEST-BB-428 | D | `TestWorkItemBenchActionTests.ToVerify_KeepsWaitingResults_ButNotEmptyBlockedOrOwnAboRhLines` | URS-BB-499 / FRS-BB-567 |
| TEST-BB-429 | D / I | `ShiftHandoffSummaryTests.Summary_NamesTheNewCounts_OnlyWhenAboveZero`; `TestWorklistTests.ShiftHandoff_CountsResultsToVerifyBeforeSpecimenExpiry`; `Phase4IssuingTests.LapsedReservation_StaysOnReadyToIssue_UntilReleasedWithAReason` | URS-BB-500 / FRS-BB-568 |
| TEST-BB-430 | I | `DowntimePatientSummaryTests.Summary_ListsPatientsBeingWorkedFor_WithTypeAntibodiesRequirementsSpecimensAndUnits`; `DowntimePatientSummaryTests.Print_IsAudited_WithThePatientsOnTheSheet` | URS-BB-501 / FRS-BB-569 |
| TEST-BB-431 | I | `TestWorklistTests.DowntimeSnapshot_CountsResultsToVerify_AndThoseDueBeforeSpecimenExpiry`; `Phase4IssuingTests.LapsedReservation_StaysOnReadyToIssue_UntilReleasedWithAReason` | URS-BB-502 / FRS-BB-570 |
| TEST-BB-432 | D | `TestWorkItemBenchActionTests.ForTube_KeepsTheScannedTubesLines_AndFirstToWorkSkipsBlockedOnes` | URS-BB-503 / FRS-BB-571 |
| TEST-BB-433 | D / I | `TestWorkItemBenchActionTests.ForTube_MatchesTheLabelBarcode_AndNamesTheTubeByItsAccession`; `TestWorklistTests.WorklistRow_CarriesTheLabelBarcode_AndALabelScanFindsIt` | URS-BB-503 / FRS-BB-572 |
| TEST-BB-434 | I | `DowntimePatientSummaryTests.OnePatient_IsOnTheSheetEvenWithNothingCurrent_AndThePrintIsAuditedAgainstThem` | URS-BB-501 / FRS-BB-573 |
| TEST-BB-435 | D | `DashboardQueueCatalogTests.ToVerify_ForVerifiersOnly_CountsTheirOwnVerifiableResults_WithTheExpiryNote` | URS-BB-504 / FRS-BB-574 |
| TEST-BB-436 | I | `PatientListFlagsTests.PatientList_ShowsCurrentType_ActiveAntibodies_AndRequirementsInEffect` | URS-BB-505 / FRS-BB-575 |
| TEST-BB-437 | I | `UniversalLookupTests.PatientHit_CarriesTypeAndAlerts_FromTheSurvivingChart` | URS-BB-505 / FRS-BB-576 |
| TEST-BB-438 | D | `PatientPickerLabelTests.PickerLabel_ShowsTypeAndAlerts_AndMarksMergedRecords` | URS-BB-505 / FRS-BB-577 |
| TEST-BB-439 | I | `SeederTests.Seed_PopulatesReferenceAndDemoData_AndIsIdempotent` (special-requirements demo patient: MRN0023 with IRRAD-RBC, CMVNEG-RBC, and CALL-TSMD in effect, typed O+ twice) | SRS-BB-457 |
| TEST-BB-440 | D / I | `PatientListFlagsTests.PatientList_ShowsCurrentType_ActiveAntibodies_AndRequirementsInEffect`; `UniversalLookupTests.PatientHit_CarriesTypeAndAlerts_FromTheSurvivingChart`; `PatientPickerLabelTests.PickerLabel_ShowsTypeAndAlerts_AndMarksMergedRecords` | URS-BB-505 / FRS-BB-578 |
| TEST-BB-441 | I | `Phase4IssuingTests.CompatibilityReserve_RefusesAUnitThatMissesARequirement_ButReservesOneThatMeetsIt` | URS-BB-506 / FRS-BB-579 |
| TEST-BB-442 | D / I | `ReactionWorkupRulesTests.Settings_AcceptOnlyReleasedGroups_AndNeedAReason`; `ReactionWorkupServiceTests.Settings_StartAtTheStarterGroups_AndAcceptOnlyReleasedGroupsWithAReason` | URS-BB-507 / FRS-BB-581 |
| TEST-BB-443 | I | `ReactionWorkupServiceTests.PreSpecimen_OrdersItsTestGroup_OnTheChosenTube_AndOptionsSuggestTheCrossmatchTube`; `ReactionWorkupServiceTests.Attach_RefusesTheWrongTube_AnUnchosenGroup_AndAClosedCase` | URS-BB-507 / FRS-BB-582 |
| TEST-BB-444 | I | `ReactionWorkupServiceTests.ExpiredPreTube_IsUsed_AndThePostTubeIsAccessionedWithItsGroup`; `TestWorklistTests.ExpiredTube_TakesReactionWorkupResults_ButNotOrdinaryOnes` | URS-BB-507 / FRS-BB-583 |
| TEST-BB-445 | D | `ReactionWorkupRulesTests.PreSpecimen_MustBeThePatients_Usable_AndCollectedBeforeTheIssue`; `ReactionWorkupRulesTests.PostSpecimen_MustBeANewAcceptedTube_CollectedAfterTheStart`; `ReactionWorkupRulesTests.ExpiredSpecimens_AreAllowedOnlyForReactionWorkupOrders` | URS-BB-507 / FRS-BB-582, FRS-BB-583 |
| TEST-BB-446 | D / I | `ReactionWorkupRulesTests.CloseGate_NamesTheNextMissingStep_InOrder`; `ReactionInvestigationServiceTests.Close_WaitsForEveryWorkupTestToBeVerified_AndTheBoardShowsTheResults`; `ReactionInvestigationServiceTests.RepeatAboRh_ComesFromTheWorkupResults` | URS-BB-507 / FRS-BB-584 |
| TEST-BB-447 | I | `SeederTests.Seed_PopulatesReferenceAndDemoData_AndIsIdempotent` (MRN0006 workup: ACC0014 pre with ORD0016, ACC0024 post with RXN-n-POST unresulted, settings TRX-PRE and TRX-POST) | URS-BB-507 / FRS-BB-584 |
| TEST-BB-448 | I | `ReactionWorkupServiceTests.CancelledWorkupOrder_FreesTheRole_ForAReplacementTube` | URS-BB-507 / FRS-BB-582 |
| TEST-BB-449 | I | `ReactionInvestigationServiceTests.Report_CarriesTheTransfusionRecordAndWorkup_AndItsPrintIsAudited` | URS-BB-508 / FRS-BB-585 |
| TEST-BB-450 | I | `TestWorklistTests.ExpiredTube_TakesReactionWorkupResults_ButNotOrdinaryOnes` | URS-BB-507 / FRS-BB-586 |
| TEST-BB-451 | I | `TestWorklistTests.RejectedTube_NamesItsWaitingOrders_AndTheBenchAsksForRecollection` | URS-BB-509 / FRS-BB-587 |
| TEST-BB-452 | D | `DashboardQueueCatalogTests.PendingTestsNote_NamesRecollections_AndTheRecollectViewKeepsThem` | URS-BB-509 / FRS-BB-588 |
| TEST-BB-453 | I | `InventoryServiceTests.TransferByScan_MovesTheScannedUnit_AndRefusesWhatItCannotMove` | URS-BB-510 / FRS-BB-589 |
| TEST-BB-454 | I | `InventoryServiceTests.StockCount_ReconcilesScansAgainstTheLocation_AndFinishingIsAudited` | URS-BB-511 / FRS-BB-590 |
| TEST-BB-455 | I | `InventoryServiceTests.StockCount_ReconcilesScansAgainstTheLocation_AndFinishingIsAudited` | URS-BB-511 / FRS-BB-591 |
| TEST-BB-456 | I | `InventoryServiceTests.CoolerCheck_CatchesWrongAndUnissuedUnits_AndFinishingIsAudited` | URS-BB-512 / FRS-BB-592 |
| TEST-BB-457 | U | `CoolerCheckNoticeTests.CoolerCheckNotice_SaysWhetherTheCoolerWasCheckedBeforeItLeft` | URS-BB-512 / FRS-BB-593 |
| TEST-BB-458 | U | `IssueRunTests.NextToReturn_OffersTheRestOfTheCooler_ForThatPatient_NotOnesTransfusing` | URS-BB-513 / FRS-BB-594 |
| TEST-BB-459 | I | `InventoryServiceTests.StockCount_MissingUnit_CanBeMarkedMissing_ThenFoundOnALaterCount` | URS-BB-511 / FRS-BB-595 |
| TEST-BB-460 | U, I | `ShiftHandoffSummaryTests.Summary_NamesCoolersOut_AndTheirChecks`; `ShiftHandoffServiceTests.Create_CountsCoolersInTransit_AndTheirChecks` | URS-BB-512 / FRS-BB-596 |
| TEST-BB-461 | U | `DashboardQueueCatalogTests.InTransitNote_NamesOverdueUnits_AndCoolerChecks` | URS-BB-512 / FRS-BB-597 |
| TEST-BB-462 | I | `SeederTests.Seed_DemoCoolers_ShowEachCheckState_Once` | URS-BB-512 / FRS-BB-592 |
| TEST-BB-463 | U | `DashboardDueCountTests.ReactionsNote_NamesPostSpecimenAndWorkupStillToVerify` | URS-BB-507 / FRS-BB-598 |
| TEST-BB-464 | I, U | `SupervisorQueueServiceTests.List_WithInventoryRelease_IncludesMissingUnit_UntilLocated`; `SupervisorQueueRuleTests.KindSortOrder_PutsAntibodyReviewFirst` | URS-BB-337 / FRS-BB-599 |
| TEST-BB-465 | U, I | `DashboardQueueCatalogTests.NoSpecimen_ListsRushFirst_AndTheNoteCountsStatOrders`; `SeederTests.Seed_PopulatesReferenceAndDemoData_AndIsIdempotent` | URS-BB-509 / FRS-BB-600 |
| TEST-BB-466 | U, I | `PossibleDuplicatePatientRuleTests.SameBirthday_MatchesOnLastNameAndInitial_OrSwappedNames`; `PatientServiceTests.Create_PossibleDuplicate_IsRefusedUntilConfirmed_AndTheConfirmationIsAudited` | URS-BB-514 / FRS-BB-601 |
| TEST-BB-467 | I | `PatientServiceTests.PossibleDuplicatePairs_ListActiveMatches_OlderFirst`; `SeederTests.Seed_PopulatesReferenceAndDemoData_AndIsIdempotent` | URS-BB-514 / FRS-BB-602 |
| TEST-BB-468 | I | `SupplierOrderTests.Reconcile_CountsEachDeliveredUnitOnce`; `SupplierOrderTests.Order_FromMinimumStockShortfall_EditSubmitReceive` | URS-BB-411 / FRS-BB-603 |
| TEST-BB-469 | I | `SeederTests.Seed_PopulatesReferenceAndDemoData_AndIsIdempotent` | URS-BB-411 / FRS-BB-603 |
| TEST-BB-470 | U | `BenchShortcutTests.Shortcut_clicks_only_row_toggles_and_ignores_typing`; `BenchShortcutTests.Worklist_rows_carry_the_toggle_and_entry_markers` | URS-BB-515 / FRS-BB-604 |
| TEST-BB-471 | U | `CrossmatchUnitOfferTests.Offer_IsReservedPlusCompatible_CountedOnce`; `CrossmatchUnitOfferTests.UnreadableCompatibleList_OffersEveryUnit_AndSaysWhy` | URS-BB-506 / FRS-BB-605 |
| TEST-BB-472 | I | `InventoryServiceTests.DiscardExpiredByScan_DiscardsOnlyExpiredUnits_WithTheExpiredReason` | URS-BB-516 / FRS-BB-606 |
| TEST-BB-473 | U | `DashboardHomeTests.ExpiredOnShelf_IsForDiscarders_AndNeedsAttentionAboveZero` | URS-BB-516 / FRS-BB-607 |
| TEST-BB-474 | U, I | `ShiftHandoffSummaryTests.Summary_NamesExpiredUnitsOnTheShelf`; `InventoryServiceTests.DiscardExpiredByScan_DiscardsOnlyExpiredUnits_WithTheExpiredReason` | URS-BB-516 / FRS-BB-608 |
| TEST-BB-475 | I | `InventoryServiceTests.ReturnToSupplierByScan_ReturnsEachUnit_WithTheShipmentReason`; `InventoryServiceTests.StockCount_MissingUnit_CanBeMarkedMissing_ThenFoundOnALaterCount` | URS-BB-517 / FRS-BB-609 |
| TEST-BB-476 | I | `InventoryServiceTests.RepeatedStatusActions_AreRefused_AndKeepTheFirstReason` | URS-BB-517 / FRS-BB-610 |
| TEST-BB-477 | I | `ReportServiceTests.TestTurnaround_MinutesFromOrderAndReceipt_ByPriority`; `ReportServiceTests.SeededReports_Exist_Run_AndCannotBeDeleted` | URS-BB-518 / FRS-BB-611 |
| TEST-BB-478 | I | `DowntimePatientSummaryTests.AntibodyCard_CarriesTypeAndActiveAntibodies_AndPrintIsAudited` | URS-BB-519 / FRS-BB-612 |
| TEST-BB-479 | U, I | `DormantAccountRuleTests.Dormant_WhenActivePersonNotSignedInWithinTheDays`; `UserAdminAuthorizationTests.AccessReview_RecordsActiveAccounts_AndIsReadBack` | URS-BB-520 / FRS-BB-613 |
| TEST-BB-480 | U, I | `IssueRequestServiceTests.Request_ThenCancel_KeepsTheTrail_AndRefusesWhatItShould`; `DashboardDueCountTests.Requested_LeadReadyToIssue_AndAreNamedOnTheCard` | URS-BB-521 / FRS-BB-614 |
| TEST-BB-481 | U | `PatientVisitLocationTests.ReservationVisit_WinsWhenOpen_ElseLatestOpenVisit`; `PatientVisitLocationTests.NoOpenVisitWithALocation_SuggestsNothing` | URS-BB-521 / FRS-BB-615 |
| TEST-BB-482 | U | `PatientVisitLocationTests.ChartVisitList_GivesTheSameChoice` | URS-BB-521 / FRS-BB-616 |
| TEST-BB-483 | I | `Phase4IssuingTests.CompletionOverdue_LeadsTheIssuedList` | URS-BB-451 / FRS-BB-617 |

Layers: D = Domain.Tests, A = Application.Tests, I = Integration.Tests (including `tests/safety_regression`).
