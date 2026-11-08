using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>How far one order's units are covered: ordered, reserved, issued, still needed, and the line to show.</summary>
public sealed record BloodOrderProgressDto(long OrderId, string OrderNumber, int Requested, int Reserved, int Issued, int Remaining, string Message);

/// <summary>An open blood order with units still to prepare.</summary>
public sealed record BloodOrderToPrepareDto(
    long OrderId,
    string OrderNumber,
    long PatientId,
    string PatientName,
    string Mrn,
    string? CurrentBloodType,
    IReadOnlyList<string> ActiveAntibodies,
    OrderPriority Priority,
    bool Rush,
    DateTime OrderedUtc,
    string Products,
    int Requested,
    int Reserved,
    int Issued,
    int Remaining,
    bool HasUsableSpecimen,
    DateTime? SpecimenExpiresUtc,
    string NextStep,
    string Href,
    // The usable specimen the reserve form starts with (one with a completed type and screen when there is one), or null.
    long? UsableSpecimenId = null,
    // A usable specimen is on file but its ABO/Rh and antibody screen are not both verified yet.
    bool TypeAndScreenPending = false,
    // When it is needed, and whether that time has passed.
    DateTime? NeededByUtc = null,
    bool NeededByPassed = false,
    // The patient's clinically active special transfusion requirements (not informational ones): what the units must be.
    IReadOnlyList<string>? Requirements = null,
    // The needed-by time is within the facility's warning window (OCD-075; off by default), and how many minutes away.
    bool NeededBySoon = false,
    int? NeededInMinutes = null,
    // The usable specimen expires before the needed-by time: a new specimen is needed for the units to be issuable then.
    bool SpecimenExpiresBeforeNeeded = false);

/// <summary>
/// Blood orders to prepare, across patients: open orders whose product lines are not yet covered by units reserved or
/// issued on them, rush priorities first, then oldest. The bench's list of what to crossmatch and reserve next.
/// </summary>
public sealed class BloodOrderWorklistService
{
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderLine> _lines;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IClock _clock;
    private readonly BloodBankLIS.Application.Compatibility.AntibodyScreenCompatLoader? _typeAndScreen;
    private readonly IRepository<SpecialTransfusionRequirement>? _requirements;
    private readonly BloodBankLIS.Application.Compliance.FacilityPolicyService? _policy;
    private readonly IRepository<BloodBankLIS.Domain.Entities.Configuration.SpecialRequirementDefinition>? _requirementDefinitions;

    public BloodOrderWorklistService(
        IRepository<Order> orders,
        IRepository<OrderLine> lines,
        IRepository<Allocation> allocations,
        IRepository<Issue> issues,
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IClock clock,
        BloodBankLIS.Application.Compatibility.AntibodyScreenCompatLoader? typeAndScreen = null,
        IRepository<SpecialTransfusionRequirement>? requirements = null,
        IRepository<BloodBankLIS.Domain.Entities.Configuration.SpecialRequirementDefinition>? requirementDefinitions = null,
        BloodBankLIS.Application.Compliance.FacilityPolicyService? policy = null)
    {
        _policy = policy;
        _requirements = requirements;
        _requirementDefinitions = requirementDefinitions;
        _typeAndScreen = typeAndScreen;
        _orders = orders;
        _lines = lines;
        _allocations = allocations;
        _issues = issues;
        _patients = patients;
        _specimens = specimens;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _clock = clock;
    }

    /// <summary>One order's progress (also when it is fully covered), or null when it is not a patient's order.</summary>
    public async Task<BloodOrderProgressDto?> GetProgressAsync(long orderId, CancellationToken ct = default)
    {
        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            return null;
        }

