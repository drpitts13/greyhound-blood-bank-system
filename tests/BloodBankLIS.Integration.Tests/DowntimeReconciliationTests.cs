using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class DowntimeReconciliationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public DowntimeReconciliationTests(SqliteContextFactory factory) => _factory = factory;

    private DowntimeReconciliationService Service(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<InterfaceErrorQueueItem>(c),
            new EfRepository<Hl7MessageLog>(c),
            new EfRepository<Issue>(c),
            new AuditQuery(c),
            _factory.Clock,
            _factory.CurrentUser,
            permissions);

    [Fact]
    public async Task GetSnapshot_WithoutAuditRead_IsBlocked()
    {
        await using var c = _factory.Create();
        var denied = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .GetSnapshotAsync();

        Assert.False(denied.Succeeded);
        Assert.NotNull(denied.Evaluation);
        Assert.Contains(
            denied.Evaluation!.HardStops,
            r => r.Code == DowntimeReconciliationAuthorizationRule.PermissionCode);
    }

    [Fact]
    public async Task GetSnapshot_CountsOpenWorkAndReportsChain()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var before = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .GetSnapshotAsync();
        Assert.True(before.Succeeded, before.Error);
        Assert.NotNull(before.Value);

        var inbound = new Hl7MessageLog
        {
            MessageControlId = $"DT-IN-{key}",
            RawMessage = "MSH|",
            ReceivedUtc = _factory.Clock.UtcNow,
            Direction = Hl7Direction.Inbound,
            Status = Hl7MessageStatus.Received
        };
        var outboundPending = new Hl7MessageLog
        {
            MessageControlId = $"DT-OUT-{key}",
            RawMessage = "MSH|",
            ReceivedUtc = _factory.Clock.UtcNow,
            Direction = Hl7Direction.Outbound,
            Status = Hl7MessageStatus.Received
        };
        var outboundAcked = new Hl7MessageLog
        {
            MessageControlId = $"DT-ACK-{key}",
            RawMessage = "MSH|",
            ReceivedUtc = _factory.Clock.UtcNow,
            Direction = Hl7Direction.Outbound,
            Status = Hl7MessageStatus.Acked
        };
        c.Hl7Messages.AddRange(inbound, outboundPending, outboundAcked);
        await c.SaveChangesAsync();

        c.InterfaceErrorQueue.AddRange(
            new InterfaceErrorQueueItem
            {
                Hl7MessageId = inbound.Id,
                ErrorType = "Parse",
                ErrorDetail = "Unresolved downtime fixture",
                Resolved = false
            },
            new InterfaceErrorQueueItem
            {
                Hl7MessageId = outboundAcked.Id,
                ErrorType = "Ack",
                ErrorDetail = "Already resolved",
                Resolved = true,
                ResolvedBy = "tech-test",
                ResolvedUtc = _factory.Clock.UtcNow
            });

        var product = new ProductType
        {
            ProductCode = $"RBC-DT-{key}",
            Name = "Downtime snapshot RBC",
            ComponentClass = ComponentClass.RedBloodCells
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = $"U-DT-{key}",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Issued
        };
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-DT-{key}",
            LastName = "Downtime",
            FirstName = "Snapshot",
            DateOfBirth = new DateOnly(1980, 1, 1)
        };
        c.Patients.Add(patient);
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        c.Issues.Add(new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            IssuedUtc = _factory.Clock.UtcNow.AddHours(-2),
            IssuedBy = "tech-test",
            Status = IssueStatus.Issued,
            RetrospectiveCrossmatchDueUtc = _factory.Clock.UtcNow.AddHours(-1)
        });
        await c.SaveChangesAsync();

        var after = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.AuditRead))
            .GetSnapshotAsync();
        Assert.True(after.Succeeded, after.Error);
        var snap = after.Value!;
        Assert.Equal(before.Value!.UnresolvedInterfaceErrors + 1, snap.UnresolvedInterfaceErrors);
        Assert.Equal(before.Value.PendingOutboundHl7 + 1, snap.PendingOutboundHl7);
        Assert.Equal(before.Value.OpenIssues + 1, snap.OpenIssues);
        Assert.Equal(before.Value.PendingRetrospectiveCrossmatches + 1, snap.PendingRetrospectiveCrossmatches);
        Assert.Equal(AuditHashChainRule.OkCode, snap.AuditChainCode);
        Assert.True(snap.HashedAuditRows >= 0);
        Assert.Equal(_factory.Clock.UtcNow, snap.GeneratedUtc);
    }
}
