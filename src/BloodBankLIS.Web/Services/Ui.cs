using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>Presentation helpers: maps domain enums to badge styles and labels.</summary>
public static class Ui
{
    public static string Badge(UnitStatus status) => status switch
    {
        UnitStatus.Available => "badge badge-green",
        UnitStatus.Allocated => "badge badge-blue",
        UnitStatus.Issued => "badge badge-amber",
        UnitStatus.Transfused => "badge badge-gray",
        UnitStatus.Quarantine or UnitStatus.Received or UnitStatus.OnHold or UnitStatus.Missing => "badge badge-amber",
        UnitStatus.Discarded or UnitStatus.Expired or UnitStatus.ReturnedToSupplier or UnitStatus.Damaged => "badge badge-red",
        UnitStatus.ShippedOut => "badge badge-gray",
        UnitStatus.Returned => "badge badge-gray",
        _ => "badge badge-gray"
    };

    public static string StatusLabel(UnitStatus status) =>
        status == UnitStatus.Received ? "Pending Retype" : status.ToString();

    /// <summary>
    /// How a choice reads in a dropdown: an enum in words ("EmergencyRelease" → "Emergency release", "CmvNegative" →
    /// "CMV negative", "Hl7" → "HL7"), using the curated label where one exists (product classes, pending retype);
    /// anything else as it is. The option's value stays the enum name, so binding and stored data do not change.
    /// </summary>
    public static string EnumText(object? value) => value switch
    {
        null => string.Empty,
        ComponentClass componentClass => ComponentClassLabels.Label(componentClass),
        UnitStatus.Received => StatusLabel(UnitStatus.Received),
        Enum e => CodeNameText(e.ToString()),
        _ => value.ToString() ?? string.Empty
    };

