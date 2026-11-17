using BloodBankLIS.Application.Inventory;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// What the chart says when the patient's ABO/Rh was tested with a reagent lot whose QC failed and the result is not yet
/// reviewed (end-user testing pass 181).
/// </summary>
public static class QcReviewText
{
    public static string? AboRh(IReadOnlyList<PatientResultUnderQcReviewDto> rows)
    {
        var lots = rows.Where(r => string.Equals(r.TestCode, "ABORH", StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{r.ReagentCode} lot {r.LotNumber}").Distinct().ToList();
        return lots.Count == 0
            ? null
            : $"ABO/Rh tested with {string.Join(", ", lots)}, whose QC failed; the result is to review on Reagent QC before relying on it.";
    }
}
