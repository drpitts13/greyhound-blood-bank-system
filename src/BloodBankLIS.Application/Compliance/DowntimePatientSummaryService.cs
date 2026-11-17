using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Compliance;

public sealed record DowntimeUnitLine(
    string UnitNumber,
    string Product,
    string AboRh,
    DateTime UnitExpiresUtc,
    // Reserved for the patient, or issued and not yet returned or transfused.
    bool Issued,
    DateTime? ReservationEndsUtc = null,
    string? IssuedToLocation = null,
    // Pass 158: reserved while still Received, its ABO/Rh retype not confirmed: not to be issued routinely on paper.
    bool RetypePending = false)
{
    public const string RetypePendingText = "ABO/Rh retype not confirmed: retype before issue";
}

/// <summary>
/// A transfusion reaction on the patient's record (end-user testing pass 104: the downtime sheet and the one-patient
/// summary carried antibodies and requirements but no word of a prior reaction, so the bench issuing on paper did not know
/// the patient had reacted, or that a workup was still open).
/// </summary>
public sealed record DowntimeReactionLine(string? ReactionType, ReactionSeverity Severity, DateTime ReportedUtc, bool InvestigationOpen)
{
    /// <summary>"Febrile non-hemolytic (mild); investigation open": the date is shown beside it in local time.</summary>
    public string Text =>
        (string.IsNullOrWhiteSpace(ReactionType) ? "Reaction, type not yet recorded" : ReactionType.Trim())
        + (Severity == ReactionSeverity.Unknown ? "" : $" ({Severity.ToString().ToLowerInvariant()})")
        + (InvestigationOpen ? "; investigation open" : "");
}

public sealed record DowntimeSpecimenLine(string AccessionNumber, DateTime CollectedUtc, DateTime? ExpiresUtc);

public sealed record DowntimePatientRow(
    long PatientId,
    string MedicalRecordNumber,
    string Name,
    DateOnly DateOfBirth,
    // "O+", or "Not on file" when no current type is recorded.
    string AboRh,
    // Two concordant determinations on file (the same check issue uses).
    bool SecondDeterminationOnFile,
    IReadOnlyList<string> Antibodies,
    IReadOnlyList<string> SpecialRequirements,
    IReadOnlyList<string> Informational,
    IReadOnlyList<DowntimeSpecimenLine> Specimens,
    IReadOnlyList<DowntimeUnitLine> Units,
    // RhIG evaluations still needing work (not completed, or a candidate not yet given).
    IReadOnlyList<string>? Rhig = null,
    // Blood orders whose units are not all reserved yet: what to crossmatch and by when.
    IReadOnlyList<string>? BloodOrders = null)
{
    /// <summary>
    /// An ABO/Rh result not yet verified that disagrees with the type on file ("ACC-DEMO-RCV1 reads A+, not yet verified,
    /// against O+ on file"). End-user testing pass 33: the sheet gave the type on file with no word of it.
    /// </summary>
    public IReadOnlyList<string> TypeDiscrepancies { get; init; } = [];

    /// <summary>Transfusion reactions on record, newest first (pass 104).</summary>
    public IReadOnlyList<DowntimeReactionLine> Reactions { get; init; } = [];

    /// <summary>
    /// The reagent lots the ABO/Rh was tested with whose QC failed, the result not yet reviewed ("ANTI-D lot AD-24092";
    /// end-user testing pass 199: the sheet, used when the chart cannot be seen, gave the type with no word of it).
    /// </summary>
    public IReadOnlyList<string> AboRhUnderQcReview { get; init; } = [];

    /// <summary>
    /// For an infant linked to its mother: her MRN and active antibodies, which may be present in the infant (end-user
    /// testing pass 208: the sheet read "None on file" for an infant whose mother has anti-K).
    /// </summary>
    public string? MaternalAntibodies { get; init; }
}

public sealed record DowntimePatientSummary(DateTime GeneratedUtc, IReadOnlyList<DowntimePatientRow> Patients);

