using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Alternate patient identifier (MRN is on <see cref="Patient"/>). Supports AABB
/// two-independent-identifier practice and look-up by non-MRN tokens.
/// </summary>
public class PatientIdentifier : BaseEntity
{
    public long PatientId { get; set; }

    public IdentityTokenType IdentifierType { get; set; }

    public string Value { get; set; } = string.Empty;

    public string? AssigningAuthority { get; set; }

    public bool IsActive { get; set; } = true;
}
