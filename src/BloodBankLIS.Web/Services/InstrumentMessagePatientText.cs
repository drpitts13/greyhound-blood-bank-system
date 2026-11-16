using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The Patient column of the instrument message list (end-user testing pass 166: the analyzer's QC samples showed the
/// instrument's fixed test patient ID, 1171984, as if a patient had been tested). A QC sample is not a patient specimen,
/// so the column reads "— QC" and the instrument's value is kept in the tooltip.
/// </summary>
public static class InstrumentMessagePatientText
{
    public static (string Text, string? Title, bool IsQc) For(InstrumentMessageStatus status, string? patientId) =>
        status == InstrumentMessageStatus.QualityControl
            ? ("— QC", string.IsNullOrWhiteSpace(patientId)
                ? "Not a patient: QC sample."
                : $"Not a patient: QC sample. The instrument sent patient ID {patientId.Trim()}.", true)
            : (patientId ?? "", null, false);
}
