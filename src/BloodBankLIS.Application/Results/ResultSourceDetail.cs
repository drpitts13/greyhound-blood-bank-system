namespace BloodBankLIS.Application.Results;

/// <summary>
/// What an instrument or interface result's source reference says beyond its key ("ASTM:ACC1:ABORH · M0002 · completed
/// … · instrument flag W (Warn)" reads "M0002 · completed … · instrument flag W (Warn)"). End-user testing pass 30: flags
/// written "for the verifier" were not shown on the Test worklist, where results are verified.
/// </summary>
public static class ResultSourceDetail
{
    public static string? For(string? sourceReference)
    {
        if (string.IsNullOrWhiteSpace(sourceReference))
        {
            return null;
        }

        var at = sourceReference.IndexOf(" · ", StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var detail = sourceReference[(at + 3)..].Trim();
        return detail.Length == 0 ? null : detail;
    }
}
