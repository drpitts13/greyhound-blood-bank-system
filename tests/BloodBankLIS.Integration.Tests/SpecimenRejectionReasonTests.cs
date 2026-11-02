using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-334: rejection requires an active coded reason, stored with its text.
public class SpecimenRejectionReasonTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SpecimenRejectionReasonTests(SqliteContextFactory factory) => _factory = factory;

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            rejectionReasons: new EfRepository<SpecimenRejectionReason>(c));

    private async Task<long> AccessionAsync(string accession)
    {
        await using var context = _factory.Create();
        if (!await context.SpecimenRejectionReasons.AnyAsync())
        {
            context.SpecimenRejectionReasons.AddRange(
                new SpecimenRejectionReason { Code = "CLOTTED", Text = "Clotted", SortOrder = 1, IsActive = true },
                new SpecimenRejectionReason { Code = "OTHER", Text = "Other", RequiresComment = true, SortOrder = 2, IsActive = true },
                new SpecimenRejectionReason { Code = "RETIRED", Text = "Retired reason", SortOrder = 3, IsActive = false });
        }

        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-" + accession,
            LastName = "Reject",
            FirstName = "Coded",
            DateOfBirth = new DateOnly(1975, 5, 5),
            Sex = Sex.Unknown
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var result = await Specimens(context).AccessionAsync(
            new AccessionSpecimenRequest(accession, patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(result.Succeeded, result.Error);
        return result.Value!.Id;
    }

    [Fact]
    public async Task CodedRejection_StoresCodeAndText_AndAudits()
    {
        var id = await AccessionAsync("ACC-REJ-CODED");
        await using var context = _factory.Create();
        var r = await Specimens(context).RejectAsync(id, "clotted", null);
        Assert.True(r.Succeeded, r.Error);

        var specimen = await context.Specimens.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Equal(SpecimenStatus.Rejected, specimen.Status);
        Assert.Equal("CLOTTED", specimen.RejectionReasonCode);
        Assert.Equal("Clotted", specimen.RejectionReason);
        Assert.True(await context.AuditEvents.AnyAsync(a => a.EntityType == nameof(Specimen) && a.EntityId == id
            && a.NewValueJson != null && a.NewValueJson.Contains("CLOTTED")));
    }

    [Fact]
    public async Task CommentRequiredReason_WithoutComment_IsRefused_ThenStoresComment()
    {
        var id = await AccessionAsync("ACC-REJ-OTHER");
        await using var context = _factory.Create();
        var refused = await Specimens(context).RejectAsync(id, "OTHER", " ");
        Assert.False(refused.Succeeded);
        Assert.Contains(SpecimenRejectionReasonRule.CommentRequiredCode, refused.Error);

        var ok = await Specimens(context).RejectAsync(id, "OTHER", "Tube cracked in transit");
        Assert.True(ok.Succeeded, ok.Error);
        var specimen = await context.Specimens.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Equal("Other: Tube cracked in transit", specimen.RejectionReason);
    }

    [Theory]
    [InlineData("NOPE", SpecimenRejectionReasonRule.UnknownCode)]
    [InlineData("RETIRED", SpecimenRejectionReasonRule.UnknownCode)]
    [InlineData("", SpecimenRejectionReasonRule.RequiredCode)]
    public async Task UnknownInactiveOrBlankReason_IsRefused_AndSpecimenUnchanged(string code, string expected)
    {
        var id = await AccessionAsync("ACC-REJ-BAD-" + (code.Length == 0 ? "BLANK" : code));
        await using var context = _factory.Create();
        var r = await Specimens(context).RejectAsync(id, code, null);
        Assert.False(r.Succeeded);
        Assert.Contains(expected, r.Error);
        Assert.NotEqual(SpecimenStatus.Rejected, (await context.Specimens.AsNoTracking().SingleAsync(s => s.Id == id)).Status);
    }
}
