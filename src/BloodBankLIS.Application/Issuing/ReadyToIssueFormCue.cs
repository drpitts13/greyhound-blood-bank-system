using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// Repeats the Ready to issue marks on the issue form for the unit just selected.
/// Does not decide issue eligibility.
/// </summary>
public static class ReadyToIssueFormCue
{
    public const string SecondAbo =
        "Routine red-cell issue stays blocked until a second concordant ABO/Rh is on file.";

    public static IReadOnlyList<string> Notices(ReadyToIssueWorkItemDto row)
    {
        var notices = new List<string>();
        Add(notices, row.ProductName);
        Add(notices, ExpirationNotice(row.UnitExpiresUtc));
        Add(notices, UnitTypeNotice(row.UnitBloodType));
        Add(notices, StorageLocationNotice(row.StorageLocationName));
        Add(notices, SpecimenAccessionNotice(row.SpecimenAccession));
        Add(notices, CrossmatchWorklistNotice(row.LatestCrossmatchResult, row.CrossmatchExpiresUtc));
        Add(notices, row.SpecimenBlockReason);
        Add(notices, row.UnitBlockReason);
        Add(notices, row.OpenAntibodyWorkupNotice);
        Add(notices, row.ReactionNotice);
        Add(notices, row.CrossmatchNotice);
        Add(notices, row.SpecialRequirementNotice);
        Add(notices, row.AntigenNotice);
        Add(notices, row.AboRhDiscrepancyNotice);
        Add(notices, row.ElectronicCrossmatchNotice);
        Add(notices, row.DonationRestrictionNotice);
        if (row.NeedsSecondAbo)
        {
            Add(notices, SecondAbo);
        }

        return notices;
    }

    public static string? ExpirationNotice(DateTime? expiresUtc) =>
        expiresUtc is DateTime expires ? $"Unit expires {expires:yyyy-MM-dd HH:mm}" : null;

    public static string? UnitTypeNotice(string? unitBloodType) =>
        string.IsNullOrWhiteSpace(unitBloodType) ? null : $"Unit type {unitBloodType.Trim()}";

    public static string? CrossmatchExpirationNotice(DateTime? expiresUtc) =>
        expiresUtc is DateTime expires ? $"Crossmatch expires {expires:yyyy-MM-dd HH:mm}" : null;

    public static string? CrossmatchResultNotice(CrossmatchResult? result) => result switch
    {
        CrossmatchResult.Compatible => "Crossmatch compatible",
        CrossmatchResult.Incompatible => "Crossmatch incompatible",
        _ => null
    };

    public static string? CrossmatchWorklistNotice(CrossmatchResult? result, DateTime? expiresUtc)
    {
        var notices = new List<string>();
        Add(notices, CrossmatchResultNotice(result));
        Add(notices, CrossmatchExpirationNotice(expiresUtc));
        return notices.Count == 0 ? null : string.Join(" · ", notices);
    }

    public static string? StorageLocationLabel(long? locationId, string? name)
    {
        if (locationId is not long id)
        {
            return null;
        }

        return BloodBankLIS.Domain.Rules.StorageLocationLabel.Format(id, name);
    }

    public static string? StorageLocationNotice(string? locationName) =>
        string.IsNullOrWhiteSpace(locationName) ? null : $"Stored in {locationName.Trim()}";

    public static string? SpecimenAccessionNotice(string? accession) =>
        string.IsNullOrWhiteSpace(accession) ? null : $"Specimen {accession.Trim()}";

    public static string? IssuedToNotice(string? issuedTo) =>
        string.IsNullOrWhiteSpace(issuedTo) ? null : $"Issued to {issuedTo.Trim()}";

    public static string? IssuedToLocationNotice(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var place = location.Trim();
        if (place.StartsWith("Location ", StringComparison.Ordinal))
        {
            return place;
        }

        return $"Location {place}";
    }

    public static string? CoolerNotice(string? coolerId) =>
        string.IsNullOrWhiteSpace(coolerId) ? null : CoolerName(coolerId);

    // "Cooler 3" stays as it is; "CLR-1" reads "Cooler CLR-1".
    private static string CoolerName(string coolerId) =>
        coolerId.Trim().StartsWith("cooler", StringComparison.OrdinalIgnoreCase) ? coolerId.Trim() : $"Cooler {coolerId.Trim()}";

