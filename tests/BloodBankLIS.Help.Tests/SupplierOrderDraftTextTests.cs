using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class SupplierOrderDraftTextTests
{
    private static SupplierOrderDto Order(SupplierOrderStatus status, DateTime created, params SupplierOrderLineDto[] lines) =>
        new(1, "SO-1", 1, "ARC", status, null, created, "tech", null, null, null, null, null, lines.Sum(l => l.Quantity), lines);

    // TEST-BB-1322 (end-user testing pass 295): a draft supplier order says it has not been sent, for how long, and for
    // which patients; a submitted order says nothing.
    [Fact]
    public void Draft_SaysNotSent_HowLong_AndForWhom()
    {
        var now = new DateTime(2026, 11, 19, 19, 0, 0, DateTimeKind.Utc);
        var forAvery = new SupplierOrderLineDto(1, 1, "RBC-LR", "Red cells", AboGroup.O, 2) { PatientName = "Demo-Special, Avery", PatientMrn = "MRN0023" };
        var stock = new SupplierOrderLineDto(2, 1, "RBC-LR", "Red cells", AboGroup.O, 4);

        Assert.Equal("Not sent · 7 d 11 h · for Demo-Special, Avery (MRN0023)",
            SupplierOrderDraftText.Badge(Order(SupplierOrderStatus.Draft, now.AddDays(-7).AddHours(-11), forAvery, stock), now));
        Assert.Equal("Not sent · 30 min", SupplierOrderDraftText.Badge(Order(SupplierOrderStatus.Draft, now.AddMinutes(-30), stock), now));
        Assert.Null(SupplierOrderDraftText.Badge(Order(SupplierOrderStatus.Submitted, now.AddDays(-1), forAvery), now));
    }
}
