using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Compatibility;

/// <summary>
/// Resolves whether a patient has a verified positive antibody screen (current or historical).
/// </summary>
public sealed class AntibodyScreenCompatLoader
{
    public const string AntibodyScreenTestCode = "ABSC";

    private readonly IRepository<TestResult> _results;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<AntibodyHistory> _antibodies;

    public AntibodyScreenCompatLoader(
        IRepository<TestResult> results,
        IRepository<TestDefinition> testDefinitions,
        IRepository<AntibodyHistory> antibodies)
    {
        _results = results;
        _testDefinitions = testDefinitions;
        _antibodies = antibodies;
    }

    /// <summary>
    /// True when complex crossmatch is required: positive ABSC (verified, current or historical)
    /// and/or any antibody history row.
    /// </summary>
    public async Task<bool> RequiresComplexCrossmatchAsync(long patientId, CancellationToken ct = default)
    {
        if (await _antibodies.AnyAsync(a => a.PatientId == patientId, ct))
        {
            return true;
        }

        return await HasPositiveAntibodyScreenAsync(patientId, ct);
    }

    public async Task<bool> HasPositiveAntibodyScreenAsync(long patientId, CancellationToken ct = default)
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

        return results.Any(r =>
            screenCodes.Contains(r.TestCode)
            && IsPositiveScreen(r.Value, r.Interpretation));
    }

    private static bool IsPositiveScreen(string? value, string? interpretation)
    {
        if (string.Equals(interpretation?.Trim(), "Positive", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(value?.Trim(), "Positive", StringComparison.OrdinalIgnoreCase);
    }
}
