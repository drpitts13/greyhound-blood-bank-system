namespace BloodBankLIS.Application.Compliance;

/// <summary>How the deviation list reads: open items first, most severe first, then the oldest first.</summary>
public static class DeviationListView
{
    public static IReadOnlyList<DeviationDto> Order(IEnumerable<DeviationDto> rows) =>
        rows.OrderBy(d => d.Status == Domain.Enums.DeviationStatus.Closed ? 1 : 0)
            .ThenByDescending(d => d.Severity)
            .ThenBy(d => d.ReportedUtc)
            .ThenBy(d => d.Id)
            .ToList();
}