    private static readonly Dictionary<string, string> Acronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Abo"] = "ABO", ["Astm"] = "ASTM", ["Cmv"] = "CMV", ["Csv"] = "CSV", ["Dat"] = "DAT", ["Ehr"] = "EHR",
        ["Ffp"] = "FFP", ["Fhir"] = "FHIR", ["Hl7"] = "HL7", ["Hla"] = "HLA", ["Http"] = "HTTP", ["Iat"] = "IAT",
        ["Id"] = "ID", ["Isbt"] = "ISBT", ["Lis"] = "LIS", ["Mfa"] = "MFA", ["Mllp"] = "MLLP", ["Mtp"] = "MTP",
        ["Pdf"] = "PDF", ["Plt"] = "PLT", ["Rbc"] = "RBC", ["Tcp"] = "TCP", ["Url"] = "URL", ["Xm"] = "XM"
    };

    /// <summary>A code name in words: split at case changes, acronyms kept, later words lower case.</summary>
    public static string CodeNameText(string name)
    {
        // "RhD" is one word; keep it whole before splitting at case changes.
        var tokens = System.Text.RegularExpressions.Regex
            .Split(name.Replace("RhD", "Rh\u0001D", StringComparison.Ordinal), "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")
            .Where(t => t.Length > 0)
            .Select(t => t.Replace("\u0001", string.Empty, StringComparison.Ordinal))
            .ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            tokens[i] = Acronyms.TryGetValue(t, out var acronym) ? acronym
                : t.Length > 1 && t.All(char.IsUpper) ? t
                : t == "RhD" ? t
                : i == 0 ? char.ToUpperInvariant(t[0]) + t[1..]
                : t.ToLowerInvariant();
        }

        return string.Join(' ', tokens);
    }

    public static string Badge(SpecimenStatus status) => status switch
    {
        SpecimenStatus.Accepted => "badge badge-green",
        SpecimenStatus.Received or SpecimenStatus.Collected => "badge badge-blue",
        SpecimenStatus.Rejected or SpecimenStatus.Expired or SpecimenStatus.Cancelled => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(ResultStatus status) => status switch
    {
        ResultStatus.Verified => "badge badge-green",
        ResultStatus.Entered => "badge badge-blue",
        ResultStatus.PendingVerification => "badge badge-amber",
        ResultStatus.Corrected => "badge badge-amber",
        ResultStatus.Invalidated => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(BillingEventStatus status) => status switch
    {
        BillingEventStatus.Pending => "badge badge-amber",
        BillingEventStatus.Reviewed => "badge badge-blue",
        BillingEventStatus.Exported => "badge badge-green",
        BillingEventStatus.Cancelled => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(Hl7MessageStatus status) => status switch
    {
        Hl7MessageStatus.Processed or Hl7MessageStatus.Acked => "badge badge-green",
        Hl7MessageStatus.Received or Hl7MessageStatus.Replayed => "badge badge-blue",
        Hl7MessageStatus.Errored or Hl7MessageStatus.Nacked => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(PrintJobStatus status) => status switch
    {
        PrintJobStatus.Printed => "badge badge-green",
        PrintJobStatus.Queued => "badge badge-blue",
        PrintJobStatus.Failed => "badge badge-red",
        _ => "badge badge-gray"
    };

    public static string Badge(ProductAllocationDisplayStatus status) => status switch
    {
        ProductAllocationDisplayStatus.ReadyForIssue => "badge badge-green",
        ProductAllocationDisplayStatus.Exception => "badge badge-red",
        ProductAllocationDisplayStatus.Reserved => "badge badge-amber",
        ProductAllocationDisplayStatus.CrossmatchExpired => "badge badge-amber",
        _ => "badge"
    };

    public static string StatusLabel(ProductAllocationDisplayStatus status) => status switch
    {
        ProductAllocationDisplayStatus.ReadyForIssue => "Ready for issue",
        ProductAllocationDisplayStatus.Exception => "Exception",
        ProductAllocationDisplayStatus.Reserved => "Reserved",
        ProductAllocationDisplayStatus.CrossmatchExpired => "Crossmatch expired",
        _ => status.ToString()
    };

    public static string Badge(AllocationStatus status) => status switch
    {
        AllocationStatus.Reserved => "badge badge-blue",
        AllocationStatus.Consumed => "badge badge-green",
        AllocationStatus.Released or AllocationStatus.Expired => "badge badge-gray",
        _ => "badge badge-gray"
    };

    public static string Badge(OrderStatus status) => status switch
    {
        OrderStatus.New or OrderStatus.InProcess or OrderStatus.Collected or OrderStatus.Received => "badge badge-blue",
        OrderStatus.Completed or OrderStatus.PartiallyComplete => "badge badge-green",
        OrderStatus.Cancelled or OrderStatus.Discontinued => "badge badge-red",
        OrderStatus.OnHold => "badge badge-amber",
        _ => "badge badge-gray"
    };

    public static string Badge(OrderActionStatus status) => status switch
    {
        OrderActionStatus.Pending => "badge badge-amber",
        OrderActionStatus.Completed => "badge badge-green",
        OrderActionStatus.Cancelled => "badge badge-gray",
        _ => "badge badge-gray"
    };

    public static string BloodType(AboGroup abo, RhType rh)
    {
        var group = abo == AboGroup.Unknown ? "?" : abo.ToString();
        var sign = rh switch { RhType.Positive => "+", RhType.Negative => "\u2212", _ => "?" };
        return $"{group}{sign}";
    }

    public static string CompatibleUnitChoiceLabel(CompatibleUnitDto u)
    {
        var label = $"{u.UnitNumber} · {u.BloodType} · {u.ProductCode} · {ReadyToIssueFormCue.ExpirationNotice(u.ExpiresUtc)}";
        var stored = ReadyToIssueFormCue.StorageLocationNotice(u.StorageLocationName);
        if (stored is not null)
        {
            label = $"{label} · {stored}";
        }
        if (!string.IsNullOrWhiteSpace(u.SelectionCue))
        {
            label = $"{label} · {u.SelectionCue}";
        }

        if (u.ExpiresSoon)
        {
            label = $"{label} · expires soon";
        }

        var xmNotice = ReadyToIssueFormCue.CrossmatchWorklistNotice(
            u.LatestCrossmatchResult,
            u.LatestCrossmatchResult == CrossmatchResult.Compatible ? u.CrossmatchExpiresUtc : null);
        if (xmNotice is not null)
        {
            label = $"{label} · {xmNotice}";
        }

        return u.AntigenNegFlag switch
        {
            BloodAttributeCompatibilityRule.UntestedFlag => $"{label} · not tested",
            BloodAttributeCompatibilityRule.NotNegativeFlag => $"{label} · not antigen-negative",
            BloodAttributeCompatibilityRule.MixedFlag => $"{label} · antigen exception",
            _ => label
        };
    }
}
