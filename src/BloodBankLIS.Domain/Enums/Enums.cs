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
/// </summary>
public enum UnitStatus
{
    Quarantine = 0,
    Available = 1,
    Allocated = 2,
    Issued = 3,
    Transfused = 4,
    Returned = 5,
    Discarded = 6,
    Expired = 7
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
    Configure = 15
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
