using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AuditHashChainRuleTests
{
    private static readonly DateTime Occurred = new(2026, 9, 5, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ComputeRecordHash_IsDeterministic()
    {
        var first = AuditHashChainRule.ComputeRecordHash(Sample(AuditHashChainRule.GenesisPreviousHash));
        var second = AuditHashChainRule.ComputeRecordHash(Sample(AuditHashChainRule.GenesisPreviousHash));
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void ComputeRecordHash_ChangesWhenPayloadChanges()
    {
        var original = Sample(AuditHashChainRule.GenesisPreviousHash);
        var tampered = Sample(AuditHashChainRule.GenesisPreviousHash);
        tampered.NewValueJson = "{\"Status\":99}";

        Assert.NotEqual(
            AuditHashChainRule.ComputeRecordHash(original),
            AuditHashChainRule.ComputeRecordHash(tampered));
    }

    [Fact]
    public void Verify_EmptyOrUnhashed_Passes()
    {
        Assert.Equal(AuditHashChainRule.OkCode, AuditHashChainRule.Verify([]).Code);
        Assert.Equal(
            AuditHashChainRule.OkCode,
            AuditHashChainRule.Verify([Sample(previousHash: null)]).Code);
    }

    [Fact]
    public void Verify_IntactChain_Passes()
    {
        var first = Sample(AuditHashChainRule.GenesisPreviousHash);
        first.RecordHash = AuditHashChainRule.ComputeRecordHash(first);

        var second = Sample(first.RecordHash);
        second.Id = 2;
        second.EntityId = 20;
        second.RecordHash = AuditHashChainRule.ComputeRecordHash(second);

        Assert.Equal(AuditHashChainRule.OkCode, AuditHashChainRule.Verify([first, second]).Code);
        Assert.Equal(AuditHashChainRule.OkCode, AuditHashChainRule.Verify([second, first]).Code);
    }

    [Fact]
    public void Verify_BrokenLink_IsHardStop()
    {
        var first = Sample(AuditHashChainRule.GenesisPreviousHash);
        first.RecordHash = AuditHashChainRule.ComputeRecordHash(first);

        var second = Sample("NOT-THE-PREVIOUS");
        second.Id = 2;
        second.RecordHash = AuditHashChainRule.ComputeRecordHash(second);

        var result = AuditHashChainRule.Verify([first, second]);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AuditHashChainRule.LinkCode, result.Code);
    }

    [Fact]
    public void Verify_TamperedPayload_IsHardStop()
    {
        var first = Sample(AuditHashChainRule.GenesisPreviousHash);
        first.RecordHash = AuditHashChainRule.ComputeRecordHash(first);
        first.NewValueJson = "{\"tampered\":true}";

        var result = AuditHashChainRule.Verify([first]);
        Assert.Equal(AuditHashChainRule.PayloadCode, result.Code);
    }

    private static AuditEvent Sample(string? previousHash) => new()
    {
        Id = 1,
        EventType = AuditEventType.Create,
        EntityType = "Patient",
        EntityId = 10,
        UserName = "tech-test",
        Workstation = "WORKSTATION-1",
        OccurredUtc = Occurred,
        NewValueJson = "{\"Status\":1}",
        PreviousHash = previousHash
    };
}
