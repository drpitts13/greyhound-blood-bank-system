using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

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
        var trends = TiterTrends(results);

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
                    reason,
                    r.EnteredBy,
                    r.EnteredUtc,
                    r.InvalidatedBy,
                    r.Version > 1 || r.SupersededByResultId is not null || r.Status is ResultStatus.Corrected or ResultStatus.Invalidated,
                    r.Units,
                    r.AbnormalFlag,
                    trends.GetValueOrDefault(r.Id));
            })
            .ToList();
    }

    /// <summary>
    /// For each current titer, the change from the previous current titer of the same test, oldest first. Information
    /// only: what change matters is the facility's (OCD-065).
    /// </summary>
    private static Dictionary<long, string> TiterTrends(IEnumerable<TestResult> results)
    {
        var trends = new Dictionary<long, string>();
        var titers = results
            .Where(r => ResultLifecycleRule.IsCurrentRow(r.SupersededByResultId) && r.Status != ResultStatus.Invalidated)
            .Select(r => (Result: r, Ok: TiterResultValue.TryParse(r.Value, out var titer), Titer: titer))
            .Where(x => x.Ok)
            .GroupBy(x => x.Result.TestCode, StringComparer.OrdinalIgnoreCase);
        foreach (var test in titers)
        {
            var ordered = test.OrderBy(x => x.Result.VerifiedUtc ?? x.Result.EnteredUtc ?? x.Result.CreatedUtc).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                trends[ordered[i].Result.Id] = TiterRule.DescribeChange(
                    previous.Titer!, previous.Result.VerifiedUtc ?? previous.Result.EnteredUtc ?? previous.Result.CreatedUtc, ordered[i].Titer!);
            }
        }

        return trends;
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
