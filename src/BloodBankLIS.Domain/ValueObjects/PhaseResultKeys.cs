namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>Composite keys for cell × phase results and interpretation columns.</summary>
public static class PhaseResultKeys
{
    public const char Separator = '|';

    public static string Compose(string subtestCode, string phaseCode) =>
        $"{subtestCode.Trim()}{Separator}{phaseCode.Trim()}";

    public static bool TrySplit(string key, out string subtestCode, out string phaseCode)
    {
        subtestCode = string.Empty;
        phaseCode = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var idx = key.IndexOf(Separator);
        if (idx <= 0 || idx >= key.Length - 1)
        {
            return false;
        }

        subtestCode = key[..idx].Trim();
        phaseCode = key[(idx + 1)..].Trim();
        return subtestCode.Length > 0 && phaseCode.Length > 0;
    }
}