        var requested = (await _lines.ListAsync(l => l.OrderId == orderId && l.IsActive && l.LineCategory == OrderCategory.Product
            && (l.FulfillmentStatus == FulfillmentStatus.Ordered || l.FulfillmentStatus == FulfillmentStatus.PartiallyFulfilled), ct)).Count;
        var reserved = (await _allocations.ListAsync(a => a.OrderId == orderId && a.Status == AllocationStatus.Reserved, ct)).Count;
        var issued = (await _issues.ListAsync(i => i.OrderId == orderId && i.Status != IssueStatus.Returned, ct)).Count;
        var remaining = BloodOrderPreparationRule.Remaining(requested, reserved, issued);
        return new BloodOrderProgressDto(order.Id, order.OrderNumber, requested, reserved, issued, remaining,
            BloodOrderPreparationRule.ReserveProgress(requested, remaining));
    }

    public async Task<IReadOnlyList<BloodOrderToPrepareDto>> ListAsync(CancellationToken ct = default)
    {
        var productLines = await _lines.ListAsync(l =>
            l.IsActive && l.LineCategory == OrderCategory.Product
            && (l.FulfillmentStatus == FulfillmentStatus.Ordered || l.FulfillmentStatus == FulfillmentStatus.PartiallyFulfilled), ct);
        if (productLines.Count == 0)
        {
            return [];
        }

        var orderIds = productLines.Select(l => l.OrderId).Distinct().ToList();
        var orders = (await _orders.ListAsync(o => orderIds.Contains(o.Id)
                && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Discontinued, ct))
            .ToDictionary(o => o.Id);
        if (orders.Count == 0)
        {
            return [];
        }

        var openIds = orders.Keys.ToList();
        var reserved = (await _allocations.ListAsync(a => a.OrderId != null && openIds.Contains(a.OrderId.Value) && a.Status == AllocationStatus.Reserved, ct))
            .GroupBy(a => a.OrderId!.Value).ToDictionary(g => g.Key, g => g.Count());
        var issued = (await _issues.ListAsync(i => i.OrderId != null && openIds.Contains(i.OrderId.Value) && i.Status != IssueStatus.Returned, ct))
            .GroupBy(i => i.OrderId!.Value).ToDictionary(g => g.Key, g => g.Count());

        var patientIds = orders.Values.Select(o => o.PatientId).Distinct().ToList();
        var patients = (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var now = _clock.UtcNow;
        var usable = (await _specimens.ListAsync(s => patientIds.Contains(s.PatientId)
                && (s.Status == SpecimenStatus.Accepted || s.Status == SpecimenStatus.Received)
                && s.DiscardedUtc == null && (s.ExpiresUtc == null || s.ExpiresUtc > now), ct))
            .GroupBy(s => s.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.ExpiresUtc ?? DateTime.MaxValue).ThenByDescending(s => s.Id).ToList());
        // Units are reserved on a specimen with a completed type and screen (the reserve form offers only those).
        var completed = new Dictionary<long, HashSet<long>>();
        if (_typeAndScreen is not null)
        {
            foreach (var patientId in usable.Keys)
            {
                completed[patientId] = await _typeAndScreen.ListCompletedTypeAndScreenSpecimenIdsAsync(patientId, now, ct);
            }
        }
        var types = (await _bloodTypes.ListAsync(t => patientIds.Contains(t.PatientId) && t.IsCurrent && t.InvalidatedUtc == null, ct)).ToLookup(t => t.PatientId);
        var antibodies = (await _antibodies.ListAsync(a => patientIds.Contains(a.PatientId) && a.IsActive, ct)).ToLookup(a => a.PatientId);
        var requirementText = await RequirementsAsync(patientIds, now, ct);
        var warnMinutes = _policy is null ? 0 : await _policy.GetBloodOrderNeededBySoonMinutesAsync(ct);

        var rows = new List<BloodOrderToPrepareDto>();
        foreach (var group in productLines.Where(l => orders.ContainsKey(l.OrderId)).GroupBy(l => l.OrderId))
        {
            var order = orders[group.Key];
            var requested = group.Count();
            var orderReserved = reserved.GetValueOrDefault(order.Id);
            var orderIssued = issued.GetValueOrDefault(order.Id);
            var remaining = BloodOrderPreparationRule.Remaining(requested, orderReserved, orderIssued);
            if (remaining == 0)
            {
                continue;
            }

            patients.TryGetValue(order.PatientId, out var patient);
            var hasSpecimen = usable.TryGetValue(order.PatientId, out var candidates);
            var ready = !hasSpecimen ? null
                : _typeAndScreen is null ? candidates![0]
                : candidates!.FirstOrDefault(s => completed.GetValueOrDefault(order.PatientId)?.Contains(s.Id) == true);
            var specimen = ready ?? (hasSpecimen ? candidates![0] : null);
            var typeAndScreenComplete = ready is not null;
            // A new specimen is needed only when even the latest-expiring usable one ends before the order is needed.
            var expiresBeforeNeeded = hasSpecimen && OrderNeededByRule.ExpiresBeforeNeeded(candidates![0].ExpiresUtc, order.NeededByUtc, now);
            var products = string.Join(", ", group
                .GroupBy(l => l.LineName, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"{g.Count()} × {g.Key}"));
            rows.Add(new BloodOrderToPrepareDto(
                order.Id, order.OrderNumber, order.PatientId,
                patient is null ? $"Patient {order.PatientId}" : $"{patient.LastName}, {patient.FirstName}",
                patient?.MedicalRecordNumber ?? "—",
                PatientBloodBankFlags.CurrentBloodType(types[order.PatientId]),
                PatientBloodBankFlags.ActiveAntibodies(antibodies[order.PatientId]),
                order.Priority, TestWorklistView.IsRush(order.Priority), order.OrderedUtc, products,
                requested, orderReserved, orderIssued, remaining, hasSpecimen, specimen?.ExpiresUtc,
                BloodOrderPreparationRule.NextStep(remaining, hasSpecimen, typeAndScreenComplete, expiresBeforeNeeded)!,
                BloodOrderPreparationRule.NextStepHref(order.PatientId, order.Id, specimen?.Id, typeAndScreenComplete, expiresBeforeNeeded),
                specimen?.Id,
                hasSpecimen && !typeAndScreenComplete,
                order.NeededByUtc,
                OrderNeededByRule.Passed(order.NeededByUtc, now),
                requirementText.GetValueOrDefault(order.PatientId) ?? [],
                OrderNeededByRule.DueSoon(order.NeededByUtc, now, warnMinutes),
                OrderNeededByRule.DueSoon(order.NeededByUtc, now, warnMinutes) ? OrderNeededByRule.MinutesUntil(order.NeededByUtc, now) : null,
                expiresBeforeNeeded));
        }

        // Rush first, then by when it is needed (orders without a time after those with one), then the oldest.
        return rows
            .OrderBy(r => r.Rush ? 0 : 1)
            .ThenBy(r => r.NeededByUtc ?? DateTime.MaxValue)
            .ThenBy(r => r.OrderedUtc)
            .ToList();
    }

    /// <summary>Each patient's clinically active, non-informational special requirements, worded as on the downtime sheet.</summary>
    private async Task<Dictionary<long, List<string>>> RequirementsAsync(List<long> patientIds, DateTime now, CancellationToken ct)
    {
        if (_requirements is null)
        {
            return [];
        }

        var rows = await _requirements.ListAsync(r => patientIds.Contains(r.PatientId) && r.IsActive, ct);
        var definitionIds = rows.Select(r => r.RequirementDefinitionId).OfType<long>().Distinct().ToList();
        var definitions = _requirementDefinitions is null || definitionIds.Count == 0
            ? new Dictionary<long, BloodBankLIS.Domain.Entities.Configuration.SpecialRequirementDefinition>()
            : (await _requirementDefinitions.ListAsync(d => definitionIds.Contains(d.Id), ct)).ToDictionary(d => d.Id);
        return rows
            .Select(r => Compliance.SpecialRequirementDto.From(r, r.RequirementDefinitionId is { } id ? definitions.GetValueOrDefault(id) : null, now))
            .Where(r => r.IsClinicallyActive && !r.IsInformational)
            .GroupBy(r => r.PatientId)
            .ToDictionary(g => g.Key, g => g.Select(Compliance.DowntimePatientSummaryService.RequirementText)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList());
    }
}
