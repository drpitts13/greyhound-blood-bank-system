using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// TEST-BB-561: accession from the Specimens tab names the open test orders still waiting for a specimen and the types
/// they accept, warns when the chosen type does not suit them, and starts on a type that does.
/// </summary>
public class SpecimenAccessionWaitingTests
{
    private static readonly List<TestDefinitionListItemDto> Tests =
    [
        new("ABORH", "ABO/Rh", default, "EDTA"),
        new("ABSC", "Antibody screen", default, "EDTA,CLOT"),
        new("DAT", "DAT", default, null)
    ];

    private static PatientOrderDto Order(string number, OrderStatus status, long? specimenId, params string?[] codes) =>
        new(1, 1, 1, "V1", number, default, number, default, status, 1, "ER", null, null, specimenId, null, null, null, default,
            DateTime.UtcNow, null,
            codes.Select((c, i) => new OrderLineDto(i + 1, i + 1, default, c ?? "Product", c, default, null, null, null)).ToList());

    [Fact]
    public void Waiting_IsOpenTestOrdersWithoutASpecimen()
    {
        var orders = new[]
        {
            Order("ORD-1", OrderStatus.New, null, "ABORH", "absc"),
            Order("ORD-2", OrderStatus.New, 40, "DAT"),              // has a specimen
            Order("ORD-3", OrderStatus.Cancelled, null, "DAT"),      // cancelled
            Order("ORD-4", OrderStatus.New, null, (string?)null),    // no test line
            Order("ORD-5", OrderStatus.InProcess, null, "ABSC", "DAT")
        };

        var (numbers, codes) = SpecimenTypeOrderHint.Waiting(orders);
        Assert.Equal(["ORD-1", "ORD-5"], numbers);
        Assert.Equal(["ABORH", "ABSC", "DAT"], codes);
        Assert.Equal(
            "Waiting for a specimen: ORD-1, ORD-5 (ABORH, ABSC, DAT). ABORH accepts EDTA; ABSC accepts EDTA or CLOT. EDTA suits them all.",
            SpecimenTypeOrderHint.WaitingNote(orders, Tests));
        Assert.Null(SpecimenTypeOrderHint.WaitingNote([Order("ORD-2", OrderStatus.New, 40, "DAT")], Tests));
    }

    [Fact]
    public void AccessionType_StartsOnATypeTheWaitingOrdersAccept()
    {
        var orders = new[] { Order("ORD-1", OrderStatus.New, null, "ABORH") };
        var site = new List<SpecimenTypeListItemDto> { new("EDTA", "EDTA", 1, "D3", default), new("CLOT", "Clot", 2, "D3", default) };

        Assert.Equal("EDTA", SpecimenTypeOrderHint.AccessionTypeFor(orders, Tests, "CLOT", site));
        Assert.Null(SpecimenTypeOrderHint.AccessionTypeFor(orders, Tests, "EDTA", site));            // already suits
        Assert.Null(SpecimenTypeOrderHint.AccessionTypeFor([], Tests, "CLOT", site));                // nothing waiting
        Assert.Null(SpecimenTypeOrderHint.AccessionTypeFor(orders, Tests, "CLOT", [site[1]]));        // site lacks EDTA
        Assert.NotNull(SpecimenTypeOrderHint.MismatchWarning(Tests, SpecimenTypeOrderHint.Waiting(orders).TestCodes, "CLOT"));
    }

    // TEST-BB-562: the orders a tube accepted at accession is linked to.
    [Fact]
    public void LinkableOnAccession_IsTestOrdersOnTheVisitThatAcceptTheType()
    {
        var product = Order("ORD-P", OrderStatus.New, null, "ABORH") with
        {
            Lines = [new OrderLineDto(9, 1, OrderCategory.Product, "RBC", null, default, 1, null, null), new OrderLineDto(10, 2, default, "ABORH", "ABORH", default, null, null, null)]
        };
        var orders = new[]
        {
            Order("ORD-1", OrderStatus.New, null, "ABORH"),
            Order("ORD-2", OrderStatus.New, null, "ABSC", "DAT"),
            Order("ORD-3", OrderStatus.New, 40, "ABORH"),           // already has a tube
            product,                                               // product order: needs a completed type and screen
            Order("ORD-4", OrderStatus.New, null, "ABORH") with { EncounterId = 2 }
        };

        Assert.Equal(["ORD-1", "ORD-2"], SpecimenTypeOrderHint.LinkableOnAccession(orders, Tests, "EDTA", 1).Select(o => o.OrderNumber));
        Assert.Equal(["ORD-1", "ORD-2", "ORD-4"], SpecimenTypeOrderHint.LinkableOnAccession(orders, Tests, "EDTA", 0).Select(o => o.OrderNumber));
        Assert.Equal(["ORD-2"], SpecimenTypeOrderHint.LinkableOnAccession(orders, Tests, "CLOT", 1).Select(o => o.OrderNumber));
        Assert.Empty(SpecimenTypeOrderHint.LinkableOnAccession(orders, Tests, "HEP", 1));
    }
}
