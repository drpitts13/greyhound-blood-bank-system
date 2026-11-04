using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Astm.Messaging;

public sealed record AstmProcessOutcome(
    bool Accepted,
    InstrumentMessageLog Log,
    string? ReplyMessage,
    IReadOnlyList<string> Notes)
{
    /// <summary>True when a replay was refused before processing (the message processed already or was resolved).</summary>
    public bool Refused { get; init; }
}
