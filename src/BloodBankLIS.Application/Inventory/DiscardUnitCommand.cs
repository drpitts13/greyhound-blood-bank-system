namespace BloodBankLIS.Application.Inventory;

/// <summary>Discard input: an active catalog code, an optional comment, and a second verifier when policy needs one.</summary>
public sealed record DiscardUnitCommand(string ReasonCode, string? Comment = null, string? SecondVerifier = null);
