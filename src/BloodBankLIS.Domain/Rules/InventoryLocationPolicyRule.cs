using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Storage-site and issue-point policy (SafeTrace location / SoftBank dictionary).
/// Enforces AABB 5.1.8 component storage, AABB 5.11 / 5.16 remote and electronic
/// issue, and 21 CFR 606.160 location-of-disposition records.
/// </summary>
public static class InventoryLocationPolicyRule
{
    public const string ActiveCode = "INV-LOC-ACTIVE";
    public const string StorageCode = "INV-LOC-STORAGE";
    public const string IssueAllowedCode = "ISS-LOC-ISSUE";
    public const string RemoteCode = "ISS-LOC-REMOTE";
    public const string ElectronicCode = "ISS-LOC-EXM";
    public const string ExmEligibilityCode = "ISS-LOC-EXM-ELIGIBLE";
    public const string SecondVerifierCode = "ISS-LOC-VERIFY";
    public const string TempRangeCode = "INV-LOC-TEMP";

    public static bool AllowsComponent(InventoryLocation location, ComponentClass component)
    {
        ArgumentNullException.ThrowIfNull(location);
        return AllowsComponent(
            location.AllowsRbc,
            location.AllowsPlasma,
            location.AllowsPlatelets,
            location.AllowsCryo,
            location.AllowsWholeBlood,
            component);
    }

    public static bool AllowsComponent(
        bool allowsRbc,
        bool allowsPlasma,
        bool allowsPlatelets,
        bool allowsCryo,
        bool allowsWholeBlood,
        ComponentClass component) =>
        component switch
        {
            ComponentClass.RedBloodCells => allowsRbc,
            ComponentClass.WholeBlood => allowsWholeBlood,
            ComponentClass.Plasma => allowsPlasma,
            ComponentClass.Platelets => allowsPlatelets,
            ComponentClass.Cryoprecipitate => allowsCryo,
            ComponentClass.Granulocytes => allowsPlatelets || allowsRbc,
            _ => true
        };

    public static IReadOnlyList<RuleResult> EvaluateTransfer(
        bool destinationKnown,
        bool destinationActive,
        bool allowsComponent,
        decimal? storageMinC,
        decimal? storageMaxC)
    {
        if (!destinationKnown)
        {
            return [RuleResult.HardStop(ActiveCode, "Destination inventory location was not found.")];
        }

        var results = new List<RuleResult>
        {
            destinationActive
                ? RuleResult.Pass(ActiveCode)
                : RuleResult.HardStop(ActiveCode, "Destination inventory location is inactive."),
            allowsComponent
                ? RuleResult.Pass(StorageCode)
                : RuleResult.HardStop(StorageCode, "Destination does not permit this component class (AABB 5.1.8 storage).")
        };

        if (storageMinC is { } min && storageMaxC is { } max && min > max)
        {
            results.Add(RuleResult.HardStop(TempRangeCode, "Location storage temperature minimum cannot exceed the maximum."));
        }
        else
        {
            results.Add(RuleResult.Pass(TempRangeCode));
        }

        return results;
    }

    public static IReadOnlyList<RuleResult> EvaluateIssue(
        bool locationKnown,
        bool locationActive,
        bool allowsComponent,
        bool allowsIssue,
        bool allowsRemoteIssue,
        bool allowsElectronicIssue,
        bool requiresSecondVerifier,
        bool hasSecondVerifier,
        bool isRemoteIssue,
        bool isElectronicIssue,
        bool isEmergencyRelease,
        bool electronicCrossmatchEligible)
    {
        if (!locationKnown)
        {
            return EvaluateIssueWithoutLocation(isRemoteIssue, isElectronicIssue, isEmergencyRelease, electronicCrossmatchEligible);
        }

        var results = new List<RuleResult>
        {
            locationActive
                ? RuleResult.Pass(ActiveCode)
                : RuleResult.HardStop(ActiveCode, "The unit's inventory location is inactive."),
            allowsComponent
                ? RuleResult.Pass(StorageCode)
                : RuleResult.HardStop(StorageCode, "This location does not permit this component class.")
        };

        var issuePermitted = isEmergencyRelease
            ? allowsIssue || allowsRemoteIssue
            : isRemoteIssue
                ? allowsRemoteIssue
                : allowsIssue;

        results.Add(issuePermitted
            ? RuleResult.Pass(IssueAllowedCode)
            : RuleResult.HardStop(
                IssueAllowedCode,
                isRemoteIssue
                    ? "Remote issue is not enabled for this storage location."
                    : "This location is not configured as an issue point."));

        if (isRemoteIssue && !isEmergencyRelease)
        {
            results.Add(allowsRemoteIssue
                ? RuleResult.Pass(RemoteCode)
                : RuleResult.HardStop(RemoteCode, "Remote / satellite issue is not permitted from this location."));
            results.Add(electronicCrossmatchEligible
                ? RuleResult.Pass(ExmEligibilityCode)
                : RuleResult.HardStop(
                    ExmEligibilityCode,
                    "Remote issue requires electronic crossmatch eligibility (AABB 5.16) or an emergency release."));
        }
        else
        {
            results.Add(RuleResult.Pass(RemoteCode));
            results.Add(RuleResult.Pass(ExmEligibilityCode));
        }

        if (isElectronicIssue)
        {
            results.Add(allowsElectronicIssue
                ? RuleResult.Pass(ElectronicCode)
                : RuleResult.HardStop(ElectronicCode, "Electronic issue is not permitted from this location."));
            results.Add(electronicCrossmatchEligible
                ? RuleResult.Pass(ExmEligibilityCode + ".ELIG")
                : RuleResult.HardStop(
                    ExmEligibilityCode + ".ELIG",
                    "Electronic issue requires two concordant ABO/Rh determinations, a negative antibody screen, and no significant antibody history (AABB 5.16)."));
        }
        else
        {
            results.Add(RuleResult.Pass(ElectronicCode));
        }

        results.Add(!requiresSecondVerifier || hasSecondVerifier
            ? RuleResult.Pass(SecondVerifierCode)
            : RuleResult.HardStop(
                SecondVerifierCode,
                "This location requires a distinct second verifier before issue (AABB two-person identification)."));

        return results;
    }

