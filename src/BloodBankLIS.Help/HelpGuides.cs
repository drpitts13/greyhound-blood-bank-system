namespace BloodBankLIS.Help;

public static class HelpGuides
{
    public const string Clinical = "clinical";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<string> All = [Clinical, Admin];

    public static string Title(string guide) => guide switch
    {
        Clinical => "Clinical User Guide",
        Admin => "Administrator Guide",
        _ => guide
    };

    public static string FileName(string guide) => guide switch
    {
        Clinical => "Greyhound-Blood-Bank-Clinical-User-Guide.pdf",
        Admin => "Greyhound-Blood-Bank-Administrator-Guide.pdf",
        _ => throw new ArgumentException($"Unknown help guide '{guide}'.", nameof(guide))
    };

    public static bool IsKnown(string? guide) =>
        guide is not null && All.Contains(guide, StringComparer.OrdinalIgnoreCase);

    public static string Canonical(string guide)
    {
        foreach (var known in All)
        {
            if (known.Equals(guide, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        throw new ArgumentException($"Unknown help guide '{guide}'.", nameof(guide));
    }
}
