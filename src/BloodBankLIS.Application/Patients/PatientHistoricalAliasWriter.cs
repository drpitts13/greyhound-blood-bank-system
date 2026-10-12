using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Patients;

/// <summary>
/// Retains a former display name as <see cref="IdentityTokenType.PriorFullName"/>.
/// Used by chart demographics edit and inbound ADT updates.
/// </summary>
public sealed class PatientHistoricalAliasWriter
{
    private readonly IRepository<PatientIdentifier> _identifiers;

    public PatientHistoricalAliasWriter(IRepository<PatientIdentifier> identifiers) =>
        _identifiers = identifiers;

    /// <summary>
    /// When the next name differs from the current chart name, stores the current
    /// display name as an active prior-name alias. Returns the archived value, or
    /// null when the name did not change or was already archived.
    /// </summary>
    public async Task<string?> ArchivePriorNameIfChangedAsync(
        long patientId,
        string previousLast,
        string previousFirst,
        string? previousMiddle,
        string nextLast,
        string nextFirst,
        string? nextMiddle,
        CancellationToken ct = default)
    {
        if (!PatientHistoricalAliasRule.NameChanged(
                previousLast, previousFirst, previousMiddle,
                nextLast, nextFirst, nextMiddle))
        {
            return null;
        }

        var priorName = PatientHistoricalAliasRule.TruncateAliasValue(
            PatientHistoricalAliasRule.FormatDisplayName(previousLast, previousFirst, previousMiddle));
        if (string.IsNullOrWhiteSpace(priorName))
        {
            return null;
        }

        var exists = await _identifiers.AnyAsync(
            i => i.PatientId == patientId
                 && i.IdentifierType == IdentityTokenType.PriorFullName
                 && i.Value == priorName,
            ct);
        if (exists)
        {
            return priorName;
        }

        await _identifiers.AddAsync(new PatientIdentifier
        {
            PatientId = patientId,
            IdentifierType = IdentityTokenType.PriorFullName,
            Value = priorName,
            AssigningAuthority = $"Patient:{patientId}",
            IsActive = true
        }, ct);
        return priorName;
    }
}
