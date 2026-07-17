using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Identity;

/// <summary>
/// An append-only electronic signature (table <c>ElectronicSignatures</c>). Captures
/// who attested, the meaning of the signature, and the action/context it covers.
/// Referenced by audit events and overrides for dangerous actions (docs/erd.md 1,
/// docs/safety-rules.md). Never updated or deleted.
/// </summary>
public class ElectronicSignature : BaseEntity
{
    public long UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? ContextType { get; set; }

    public long? ContextId { get; set; }

    public DateTime SignedUtc { get; set; }

    public string MeaningOfSignature { get; set; } = string.Empty;

    public string? Workstation { get; set; }
}
