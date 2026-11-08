using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

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
    string Href);

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

    public BloodOrderWorklistService(
        IRepository<Order> orders,
        IRepository<OrderLine> lines,
        IRepository<Allocation> allocations,
        IRepository<Issue> issues,
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IClock clock)
    {
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
            .ToDictionary(g => g.Key, g => g.Max(s => s.ExpiresUtc));
        var types = (await _bloodTypes.ListAsync(t => patientIds.Contains(t.PatientId) && t.IsCurrent && t.InvalidatedUtc == null, ct)).ToLookup(t => t.PatientId);
        var antibodies = (await _antibodies.ListAsync(a => patientIds.Contains(a.PatientId) && a.IsActive, ct)).ToLookup(a => a.PatientId);

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
            var hasSpecimen = usable.TryGetValue(order.PatientId, out var specimenExpires);
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
                requested, orderReserved, orderIssued, remaining, hasSpecimen, hasSpecimen ? specimenExpires : null,
                BloodOrderPreparationRule.NextStep(remaining, hasSpecimen)!,
                hasSpecimen ? $"/patients/{order.PatientId}?tab=products" : $"/patients/{order.PatientId}?tab=specimens"));
        }

        return rows
            .OrderBy(r => r.Rush ? 0 : 1)
            .ThenBy(r => r.OrderedUtc)
            .ToList();
    }
}
