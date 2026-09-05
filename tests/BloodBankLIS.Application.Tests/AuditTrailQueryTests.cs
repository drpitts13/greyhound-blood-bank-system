using BloodBankLIS.Application.Audit;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class AuditTrailQueryTests
{
    [Fact]
    public void Filter_ByNamedEventType_ExcludesOtherActions()
    {
        var events = new[]
        {
            Event(1, AuditEventType.Issue, "BloodUnit", 10, "tech1", new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc)),
            Event(2, AuditEventType.EmergencyRelease, "BloodUnit", 10, "supervisor", new DateTime(2026, 5, 30, 13, 0, 0, DateTimeKind.Utc)),
            Event(3, AuditEventType.Result, "TestResult", 20, "tech1", new DateTime(2026, 5, 30, 14, 0, 0, DateTimeKind.Utc))
        }.AsQueryable();

        var filtered = AuditTrailQuery.Apply(events, null, null, AuditEventType.EmergencyRelease, null, null, null).ToList();

        Assert.Single(filtered);
        Assert.Equal(2, filtered[0].Id);
    }

    [Fact]
    public void Filter_ByUserAndWindow_IsInclusiveOnFrom_ExclusiveOnTo()
    {
        var start = new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
        var events = new[]
        {
            Event(1, AuditEventType.Result, "TestResult", 1, "tech1", start.AddMinutes(-1)),
            Event(2, AuditEventType.Result, "TestResult", 1, "tech1", start),
            Event(3, AuditEventType.Result, "TestResult", 1, "tech1", start.AddHours(1)),
            Event(4, AuditEventType.Result, "TestResult", 1, "tech2", start)
        }.AsQueryable();

        var filtered = AuditTrailQuery.Apply(
            events,
            entityType: "TestResult",
            entityId: 1,
            eventType: AuditEventType.Result,
            userName: "tech1",
            fromUtc: start,
            toUtc: start.AddHours(1)).ToList();

        Assert.Single(filtered);
        Assert.Equal(2, filtered[0].Id);
    }

    [Theory]
    [InlineData(null, true, null)]
    [InlineData("", true, null)]
    [InlineData("EmergencyRelease", true, AuditEventType.EmergencyRelease)]
    [InlineData("result", true, AuditEventType.Result)]
    [InlineData("NotAnEvent", false, null)]
    public void TryParseEventType_AcceptsNamedValues(string? raw, bool ok, AuditEventType? expected)
    {
        Assert.Equal(ok, AuditTrailQuery.TryParseEventType(raw, out var parsed));
        Assert.Equal(expected, parsed);
    }

    private static AuditEvent Event(
        long id,
        AuditEventType type,
        string entityType,
        long entityId,
        string user,
        DateTime when) =>
        new()
        {
            Id = id,
            EventType = type,
            EntityType = entityType,
            EntityId = entityId,
            UserName = user,
            OccurredUtc = when,
            Reason = type.ToString()
        };
}
