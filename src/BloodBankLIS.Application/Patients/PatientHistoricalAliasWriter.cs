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
    /// identifiers, so search finds the patient by them. An account number (type AN) is stored as one. Ones already on
    /// this chart are not repeated. An identifier another chart already holds is a possible duplicate: it is kept on
    /// this chart with its authority marked "also on" that chart's MRN (so both charts are found by it and the Possible
    /// duplicates report pairs them), and returned in <see cref="ExternalIdentifierOutcome.Conflicts"/>.
    /// </summary>
    public async Task<ExternalIdentifierOutcome> RecordExternalIdentifiersAsync(
        long patientId,
        string mrn,
        IEnumerable<(string Value, string? AssigningAuthority, string? TypeCode)> identifiers,
        Func<long, CancellationToken, Task<string?>> mrnOfPatient,
        CancellationToken ct = default)
    {
        var mine = (await _identifiers.ListAsync(i => i.PatientId == patientId, ct)).ToList();
        var added = new List<string>();
        var conflicts = new List<(string Value, string OtherMrn)>();
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
            if (mine.Any(i => i.IdentifierType == type && string.Equals(i.Value, value, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var label = string.Join(" ", new[] { authority, typeCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var stored = label.Length == 0 ? null : label;
            var holder = await _identifiers.FirstOrDefaultAsync(
                i => i.PatientId != patientId && i.IdentifierType == type && i.Value == value && i.AssigningAuthority == stored, ct);
            if (holder is not null)
            {
                var otherMrn = await mrnOfPatient(holder.PatientId, ct) ?? $"patient {holder.PatientId}";
                conflicts.Add((value, otherMrn));
                stored = Truncate(ExternalIdentifierAuthority.AlsoOn(label, otherMrn));
            }

            var row = new PatientIdentifier
            {
                PatientId = patientId,
                IdentifierType = type,
                Value = value,
                AssigningAuthority = stored,
                IsActive = true
            };
            await _identifiers.AddAsync(row, ct);
            mine.Add(row);
            added.Add(stored is null ? value : $"{value} ({stored})");
        }

        return new ExternalIdentifierOutcome(added, conflicts);
    }

    private static string Truncate(string value) => value.Length <= 100 ? value : value[..100];
}

/// <summary>Identifiers recorded on the chart, and those another chart already held (a possible duplicate).</summary>
public sealed record ExternalIdentifierOutcome(
    IReadOnlyList<string> Added,
    IReadOnlyList<(string Value, string OtherMrn)> Conflicts);
