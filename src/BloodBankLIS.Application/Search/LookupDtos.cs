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
    ReagentLot = 7,
    /// <summary>A deviation by its reference ("DEV-3", as alerts and reports name it); opens it on Deviations (pass 263).</summary>
    Deviation = 8,
    /// <summary>
    /// A sample ID an instrument sent that matches no specimen: opens Instrument Interfaces searched for it (pass 264).
    /// </summary>
    InstrumentMessages = 9,
    /// <summary>A delivery by its packing list number; opens the delivery (pass 265).</summary>
    Delivery = 10,
    /// <summary>An inbound shipment (ASN) with units still expected; opens Expected inbound (pass 265).</summary>
    InboundShipment = 11
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
    int InformationalNotes = 0)
{
    /// <summary>
    /// Patient hits: a transfusion reaction investigation is open or under review (end-user testing pass 188, as the
    /// patient list since pass 187).
    /// </summary>
    public bool ReactionUnderInvestigation { get; init; }

    /// <summary>An infant's linked mother's active antibodies, which may be present in the infant (end-user testing pass 210).</summary>
    public IReadOnlyList<string> MaternalAntibodies { get; init; } = [];
}

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
