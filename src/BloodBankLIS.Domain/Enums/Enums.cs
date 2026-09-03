namespace BloodBankLIS.Domain.Enums;

public enum Sex
{
    Unknown = 0,
    Male = 1,
    Female = 2,
    Other = 3
}

public enum AboGroup
{
    Unknown = 0,
    O = 1,
    A = 2,
    B = 3,
    AB = 4
}

public enum RhType
{
    Unknown = 0,
    Positive = 1,
    Negative = 2
}

public enum PatientStatus
{
    Active = 0,
    Merged = 1,
    Inactive = 2
}

public enum SpecimenStatus
{
    Collected = 0,
    Received = 1,
    Accepted = 2,
    Rejected = 3,
    Expired = 4,
    Cancelled = 5
}

public enum OrderType
{
    TypeAndScreen = 0,
    Crossmatch = 1,
    AntibodyIdentification = 2,
    AntigenTyping = 3,
    DirectAntiglobulinTest = 4,
    AboRh = 5,
    AntibodyScreen = 6,
    TransfusionReactionWorkup = 7,
    Other = 99
}

public enum OrderCategory
{
    Test = 0,
    Product = 1,
    Mixed = 2
}

public enum OrderPriority
{
    Routine = 0,
    Stat = 1,
    Timed = 2,
    Urgent = 3,
    EmergencyRelease = 4,
    MassiveTransfusionProtocol = 5,
    PreOp = 6,
    OutpatientScheduled = 7
}

public enum OrderStatus
{
    New = 0,
    InProcess = 1,
    Completed = 2,
    Cancelled = 3,
    Collected = 4,
    Received = 5,
    PartiallyComplete = 6,
    Discontinued = 7,
    OnHold = 8
}

public enum FulfillmentStatus
{
    Ordered = 0,
    PartiallyFulfilled = 1,
    Complete = 2,
    Cancelled = 3
}

public enum EncounterType
{
    Inpatient = 0,
    Outpatient = 1,
    Emergency = 2,
    Observation = 3,
    Ambulatory = 4,
    SameDaySurgery = 5,
    RecurringOutpatient = 6,
    Unknown = 99
}

public enum EncounterStatus
{
    Active = 0,
    Discharged = 1,
    Cancelled = 2,
    PreAdmit = 3,
    Historical = 4
}

/// <summary>Event types surfaced in the patient product history read model (not persisted).</summary>
public enum PatientProductHistoryEventType
{
    Assigned = 0,
    Allocated = 1,
    Crossmatched = 2,
    Issued = 3,
    Returned = 4,
    Transfused = 5,
    PartiallyTransfused = 6,
    Wasted = 7,
    Discarded = 8,
    Cancelled = 9
}

public enum OrderSource
{
    Manual = 0,
    Hl7 = 1
}

public enum ResultStatus
{
    Pending = 0,
    Entered = 1,
    Verified = 2,
    Corrected = 3
}

public enum BloodTypeSource
{
    TestResult = 0,
    Hl7 = 1,
    ManualEntry = 2,
    HistoricalImport = 3
}

public enum AntibodyStatus
{
    Identified = 0,
    Suspected = 1,
    HistoricalOnly = 2
}

/// <summary>
/// Lifecycle states for a blood unit. Allowed transitions are enforced by the
/// transition guard (see <c>InventoryStatusTransition</c> and docs/safety-rules.md).
/// Existing integer values (0–7) are preserved for database compatibility; new
/// primary and exception states are appended.
/// INSTITUTIONAL_POLICY_REVIEW: confirm facility-specific exception-state usage.
/// </summary>
public enum UnitStatus
{
    Quarantine = 0,
    Available = 1,
    /// <summary>Legacy synonym retained for open allocations; prefer Assigned/Crossmatched.</summary>
    Allocated = 2,
    Issued = 3,
    Transfused = 4,
    Returned = 5,
    Discarded = 6,
    Expired = 7,
    Expected = 8,
    Received = 9,
    Selected = 10,
    Assigned = 11,
    Crossmatched = 12,
    TransfusionStarted = 13,
    ReturnPending = 14,
    Transferred = 15,
    Recalled = 16,
    Missing = 17,
    Damaged = 18,
    CancelledAssignment = 19,
    TransfusionStopped = 20,
    /// <summary>
    /// Terminal state: the unit was consumed as the source of a product modification
    /// (divide/pool/irradiate/thaw/volume-reduce/leukoreduce) and replaced by one or
    /// more result units. See <c>UnitModification</c> / docs/safety-rules.md.
    /// </summary>
    Modified = 21,
    /// <summary>
    /// Operational hold (paperwork, pending review, reserved investigation). Distinct
    /// from <see cref="Quarantine"/>, which is a quality/safety disposition.
    /// Not issuable until released back to Available or escalated to Quarantine.
    /// </summary>
    OnHold = 22,
    /// <summary>
    /// Terminal consignee/supplier disposition (SoftBank/SafeTrace return-to-vendor).
    /// Distinct from <see cref="Returned"/> (ward return to inventory) and from
    /// <see cref="CancelledAssignment"/> (packing list cancelled; unit never arrived).
    /// Not issuable.
    /// </summary>
    ReturnedToSupplier = 23
}

