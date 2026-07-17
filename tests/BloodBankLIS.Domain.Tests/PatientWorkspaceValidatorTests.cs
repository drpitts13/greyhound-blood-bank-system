using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.PatientWorkspace;

namespace BloodBankLIS.Domain.Tests;

public class PatientWorkspaceValidatorTests
{
    [Fact]
    public void EncounterValidator_RejectsDischargeBeforeAdmit()
    {
        var e = new Encounter
        {
            PatientId = 1,
            VisitNumber = "V1",
            AdmitUtc = DateTime.UtcNow,
            DischargeUtc = DateTime.UtcNow.AddHours(-1)
        };

        var result = EncounterValidator.Validate(e);
        Assert.True(result.IsHardStopped);
    }

    [Fact]
    public void OrderValidator_RequiresEncounter()
    {
        var order = new Order
        {
            PatientId = 1,
            EncounterId = 0,
            OrderingLocationId = 1,
            OrderNumber = "O1",
            OrderName = "Type and Screen",
            OrderedUtc = DateTime.UtcNow
        };

        var result = OrderValidator.Validate(order, [], true, false, false, true, true);
        Assert.Contains(result.HardStops, r => r.Code == "ORDER.ENCOUNTER.REQUIRED");
    }

    [Fact]
    public void OrderValidator_RequiresOrderingLocation()
    {
        var order = new Order
        {
            PatientId = 1,
            EncounterId = 1,
            OrderingLocationId = 0,
            OrderNumber = "O1",
            OrderName = "Type and Screen",
            OrderedUtc = DateTime.UtcNow
        };

        var result = OrderValidator.Validate(order, [], true, true, true, false, false);
        Assert.Contains(result.HardStops, r => r.Code == "ORDER.LOCATION.REQUIRED");
    }

    [Fact]
    public void OrderValidator_CancelledRequiresReason()
    {
        var order = new Order
        {
            PatientId = 1,
            EncounterId = 1,
            OrderingLocationId = 1,
            OrderNumber = "O1",
            OrderName = "Type and Screen",
            OrderedUtc = DateTime.UtcNow,
            Status = OrderStatus.Cancelled
        };

        var result = OrderValidator.Validate(order, [], true, true, true, true, true);
        Assert.Contains(result.HardStops, r => r.Code == "ORDER.CANCEL.REASON.REQUIRED");
    }

    [Fact]
    public void OrderValidator_RequiresAtLeastOneLine()
    {
        var order = new Order
        {
            PatientId = 1,
            EncounterId = 1,
            OrderingLocationId = 1,
            OrderNumber = "O1",
            OrderedUtc = DateTime.UtcNow
        };

        var result = OrderValidator.Validate(order, [], true, true, true, true, true);
        Assert.Contains(result.HardStops, r => r.Code == "ORDER.LINES.REQUIRED");
    }

    [Fact]
    public void OrderValidator_ProductRequiresProductType()
    {
        var order = new Order
        {
            PatientId = 1,
            EncounterId = 1,
            OrderingLocationId = 1,
            OrderNumber = "O1",
            OrderName = "RBC",
            OrderCategory = OrderCategory.Product,
            OrderedUtc = DateTime.UtcNow
        };

        var lines = new[]
        {
            new OrderLine { LineCategory = OrderCategory.Product, LineName = "RBC" }
        };

        var result = OrderValidator.Validate(order, lines, true, true, true, true, true);
        Assert.Contains(result.HardStops, r => r.Code == "ORDER.PRODUCT.REQUIRED");
    }
}