    /// <summary>
    /// The cooler with its latest check before dispatch, for the person receiving it: checked with all units packed,
    /// checked with differences, or not checked. No cooler, no notice; unknown check state falls back to the cooler id.
    /// </summary>
    public static string? CoolerCheckNotice(string? coolerId, bool checkKnown, DateTime? checkedLocal, bool? allPacked)
    {
        if (string.IsNullOrWhiteSpace(coolerId))
        {
            return null;
        }

        var cooler = CoolerName(coolerId);
        if (!checkKnown)
        {
            return cooler;
        }

        return checkedLocal is not DateTime at
            ? $"{cooler}: not checked before it left"
            : allPacked == true
                ? $"{cooler}: checked {at:yyyy-MM-dd HH:mm}, all units packed"
                : $"{cooler}: checked {at:yyyy-MM-dd HH:mm} with differences; compare each unit with its issue";
    }

    public static string? IssuedByNotice(string? issuedBy) =>
        string.IsNullOrWhiteSpace(issuedBy) ? null : $"Issued by {issuedBy.Trim()}";

    public static string? SecondVerifierNotice(string? secondVerifier) =>
        string.IsNullOrWhiteSpace(secondVerifier) ? null : $"Second verifier {secondVerifier.Trim()}";

    public static string? EmergencyReleaseReasonNotice(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : $"Release reason: {reason.Trim()}";

    public static string? EmergencyReleaseAuthorizerNotice(string? authorizer) =>
        string.IsNullOrWhiteSpace(authorizer) ? null : $"Authorized by {authorizer.Trim()}";

    public static string IssueAppearanceNotice(UnitAppearance appearance) =>
        $"Appearance {appearance}";

    public static string? WardAppearanceNotice(string? appearance) =>
        string.IsNullOrWhiteSpace(appearance) ? null : $"Ward appearance {appearance.Trim()}";

    public static string? PtagNotice(string? ptagCode) =>
        string.IsNullOrWhiteSpace(ptagCode) ? null : $"Pickup tag {ptagCode.Trim()}";

    public static string CrossmatchStatusNotice(CrossmatchClinicalStatus status) =>
        $"Crossmatch at issue: {CrossmatchStatusLabel(status)}";

    /// <summary>The crossmatch status in words (end-user testing, 2026-11-12: shown as the code NotCrossmatchedEmergency).</summary>
    public static string CrossmatchStatusLabel(CrossmatchClinicalStatus status) => status switch
    {
        CrossmatchClinicalStatus.Compatible => "Compatible",
        CrossmatchClinicalStatus.Incompatible => "Incompatible",
        CrossmatchClinicalStatus.NotPerformed => "Not crossmatched",
        CrossmatchClinicalStatus.NotCrossmatchedEmergency => "Not crossmatched (emergency release)",
        CrossmatchClinicalStatus.Expired => "Crossmatch expired",
        _ => status.ToString()
    };

    public static string TimeOutNotice(bool governed, bool acceptable) =>
        ReturnReissueRule.TimeOutLabel(governed, acceptable) switch
        {
            "Exempt" => "Storage device is exempt from the return-to-available time limit.",
            "Window elapsed" => "Storage device is governed. The return-to-available window has elapsed. Available is not allowed.",
            _ => "Storage device is governed. The return-to-available window is still open."
        };

    public static string? RetrospectiveFollowUpNotice(string? issuedTo, string? coolerId, string? location = null, string? issuedBy = null, string? secondVerifier = null)
    {
        string? joined = null;
        foreach (var part in new[] { IssuedToNotice(issuedTo), CoolerNotice(coolerId), IssuedToLocationNotice(location), IssuedByNotice(issuedBy), SecondVerifierNotice(secondVerifier) })
        {
            if (part is null)
            {
                continue;
            }

            joined = joined is null ? part : $"{joined} · {part}";
        }

        return joined;
    }

    private static void Add(List<string> notices, string? notice)
    {
        if (!string.IsNullOrWhiteSpace(notice))
        {
            notices.Add(notice.Trim());
        }
    }
}