/// <summary>
/// SoftBank/SafeTrace coded appearance at receipt. Only <see cref="Acceptable"/>
/// may enter inventory when visual inspection is required.
/// </summary>
public enum UnitAppearance
{
    Acceptable = 0,
    Clots = 1,
    Hemolysis = 2,
    Discoloration = 3,
    Lipemia = 4,
    Leaking = 5,
    LabelIllegible = 6,
    OtherDefect = 7
}

/// <summary>How a blood-component identity was entered into the LIS.</summary>
public enum ComponentEntrySource
{
    Scanner = 0,
    Manual = 1,
    Interface = 2,
    Migration = 3
}

/// <summary>Detected entry mode for a single input string (auto-detected, not user-toggle alone).</summary>
public enum IsbtInputMode
{
    HumanReadable = 0,
    ScannedIsbt = 1
}

/// <summary>ISBT 128 data-structure family recognized by the classifier.</summary>
public enum IsbtDataStructureKind
{
    Unknown = 0,
    DonationIdentificationNumber = 1,
    AboRhd = 2,
    ProductCode = 3,
    ExpirationDate = 4,
    ExpirationDateTime = 5,
    CollectionDate = 6,
    CollectionDateTime = 7,
    ExtendedDivision = 8
}

/// <summary>Patient–component assignment pathway (not a generic “linked” flag).</summary>
public enum AssignmentType
{
    Reservation = 0,
    ElectronicCrossmatch = 1,
    SerologicCrossmatch = 2,
    EmergencyRelease = 3,
    NoCrossmatchRequired = 4
}

/// <summary>Outcome of the table-driven compatibility rules engine.</summary>
public enum CompatibilityOutcome
{
    Compatible = 0,
    Incompatible = 1,
    RequiresOverride = 2
}

/// <summary>Crossmatch / issue pathway selected by compatibility evaluation.</summary>
public enum CompatibilityPathway
{
    NoCrossmatch = 0,
    ElectronicCrossmatch = 1,
    SerologicImmediateSpin = 2,
    SerologicAhg = 3,
    EmergencyRelease = 4
}

/// <summary>Distinct from Compatible — emergency-release units are not marked compatible.</summary>
public enum CrossmatchClinicalStatus
{
    Compatible = 0,
    Incompatible = 1,
    NotPerformed = 2,
    NotCrossmatchedEmergency = 3,
    Expired = 4
}

public enum CrossmatchMethod
{
    Serologic = 0,
    Electronic = 1,
    ImmediateSpin = 2,
    Ahg = 3
}

public enum CrossmatchResult
{
    NotPerformed = 0,
    Compatible = 1,
    Incompatible = 2
}

public enum AllocationStatus
{
    Reserved = 0,
    Released = 1,
    Consumed = 2,
    Expired = 3
}

public enum IssueType
{
    Standard = 0,
    EmergencyRelease = 1,
    MassiveTransfusion = 2
}

public enum IssueStatus
{
    Issued = 0,
    Returned = 1,
    Transfused = 2
}

public enum TransfusionDisposition
{
    Completed = 0,
    Stopped = 1,
    Wasted = 2,
    Returned = 3
}

public enum OverrideAction
{
    WarningOverride = 0,
    EmergencyRelease = 1
}

/// <summary>
/// How to resolve an ABO/Rh delta between a verified result and historical type.
/// </summary>
public enum AboRhHistoryResolution
{
    /// <summary>Keep the patient's current historical ABO/Rh; do not flip IsCurrent.</summary>
    Retain = 1,

    /// <summary>Replace historical ABO/Rh with the verified result (append + flip IsCurrent).</summary>
    Replace = 2
}

public enum BillingTriggerType
{
    TestVerified = 0,
    UnitIssued = 1,
    Procedure = 2
}

/// <summary>Which catalog produced a captured billing event.</summary>
public enum BillingChargeSourceKind
{
    ChargeRule = 0,
    TestService = 1,
    Product = 2
}

