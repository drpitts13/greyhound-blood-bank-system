using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Results;

/// <summary>
/// Queries order-driven test work items for patient tabs and the global pending worklist.
/// </summary>
public sealed class TestWorklistService
{
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderLine> _orderLines;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<SpecimenTypeDefinition> _specimenTypes;
    private readonly IClock _clock;

    public TestWorklistService(
        IRepository<Order> orders,
        IRepository<OrderLine> orderLines,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<Specimen> specimens,
        IRepository<Patient> patients,
        IRepository<TestResult> results,
        IRepository<TestDefinition> testDefinitions,
        IRepository<SpecimenTypeDefinition> specimenTypes,
        IClock clock)
    {
        _orders = orders;
        _orderLines = orderLines;
        _orderSpecimens = orderSpecimens;
        _specimens = specimens;
        _patients = patients;
        _results = results;
        _testDefinitions = testDefinitions;
        _specimenTypes = specimenTypes;
        _clock = clock;
    }

    public Task<IReadOnlyList<TestWorkItemDto>> ListForPatientAsync(
        long patientId,
        TestWorklistFilter filter = TestWorklistFilter.Pending,
        string? search = null,
        CancellationToken ct = default) =>
        ListInternalAsync(patientId, specimenId: null, filter, pendingOnly: false, search, ct);

    public Task<IReadOnlyList<TestWorkItemDto>> ListForSpecimenAsync(
        long specimenId,
        TestWorklistFilter filter = TestWorklistFilter.All,
        CancellationToken ct = default) =>
        ListInternalAsync(patientId: null, specimenId, filter, pendingOnly: false, search: null, ct);

    public Task<IReadOnlyList<TestWorkItemDto>> ListPendingGlobalAsync(CancellationToken ct = default) =>
        ListInternalAsync(patientId: null, specimenId: null, TestWorklistFilter.Pending, pendingOnly: true, search: null, ct);

    private async Task<IReadOnlyList<TestWorkItemDto>> ListInternalAsync(
        long? patientId,
        long? specimenId,
        TestWorklistFilter filter,
        bool pendingOnly,
        string? search,
        CancellationToken ct)
    {
        if (specimenId is > 0)
        {
            var specimen = await _specimens.GetByIdAsync(specimenId.Value, ct);
            if (specimen is null)
            {
                return Array.Empty<TestWorkItemDto>();
            }

            patientId ??= specimen.PatientId;
            if (specimen.PatientId != patientId)
            {
                return Array.Empty<TestWorkItemDto>();
            }
        }

        var orders = patientId.HasValue
            ? await _orders.ListAsync(o => o.PatientId == patientId.Value, ct)
            : await _orders.ListAsync(ct);

        if (specimenId is > 0)
        {
            var linkedOrderIds = (await _orderSpecimens.ListAsync(os => os.SpecimenId == specimenId.Value, ct))
                .Select(os => os.OrderId)
                .ToHashSet();
            orders = orders.Where(o => linkedOrderIds.Contains(o.Id)).ToList();
        }

        if (pendingOnly)
        {
            orders = orders
                .Where(o => o.Status is not (OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued or OrderStatus.OnHold))
                .ToList();
        }

        var orderIds = orders.Select(o => o.Id).ToList();
        if (orderIds.Count == 0)
        {
            return Array.Empty<TestWorkItemDto>();
        }

        var lines = await _orderLines.ListAsync(
            l => orderIds.Contains(l.OrderId) && l.IsActive && l.LineCategory == OrderCategory.Test, ct);
        var links = await _orderSpecimens.ListAsync(os => orderIds.Contains(os.OrderId), ct);
        var specimenIds = links.Select(l => l.SpecimenId).Distinct().ToList();
        if (specimenId is > 0 && !specimenIds.Contains(specimenId.Value))
        {
            specimenIds.Add(specimenId.Value);
        }
        var specimens = specimenIds.Count == 0
            ? new Dictionary<long, Specimen>()
            : (await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct)).ToDictionary(s => s.Id);

        var patientIds = orders.Select(o => o.PatientId).Distinct().ToList();
        var patients = (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id);

