using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// DATs recorded in a patient's transfusion reaction investigations, for the chart's DAT / Elution tab (end-user testing
/// pass 197: Febrile, Marcus had a DAT recorded in his reaction workup, but the tab said "No DAT or elution workups").
/// </summary>
public static class ReactionDatText
{
    public static IReadOnlyList<(long InvestigationId, string Text)> For(IEnumerable<ReactionInvestigationDto> investigations, long patientId) =>
        investigations
            .Where(r => r.PatientId == patientId && r.DatResult != DatWorkupResult.NotRecorded)
            .OrderByDescending(r => r.ReportedUtc)
            .Select(r => (r.Id,
                $"Reaction investigation #{r.Id} (reported {r.ReportedUtc.ToLocalTime():yyyy-MM-dd}): DAT {Ui.EnumText(r.DatResult).ToLowerInvariant()}"
                + (string.IsNullOrWhiteSpace(r.ElutionResult) ? "" : $", elution: {r.ElutionResult.Trim()}")
                + "."))
            .ToList();
}