public enum BillingEventStatus
{
    Pending = 0,
    Reviewed = 1,
    Exported = 2,
    Cancelled = 3
}

public enum PrintJobType
{
    SpecimenLabel = 0,
    CompatibilityTag = 1,
    ProductLabel = 2,
    Armband = 3
}

public enum PrintJobStatus
{
    Queued = 0,
    Printed = 1,
    Failed = 2
}

public enum LabelFormat
{
    Zpl = 0,
    Preview = 1
}

public enum Hl7Direction
{
    Inbound = 0,
    Outbound = 1
}

public enum Hl7MessageStatus
{
    Received = 0,
    Processed = 1,
    Errored = 2,
    Acked = 3,
    Nacked = 4,
    Replayed = 5
}

public enum InterfaceTransport
{
    Mllp = 0,
    File = 1
}

/// <summary>Clinical purpose of an HL7 interface endpoint.</summary>
public enum InterfaceType
{
    Adt = 0,
    Billing = 1,
    Orders = 2,
    Results = 3,
    Bpam = 4
}

/// <summary>Whether field mappings were applied from a vendor preset or edited by hand.</summary>
public enum InterfaceMappingMode
{
    Vendor = 0,
    Custom = 1
}

/// <summary>Whether a value-translation row applies to inbound, outbound, or both directions.</summary>
public enum InterfaceTranslationDirection
{
    Inbound = 0,
    Outbound = 1,
    Both = 2
}

public enum ComponentClass
{
    RedBloodCells = 0,
    Plasma = 1,
    Platelets = 2,
    Cryoprecipitate = 3,
    WholeBlood = 4,
    Granulocytes = 5,
    Other = 99
}

public enum LocationType
{
    Refrigerator = 0,
    Freezer = 1,
    Issue = 2,
    Transit = 3,
    External = 4
}

/// <summary>
/// Types of audited actions. Every clinical create/update/verify/issue/return/
/// discard/override/reprint produces an <c>AuditEvent</c> (see docs/architecture.md 4.1).
/// </summary>
public enum AuditEventType
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Verify = 3,
    Correct = 4,
    Issue = 5,
    Return = 6,
    Discard = 7,
    Override = 8,
    Reprint = 9,
    Activate = 10,
    Deactivate = 11,
    Clone = 12,
    Import = 13,
    Export = 14,
    Configure = 15,
    Modify = 16,
    Login = 17,
    Logout = 18,
    Lockout = 19,
    SignatureFailed = 20,
    Lookback = 21,
    ReactionInvestigation = 22,
    Deviation = 23
}

/// <summary>
/// A blood-product modification: transforms one or more source units into one or
/// more result units under an admin-configured <c>ModificationRule</c>.
/// Divide is 1 source -&gt; N results; Pool is N sources -&gt; 1 result; the rest are
/// 1 source -&gt; 1 result. See docs/erd.md and docs/workflows.md.
/// </summary>
public enum ModificationType
{
    Divide = 0,
    Pool = 1,
    Irradiate = 2,
    Thaw = 3,
    VolumeReduction = 4,
    Leukoreduction = 5,

    /// <summary>Saline washing to remove plasma proteins, for example for IgA deficient recipients.</summary>
    Wash = 6
}

/// <summary>Whether a unit participated in a <c>UnitModification</c> as an input or an output.</summary>
public enum ModificationUnitRole
{
    Source = 0,
    Result = 1
}

/// <summary>
/// Anchor for an <c>ExpirationModificationCode</c> offset: either the modification
/// date/time or the unit's collection date/time.
/// </summary>
public enum ExpirationRelativeTo
{
    ModificationDateTime = 0,
    CollectionDateTime = 1
}

/// <summary>
/// Action recorded against a versioned configuration record in
/// <c>ConfigurationChangeHistory</c>. Mirrors the admin lifecycle.
/// </summary>
public enum ConfigChangeAction
{
    Create = 0,
    Update = 1,
    Activate = 2,
    Deactivate = 3,
    Clone = 4,
    Import = 5,
    Export = 6,
    Approve = 7
}

/// <summary>Broad category of a blood bank test definition (see docs/architecture.md).</summary>
public enum TestCategory
{
    AboRh = 0,
    AntibodyScreen = 1,
    AntibodyIdentification = 2,
    DirectAntiglobulinTest = 3,
    Elution = 4,
    Crossmatch = 5,
    AntigenTyping = 6,
    ProductModification = 7,
    TransfusionReactionInvestigation = 8,
    AboRhRetype = 9,
    Other = 99
}

