using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// The local values an interface field may translate to: active tests and groupers for procedure codes, the visit types
/// for patient class, the local race and ethnicity lists, sex codes, and the abnormal flags. Other fields take any
/// value. Drives the admin picker and refuses a local value the LIS does not have.
/// </summary>
public sealed class InterfaceValueSets
{
    private readonly IRepository<TestDefinition> _tests;
    private readonly IRepository<TestGrouper> _groupers;

    public InterfaceValueSets(IRepository<TestDefinition> tests, IRepository<TestGrouper> groupers)
    {
        _tests = tests;
        _groupers = groupers;
    }

    public static bool IsProcedureKey(string key) =>
        key is InterfaceDataItemKeys.ProcedureCode or InterfaceDataItemKeys.OrderTestCode or InterfaceDataItemKeys.ResultObrTestCode;

    public static bool HasValueList(string key) =>
        IsProcedureKey(key) || FixedValues(key) is not null;

    /// <summary>The local values for a field, or an empty list when any value is allowed.</summary>
    public async Task<IReadOnlyList<InterfaceLocalValueDto>> ForAsync(string key, CancellationToken ct = default)
    {
        if (IsProcedureKey(key))
        {
            var groupers = (await _groupers.ListAsync(g => g.IsActive && !g.IsDraft, ct))
                .OrderBy(g => g.Code)
                .Select(g => new InterfaceLocalValueDto(g.Code, g.Name, "Grouper"));
            var tests = (await _tests.ListAsync(t => t.IsActive && !t.IsDraft, ct))
                .OrderBy(t => t.Code)
                .Select(t => new InterfaceLocalValueDto(t.Code, t.Name, "Test"));
            return groupers.Concat(tests).ToList();
        }

        return FixedValues(key) ?? [];
    }

    /// <summary>Active, non-draft grouper codes (a grouper wins over a test for the same external code).</summary>
    public async Task<HashSet<string>> GrouperCodesAsync(CancellationToken ct = default) =>
        (await _groupers.ListAsync(g => g.IsActive && !g.IsDraft, ct)).Select(g => g.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<InterfaceLocalValueDto>? FixedValues(string key) => key switch
    {
        InterfaceDataItemKeys.PatientSex =>
            [new("M", "Male"), new("F", "Female"), new("O", "Other"), new("U", "Unknown")],
        InterfaceDataItemKeys.PatientRace =>
            PatientDemographicCodes.Races.Select(r => new InterfaceLocalValueDto(r.Code, r.Label)).ToList(),
        InterfaceDataItemKeys.PatientEthnicity =>
            PatientDemographicCodes.Ethnicities.Select(e => new InterfaceLocalValueDto(e.Code, e.Label)).ToList(),
        InterfaceDataItemKeys.EncounterPatientClass =>
            Enum.GetValues<EncounterType>().Select(t => new InterfaceLocalValueDto(t.ToString(), t.ToString(), "Visit type")).ToList(),
        InterfaceDataItemKeys.ResultAbnormalFlag =>
            [new(NumericResultRule.Low, "Low"), new(NumericResultRule.High, "High"),
             new(NumericResultRule.CriticalLowFlag, "Critical low"), new(NumericResultRule.CriticalHighFlag, "Critical high")],
        _ => null
    };
}
