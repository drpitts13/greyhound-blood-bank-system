using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Astm.Messaging;
using BloodBankLIS.Astm.Simulator;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloodBankLIS.Integration.Tests;

public class InstrumentInterfaceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InstrumentInterfaceTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task AdminCreate_PersistsConnectionMapsAndRules()
    {
        await using var context = _factory.Create();
        var svc = Admin(context);
        var result = await svc.CreateAsync(new SaveInstrumentInterfaceRequest(
            $"Echo Test {Guid.NewGuid():N}"[..20],
            "Development",
            "seed",
            new InstrumentConnectionDto(null, 2577, true),
            [new InstrumentFieldMappingDto(InstrumentDataItemKeys.PatientId, "P.3", true)],
            [new InstrumentTranslationDto(InstrumentTranslationKeys.ResultTestCode, "Group", "ABORH", InterfaceTranslationDirection.Inbound, 1)],
            [new InstrumentRuleDto("forty", "message.subtest_result >= 40", "set('subtest_result','4+')", 1, true, false)]));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Value!.Connection.IsServer);
        Assert.Equal(2577, result.Value.Connection.Port);
        Assert.Single(result.Value.FieldMappings);
        Assert.Single(result.Value.Translations);
        Assert.Single(result.Value.Rules);
        Assert.False(result.Value.IsEnabled);
    }

    [Fact]
    public async Task InboundGroupResult_PostsInstrumentPendingVerification()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        await SeedOrderAsync(context, "MRN-INST-GRP", "ACC-INST-GRP", "ABORH");

        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-GRP", "MRN-INST-GRP", "Patient", "Test", "19590422", "M", 3));
        var outcome = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-GRP", "ABORH");
        Assert.Equal(ResultSource.Instrument, posted.Source);
        Assert.Equal(ResultStatus.PendingVerification, posted.Status);
        Assert.True(AboRhResultValueLooksLikePanel(posted.Value));
        Assert.True(await context.InstrumentMessages.AnyAsync(m => m.SpecimenId == "ACC-INST-GRP"));
        // TEST-BB-1042: the analyzer and its completion time go with the result (end-user testing pass 30).
        Assert.Matches(@" · M0002 · completed \d{4}-\d{2}-\d{2} \d{2}:\d{2} \(instrument clock\)", posted.SourceReference);
        Assert.StartsWith("M0002 · completed", ResultSourceDetail.For(posted.SourceReference));
    }

    // TEST-BB-400: a failed instrument message stays open until a replay applies it or someone resolves it with a
    // reason; a processed or resolved message is never replayed, so results are not filed twice.
    [Fact]
    public async Task FailedMessage_ClosesOnASuccessfulReplay_AndProcessedMessages_AreNotReplayed()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-LATE", "MRN-INST-LATE", "Patient", "Late", "19590422", "M", 3));

        // The result arrives before its order exists.
        var first = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);
        Assert.False(first.Accepted);
        var failedId = first.Log.Id;
        Assert.True(InstrumentMessageFollowUpRule.IsOpenFailure(first.Log.Status, first.Log.ResolvedUtc));

        await SeedOrderAsync(context, "MRN-INST-LATE", "ACC-INST-LATE", "ABORH");
        var replay = await Processor(context).ReplayAsync(failedId);
        Assert.True(replay!.Accepted, string.Join("; ", replay.Notes));
        var closed = await context.InstrumentMessages.AsNoTracking().SingleAsync(m => m.Id == failedId);
        Assert.NotNull(closed.ResolvedUtc);
        Assert.Equal(InstrumentMessageFollowUpRule.ReplayResolutionNote, closed.ResolutionNote);
        Assert.True(await context.AuditEvents.AnyAsync(a => a.EntityType == nameof(InstrumentMessageLog) && a.EntityId == failedId));

        // Neither the closed failure nor the processed replay can be replayed again.
        var count = await context.InstrumentMessages.CountAsync();
        var again = await Processor(context).ReplayAsync(failedId);
        Assert.True(again!.Refused);
        var processedAgain = await Processor(context).ReplayAsync(replay.Log.Id);
        Assert.True(processedAgain!.Refused);
        Assert.Contains("twice", processedAgain.Notes.Single());
        Assert.Equal(count, await context.InstrumentMessages.CountAsync());
    }

    [Fact]
    public async Task FailedMessage_ResolvedByHand_NeedsAReason_AndIsAudited()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        var failed = await Processor(context).ProcessAsync("not an ASTM message", iface.Id);
        Assert.False(failed.Accepted);

        var blank = await Processor(context).ResolveAsync(failed.Log.Id, "  ");
        Assert.Null(blank.Message);
        Assert.Contains(InstrumentMessageFollowUpRule.ResolveCode, blank.Error);

        var resolved = await Processor(context).ResolveAsync(failed.Log.Id, "Instrument resent after a cable fault; result entered by hand.");
        Assert.NotNull(resolved.Message);
        Assert.Equal("tech-test", resolved.Message!.ResolvedBy);
        Assert.True(await context.AuditEvents.AnyAsync(a => a.EntityType == nameof(InstrumentMessageLog)
            && a.EntityId == failed.Log.Id && a.Reason!.StartsWith("Instrument resent")));

        var twice = await Processor(context).ResolveAsync(failed.Log.Id, "Again");
        Assert.Contains("already resolved", twice.Error);
        Assert.True((await Processor(context).ReplayAsync(failed.Log.Id))!.Refused);
    }

    [Fact]
    public async Task InboundScreenResult_PostsInstrumentPendingVerification()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        await SeedOrderAsync(context, "MRN-INST-SCR", "ACC-INST-SCR", "ABSC");

        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Screen", "ACC-INST-SCR", "MRN-INST-SCR", "Patient", "Test", "19590422", "M", 5));
        var outcome = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-SCR", "ABSC");
        Assert.Equal(ResultSource.Instrument, posted.Source);
        Assert.Equal(ResultStatus.PendingVerification, posted.Status);
        Assert.Contains("AHG", posted.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cell1", posted.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InboundCrossmatchResult_PostsInstrumentPendingVerification()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        await SeedOrderAsync(context, "MRN-INST-XM", "ACC-INST-XM", "XM");

        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Crossmatch", "ACC-INST-XM", "MRN-INST-XM", "Patient", "Test", "19590422", "M", 6));
        var outcome = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-XM", "XM");
        Assert.Equal(ResultSource.Instrument, posted.Source);
        Assert.Equal(ResultStatus.PendingVerification, posted.Status);
        Assert.True(
            posted.Value.Contains("Compatible", StringComparison.OrdinalIgnoreCase)
            || posted.Value.Contains("Incompatible", StringComparison.OrdinalIgnoreCase)
            || posted.Interpretation is { } interp
                && (interp.Contains("Compatible", StringComparison.OrdinalIgnoreCase)
                    || interp.Contains("Incompatible", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task InboundQuery_BuildsOrderDownload()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        await SeedOrderAsync(context, "MRN-INST-Q", "ACC-INST-Q", "ABORH");

        const string query = "H|\\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219140243\rQ|1|ACC-INST-Q||ALL\rL|1|N\r";
        var outcome = await Processor(context).ProcessAsync(query, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        Assert.Contains("^^^Group", outcome.ReplyMessage);
        Assert.Contains("ACC-INST-Q", outcome.ReplyMessage);
    }

    [Fact]
    public async Task VerifiedResult_IsNotOverwritten()
    {
        await using var context = _factory.Create();
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        var iface = await context.InstrumentInterfaces.SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        var ids = await SeedOrderAsync(context, "MRN-INST", "ACC-INST-1", "ABORH");
        var specimen = await context.Specimens.SingleAsync(s => s.Id == ids.SpecimenId);
        context.TestResults.Add(new TestResult
        {
            SpecimenId = ids.SpecimenId,
            PatientId = specimen.PatientId,
            OrderId = ids.OrderId,
            TestCode = "ABORH",
            Value = "O|Positive",
            Status = ResultStatus.Verified,
            Source = ResultSource.Manual,
            EnteredUtc = _factory.Clock.UtcNow,
            EnteredBy = "tech",
            VerifiedUtc = _factory.Clock.UtcNow,
            VerifiedBy = "sup"
        });
        await context.SaveChangesAsync();

        var generated = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-1", "MRN-INST", "Patient", "Test", "19590422", "M", 4));
        var outcome = await Processor(context).ProcessAsync(generated.RawMessage, iface.Id);

        Assert.Contains(outcome.Notes, n => n.Contains("verified", StringComparison.OrdinalIgnoreCase)
            || n.Contains("cannot be overwritten", StringComparison.OrdinalIgnoreCase)
            || !outcome.Accepted);
        var remaining = await PostedResultAsync(context, "ACC-INST-1", "ABORH");
        Assert.Equal(ResultStatus.Verified, remaining.Status);
        Assert.Equal("O|Positive", remaining.Value);
    }

    [Fact]
    public async Task Create_WithoutAdminInstrumentsManage_IsRejected()
    {
        await using var context = _factory.Create();
        var name = $"INST-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var request = new SaveInstrumentInterfaceRequest(
            name,
            "Development",
            "seed",
            new InstrumentConnectionDto(null, 2580, true),
            [new InstrumentFieldMappingDto(InstrumentDataItemKeys.PatientId, "P.3", true)],
            [new InstrumentTranslationDto(InstrumentTranslationKeys.ResultTestCode, "Group", "ABORH", InterfaceTranslationDirection.Inbound, 1)],
            []);

        var denied = await Admin(context, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InstrumentInterfaceAuthorizationRule.CreateCode);
        Assert.False(await context.InstrumentInterfaces.AnyAsync(i => i.Name == name));

        var allowed = await Admin(context, new FixedPermissionEvaluator(1, PermissionCodes.AdminInstrumentsManage))
            .CreateAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(name, allowed.Value!.Name);
    }

    // TEST-BB-530: an assay code with no translation does not post; the message waits in failures and the code is
    // listed; saving a translation clears the list entry and a replay posts.
    [Fact]
    public async Task UnknownAssay_IsNotPosted_IsListed_AndPostsOnReplayAfterATranslation()
    {
        await using var context = _factory.Create();
        var iface = await EchoAsync(context);
        await SeedOrderAsync(context, "MRN-INST-ANT", "ACC-INST-ANT", "ABORH");
        var raw = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-ANT", "MRN-INST-ANT", "Patient", "Test", "19590422", "M", 3)).RawMessage
            .Replace("^^^Group", "^^^Antigen", StringComparison.Ordinal);

        var first = await Processor(context).ProcessAsync(raw, iface.Id);

        Assert.False(first.Accepted);
        Assert.Contains(InstrumentTranslationKeys.UnknownCode, first.Log.ErrorDetail);
        Assert.False(await context.TestResults.AnyAsync(r => r.TestCode == "ABORH" && r.Specimen!.AccessionNumber == "ACC-INST-ANT"));
        var listed = await context.InterfaceUnmappedValues.AsNoTracking()
            .SingleAsync(v => v.InstrumentInterfaceId == iface.Id && v.Value == "Antigen");
        Assert.Equal(InstrumentTranslationKeys.ResultTestCode, listed.DataItemKey);

        var current = (await Admin(context, unmapped: true).GetAsync(iface.Id))!;
        var saved = await Admin(context, unmapped: true).UpdateAsync(iface.Id, Request(current, "Antigen is the Echo's group on this card",
            current.Translations.Append(new InstrumentTranslationDto(InstrumentTranslationKeys.ResultTestCode, "Antigen", "ABORH", InterfaceTranslationDirection.Inbound, 50))));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.NotNull((await context.InterfaceUnmappedValues.AsNoTracking().SingleAsync(v => v.Id == listed.Id)).ResolvedUtc);

        var replay = await Processor(context).ReplayAsync(first.Log.Id);
        Assert.True(replay!.Accepted, string.Join("; ", replay.Notes));
        Assert.Equal(ResultSource.Instrument, (await PostedResultAsync(context, "ACC-INST-ANT", "ABORH")).Source);
    }

    // TEST-BB-1017: a result whose patient ID is not the specimen's patient is not posted and waits in failures; with the
    // right patient ID, or none, it posts (end-user testing pass 10).
    [Fact]
    public async Task PatientIdNotTheSpecimensPatient_IsNotPosted()
    {
        await using var context = _factory.Create();
        var iface = await EchoAsync(context);
        await SeedOrderAsync(context, "MRN-INST-PID", "ACC-INST-PID", "ABORH");
        string Group(string patientId) => EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-PID", patientId, "Patient", "Test", "19590422", "M", 3)).RawMessage;

        var wrong = await Processor(context).ProcessAsync(Group("MRN-SOMEONE-ELSE"), iface.Id);
        Assert.False(wrong.Accepted);
        Assert.Contains(InstrumentTranslationKeys.PatientMismatchCode, wrong.Log.ErrorDetail);
        Assert.Contains("not the patient of specimen ACC-INST-PID (MRN MRN-INST-PID)", wrong.Log.ErrorDetail);
        Assert.False(await context.TestResults.AnyAsync(r => r.Specimen!.AccessionNumber == "ACC-INST-PID"));

        var right = await Processor(context).ProcessAsync(Group("mrn-inst-pid"), iface.Id);
        Assert.True(right.Accepted, string.Join("; ", right.Notes));
        Assert.Equal(ResultSource.Instrument, (await PostedResultAsync(context, "ACC-INST-PID", "ABORH")).Source);

        // The simulator sends a previewed message as it was shown, one record per line.
        var wire = EchoSimulatorSendRequest.ToWire("H|\\^&\nP|1|X\n\nL|1|N\n");
        Assert.Equal("H|\\^&\rP|1|X\rL|1|N\r", wire);
    }

    // TEST-BB-530: a result for a test that is not ordered on the specimen is not filed against another test's order,
    // and an unknown analyte stops its assay from posting.
    [Fact]
    public async Task NotOrderedTest_AndUnknownAnalyte_AreNotPosted()
    {
        await using var context = _factory.Create();
        var iface = await EchoAsync(context);
        await SeedOrderAsync(context, "MRN-INST-NO", "ACC-INST-NO", "ABSC");
        var group = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-NO", "MRN-INST-NO", "Patient", "Test", "19590422", "M", 3)).RawMessage;

        var notOrdered = await Processor(context).ProcessAsync(group, iface.Id);
        Assert.False(notOrdered.Accepted);
        Assert.Contains(InstrumentTranslationKeys.NotOrderedCode, notOrdered.Log.ErrorDetail);
        Assert.False(await context.TestResults.AnyAsync(r => r.Specimen!.AccessionNumber == "ACC-INST-NO"));

        await SeedOrderAsync(context, "MRN-INST-AN", "ACC-INST-AN", "ABORH");
        var oddAnalyte = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-AN", "MRN-INST-AN", "Patient", "Test", "19590422", "M", 3)).RawMessage
            .Replace("^^^Anti-A", "^^^Anti-Q", StringComparison.Ordinal);
        var unknown = await Processor(context).ProcessAsync(oddAnalyte, iface.Id);
        Assert.False(unknown.Accepted);
        Assert.Contains("Anti-Q", unknown.Log.ErrorDetail);
        Assert.False(await context.TestResults.AnyAsync(r => r.Specimen!.AccessionNumber == "ACC-INST-AN"));
        Assert.True(await context.InterfaceUnmappedValues.AnyAsync(v => v.InstrumentInterfaceId == iface.Id
            && v.DataItemKey == InstrumentTranslationKeys.SubtestCode && v.Value == "Anti-Q"));
    }

    // TEST-BB-531: the Echo's interpretation reaches the LIS through a translation (B Pos to B|Positive), not a rule.
    [Fact]
    public async Task GroupInterpretation_IsTranslated_WithoutTheOldRule()
    {
        await using var context = _factory.Create();
        var iface = await EchoAsync(context);
        Assert.DoesNotContain(iface.Rules, r => r.IsEnabled && r.Name == "B Pos");
        await SeedOrderAsync(context, "MRN-INST-INT", "ACC-INST-INT", "ABORH");
        var raw = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-INT", "MRN-INST-INT", "Patient", "Test", "19590422", "M", 3)).RawMessage;

        var outcome = await Processor(context).ProcessAsync(raw, iface.Id);

        Assert.True(outcome.Accepted, string.Join("; ", outcome.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-INT", "ABORH");
        Assert.Matches("^(A|B|O|AB)\\|(Positive|Negative)$", posted.Interpretation ?? "");
    }

    // TEST-BB-532: once an instrument lists error codes, a Hold code (or one with no translation) keeps the result off
    // the record for review, and a Warn code posts it with the flag for the verifier.
    [Fact]
    public async Task ErrorCodes_HoldOrWarn()
    {
        await using var context = _factory.Create();
        var iface = await EchoAsync(context);
        await SeedOrderAsync(context, "MRN-INST-HOLD", "ACC-INST-HOLD", "ABORH");
        var held = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-HOLD", "MRN-INST-HOLD", "Patient", "Test", "19590422", "M", 3)).RawMessage
            .Replace("L|1|N", "L|1|Q", StringComparison.Ordinal);

        var hold = await Processor(context).ProcessAsync(held, iface.Id);

        Assert.False(hold.Accepted);
        Assert.Contains(InstrumentTranslationKeys.HeldCode, hold.Log.ErrorDetail);
        Assert.True(InstrumentMessageFollowUpRule.IsOpenFailure(hold.Log.Status, hold.Log.ResolvedUtc));
        Assert.False(await context.TestResults.AnyAsync(r => r.Specimen!.AccessionNumber == "ACC-INST-HOLD"));
        Assert.True(await context.InterfaceUnmappedValues.AnyAsync(v => v.InstrumentInterfaceId == iface.Id
            && v.DataItemKey == InstrumentTranslationKeys.ErrorCode && v.Value == "Q"));

        await SeedOrderAsync(context, "MRN-INST-WARN", "ACC-INST-WARN", "ABORH");
        var warned = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-WARN", "MRN-INST-WARN", "Patient", "Test", "19590422", "M", 3)).RawMessage
            .Replace("L|1|N", "L|1|W", StringComparison.Ordinal);
        var warn = await Processor(context).ProcessAsync(warned, iface.Id);
        Assert.True(warn.Accepted, string.Join("; ", warn.Notes));
        // The warning carries the code's meaning from the error code table.
        Assert.Contains("instrument flag W (Reaction flagged by the instrument", (await PostedResultAsync(context, "ACC-INST-WARN", "ABORH")).SourceReference);
    }

    // TEST-BB-1002: graded reactions post as the analyzer sent them (the seeded score rules no longer turn 2+ into 1+),
    // and when the Echo's Anti-D1 and Anti-D2 disagree the result carries both readings for the verifier.
    [Fact]
    public async Task EchoGrades_PostAsSent_AndDiscordantAntiD_IsFlagged()
    {
        await using var context = _factory.Create();
        var iface = await EchoAsync(context);
        await SeedOrderAsync(context, "MRN-INST-GRD", "ACC-INST-GRD", "ABORH");
        var raw = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-GRD", "MRN-INST-GRD", "Patient", "Test", "19590422", "M", 3)).RawMessage;
        raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\^\^\^Anti-A\|[^|]*\|", "^^^Anti-A|2+^2+^35|");
        raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\^\^\^Anti-D1\|[^|]*\|", "^^^Anti-D1|0^0^0|");
        raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\^\^\^Anti-D2\|[^|]*\|", "^^^Anti-D2|2+^2+^35|");

        var processed = await Processor(context).ProcessAsync(raw, iface.Id);
        Assert.True(processed.Accepted, string.Join("; ", processed.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-GRD", "ABORH");
        Assert.Contains("\"Anti-A\":\"2+\"", posted.Value);
        Assert.Contains("Anti-D1 0 / Anti-D2 2+", posted.SourceReference);
    }

    // TEST-BB-538: a held result is posted only by a release with a reason: it files as pending verification with the
    // held code beside its source, the failure closes with the reason (audited), and nothing else can be released.
    [Fact]
    public async Task HeldResult_IsReleasedWithAReason_AndOtherFailuresAreNot()
    {
        await using var context = _factory.Create();
        var iface = await EchoAsync(context);
        await SeedOrderAsync(context, "MRN-INST-REL", "ACC-INST-REL", "ABORH");
        var raw = EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-REL", "MRN-INST-REL", "Patient", "Test", "19590422", "M", 3)).RawMessage
            .Replace("L|1|N", "L|1|Q", StringComparison.Ordinal);
        var held = await Processor(context).ProcessAsync(raw, iface.Id);
        Assert.False(held.Accepted);

        var replayed = await Processor(context).ReplayAsync(held.Log.Id);
        Assert.False(replayed!.Accepted);
        var noReason = await Processor(context).ReleaseHeldAsync(held.Log.Id, " ");
        Assert.True(noReason!.Refused);
        Assert.False(await context.TestResults.AnyAsync(r => r.Specimen!.AccessionNumber == "ACC-INST-REL"));

        var released = await Processor(context).ReleaseHeldAsync(held.Log.Id, "Reactions reviewed on the instrument; QC in range");

        Assert.True(released!.Accepted, string.Join("; ", released.Notes));
        var posted = await PostedResultAsync(context, "ACC-INST-REL", "ABORH");
        Assert.Equal(ResultStatus.PendingVerification, posted.Status);
        Assert.Contains("Q held, released by tech-test", posted.SourceReference);
        var closed = await context.InstrumentMessages.AsNoTracking().SingleAsync(m => m.Id == held.Log.Id);
        Assert.StartsWith("Held result released and posted: Reactions reviewed", closed.ResolutionNote);
        Assert.True(await context.AuditEvents.AnyAsync(a => a.EntityType == nameof(InstrumentMessageLog) && a.EntityId == held.Log.Id
            && a.Reason!.StartsWith("Held result released")));
        Assert.True((await Processor(context).ReleaseHeldAsync(held.Log.Id, "again"))!.Refused);

        // A failure that is not a hold (an unknown assay) cannot be released.
        await SeedOrderAsync(context, "MRN-INST-REL2", "ACC-INST-REL2", "ABORH");
        var unknown = await Processor(context).ProcessAsync(EchoResultSimulator.Generate(new EchoSimulatorRequest(
            "Group", "ACC-INST-REL2", "MRN-INST-REL2", "Patient", "Test", "19590422", "M", 3)).RawMessage
            .Replace("^^^Group", "^^^Antigen2", StringComparison.Ordinal), iface.Id);
        var refused = await Processor(context).ReleaseHeldAsync(unknown.Log.Id, "try");
        Assert.True(refused!.Refused);
        Assert.Contains(InstrumentMessageFollowUpRule.ReleaseCode, refused.Notes.Single());
    }

    // TEST-BB-533: a test the instrument has no code for is left out of its order download and listed (outbound).
    [Fact]
    public async Task Query_ForATestWithNoInstrumentCode_LeavesItOut_AndListsIt()
    {
        await using var context = _factory.Create();
        var iface = await EchoAsync(context);
        await SeedOrderAsync(context, "MRN-INST-QX", "ACC-INST-QX", "ELUTION");

        const string query = "H|\\^&|||ImmuLINK|||||LIS|||LIS2-A2|20120219140243\rQ|1|ACC-INST-QX||ALL\rL|1|N\r";
        var outcome = await Processor(context).ProcessAsync(query, iface.Id);

        Assert.Contains(outcome.Notes, n => n.Contains("ELUTION not sent", StringComparison.Ordinal));
        Assert.DoesNotContain("ELUTION", outcome.ReplyMessage ?? "");
        Assert.True(await context.InterfaceUnmappedValues.AnyAsync(v => v.InstrumentInterfaceId == iface.Id
            && v.DataItemKey == InstrumentTranslationKeys.OrderTestCode && v.Value == "ELUTION" && v.Direction == InterfaceTranslationDirection.Outbound));
    }

    // TEST-BB-534: a translation to a test the LIS does not have, or an error code without Hold, Warn, or Ignore, is refused.
    [Fact]
    public async Task Admin_RefusesUnknownLocalValues_AndBadErrorActions()
    {
        await using var context = _factory.Create();
        var request = new SaveInstrumentInterfaceRequest(
            $"XLAT-{Guid.NewGuid():N}"[..14],
            "Development",
            "test",
            new InstrumentConnectionDto(null, 2590, true),
            [new InstrumentFieldMappingDto(InstrumentDataItemKeys.PatientId, "P.3", true)],
            [
                new InstrumentTranslationDto(InstrumentTranslationKeys.ResultTestCode, "Group", "NOPE", InterfaceTranslationDirection.Inbound, 1),
                new InstrumentTranslationDto(InstrumentTranslationKeys.ErrorCode, "Q", "Maybe", InterfaceTranslationDirection.Inbound, 1),
                new InstrumentTranslationDto(InstrumentTranslationKeys.SubtestCode, "Pos Ctrl", InstrumentTranslationKeys.Keep, InterfaceTranslationDirection.Inbound, 1)
            ],
            []);

        var refused = await Admin(context, catalog: true).CreateAsync(request);

        Assert.False(refused.Succeeded);
        Assert.Contains(refused.Evaluation!.HardStops, r => r.Code == "INST-XLAT-LOCAL" && r.Message.Contains("NOPE"));
        Assert.Contains(refused.Evaluation.HardStops, r => r.Code == "INST-XLAT-ACTION");
        Assert.DoesNotContain(refused.Evaluation.HardStops, r => r.Message.Contains("Pos Ctrl"));
    }

    private async Task<InstrumentInterface> EchoAsync(BloodBankDbContext context)
    {
        await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        return await context.InstrumentInterfaces.AsNoTracking().Include(i => i.Rules)
            .SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
    }

    private static SaveInstrumentInterfaceRequest Request(InstrumentInterfaceDto dto, string reason, IEnumerable<InstrumentTranslationDto> translations) =>
        new(dto.Name, dto.Environment, reason, dto.Connection, dto.FieldMappings, translations.ToList(), dto.Rules,
            dto.InterpretationMatch, dto.InterpretationAnalyteCodes);

    private InterfaceUnmappedValueService Unmapped(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new InterfaceUnmappedValueService(
            new EfRepository<InterfaceUnmappedValue>(context), context, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            instruments: new EfRepository<InstrumentInterface>(context));
    }

    private InstrumentInterfaceAdminService Admin(
        BloodBankDbContext context,
        IPermissionEvaluator? permissions = null,
        bool unmapped = false,
        bool catalog = false)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new InstrumentInterfaceAdminService(
            new InstrumentInterfaceRepository(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions,
            tests: catalog ? new EfRepository<BloodBankLIS.Domain.Entities.Configuration.TestDefinition>(context) : null,
            subtests: catalog ? new EfRepository<BloodBankLIS.Domain.Entities.Configuration.SubtestDefinition>(context) : null,
            phases: catalog ? new EfRepository<BloodBankLIS.Domain.Entities.Configuration.PhaseDefinition>(context) : null,
            unmapped: unmapped ? Unmapped(context) : null);
    }

    private AstmInboundProcessor Processor(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var results = new ResultService(
            new EfRepository<TestResult>(context),
            new EfRepository<Specimen>(context),
            new EfRepository<PatientBloodTypeHistory>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            orders: new EfRepository<Order>(context),
            orderLines: new EfRepository<OrderLine>(context));

        return new AstmInboundProcessor(
            new InstrumentInterfaceRepository(context),
            new EfRepository<InstrumentMessageLog>(context),
            new EfRepository<Specimen>(context),
            new EfRepository<Order>(context),
            new EfRepository<OrderSpecimen>(context),
            new EfRepository<OrderLine>(context),
            new EfRepository<Patient>(context),
            _factory.Clock,
            context,
            results,
            NullLogger<AstmInboundProcessor>.Instance,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            Unmapped(context));
    }

    private async Task<(long SpecimenId, long OrderId)> SeedOrderAsync(
        BloodBankDbContext context,
        string mrn,
        string accession,
        string testCode)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = "Patient",
            FirstName = "Test",
            DateOfBirth = new DateOnly(1959, 4, 22),
            Sex = Sex.Male
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var location = new OrderingLocation { Code = $"BB-{accession}", Name = "Blood Bank", IsActive = true };
        context.OrderingLocations.Add(location);
        await context.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{accession}",
            Status = EncounterStatus.Active
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync();

        var specimen = new Specimen
        {
            AccessionNumber = accession,
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            Status = SpecimenStatus.Accepted
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = $"ORD-{accession}",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            TestCode = testCode,
            Status = OrderStatus.InProcess,
            OrderedUtc = _factory.Clock.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        context.OrderLines.Add(new OrderLine
        {
            OrderId = order.Id,
            LineNumber = 1,
            LineName = testCode,
            TestCode = testCode,
            ResultStatus = ResultStatus.Pending,
            IsActive = true
        });
        context.OrderSpecimens.Add(new OrderSpecimen
        {
            OrderId = order.Id,
            SpecimenId = specimen.Id,
            IsPrimary = true
        });
        await context.SaveChangesAsync();
        return (specimen.Id, order.Id);
    }

    private static async Task<TestResult> PostedResultAsync(
        BloodBankDbContext context,
        string accession,
        string testCode)
    {
        var specimen = await context.Specimens.SingleAsync(s => s.AccessionNumber == accession);
        return await context.TestResults.SingleAsync(r => r.SpecimenId == specimen.Id && r.TestCode == testCode);
    }

    private static bool AboRhResultValueLooksLikePanel(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains("\"subtests\"", StringComparison.OrdinalIgnoreCase);
}
