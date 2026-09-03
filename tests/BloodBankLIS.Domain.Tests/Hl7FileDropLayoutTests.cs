using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.Domain.Tests;

public class Hl7FileDropLayoutTests
{
    [Theory]
    [InlineData("patient.hl7", true)]
    [InlineData("order.ADT", true)]
    [InlineData("note.txt", true)]
    [InlineData("message.ack", false)]
    [InlineData(".hidden.hl7", false)]
    [InlineData("", false)]
    public void IsInboxFileName(string name, bool expected) =>
        Assert.Equal(expected, Hl7FileDropLayout.IsInboxFileName(name));

    [Fact]
    public void OutboundFileName_SanitizesControlId()
    {
        var name = Hl7FileDropLayout.OutboundFileName("CTRL/1:A", new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal("20260903120000000_CTRL_1_A.hl7", name);
    }

    [Fact]
    public void AckFileName_ReplacesExtension() =>
        Assert.Equal("patient.ack", Hl7FileDropLayout.AckFileName("patient.hl7"));
}