        var primarySpecimenByOrder = links
            .GroupBy(l => l.OrderId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsPrimary).First().SpecimenId);

        var orderById = orders.ToDictionary(o => o.Id);
        var testCodes = lines.Select(l => l.TestCode!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var testDefs = testCodes.Count == 0
            ? new Dictionary<string, TestDefinition>(StringComparer.OrdinalIgnoreCase)
            : (await _testDefinitions.ListAsync(t => t.IsActive && testCodes.Contains(t.Code), ct))
                .GroupBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Version).First(), StringComparer.OrdinalIgnoreCase);
        var specimenTypeDefs = (await _specimenTypes.ListAsync(t => t.IsActive && !t.IsDraft, ct))
            .ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        var results = await _results.ListAsync(
            r => r.OrderId != null && orderIds.Contains(r.OrderId.Value), ct);
        var currentResults = BuildCurrentResults(results);

        var items = new List<TestWorkItemDto>();
        foreach (var line in lines.OrderByDescending(l => orderById[l.OrderId].OrderedUtc).ThenBy(l => l.LineNumber))
        {
            if (string.IsNullOrWhiteSpace(line.TestCode))
            {
                continue;
            }

            var order = orderById[line.OrderId];
            patients.TryGetValue(order.PatientId, out var patient);

            long resolvedSpecimenId;
            if (specimenId is > 0)
            {
                resolvedSpecimenId = specimenId.Value;
            }
            else
            {
                primarySpecimenByOrder.TryGetValue(order.Id, out resolvedSpecimenId);
            }

            Specimen? specimen = null;
            if (resolvedSpecimenId > 0)
            {
                specimens.TryGetValue(resolvedSpecimenId, out specimen);
            }

            var resultKey = BuildResultKey(order.Id, line.TestCode, resolvedSpecimenId);
            currentResults.TryGetValue(resultKey, out var current);

            var isPending = current is null
                || current.Status is ResultStatus.Entered or ResultStatus.Corrected or ResultStatus.PendingVerification or ResultStatus.Invalidated;
            var isCompleted = current?.Status == ResultStatus.Verified;

            if (filter == TestWorklistFilter.Pending && !isPending)
            {
                continue;
            }

            if (filter == TestWorklistFilter.Completed && !isCompleted)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                var matches =
                    line.LineName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || line.TestCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || order.OrderNumber.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (specimen?.AccessionNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
                if (!matches)
                {
                    continue;
                }
            }

            var (canEnter, blockReason) = EvaluateEntryGate(
                specimen,
                line.TestCode!,
                testDefs.GetValueOrDefault(line.TestCode!)?.RequiredSpecimenType,
                specimenTypeDefs);
            items.Add(new TestWorkItemDto(
                line.Id,
                order.Id,
                order.PatientId,
                patient is null ? "—" : $"{patient.LastName}, {patient.FirstName}",
                patient?.MedicalRecordNumber ?? "—",
                line.TestCode,
                line.LineName,
                order.OrderNumber,
                order.Priority,
                order.OrderedUtc,
                specimen?.AccessionNumber,
                resolvedSpecimenId > 0 ? resolvedSpecimenId : null,
                specimen?.Status,
                current?.Id,
                current?.Status,
                current?.Value,
                current?.Interpretation,
                current?.Source,
                canEnter,
                blockReason));
        }

        return items;
    }

    private (bool CanEnter, string? BlockReason) EvaluateEntryGate(
        Specimen? specimen,
        string testCode,
        string? requiredSpecimenType,
        IReadOnlyDictionary<string, SpecimenTypeDefinition> specimenTypeDefs)
    {
        if (specimen is null)
        {
            return (false, "No specimen is linked to this order. Accession a specimen and link it before entering results.");
        }

        if (specimen.Status != SpecimenStatus.Accepted)
        {
            return (false, $"Specimen {specimen.AccessionNumber} is {specimen.Status}; only Accepted specimens allow result entry.");
        }

        if (specimen.ExpiresUtc.HasValue && specimen.ExpiresUtc.Value <= _clock.UtcNow)
        {
            return (false, $"Specimen {specimen.AccessionNumber} has expired.");
        }

        var excluded = specimenTypeDefs.TryGetValue(specimen.SpecimenType, out var typeDef)
            ? SpecimenTypeExcludedTests.Parse(typeDef.ExcludedTestCodesJson).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var compatibility = SpecimenTypeCompatibilityRule.Evaluate(
            specimen.SpecimenType,
            testCode,
            requiredSpecimenType,
            excluded);

        if (compatibility.IsHardStopped)
        {
            return (false, compatibility.HardStops.First().Message);
        }

        return (true, null);
    }

    private static string BuildResultKey(long orderId, string testCode, long specimenId) =>
        $"{orderId}|{testCode.ToUpperInvariant()}|{specimenId}";

    private static Dictionary<string, TestResult> BuildCurrentResults(IReadOnlyList<TestResult> results) =>
        results
            .Where(r => r.OrderId.HasValue && r.SupersededByResultId == null)
            .GroupBy(r => BuildResultKey(r.OrderId!.Value, r.TestCode, r.SpecimenId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Version).First());
}
