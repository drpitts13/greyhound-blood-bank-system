using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Patients;

public sealed record CreatePatientRequest(
    string MedicalRecordNumber,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly DateOfBirth,
    Sex Sex,
    string? Comment = null,
    // The possible duplicates the registrar reviewed and confirmed are different people (their MRNs).
    IReadOnlyList<string>? ConfirmedDifferentFromMrns = null);

/// <summary>Two active charts that may be one person (<c>PossibleDuplicatePatientRule</c>), older registration first.</summary>
public sealed record PossibleDuplicatePairDto(PatientDto First, PatientDto Second, string? Reason = null);

/// <summary>Two charts on the Possible duplicates report the reviewer found to be different people.</summary>
public sealed record DismissPossibleDuplicateRequest(long FirstPatientId, long SecondPatientId, string Reason);

public sealed record UpdatePatientRequest(
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly DateOfBirth,
    Sex Sex,
    PatientStatus Status,
    DateTime? RecentPregnancyUtc = null,
    string? Comment = null,
    string? NameChangeReason = null,
    // Local race and ethnicity codes: null leaves them as they are, blank clears them.
    string? Race = null,
    string? Ethnicity = null);

public sealed record MergePatientsRequest(string DuplicateMrn, string Reason);

public sealed record UnmergePatientRequest(string Reason);

public sealed record PatientAliasDto(
    long Id,
    IdentityTokenType IdentifierType,
    string Value,
    bool IsActive,
    string? AssigningAuthority = null)
{
    public static PatientAliasDto From(PatientIdentifier i) =>
        new(i.Id, i.IdentifierType, i.Value, i.IsActive, i.AssigningAuthority);

    /// <summary>For an external identifier also held by another chart: that chart's MRN, to open it.</summary>
    public string? AlsoOnMrn => BloodBankLIS.Domain.Rules.ExternalIdentifierAuthority.OtherMrn(AssigningAuthority);

    public string KindLabel => IdentifierType switch
    {
        IdentityTokenType.PriorFullName => "Prior name",
        IdentityTokenType.PriorMedicalRecordNumber => "Prior MRN",
        IdentityTokenType.Other => string.IsNullOrWhiteSpace(AssigningAuthority) ? "External ID" : $"External ID ({AssigningAuthority})",
        IdentityTokenType.AccountNumber => string.IsNullOrWhiteSpace(AssigningAuthority) ? "Account" : $"Account ({AssigningAuthority})",
        _ => IdentifierType.ToString()
    };
}

public sealed record PatientDto(
    long Id,
    string MedicalRecordNumber,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly DateOfBirth,
    Sex Sex,
    PatientStatus Status,
    DateTime CreatedUtc,
    string CreatedBy,
    DateTime? RecentPregnancyUtc = null,
    long? MergedIntoPatientId = null,
    string? Comment = null,
    IReadOnlyList<PatientAliasDto>? Aliases = null,
    // Blood bank flags for the patient list (filled by ListDtosAsync only): current ABO/Rh, active antibodies,
    // and special requirements in effect, so the right patient and their risks are visible before opening the chart.
    string? CurrentBloodType = null,
    IReadOnlyList<string>? ActiveAntibodies = null,
    int ActiveSpecialRequirements = 0,
    // Informational notes in effect: counted apart from the enforced requirements above.
    int InformationalNotes = 0,
    // The current open visit (CurrentVisit.For) for the patient list: its number, where the patient is ("Inpatient ·
    // ICU 4"), and how many other visits are open. Null visit number: no open visit.
    string? CurrentVisitNumber = null,
    string? CurrentVisitWhere = null,
    int OtherOpenVisits = 0,
    string? Race = null,
    string? Ethnicity = null)
{
    public static PatientDto From(Patient p, IReadOnlyList<PatientAliasDto>? aliases = null) => new(
        p.Id, p.MedicalRecordNumber, p.LastName, p.FirstName, p.MiddleName,
        p.DateOfBirth, p.Sex, p.Status, p.CreatedUtc, p.CreatedBy, p.RecentPregnancyUtc, p.MergedIntoPatientId,
        p.Comment, aliases, Race: p.Race, Ethnicity: p.Ethnicity);

    /// <summary>
    /// How a patient reads in a picker (issue, crossmatch, allocation, receipt): name and MRN, where the patient is (the
    /// current open visit, from the patient list), then ABO/Rh and alerts, so similar names are told apart and risks are
    /// seen before the patient is chosen. A merged-away record says so.
    /// </summary>
    public string PickerLabel
    {
        get
        {
            var parts = new List<string> { $"{LastName}, {FirstName} ({MedicalRecordNumber})" };
            if (MergedIntoPatientId is not null || Status == PatientStatus.Merged)
            {
                parts.Add("merged into another record");
                return string.Join(" · ", parts);
            }

            if (CurrentVisitNumber is not null)
            {
                var where = string.IsNullOrEmpty(CurrentVisitWhere) ? $"visit {CurrentVisitNumber}" : CurrentVisitWhere;
                parts.Add(OtherOpenVisits > 0 ? $"{where} (+{OtherOpenVisits} more open)" : where);
            }

            parts.Add(CurrentBloodType ?? "no type on file");
            if (ActiveAntibodies is { Count: > 0 } antibodies)
            {
                parts.Add(string.Join(", ", antibodies));
            }

            if (ActiveSpecialRequirements > 0)
            {
                parts.Add(PatientBloodBankFlags.RequirementsLabel(ActiveSpecialRequirements));
            }

            if (InformationalNotes > 0)
            {
                parts.Add(PatientBloodBankFlags.NotesLabel(InformationalNotes));
            }

            return string.Join(" · ", parts);
        }
    }

    public bool MatchesSearch(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        if (MedicalRecordNumber.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || LastName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || FirstName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(MiddleName)
                && MiddleName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return Aliases?.Any(a => a.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)) == true;
    }
}
