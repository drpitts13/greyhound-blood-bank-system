using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// Read-only longitudinal list of the patient's released test results,
/// including superseded verified versions retained after correction or invalidation.
/// </summary>
public sealed class PatientTestHistoryService
{
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<TestDefinition> _testDefinitions;

    public PatientTestHistoryService(
        IRepository<TestResult> results,
        IRepository<Specimen> specimens,
        IRepository<Order> orders,
        IRepository<TestDefinition> testDefinitions)
    {
        _results = results;
        _specimens = specimens;
        _orders = orders;
        _testDefinitions = testDefinitions;
    }

    public async Task<IReadOnlyList<PatientTestHistoryRowDto>> ListByPatientAsync(
        long patientId,
        CancellationToken ct = default)
    {
        var all = await _results.ListAsync(r => r.PatientId == patientId, ct);
        var results = all.Where(r => r.VerifiedUtc is not null).ToList();

        if (results.Count == 0)
        {
            return Array.Empty<PatientTestHistoryRowDto>();
        }

        var specimens = (await _specimens.ListAsync(s => s.PatientId == patientId, ct))
            .ToDictionary(s => s.Id);
        var orders = (await _orders.ListAsync(o => o.PatientId == patientId, ct))
            .ToDictionary(o => o.Id);
        var testNames = ResolveTestNames(await _testDefinitions.ListAsync(ct));

        return results
            .OrderByDescending(r => ResultLifecycleRule.IsCurrentRow(r.SupersededByResultId))
            .ThenByDescending(r => r.VerifiedUtc ?? r.EnteredUtc ?? r.CreatedUtc)
            .ThenByDescending(r => r.Version)
            .Select(r =>
            {
                var accession = specimens.TryGetValue(r.SpecimenId, out var specimen)
                    ? specimen.AccessionNumber
                    : string.Empty;
                var orderNumber = r.OrderId.HasValue && orders.TryGetValue(r.OrderId.Value, out var order)
                    ? order.OrderNumber
                    : null;
                var testName = testNames.TryGetValue(r.TestCode, out var name) ? name : r.TestCode;
                var successor = r.SupersededByResultId is long nextId
                    ? all.FirstOrDefault(s => s.Id == nextId)
                    : null;
                var reason = FirstReason(r.InvalidationReason, r.CorrectionReason,
                    successor?.InvalidationReason, successor?.CorrectionReason);
                return new PatientTestHistoryRowDto(
                    r.Id,
                    r.VerifiedUtc ?? r.EnteredUtc ?? r.CreatedUtc,
                    r.TestCode,
                    testName,
                    r.Value,
                    r.Interpretation,
                    accession,
                    r.SpecimenId,
                    r.OrderId,
                    orderNumber,
                    r.Version,
                    r.VerifiedBy,
                    r.Status,
                    r.Source,
                    ResultLifecycleRule.IsCurrentRow(r.SupersededByResultId),
                    reason);
            })
            .ToList();
    }

    private static string? FirstReason(params string?[] reasons) =>
        reasons.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));

    private static Dictionary<string, string> ResolveTestNames(IReadOnlyList<TestDefinition> definitions)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in definitions
                     .OrderByDescending(d => d.IsActive && !d.IsDraft)
                     .ThenByDescending(d => d.Id))
        {
            if (!names.ContainsKey(def.Code))
            {
                names[def.Code] = def.Name;
            }
        }

        return names;
    }
}
