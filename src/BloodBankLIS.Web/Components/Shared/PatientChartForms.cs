using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Web.Components.Shared;

public sealed class PatientAccessionForm
{
    public string AccessionNumber { get; set; } = string.Empty;
    public string SpecimenType { get; set; } = "EDTA";
    public DateTime CollectedLocal { get; set; } = DateTime.UtcNow;
    public int? ValidityHours { get; set; }
    public string? Identifier1Value { get; set; }
    public string? Identifier2Value { get; set; }
    public string? Collector { get; set; }
    public string? DrawLocation { get; set; }
    public string? Comment { get; set; }
    public bool AcceptImmediately { get; set; } = true;
    // The visit the tube was drawn on; 0 when none is chosen.
    public long EncounterId { get; set; }
    // Waiting orders the user unticked under "Link to waiting orders"; the rest that suit the tube are linked.
    public HashSet<long> SkipLinkOrderIds { get; } = new();
}

public sealed class PatientSpecimenEditForm
{
    public DateTime CollectedLocal { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresLocal { get; set; } = DateTime.UtcNow;
    public string SpecimenType { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? DrawLocation { get; set; }
    public string? Collector { get; set; }
    public int PolicyHours { get; set; } = 168;
    public DateTime PolicyExpires { get; set; } = DateTime.UtcNow;
    public bool ExpiryTouched { get; set; }
    public string? Comment { get; set; }
    public string OverrideReason { get; set; } = string.Empty;
    public string AuthorizedBy { get; set; } = string.Empty;
}

public sealed class PatientDemoEditModel
{
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Sex Sex { get; set; } = Sex.Unknown;
    public string Race { get; set; } = string.Empty;
    public string Ethnicity { get; set; } = string.Empty;
    public PatientStatus Status { get; set; } = PatientStatus.Active;
    public string? Comment { get; set; }
    public string NameChangeReason { get; set; } = string.Empty;
}

public static class SpecimenTypeOrderHint
{
    public static string? FirstRequiredType(
        IEnumerable<TestDefinitionListItemDto> tests,
        IEnumerable<string> selectedCodes)
    {
        // A type that suits every selected test, when there is one; otherwise the first test's preferred type.
        var codes = selectedCodes.ToList();
        var (common, _) = AcceptedSpecimenTypes.Common(Selected(tests, codes));
        if (common.Count > 0)
        {
            return common[0];
        }

        var byCode = tests.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            if (byCode.TryGetValue(code, out var test) && AcceptedSpecimenTypes.Preferred(test.RequiredSpecimenType) is { } preferred)
            {
                return preferred;
            }
        }

        return null;
    }

    /// <summary>The selected tests as (code, accepted types) pairs for <see cref="AcceptedSpecimenTypes"/>.</summary>
    public static IReadOnlyList<(string TestCode, string? Stored)> Selected(
        IEnumerable<TestDefinitionListItemDto> tests,
        IEnumerable<string> selectedCodes)
    {
        var byCode = tests.GroupBy(t => t.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return selectedCodes
            .Where(byCode.ContainsKey)
            .Select(c => (byCode[c].Code, byCode[c].RequiredSpecimenType))
            .ToList();
    }

    public static string? MismatchWarning(
        IEnumerable<TestDefinitionListItemDto> tests,
        IEnumerable<string> selectedCodes,
        string? specimenType)
    {
        if (string.IsNullOrWhiteSpace(specimenType))
        {
            return null;
        }

        var mismatched = tests
            .Where(t => selectedCodes.Contains(t.Code, StringComparer.OrdinalIgnoreCase)
                && !AcceptedSpecimenTypes.Accepts(t.RequiredSpecimenType, specimenType))
            .Select(t => $"{t.Code} requires {AcceptedSpecimenTypes.Describe(t.RequiredSpecimenType)}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (mismatched.Count == 0)
        {
            return null;
        }

        return $"Specimen type '{specimenType}' does not match {string.Join("; ", mismatched)}. Result entry will be blocked unless the specimen type matches.";
    }

    /// <summary>
    /// The chart's open orders with test lines and no specimen yet: their order numbers and the test codes they carry.
    /// </summary>
    public static (IReadOnlyList<string> OrderNumbers, IReadOnlyList<string> TestCodes) Waiting(IEnumerable<PatientOrderDto> orders)
    {
        var waiting = WaitingOrders(orders).Select(o => (o.OrderNumber, Codes: TestCodes(o))).ToList();
        return (
            waiting.Select(o => o.OrderNumber).ToList(),
            waiting.SelectMany(o => o.Codes).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>Open orders with test lines and no specimen yet.</summary>
    public static IReadOnlyList<PatientOrderDto> WaitingOrders(IEnumerable<PatientOrderDto> orders) =>
        orders
            .Where(o => o.SpecimenId is null or 0
                && o.Status is not (OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued)
                && TestCodes(o).Count > 0)
            .ToList();

    private static List<string> TestCodes(PatientOrderDto order) =>
        order.Lines
            .Select(l => l.TestCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

    /// <summary>
    /// Waiting orders a new tube can be linked to as it is accepted: test-only orders (a product order needs a completed
    /// type and screen first) whose tests all accept the tube's type, on the visit the tube was drawn on when one is chosen.
    /// </summary>
    public static IReadOnlyList<PatientOrderDto> LinkableOnAccession(
        IEnumerable<PatientOrderDto> orders,
        IEnumerable<TestDefinitionListItemDto> tests,
        string? specimenType,
        long encounterId)
    {
        var testList = tests.ToList();
        return WaitingOrders(orders)
            .Where(o => o.Lines.All(l => l.LineCategory != OrderCategory.Product))
            .Where(o => encounterId <= 0 || o.EncounterId == encounterId)
            .Where(o => MismatchWarning(testList, TestCodes(o), specimenType) is null)
            .ToList();
    }

    /// <summary>
    /// For accession from the Specimens tab: "Waiting for a specimen: ORD-1 (ABORH, ABSC). ABORH accepts EDTA; …", or
    /// null when no open test order is waiting.
    /// </summary>
    public static string? WaitingNote(IEnumerable<PatientOrderDto> orders, IEnumerable<TestDefinitionListItemDto> tests)
    {
        var (numbers, codes) = Waiting(orders);
        if (codes.Count == 0)
        {
            return null;
        }

        var head = $"Waiting for a specimen: {string.Join(", ", numbers)} ({string.Join(", ", codes)}).";
        return AcceptedSpecimenTypes.OrderNote(Selected(tests, codes)) is { } note ? $"{head} {note}" : head;
    }

    /// <summary>
    /// The tube type to start a Specimens-tab accession on: the type that suits the waiting orders when the current one
    /// does not and the site offers it; otherwise null (keep the current type).
    /// </summary>
    public static string? AccessionTypeFor(
        IEnumerable<PatientOrderDto> orders,
        IReadOnlyList<TestDefinitionListItemDto> tests,
        string? currentType,
        IReadOnlyList<SpecimenTypeListItemDto> siteTypes)
    {
        var (_, codes) = Waiting(orders);
        if (codes.Count == 0 || MismatchWarning(tests, codes, currentType) is null)
        {
            return null;
        }

        var type = FirstRequiredType(tests, codes);
        return type is not null && (siteTypes.Count == 0 || siteTypes.Any(t => string.Equals(t.Code, type, StringComparison.OrdinalIgnoreCase)))
            ? type
            : null;
    }

    /// <summary>"ORD-1 (ABORH, ABSC); ORD-5 (DAT)".</summary>
    public static string OrderList(IEnumerable<PatientOrderDto> orders) =>
        string.Join("; ", orders.Select(o => $"{o.OrderNumber} ({string.Join(", ", TestCodes(o))})"));

    /// <summary>The outcome of linking a tube to waiting orders: the orders linked, and any not linked with the reason.</summary>
    public static string LinkOutcome(string accession, IReadOnlyList<string> linked, IReadOnlyList<string> failed) =>
        failed.Count == 0
            ? $"Tube {accession} linked to {string.Join(", ", linked)}."
            : $"Tube {accession} {(linked.Count > 0 ? $"linked to {string.Join(", ", linked)}, but " : string.Empty)}not linked to {string.Join("; ", failed)}. Link it from the Orders tab.";

    public static DateTime? PreviewExpiresUtc(
        DateTime collectedUtc,
        string? specimenType,
        IEnumerable<SpecimenTypeListItemDto> types)
    {
        var type = types.FirstOrDefault(t =>
            string.Equals(t.Code, specimenType, StringComparison.OrdinalIgnoreCase));
        if (type is null || !SpecimenExpirationCode.TryParse(type.ExpirationCode, out var code))
        {
            return null;
        }

        return SpecimenExpirationCalculator.ComputeExpiresUtc(collectedUtc, code, type.ExpirationMode);
    }
}
