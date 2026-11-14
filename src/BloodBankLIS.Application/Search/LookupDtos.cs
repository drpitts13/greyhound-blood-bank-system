namespace BloodBankLIS.Application.Search;

public sealed record LookupRequest(string? Query);

public enum LookupHitKind
{
    Patient = 0,
    Unit = 1,
    Specimen = 2,
    Order = 3,
    /// <summary>An RhIG lot recorded as given; opens the lot trace.</summary>
    RhigLot = 4,
    /// <summary>An outbound shipment's reference (packing list); opens Ship units with that shipment.</summary>
    Shipment = 5,
    /// <summary>A transport cooler with issued units still in transit; opens Check a cooler with it.</summary>
    Cooler = 6,
    /// <summary>A reagent lot (vial label); opens Reagent QC with the lot's results (end-user testing pass 37).</summary>
    ReagentLot = 7
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