/// <summary>
/// The paper copy for an outage: every patient the blood bank is working for now (a specimen not yet expired, a unit
/// reserved, or a unit issued and still out) with what the bench needs to issue safely without the system. Read only;
/// the print is audited because the sheet carries patient data off the system.
/// </summary>
public sealed class DowntimePatientSummaryService
{
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<SpecialTransfusionRequirement> _requirements;
    private readonly IRepository<SpecialRequirementDefinition> _requirementDefinitions;
    private readonly IAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IRepository<RhigEvaluation>? _rhig;
    private readonly BloodBankLIS.Application.PatientWorkspace.BloodOrderWorklistService? _bloodOrders;
    private readonly IRepository<BloodAttributeDefinition>? _attributes;
    private readonly IRepository<TestResult>? _results;
    private readonly IRepository<ReactionInvestigation>? _reactions;
    private readonly BloodBankLIS.Application.Inventory.ReagentQcService? _reagentQc;

    public DowntimePatientSummaryService(
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IRepository<Allocation> allocations,
        IRepository<Issue> issues,
        IRepository<BloodUnit> units,
        IRepository<ProductType> productTypes,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IRepository<SpecialTransfusionRequirement> requirements,
        IRepository<SpecialRequirementDefinition> requirementDefinitions,
        IAuditWriter audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        IRepository<RhigEvaluation>? rhig = null,
        BloodBankLIS.Application.PatientWorkspace.BloodOrderWorklistService? bloodOrders = null,
        IRepository<BloodAttributeDefinition>? attributes = null,
        IRepository<TestResult>? results = null,
        IRepository<ReactionInvestigation>? reactions = null,
        BloodBankLIS.Application.Inventory.ReagentQcService? reagentQc = null)
    {
        _reagentQc = reagentQc;
        _reactions = reactions;
        _results = results;
        _attributes = attributes;
        _bloodOrders = bloodOrders;
        _rhig = rhig;
        _patients = patients;
        _specimens = specimens;
        _allocations = allocations;
        _issues = issues;
        _units = units;
        _productTypes = productTypes;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _requirements = requirements;
        _requirementDefinitions = requirementDefinitions;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <param name="patientId">
    /// One patient, from their chart (for example before a planned outage or a transfer): that patient is on the sheet
    /// whether or not the blood bank is working for them now. Null lists everyone being worked for.
    /// </param>
    public async Task<DowntimePatientSummary> GetAsync(long? patientId = null, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var specimens = (await _specimens.ListAsync(
                s => s.Status != SpecimenStatus.Rejected
                    && s.Status != SpecimenStatus.Expired
                    && s.Status != SpecimenStatus.Cancelled
                    && (patientId == null || s.PatientId == patientId),
                ct))
            .Where(s => s.ExpiresUtc is null || s.ExpiresUtc > now)
            .ToList();
        var reserved = await _allocations.ListAsync(
            a => a.Status == AllocationStatus.Reserved && (patientId == null || a.PatientId == patientId), ct);
        var issued = await _issues.ListAsync(
            i => i.Status == IssueStatus.Issued && (patientId == null || i.PatientId == patientId), ct);
        // An RhIG evaluation still needing work puts the patient on the sheet: the dose may fall due during the outage.
        var rhigOpen = _rhig is null
            ? []
            : (await _rhig.ListAsync(
                e => (e.CompletedUtc == null || (e.Decision == RhigDecision.Candidate && e.GivenUtc == null))
                    && (patientId == null || e.PatientId == patientId), ct)).ToList();

        // A blood order still to prepare puts the patient on the sheet: the bench may have to crossmatch on paper.
        var ordersToPrepare = _bloodOrders is null
            ? []
            : (await _bloodOrders.ListAsync(ct)).Where(o => patientId == null || o.PatientId == patientId).ToList();

        var patientIds = patientId is long one
            ? new HashSet<long> { one }
            : specimens.Select(s => s.PatientId)
                .Concat(reserved.Select(a => a.PatientId))
                .Concat(issued.Select(i => i.PatientId))
                .Concat(rhigOpen.Select(e => e.PatientId))
                .Concat(ordersToPrepare.Select(o => o.PatientId))
                .ToHashSet();
        if (patientIds.Count == 0)
        {
            return new DowntimePatientSummary(now, []);
        }

        var patients = (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct))
            .Where(p => p.MergedIntoPatientId is null)
            .ToList();
        var unitIds = reserved.Select(a => a.BloodProductId).Concat(issued.Select(i => i.BloodProductId)).ToHashSet();
        var units = (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id);
        var productNames = (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
        var types = (await _bloodTypes.ListAsync(t => patientIds.Contains(t.PatientId), ct)).ToLookup(t => t.PatientId);
        var antibodyRows = await _antibodies.ListAsync(a => patientIds.Contains(a.PatientId), ct);
        // Pass 208: the mothers of infants on the sheet, and their active antibodies.
        var motherIds = patients.Where(p => p.MotherPatientId is not null).Select(p => p.MotherPatientId!.Value).Distinct().ToList();
        var mothers = motherIds.Count == 0 ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => motherIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var maternalAntibodies = motherIds.Count == 0 ? Array.Empty<AntibodyHistory>().ToLookup(a => a.PatientId)
            : (await _antibodies.ListAsync(a => motherIds.Contains(a.PatientId) && a.IsActive, ct)).ToLookup(a => a.PatientId);
        var antibodies = antibodyRows.Where(a => a.IsActive).ToLookup(a => a.PatientId);
        // The gate's antigen-negative requirements from every antibody on the history, current or historical.
        var allAntibodies = antibodyRows.ToLookup(a => a.PatientId);
        var attributeDefinitions = _attributes is null
            ? new Dictionary<long, BloodAttributeDefinition>()
            : (await _attributes.ListAsync(d => d.IsActive, ct)).ToDictionary(d => d.Id);
        var requirements = (await _requirements.ListAsync(r => patientIds.Contains(r.PatientId), ct))
            .ToLookup(r => r.PatientId);
        var definitions = (await _requirementDefinitions.ListAsync(ct)).ToDictionary(d => d.Id);

        DowntimeUnitLine? UnitLine(long unitId, bool isIssued, DateTime? reservationEnds, string? location) =>
            units.TryGetValue(unitId, out var u)
                ? new DowntimeUnitLine(
                    u.UnitNumber,
                    productNames.GetValueOrDefault(u.ProductTypeId) ?? "Unknown product",
                    AboRhText(u.Abo, u.RhD),
                    u.ExpiresUtc,
                    isIssued,
                    reservationEnds,
                    location,
                    RetypePending: !isIssued && u.Status == UnitStatus.Received)
                : null;

        // ABO/Rh results entered but not verified, on the patients' current specimens.
        var specimenAccessions = specimens.ToDictionary(sp => sp.Id, sp => sp.AccessionNumber);
        var pendingTypes = _results is null
            ? []
            : (await _results.ListAsync(r => patientIds.Contains(r.PatientId) && r.TestCode == "ABORH"
                    && (r.Status == ResultStatus.Entered || r.Status == ResultStatus.PendingVerification), ct))
                .Where(r => specimenAccessions.ContainsKey(r.SpecimenId))
                .ToList();

        // Pass 104: prior transfusion reactions, and whether a workup is still open.
        var reactionRows = _reactions is null
            ? []
            : await _reactions.ListAsync(r => patientIds.Contains(r.PatientId), ct);
        var reactionsByPatient = reactionRows.ToLookup(r => r.PatientId);

        var rows = patients.Select(p =>
            {
                var history = types[p.Id].ToList();
                var current = history.FirstOrDefault(t => t.IsCurrent && t.InvalidatedUtc is null);
                var active = requirements[p.Id]
                    .Select(r => SpecialRequirementDto.From(
                        r, r.RequirementDefinitionId is { } id ? definitions.GetValueOrDefault(id) : null, now))
                    .Where(r => r.IsClinicallyActive)
                    .ToList();
                var unitLines = reserved.Where(a => a.PatientId == p.Id)
                    .Select(a => UnitLine(a.BloodProductId, false, a.ExpiresUtc, null))
                    .Concat(issued.Where(i => i.PatientId == p.Id)
                        .Select(i => UnitLine(i.BloodProductId, true, null, i.IssuedToLocation)))
                    .OfType<DowntimeUnitLine>()
                    .OrderBy(l => l.Issued)
                    .ThenBy(l => l.UnitNumber, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new DowntimePatientRow(
                    p.Id,
                    p.MedicalRecordNumber,
                    $"{p.LastName}, {p.FirstName}",
                    p.DateOfBirth,
                    current is null ? "Not on file" : AboRhText(current.Abo, current.RhD),
                    SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(history)),
                    // An antibody known only from another facility's report says so; found here as well, it reads plain.
                    // The same wording as the patient list and find box (one definition).
                    BloodBankLIS.Application.Patients.PatientBloodBankFlags.ActiveAntibodies(antibodies[p.Id]),
                    BloodBankLIS.Application.Compatibility.BloodAttributeCompatLoader
                        .AntigenNegativeRequirements(allAntibodies[p.Id].ToList(), attributeDefinitions)
                        .Concat(active.Where(r => !r.IsInformational).Select(RequirementText).Order(StringComparer.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    active.Where(r => r.IsInformational)
                        .Select(r => string.IsNullOrWhiteSpace(r.Instruction) ? r.Name ?? string.Empty : r.Instruction!)
                        .ToList(),
                    specimens.Where(s => s.PatientId == p.Id)
                        .OrderByDescending(s => s.CollectedUtc)
                        .Select(s => new DowntimeSpecimenLine(s.AccessionNumber, s.CollectedUtc, s.ExpiresUtc))
                        .ToList(),
                    unitLines,
                    rhigOpen.Where(e => e.PatientId == p.Id)
                        .OrderBy(e => e.EventUtc)
                        .Select(e => RhigEvaluationRule.OutstandingLine(
                            e.Context, e.EventUtc, e.Decision, e.VialsRecommended, e.CompletedUtc is not null, e.GivenUtc is not null))
                        .OfType<string>()
                        .ToList(),
                    ordersToPrepare.Where(o => o.PatientId == p.Id)
                        .Select(o => BloodOrderPreparationRule.DowntimeLine(o.OrderNumber, Words(o.Priority.ToString()), o.Products, o.Remaining, o.NeededByUtc))
                        .ToList())
                {
                    TypeDiscrepancies = current is null
                        ? []
                        : pendingTypes.Where(r => r.PatientId == p.Id)
                            .Select(r => AboRhResultValue.TryParse(r.Value, out var pending)
                                         && pending.IsKnown
                                         && AboRhDeltaRule.Evaluate(current.BloodType, pending).Severity != RuleSeverity.Pass
                                ? $"{specimenAccessions[r.SpecimenId]} reads {pending}, not yet verified, against {AboRhText(current.Abo, current.RhD)} on file"
                                : null)
                            .OfType<string>()
                            .ToList(),
                    MaternalAntibodies = p.MotherPatientId is long motherId && mothers.TryGetValue(motherId, out var mother)
                        && BloodBankLIS.Application.Patients.PatientBloodBankFlags.ActiveAntibodies(maternalAntibodies[motherId]) is { Count: > 0 } maternal
                        ? $"Mother {mother.MedicalRecordNumber}: {string.Join(", ", maternal)}"
                        : null,
                    Reactions = reactionsByPatient[p.Id]
                        .OrderByDescending(r => r.ReportedUtc)
                        .Select(r => new DowntimeReactionLine(
                            r.ReactionType, r.Severity, r.ReportedUtc, OpenReactionInvestigationRule.NeedsAttention(r.Status)))
                        .ToList()
                };
            })
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.MedicalRecordNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_reagentQc is not null)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var lots = (await _reagentQc.PatientResultsUnderReviewAsync(rows[i].PatientId, ct))
                    .Where(r => string.Equals(r.TestCode, "ABORH", StringComparison.OrdinalIgnoreCase))
                    .Select(r => $"{r.ReagentCode} lot {r.LotNumber}")
                    .Distinct()
                    .ToList();
                if (lots.Count > 0)
                {
                    rows[i] = rows[i] with { AboRhUnderQcReview = lots };
                }
            }
        }

        return new DowntimePatientSummary(now, rows);
    }

