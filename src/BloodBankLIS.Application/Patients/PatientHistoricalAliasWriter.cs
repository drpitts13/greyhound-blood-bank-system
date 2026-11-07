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

    /// <summary>
    /// Keeps identifiers an interface sent beside the MRN (an enterprise id, another facility's number) as alternate
    /// identifiers, so search finds the patient by them. An account number (type AN) is stored as one. Returns the
    /// identifiers newly recorded; ones already on the chart are not repeated.
    /// </summary>
    public async Task<IReadOnlyList<string>> RecordExternalIdentifiersAsync(
        long patientId,
        string mrn,
        IEnumerable<(string Value, string? AssigningAuthority, string? TypeCode)> identifiers,
        CancellationToken ct = default)
    {
        var existing = (await _identifiers.ListAsync(i => i.PatientId == patientId, ct)).ToList();
        var added = new List<string>();
        foreach (var (rawValue, authority, typeCode) in identifiers)
        {
            var value = PatientHistoricalAliasRule.TruncateAliasValue(rawValue.Trim());
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, mrn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var type = string.Equals(typeCode, "AN", StringComparison.OrdinalIgnoreCase)
                ? IdentityTokenType.AccountNumber
                : IdentityTokenType.Other;
            var label = string.Join(" ", new[] { authority, typeCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (existing.Any(i => i.IdentifierType == type && string.Equals(i.Value, value, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var row = new PatientIdentifier
            {
                PatientId = patientId,
                IdentifierType = type,
                Value = value,
                AssigningAuthority = label.Length == 0 ? null : label,
                IsActive = true
            };
            await _identifiers.AddAsync(row, ct);
            existing.Add(row);
            added.Add(label.Length == 0 ? value : $"{value} ({label})");
        }

        return added;
    }
}
