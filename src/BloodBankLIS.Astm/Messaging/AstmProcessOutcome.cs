using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Astm.Messaging;

public sealed record AstmProcessOutcome(
    bool Accepted,
    InstrumentMessageLog Log,
    string? ReplyMessage,
    IReadOnlyList<string> Notes);
