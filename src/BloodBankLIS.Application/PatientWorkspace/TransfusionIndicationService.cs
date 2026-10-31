using System.Globalization;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

public sealed record ProductIndicationInputDto(long ProductTypeId, string? IndicationCode = null, string? Justification = null);

public sealed record IndicationOptionDto(string Code, string Name);

public sealed record ProductIndicationResultDto(
    long ProductTypeId,
    string ProductCode,
    ComponentClass ComponentClass,
    PbmStatus Status,
    string Detail,
    string? MetCode,
    bool ReasonRequired,
    IReadOnlyList<IndicationOptionDto> Options);

public sealed record PbmCheckRequest(IReadOnlyList<long> ProductTypeIds, IReadOnlyList<ProductIndicationInputDto>? Indications = null);

public sealed record PbmReviewRowDto(
    long OrderId,
    string OrderNumber,
    DateTime OrderedUtc,
    string Mrn,
    string ProductCode,
    string OrderingLocation,
    string OrderingProvider,
    string? Detail,
    string? Justification);

/// <summary>
/// Patient-blood-management ordering check (OCD-038). Off unless the facility turns on
/// <c>Order.PbmEvaluate</c>; criteria and thresholds come only from the facility's catalog.
/// </summary>
public sealed class TransfusionIndicationService
{
    private static readonly ResultStatus[] FinalStatuses = [ResultStatus.Verified, ResultStatus.Corrected];

    private readonly IRepository<TransfusionIndicationCriterion> _criteria;
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<ProductType> _products;
    private readonly IRepository<OrderLine> _lines;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<OrderingLocation> _locations;
    private readonly FacilityPolicyService _policy;
    private readonly IClock _clock;

    public TransfusionIndicationService(
        IRepository<TransfusionIndicationCriterion> criteria,
        IRepository<TestResult> results,
        IRepository<ProductType> products,
        IRepository<OrderLine> lines,
        IRepository<Order> orders,
        IRepository<Patient> patients,
        IRepository<OrderingLocation> locations,
        FacilityPolicyService policy,
        IClock clock)
    {
        _criteria = criteria;
        _results = results;
        _products = products;
        _lines = lines;
        _orders = orders;
        _patients = patients;
        _locations = locations;
        _policy = policy;
        _clock = clock;
    }

    /// <summary>One result per product type; NotEvaluated for everything when evaluation is off.</summary>
    public async Task<IReadOnlyList<ProductIndicationResultDto>> EvaluateAsync(
        long patientId,
        IReadOnlyCollection<long> productTypeIds,
        DateTime orderedUtc,
        IReadOnlyCollection<ProductIndicationInputDto>? indications,
        CancellationToken ct = default)
    {
        var ids = productTypeIds.Distinct().ToList();
        var products = (await _products.ListAsync(p => ids.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var evaluate = await _policy.GetPbmEvaluateOrdersAsync(ct);
        var requireReason = evaluate && await _policy.GetPbmRequireJustificationAsync(ct);
        var criteria = evaluate ? await _criteria.ListAsync(c => c.IsActive, ct) : [];
        var labs = evaluate ? await LoadLabsAsync(patientId, criteria, orderedUtc, ct) : [];

        var output = new List<ProductIndicationResultDto>();
        foreach (var id in ids)
        {
            if (!products.TryGetValue(id, out var product))
            {
                continue;
            }

            var forClass = criteria.Where(c => c.ComponentClass == product.ComponentClass).ToList();
            var options = forClass
                .Where(c => !TransfusionIndicationRule.IsLaboratory(c))
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
                .Select(c => new IndicationOptionDto(c.Code, c.Name))
                .ToList();
            if (!evaluate)
            {
                output.Add(new ProductIndicationResultDto(id, product.ProductCode, product.ComponentClass, PbmStatus.NotEvaluated, string.Empty, null, false, options));
                continue;
            }

            var input = indications?.FirstOrDefault(i => i.ProductTypeId == id);
            var outcome = TransfusionIndicationRule.Evaluate(forClass, labs, orderedUtc, input?.IndicationCode);
            var reason = TransfusionIndicationRule.EvaluateJustification(outcome.Status, requireReason, input?.Justification);
            output.Add(new ProductIndicationResultDto(
                id, product.ProductCode, product.ComponentClass, outcome.Status, outcome.Detail, outcome.MetCriterionCode,
                reason.Severity == RuleSeverity.HardStop, options));
        }

        return output;
    }

    /// <summary>Product order lines recorded outside criteria in the period, for PBM review.</summary>
    public async Task<IReadOnlyList<PbmReviewRowDto>> ListOutsideCriteriaAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var to = toUtc ?? _clock.UtcNow;
        var from = fromUtc ?? to.AddDays(-30);
        var orders = (await _orders.ListAsync(o => o.OrderedUtc >= from && o.OrderedUtc <= to, ct)).ToDictionary(o => o.Id);
        if (orders.Count == 0)
        {
            return [];
        }

        var orderIds = orders.Keys.ToList();
        var lines = await _lines.ListAsync(l => orderIds.Contains(l.OrderId) && l.PbmStatus == PbmStatus.OutsideCriteria, ct);
        var productIds = lines.Where(l => l.ProductTypeId != null).Select(l => l.ProductTypeId!.Value).Distinct().ToList();
        var products = (await _products.ListAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id, p => p.ProductCode);
        var patientIds = orders.Values.Select(o => o.PatientId).Distinct().ToList();
        var mrns = (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id, p => p.MedicalRecordNumber);
        var locations = (await _locations.ListAsync(ct)).ToDictionary(l => l.Id, l => l.Name);

        return lines
            .Select(l =>
            {
                var order = orders[l.OrderId];
                return new PbmReviewRowDto(
                    order.Id,
                    order.OrderNumber,
                    order.OrderedUtc,
                    mrns.GetValueOrDefault(order.PatientId, string.Empty),
                    l.ProductTypeId is long pid ? products.GetValueOrDefault(pid, string.Empty) : string.Empty,
                    locations.GetValueOrDefault(order.OrderingLocationId, string.Empty),
                    string.IsNullOrWhiteSpace(order.OrderingProvider) ? "(no provider)" : order.OrderingProvider,
                    l.PbmDetail,
                    l.PbmJustification);
            })
            .OrderByDescending(r => r.OrderedUtc)
            .ToList();
    }

    private async Task<IReadOnlyList<IndicationLabValue>> LoadLabsAsync(
        long patientId,
        IReadOnlyCollection<TransfusionIndicationCriterion> criteria,
        DateTime orderedUtc,
        CancellationToken ct)
    {
        var codes = criteria.Where(TransfusionIndicationRule.IsLaboratory)
            .Select(c => c.LabTestCode!.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        if (codes.Count == 0)
        {
            return [];
        }

        var since = orderedUtc.AddHours(-TransfusionIndicationRule.MaxLookbackHours);
        var rows = await _results.ListAsync(
            r => r.PatientId == patientId
                 && FinalStatuses.Contains(r.Status)
                 && r.SupersededByResultId == null
                 && r.VerifiedUtc != null && r.VerifiedUtc >= since,
            ct);
        return rows
            .Where(r => codes.Contains(r.TestCode.Trim().ToUpperInvariant()))
            .Select(r => decimal.TryParse(r.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? new IndicationLabValue(r.TestCode, value, r.Units, r.VerifiedUtc!.Value)
                : null)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();
    }
}
