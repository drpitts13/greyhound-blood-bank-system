using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// Read-only list of the patient's current verified test results.
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
        var results = await _results.ListAsync(
            r => r.PatientId == patientId
                 && r.SupersededByResultId == null
                 && r.Status == ResultStatus.Verified,
            ct);

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
            .OrderByDescending(r => r.VerifiedUtc ?? r.EnteredUtc ?? r.CreatedUtc)
            .Select(r =>
            {
                var accession = specimens.TryGetValue(r.SpecimenId, out var specimen)
                    ? specimen.AccessionNumber
                    : string.Empty;
                var orderNumber = r.OrderId.HasValue && orders.TryGetValue(r.OrderId.Value, out var order)
                    ? order.OrderNumber
                    : null;
                var testName = testNames.TryGetValue(r.TestCode, out var name) ? name : r.TestCode;
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
                    r.VerifiedBy);
            })
            .ToList();
    }

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