    public static void ApplyTypeDefaults(InventoryLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        switch (location.LocationType)
        {
            case LocationType.Refrigerator:
                location.AllowsRbc = true;
                location.AllowsWholeBlood = true;
                location.AllowsPlasma = false;
                location.AllowsPlatelets = false;
                location.AllowsCryo = false;
                location.AllowsIssue = true;
                location.AllowsRemoteIssue = false;
                location.AllowsElectronicIssue = true;
                location.IsSatellite = false;
                location.StorageTempMinC ??= 1m;
                location.StorageTempMaxC ??= 6m;
                break;
            case LocationType.SatelliteRefrigerator:
                location.AllowsRbc = true;
                location.AllowsWholeBlood = true;
                location.AllowsPlasma = false;
                location.AllowsPlatelets = false;
                location.AllowsCryo = false;
                location.AllowsIssue = false;
                location.AllowsRemoteIssue = true;
                location.AllowsElectronicIssue = true;
                location.IsSatellite = true;
                location.StorageTempMinC ??= 1m;
                location.StorageTempMaxC ??= 6m;
                break;
            case LocationType.Freezer:
                location.AllowsRbc = false;
                location.AllowsWholeBlood = false;
                location.AllowsPlasma = true;
                location.AllowsPlatelets = false;
                location.AllowsCryo = true;
                location.AllowsIssue = true;
                location.AllowsRemoteIssue = false;
                location.AllowsElectronicIssue = false;
                location.IsSatellite = false;
                location.StorageTempMinC ??= -30m;
                location.StorageTempMaxC ??= -18m;
                break;
            case LocationType.PlateletIncubator:
                location.AllowsRbc = false;
                location.AllowsWholeBlood = false;
                location.AllowsPlasma = false;
                location.AllowsPlatelets = true;
                location.AllowsCryo = false;
                location.AllowsIssue = true;
                location.AllowsRemoteIssue = false;
                location.AllowsElectronicIssue = false;
                location.IsSatellite = false;
                location.StorageTempMinC ??= 20m;
                location.StorageTempMaxC ??= 24m;
                break;
            case LocationType.Cooler:
                location.AllowsRbc = true;
                location.AllowsWholeBlood = true;
                location.AllowsPlasma = false;
                location.AllowsPlatelets = false;
                location.AllowsCryo = false;
                location.AllowsIssue = true;
                location.AllowsRemoteIssue = true;
                location.AllowsElectronicIssue = true;
                location.IsSatellite = false;
                location.StorageTempMinC ??= 1m;
                location.StorageTempMaxC ??= 10m;
                break;
            case LocationType.Issue:
                location.AllowsIssue = true;
                location.AllowsRemoteIssue = false;
                location.AllowsElectronicIssue = true;
                location.IsSatellite = false;
                break;
        }
    }

    private static IReadOnlyList<RuleResult> EvaluateIssueWithoutLocation(
        bool isRemoteIssue,
        bool isElectronicIssue,
        bool isEmergencyRelease,
        bool electronicCrossmatchEligible)
    {
        var results = new List<RuleResult>
        {
            RuleResult.Pass(ActiveCode),
            RuleResult.Pass(StorageCode),
            RuleResult.Pass(IssueAllowedCode),
            RuleResult.Pass(RemoteCode),
            RuleResult.Pass(ElectronicCode),
            RuleResult.Pass(SecondVerifierCode)
        };

        if (isRemoteIssue && !isEmergencyRelease && !electronicCrossmatchEligible)
        {
            results.Add(RuleResult.HardStop(
                ExmEligibilityCode,
                "Remote issue requires electronic crossmatch eligibility (AABB 5.16) or an emergency release."));
        }
        else if (isElectronicIssue && !electronicCrossmatchEligible)
        {
            results.Add(RuleResult.HardStop(
                ExmEligibilityCode + ".ELIG",
                "Electronic issue requires two concordant ABO/Rh determinations, a negative antibody screen, and no significant antibody history (AABB 5.16)."));
        }
        else
        {
            results.Add(RuleResult.Pass(ExmEligibilityCode));
        }

        return results;
    }
}