    /// <summary>A code name in words: "EmergencyRelease" reads "Emergency release".</summary>
    private static string Words(string code)
    {
        var parts = System.Text.RegularExpressions.Regex.Split(code, "(?<=[a-z0-9])(?=[A-Z])").Where(x => x.Length > 0).ToList();
        return string.Join(' ', parts.Select((x, i) => i == 0 ? x : x.ToLowerInvariant()));
    }

    /// <summary>Records who printed the sheet and which patients it carried, before the browser prints it.</summary>
    public async Task<int> RecordPrintedAsync(long? patientId = null, CancellationToken ct = default)
    {
        var summary = await GetAsync(patientId, ct);
        _audit.Record(
            AuditEventType.Print,
            "DowntimePatientSummary",
            patientId,
            newValue: new
            {
                Document = patientId is null ? "Downtime patient summary" : "Downtime patient summary (one patient)",
                Patients = summary.Patients.Count,
                PatientIds = summary.Patients.Select(p => p.PatientId).ToList()
            },
            reason: "Downtime patient summary printed.");
        await _unitOfWork.SaveChangesAsync(ct);
        return summary.Patients.Count;
    }

    /// <summary>
    /// The patient's antibody card: current ABO/Rh, active antibodies, and special requirements in effect, for the patient
    /// to carry to any hospital. Null when the patient is not found or merged away.
    /// </summary>
    public async Task<DowntimePatientRow?> GetAntibodyCardAsync(long patientId, CancellationToken ct = default) =>
        (await GetAsync(patientId, ct)).Patients.SingleOrDefault();