/// <summary>How a test definition's result value is captured/validated.</summary>
public enum ResultValueType
{
    Coded = 0,
    Numeric = 1,
    FreeText = 2,
    AboRh = 3,
    Subtest = 4,
    BloodAttribute = 5,
    Crossmatch = 6,
    ComplexCrossmatch = 7
}

/// <summary>
/// Presentation status for an active patient product allocation (does not replace
/// <see cref="AllocationStatus"/>).
/// </summary>
public enum ProductAllocationDisplayStatus
{
    Reserved = 0,
    Exception = 1,
    ReadyForIssue = 2
}

/// <summary>Antigen typing result on a patient or unit.</summary>
public enum AntigenResult
{
    Positive = 0,
    Negative = 1,
    NotTested = 2
}

/// <summary>Whether a blood attribute record represents an antigen or antibody.</summary>
public enum BloodAttributeKind
{
    Antigen = 0,
    Antibody = 1
}

/// <summary>How a panel subtest result is captured at result entry.</summary>
public enum SubtestResultType
{
    GradedReaction = 0,
    FreeText = 1,
    PickList = 2
}

/// <summary>Positive/negative classification for graded-reaction choices used in interpretation logic.</summary>
public enum ReactionPolarity
{
    Negative = 0,
    Positive = 1,
    Neutral = 2
}

/// <summary>
/// How an interpretation logic row compares entered cell/phase reactions.
/// </summary>
public enum InterpretationMatchMode
{
    AllMatch = 0,
    AnyPositive = 1
}

/// <summary>
/// Which workflow stage a configurable <c>RuleDefinition</c> is evaluated at.
/// Order rules run when an order is created or updated; test rules run when a
/// result is verified. See docs/safety-rules.md.
/// </summary>
public enum RuleLevel
{
    Order = 0,
    Test = 1
}

/// <summary>Action a matched <c>RuleDefinition</c> performs.</summary>
public enum RuleActionKind
{
    /// <summary>Add a test line to the order if it is not already present.</summary>
    AddTest = 0,

    /// <summary>Deactivate a pending test line on the order.</summary>
    CancelTest = 1,

    /// <summary>Surface an overridable warning to the operator.</summary>
    Warn = 2,

    /// <summary>Hard-stop the order (order-level rules only).</summary>
    Block = 3
}

/// <summary>Workflow area a configurable exception rule applies to (used in later phases).</summary>
public enum WorkflowArea
{
    ResultEntry = 0,
    ResultVerification = 1,
    SpecimenAcceptance = 2,
    UnitSelection = 3,
    Crossmatch = 4,
    Allocation = 5,
    Issue = 6,
    Return = 7,
    Discard = 8,
    EmergencyRelease = 9,
    LabelPrinting = 10,
    PtagReprint = 11,
    Hl7Processing = 12,
    Billing = 13
}

/// <summary>Patient-level special transfusion requirement (AABB special needs / irradiated, CMV-neg, etc.).</summary>
public enum SpecialTransfusionRequirementType
{
    Irradiated = 0,
    CmvNegative = 1,
    Leukoreduced = 2,
    Washed = 3,
    AntigenNegative = 4,
    Other = 99
}

/// <summary>Independent identifier used to confirm patient or specimen identity.</summary>
public enum IdentityTokenType
{
    MedicalRecordNumber = 0,
    DateOfBirth = 1,
    FullName = 2,
    AccountNumber = 3,
    Other = 99
}

public enum ReactionInvestigationStatus
{
    Open = 0,
    UnderReview = 1,
    Closed = 2
}

public enum ReactionSeverity
{
    Unknown = 0,
    Mild = 1,
    Moderate = 2,
    Severe = 3,
    Fatal = 4
}

public enum FatalityNotificationStatus
{
    NotApplicable = 0,
    Pending = 1,
    CberNotified = 2,
    WrittenReportSubmitted = 3
}

/// <summary>Post-transfusion DAT recorded on the AABB reaction workup checklist.</summary>
public enum DatWorkupResult
{
    NotRecorded = 0,
    Negative = 1,
    Positive = 2,
    NotPerformed = 3
}

public enum LookbackNotificationStatus
{
    Pending = 0,
    Attempted = 1,
    Completed = 2,
    NotApplicable = 3
}

public enum DeviationStatus
{
    Open = 0,
    UnderReview = 1,
    CorrectiveAction = 2,
    Closed = 3
}

public enum DeviationSeverity
{
    Minor = 0,
    Major = 1,
    Critical = 2
}

public enum ElectronicSignatureAuthenticationMethod
{
    Password = 0,
    Pin = 1,
    FederatedStepUp = 2,
    DevMode = 3
}
