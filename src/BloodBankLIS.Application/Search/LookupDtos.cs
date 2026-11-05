namespace BloodBankLIS.Application.Search;

public sealed record LookupRequest(string? Query);

public enum LookupHitKind
{
    Patient = 0,
    Unit = 1,
    Specimen = 2,
    Order = 3
}

/// <summary>One record the top-bar lookup found. <see cref="Href"/> is the page that opens it.</summary>
public sealed record LookupHitDto(
    LookupHitKind Kind,
    long Id,
    bool Exact,
    string Identifier,
    string Title,
    string? Detail,
    string Href,
    // Patient hits: current ABO/Rh and alerts (active antibodies, special requirements in effect), so the right patient
    // and their risks are visible before the chart opens.
    string? BloodType = null,
    IReadOnlyList<string>? Antibodies = null,
    int RequirementsInEffect = 0,
    int InformationalNotes = 0);

public sealed record LookupResultDto(
    string Query,
    IReadOnlyList<LookupHitDto> Hits,
    bool Truncated,
    string? Message)
{
    /// <summary>
    /// Open the record without a list only when the input identified exactly one record exactly.
    /// A partial match or several matches (for example a divided DIN) always shows the list.
    /// </summary>
    public bool OpenDirectly => Hits.Count == 1 && Hits[0].Exact;

    public static LookupResultDto Empty(string query, string? message = null) => new(query, [], false, message);
}
