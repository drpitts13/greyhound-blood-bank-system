using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compatibility;

/// <summary>
/// Resolves whether a patient has a verified positive antibody screen (current or historical).
/// </summary>
public sealed class AntibodyScreenCompatLoader
{
    public const string AntibodyScreenTestCode = "ABSC";
    public const string AboRhTestCode = "ABORH";

    private readonly IRepository<TestResult> _results;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<SpecialTransfusionRequirement>? _specialRequirements;
    private readonly IRepository<SpecialRequirementDefinition>? _definitions;
    private readonly IClock? _clock;
    private readonly IRepository<Specimen>? _specimens;
    private readonly IRepository<Order>? _orders;

    public AntibodyScreenCompatLoader(
        IRepository<TestResult> results,
        IRepository<TestDefinition> testDefinitions,
        IRepository<AntibodyHistory> antibodies,
        IRepository<SpecialTransfusionRequirement>? specialRequirements = null,
        IRepository<SpecialRequirementDefinition>? definitions = null,
        IClock? clock = null,
        IRepository<Specimen>? specimens = null,
        IRepository<Order>? orders = null)
    {
        _results = results;
        _testDefinitions = testDefinitions;
        _antibodies = antibodies;
        _specialRequirements = specialRequirements;
        _definitions = definitions;
        _clock = clock;
        _specimens = specimens;
        _orders = orders;
    }

    /// <summary>
    /// True when any antibody history row exists, including deactivated
    /// ("currently undetectable") records. Historical findings must remain visible
    /// to computer-crossmatch eligibility.
    /// </summary>
    public Task<bool> HasAntibodyHistoryAsync(long patientId, CancellationToken ct = default) =>
        _antibodies.AnyAsync(a => a.PatientId == patientId, ct);

    /// <summary>
    /// True when complex crossmatch is required: positive ABSC (verified, current or historical),
    /// any antibody history row, or an active extended-crossmatch special requirement.
    /// </summary>
    public async Task<bool> RequiresComplexCrossmatchAsync(long patientId, CancellationToken ct = default)
    {
        if (await HasAntibodyHistoryAsync(patientId, ct))
        {
            return true;
        }

        if (await HasPositiveAntibodyScreenAsync(patientId, ct))
        {
            return true;
        }

        return await HasActiveExtendedCrossmatchRequirementAsync(patientId, ct);
    }

