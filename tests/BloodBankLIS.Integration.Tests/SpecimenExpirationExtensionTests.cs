using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-1429: facility policy Medical director may extend tube expiration. On (the default), an expiration later than
// the policy window needs the override naming the medical director; off, it is refused while an earlier expiration is
// still allowed with an override. An unchanged extended expiration is not approved again on a later edit, and an expired
// tube still in storage can be extended, going back to Received.
public class SpecimenExpirationExtensionTests
{
    private static SpecimenService Specimens(SqliteContextFactory f, BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, f.Clock, audit: new AuditWriter(c, f.Clock, f.CurrentUser),
            currentUser: f.CurrentUser, permissions: new FixedPermissionEvaluator(2),
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(c),
            overrides: new EfRepository<Override>(c),
            policy: new FacilityPolicyService(new EfRepository<SystemSetting>(c)));

    private static async Task<(SqliteContextFactory Factory, Patient Patient)> SetUpAsync(bool? allowExtension)
    {
        var factory = new SqliteContextFactory();
        await using var c = factory.Create();
        var patient = new Patient { MedicalRecordNumber = "MRN-EXT", LastName = "Extend", FirstName = "Tube", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.Patients.Add(patient);
        c.ExceptionDefinitions.Add(new ExceptionDefinition
        {
            RuleCode = SpecimenExpirationOverrideRule.Code, Name = "Specimen expiration override", MinSecurityLevel = 2, IsOverridable = true, IsActive = true
        });
        if (allowExtension is bool allow)
        {
            c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.SpecimenAllowExpirationExtension, Value = allow ? "true" : "false" });
        }

        await c.SaveChangesAsync();
        return (factory, patient);
    }

    [Fact]
    public async Task Extension_NeedsTheMedicalDirector_AndIsRefusedWhenTheFacilityTurnsItOff()
    {
        var (factory, patient) = await SetUpAsync(allowExtension: false);
        using var _ = factory;
        await using var c = factory.Create();
        var collected = factory.Clock.UtcNow.AddHours(-1);
        var tube = new Specimen { PatientId = patient.Id, AccessionNumber = "ACC-EXT-1", SpecimenType = "EDTA", Status = SpecimenStatus.Accepted, CollectedUtc = collected, ReceivedUtc = collected };
        c.Specimens.Add(tube);
        await c.SaveChangesAsync();
        var id = tube.Id;
        // A save with no expiration given takes the policy window.
        var atPolicy = await Specimens(factory, c).UpdateAsync(id, new UpdateSpecimenRequest(collected));
        Assert.True(atPolicy.Succeeded, atPolicy.Error);
        var policy = atPolicy.Value!.ExpiresUtc!.Value;

        // Off: an extension is refused even with the override; an earlier expiration is still allowed with one.
        var refused = await Specimens(factory, c).UpdateAsync(id, new UpdateSpecimenRequest(
            collected, ExpiresUtc: policy.AddDays(4), OverrideReason: "Pre-op hold", AuthorizedBy: "Dr. Medical"));
        Assert.Equal(SpecimenExpirationOverrideRule.ExtensionNotAllowedText, refused.Error);
        var shortened = await Specimens(factory, c).UpdateAsync(id, new UpdateSpecimenRequest(
            collected, ExpiresUtc: policy.AddHours(-12), OverrideReason: "Transfused since", AuthorizedBy: "supervisor"));
        Assert.True(shortened.Succeeded, shortened.Error);

        // On: the extension asks for the medical director, then saves with the override.
        (await c.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.SpecimenAllowExpirationExtension)).Value = "true";
        await c.SaveChangesAsync();
        var asks = await Specimens(factory, c).UpdateAsync(id, new UpdateSpecimenRequest(collected, ExpiresUtc: policy.AddDays(4)));
        Assert.Contains(SpecimenExpirationOverrideRule.ExtensionNeedsApprovalText, asks.Error);
        var extended = await Specimens(factory, c).UpdateAsync(id, new UpdateSpecimenRequest(
            collected, ExpiresUtc: policy.AddDays(4), OverrideReason: "Pre-op hold", AuthorizedBy: "Dr. Medical"));
        Assert.True(extended.Succeeded, extended.Error);
        Assert.Equal(policy.AddDays(4), extended.Value!.ExpiresUtc);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityId == id && a.Reason != null && a.Reason.Contains("approved by Dr. Medical")));

        // A later edit that keeps the extended expiration is not approved again.
        var overrides = await c.Overrides.CountAsync(o => o.ContextId == id);
        var comment = await Specimens(factory, c).UpdateAsync(id, new UpdateSpecimenRequest(
            collected, Comment: "Kept for surgery", ExpiresUtc: policy.AddDays(4)));
        Assert.True(comment.Succeeded, comment.Error);
        Assert.Equal(overrides, await c.Overrides.CountAsync(o => o.ContextId == id));
    }

    [Fact]
    public async Task ExpiredTube_CanBeExtendedByTheMedicalDirector_AndGoesBackToReceived()
    {
        var (factory, patient) = await SetUpAsync(allowExtension: null);
        using var _ = factory;
        await using var c = factory.Create();
        var now = factory.Clock.UtcNow;
        Specimen Tube(string accession) => new()
        {
            PatientId = patient.Id, AccessionNumber = accession, SpecimenType = "EDTA", Status = SpecimenStatus.Expired,
            CollectedUtc = now.AddDays(-9), ReceivedUtc = now.AddDays(-9), ExpiresUtc = now.AddDays(-1)
        };
        var expired = Tube("ACC-EXT-OLD");
        var discarded = Tube("ACC-EXT-GONE");
        discarded.DiscardedUtc = now.AddHours(-1);
        c.Specimens.AddRange(expired, discarded);
        await c.SaveChangesAsync();

        var later = now.AddDays(10);
        var notLater = await Specimens(factory, c).UpdateAsync(expired.Id, new UpdateSpecimenRequest(
            expired.CollectedUtc, Comment: "x", ExpiresUtc: expired.ExpiresUtc));
        Assert.Contains("can only have its expiration extended", notLater.Error);
        Assert.Contains("cannot be edited", (await Specimens(factory, c).UpdateAsync(discarded.Id, new UpdateSpecimenRequest(
            discarded.CollectedUtc, ExpiresUtc: later, OverrideReason: "Repeat crossmatch", AuthorizedBy: "Dr. Medical"))).Error);

        var extended = await Specimens(factory, c).UpdateAsync(expired.Id, new UpdateSpecimenRequest(
            expired.CollectedUtc, ExpiresUtc: later, OverrideReason: "Repeat crossmatch", AuthorizedBy: "Dr. Medical"));
        Assert.True(extended.Succeeded, extended.Error);
        Assert.Equal((SpecimenStatus.Received, later), (extended.Value!.Status, extended.Value.ExpiresUtc!.Value));
        Assert.True(await c.Overrides.AnyAsync(o => o.ContextId == expired.Id && o.AuthorizedBy == "Dr. Medical"));
    }
}
