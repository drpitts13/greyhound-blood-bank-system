using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class OrderCancellationReasonValidatorTests
{
    [Fact]
    public void RequiresCodeAndText()
    {
        var evaluation = OrderCancellationReasonValidator.Validate(new OrderCancellationReason(), duplicateCode: false);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == OrderCancellationReasonValidator.CodeRequired);
        Assert.Contains(evaluation.HardStops, r => r.Code == OrderCancellationReasonValidator.TextRequired);
    }

    [Fact]
    public void RejectsDuplicateCode()
    {
        var evaluation = OrderCancellationReasonValidator.Validate(
            new OrderCancellationReason { Code = "ERR", Text = "Entered in error" },
            duplicateCode: true);
        Assert.Contains(evaluation.HardStops, r => r.Code == OrderCancellationReasonValidator.DuplicateCode);
    }
}