    public async Task<bool> HasActiveExtendedCrossmatchRequirementAsync(long patientId, CancellationToken ct = default)
    {
        if (_specialRequirements is null)
        {
            return false;
        }

        var now = _clock?.UtcNow ?? DateTime.UtcNow;
        var rows = await _specialRequirements.ListAsync(r => r.PatientId == patientId && r.IsActive, ct);
        var active = rows
            .Where(r => SpecialRequirementCatalog.IsClinicallyActive(r.IsActive, r.EffectiveUtc, r.ExpiresUtc, now))
            .ToList();
        if (active.Count == 0)
        {
            return false;
        }

        var definitions = _definitions is null
            ? []
            : await _definitions.ListAsync(ct);
        var byId = definitions.ToDictionary(d => d.Id);

        foreach (var row in active)
        {
            if (row.RequirementDefinitionId is long id && byId.TryGetValue(id, out var def))
            {
                if (def.EnforcementKind == SpecialRequirementEnforcementKind.RequireComplexCrossmatch)
                {
                    return true;
                }
            }
            else if (string.Equals(
                SpecialRequirementCatalog.CodeFor(row.RequirementType),
                SpecialRequirementCatalog.ExtendedCrossmatch,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> HasPositiveAntibodyScreenAsync(long patientId, CancellationToken ct = default)
    {
        var screens = await ListVerifiedScreenResultsAsync(patientId, ct);
        return screens.Any(r => IsPositiveScreen(r.Value, r.Interpretation));
    }

    /// <summary>
    /// True when the specimen is accepted, not expired, and has verified ABO/Rh plus
    /// a verified antibody screen on that same specimen.
    /// </summary>
    public async Task<bool> HasCompletedTypeAndScreenAsync(
        long specimenId,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        if (_specimens is not null)
        {
            var specimen = await _specimens.GetByIdAsync(specimenId, ct);
            if (specimen is null || !IsAcceptedCurrent(specimen, utcNow))
            {
                return false;
            }
        }

        return await HasVerifiedTypeAndScreenOnSpecimenAsync(specimenId, ct);
    }

    /// <summary>
    /// Accepted, non-expired specimens that already have a completed type and screen.
    /// </summary>
    public async Task<HashSet<long>> ListCompletedTypeAndScreenSpecimenIdsAsync(
        long patientId,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        if (_specimens is null)
        {
            return [];
        }

        var specimens = await _specimens.ListAsync(
            s => s.PatientId == patientId && s.Status == SpecimenStatus.Accepted, ct);
        var current = specimens.Where(s => IsAcceptedCurrent(s, utcNow)).ToList();
        if (current.Count == 0)
        {
            return [];
        }

        var ids = new HashSet<long>();
        foreach (var specimen in current)
        {
            if (await HasVerifiedTypeAndScreenOnSpecimenAsync(specimen.Id, ct))
            {
                ids.Add(specimen.Id);
            }
        }

        return ids;
    }

    /// <summary>
    /// True when a verified antibody screen exists on a specimen that is still usable
    /// (not rejected, cancelled, or expired).
    /// </summary>
    public async Task<bool> HasCurrentVerifiedAntibodyScreenAsync(
        long patientId,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var screens = await ListVerifiedScreenResultsAsync(patientId, ct);
        if (screens.Count == 0)
        {
            return false;
        }

        if (_specimens is null)
        {
            return screens.Count > 0;
        }

        var specimenIds = screens.Select(r => r.SpecimenId).Distinct().ToList();
        var specimens = (await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct))
            .ToDictionary(s => s.Id);

        return screens.Any(r =>
            specimens.TryGetValue(r.SpecimenId, out var specimen)
            && IsCurrentSpecimen(specimen, utcNow));
    }

    /// <summary>
    /// Counts verified, non-positive antibody screens used as electronic XM minimums.
    /// Visits prefer the specimen encounter, then the order encounter.
    /// </summary>
    public async Task<NegativeAntibodyScreenCounts> CountNegativeAntibodyScreensAsync(
        long patientId,
        CancellationToken ct = default)
    {
        var screens = (await ListVerifiedScreenResultsAsync(patientId, ct))
            .Where(r => !IsPositiveScreen(r.Value, r.Interpretation))
            .ToList();
        if (screens.Count == 0)
        {
            return new NegativeAntibodyScreenCounts(0, 0, 0);
        }

        var specimenIds = screens.Select(r => r.SpecimenId).Distinct().ToList();
        var specimens = _specimens is null
            ? new Dictionary<long, Specimen>()
            : (await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct)).ToDictionary(s => s.Id);
        var orderIds = screens.Where(r => r.OrderId is > 0).Select(r => r.OrderId!.Value).Distinct().ToList();
        var orders = _orders is null || orderIds.Count == 0
            ? new Dictionary<long, Order>()
            : (await _orders.ListAsync(o => orderIds.Contains(o.Id), ct)).ToDictionary(o => o.Id);

        var visits = new HashSet<long>();
        var specimenSet = new HashSet<long>();
        foreach (var result in screens)
        {
            specimenSet.Add(result.SpecimenId);
            if (specimens.TryGetValue(result.SpecimenId, out var specimen) && specimen.EncounterId is > 0)
            {
                visits.Add(specimen.EncounterId.Value);
            }
            else if (result.OrderId is long orderId && orders.TryGetValue(orderId, out var order))
            {
                visits.Add(order.EncounterId);
            }
        }

        return new NegativeAntibodyScreenCounts(visits.Count, specimenSet.Count, screens.Count);
    }

    private static bool IsPositiveScreen(string? value, string? interpretation)
    {
        if (string.Equals(interpretation?.Trim(), "Positive", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(value?.Trim(), "Positive", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<TestResult>> ListVerifiedScreenResultsAsync(long patientId, CancellationToken ct)
    {
        var screenCodes = (await _testDefinitions.ListAsync(
                d => d.IsActive
                     && (d.Code == AntibodyScreenTestCode || d.Category == TestCategory.AntibodyScreen),
                ct))
            .Select(d => d.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (screenCodes.Count == 0)
        {
            screenCodes.Add(AntibodyScreenTestCode);
        }

        var results = await _results.ListAsync(
            r => r.PatientId == patientId
                 && r.Status == ResultStatus.Verified
                 && r.SupersededByResultId == null
                 && r.Value != null,
            ct);

        return results.Where(r => screenCodes.Contains(r.TestCode)).ToList();
    }

    private static bool IsCurrentSpecimen(Specimen specimen, DateTime utcNow) =>
        specimen.Status is not (SpecimenStatus.Rejected or SpecimenStatus.Cancelled or SpecimenStatus.Expired)
        && (specimen.ExpiresUtc is null || specimen.ExpiresUtc > utcNow);

    private static bool IsAcceptedCurrent(Specimen specimen, DateTime utcNow) =>
        specimen.Status == SpecimenStatus.Accepted
        && (specimen.ExpiresUtc is null || specimen.ExpiresUtc > utcNow);

    private async Task<bool> HasVerifiedTypeAndScreenOnSpecimenAsync(long specimenId, CancellationToken ct)
    {
        var results = await _results.ListAsync(
            r => r.SpecimenId == specimenId
                 && r.Status == ResultStatus.Verified
                 && r.SupersededByResultId == null
                 && r.Value != null,
            ct);
        if (results.Count == 0)
        {
            return false;
        }

        var typeCodes = await ListTypeCodesAsync(ct);
        var screenCodes = await ListScreenCodesAsync(ct);
        return results.Any(r => typeCodes.Contains(r.TestCode))
               && results.Any(r => screenCodes.Contains(r.TestCode));
    }

    private async Task<HashSet<string>> ListTypeCodesAsync(CancellationToken ct)
    {
        var codes = (await _testDefinitions.ListAsync(
                d => d.IsActive
                     && d.Category != TestCategory.AboRhRetype
                     && (d.Code == AboRhTestCode || d.Category == TestCategory.AboRh),
                ct))
            .Select(d => d.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0)
        {
            codes.Add(AboRhTestCode);
        }

        return codes;
    }

    private async Task<HashSet<string>> ListScreenCodesAsync(CancellationToken ct)
    {
        var codes = (await _testDefinitions.ListAsync(
                d => d.IsActive
                     && (d.Code == AntibodyScreenTestCode || d.Category == TestCategory.AntibodyScreen),
                ct))
            .Select(d => d.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0)
        {
            codes.Add(AntibodyScreenTestCode);
        }

        return codes;
    }
}