    /// <summary>
    /// Records the card print (who, which antibodies and requirements) before the browser prints it. A patient with no
    /// antibody and no requirement in effect has nothing to carry, so no card is printed.
    /// </summary>
    public async Task<OperationResult<DowntimePatientRow>> RecordAntibodyCardPrintedAsync(long patientId, CancellationToken ct = default)
    {
        var card = await GetAntibodyCardAsync(patientId, ct);
        if (card is null)
        {
            return OperationResult<DowntimePatientRow>.Fail("Patient not found.");
        }

        if (card.Antibodies.Count == 0 && card.SpecialRequirements.Count == 0)
        {
            return OperationResult<DowntimePatientRow>.Fail("This patient has no antibody and no special requirement on file, so there is nothing to put on a card.");
        }

        _audit.Record(
            AuditEventType.Print,
            nameof(Patient),
            patientId,
            newValue: new
            {
                Document = "Antibody card",
                card.MedicalRecordNumber,
                card.AboRh,
                card.Antibodies,
                card.SpecialRequirements
            },
            reason: "Patient antibody card printed.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<DowntimePatientRow>.Ok(card);
    }

    /// <summary>"Irradiated", "Antigen-negative (K)": a requirement as the downtime sheet and the blood order worklist show it.</summary>
    internal static string RequirementText(SpecialRequirementDto r) =>
        SpecialRequirementLabel.Display(r.Name, r.RequirementType, r.AntigenCode);

    private static string AboRhText(AboGroup abo, RhType rh) =>
        abo == AboGroup.Unknown && rh == RhType.Unknown ? "Not on file" : new Domain.ValueObjects.AboRh(abo, rh).ToString();
}
