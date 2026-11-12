using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Astm.Parsing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BloodBankLIS.Astm.Messaging;

public sealed class AstmInboundProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = ClinicalJson.WebOptions;

    private readonly IInstrumentInterfaceRepository _interfaces;
    private readonly IRepository<InstrumentMessageLog> _messages;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<OrderLine> _orderLines;
    private readonly IRepository<Patient> _patients;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ResultService _results;
    private readonly ILogger<AstmInboundProcessor> _logger;
    private readonly ICurrentUser? _currentUser;
    private readonly IAuditWriter? _audit;
    private readonly InterfaceUnmappedValueService? _unmapped;

    public AstmInboundProcessor(
        IInstrumentInterfaceRepository interfaces,
        IRepository<InstrumentMessageLog> messages,
        IRepository<Specimen> specimens,
        IRepository<Order> orders,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<OrderLine> orderLines,
        IRepository<Patient> patients,
        IClock clock,
        IUnitOfWork unitOfWork,
        ResultService results,
        ILogger<AstmInboundProcessor> logger,
        ICurrentUser? currentUser = null,
        IAuditWriter? audit = null,
        InterfaceUnmappedValueService? unmapped = null)
    {
        _currentUser = currentUser;
        _audit = audit;
        _unmapped = unmapped;
        _interfaces = interfaces;
        _messages = messages;
        _specimens = specimens;
        _orders = orders;
        _orderSpecimens = orderSpecimens;
        _orderLines = orderLines;
        _patients = patients;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _results = results;
        _logger = logger;
    }

    public Task<AstmProcessOutcome> ProcessAsync(
        string raw,
        long interfaceId,
        bool isReplay = false,
        CancellationToken ct = default) =>
        ProcessCoreAsync(raw, interfaceId, isReplay, releasedBy: null, ct);

    private async Task<AstmProcessOutcome> ProcessCoreAsync(
        string raw,
        long interfaceId,
        bool isReplay,
        string? releasedBy,
        CancellationToken ct)
    {
        var iface = await _interfaces.GetWithChildrenAsync(interfaceId, ct);
        var log = new InstrumentMessageLog
        {
            InterfaceId = interfaceId,
            Direction = Hl7Direction.Inbound,
            RawMessage = raw,
            Status = isReplay ? InstrumentMessageStatus.Replayed : InstrumentMessageStatus.Received,
            ReceivedUtc = _clock.UtcNow
        };

        if (iface is null)
        {
            log.Status = InstrumentMessageStatus.Failed;
            log.ErrorDetail = "Instrument interface was not found.";
            await PersistLogAsync(log, ct);
            return new AstmProcessOutcome(false, log, null, [log.ErrorDetail]);
        }

        if (!AstmParser.TryParse(raw, out var message, out var parseError) || message is null)
        {
            log.Status = InstrumentMessageStatus.Failed;
            log.ErrorDetail = parseError ?? "Unable to parse ASTM message.";
            log.MessageKind = InstrumentMessageKind.Unknown;
            await PersistLogAsync(log, ct);
            return new AstmProcessOutcome(false, log, null, [log.ErrorDetail]);
        }

        log.MessageKind = message.Kind;
        var ordered = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var accession in message.Records.Where(r => r.Type == 'O').Select(r => r.GetField(3)).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct())
        {
            ordered[accession!] = await ResolveOrderTestCodesAsync(accession, ct);
        }

        var view = InstrumentMessageViewBuilder.Build(message, iface, ordered);
        log.ParsedJson = JsonSerializer.Serialize(view.ParsedFields, JsonOptions);
        log.MappedJson = JsonSerializer.Serialize(view.MappedValues, JsonOptions);
        var notes = new List<string>();
        string? reply = null;

        try
        {
            if (message.Kind == InstrumentMessageKind.Query)
            {
                reply = await HandleQueryAsync(iface, message, log, notes, ct);
            }
            else
            {
                await HandleResultsAsync(iface, message, log, notes, ct, releasedBy);
            }

            log.Status = string.IsNullOrWhiteSpace(log.ErrorDetail)
                ? InstrumentMessageStatus.Processed
                : InstrumentMessageStatus.Failed;
            log.ProcessedUtc = _clock.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ASTM inbound processing failed for interface {Id}.", interfaceId);
            log.Status = InstrumentMessageStatus.Failed;
            log.ErrorDetail = ex.Message;
            notes.Add(ex.Message);
        }

        await PersistLogAsync(log, ct);
        return new AstmProcessOutcome(log.Status == InstrumentMessageStatus.Processed, log, reply, notes);
    }

    private async Task PersistLogAsync(InstrumentMessageLog log, CancellationToken ct)
    {
        await _messages.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Replays an open failed message. A message that processed or was already resolved is refused, so its results are
    /// never filed twice. When the replay applies, the failed message is closed as resolved by that replay.
    /// </summary>
    public async Task<AstmProcessOutcome?> ReplayAsync(long messageId, CancellationToken ct = default)
    {
        var existing = await _messages.GetByIdAsync(messageId, ct);
        if (existing is null)
        {
            return null;
        }

        var allowed = InstrumentMessageFollowUpRule.EvaluateReplay(existing.Status, existing.ResolvedUtc);
        if (allowed.Severity == RuleSeverity.HardStop)
        {
            return new AstmProcessOutcome(false, existing, null, [allowed.Message]) { Refused = true };
        }

        var outcome = await ProcessAsync(existing.RawMessage, existing.InterfaceId, isReplay: true, ct);
        if (outcome.Accepted)
        {
            Close(existing, InstrumentMessageFollowUpRule.ReplayResolutionNote, outcome.Log.Id);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return outcome;
    }

    /// <summary>
    /// Posts a result an instrument error code held, after review: the stored message is processed again with its
    /// holds released, the result carries the held codes in its source, and the failure closes with the reason
    /// (audited). Only a held, open failure qualifies; anything else that fails it still fails.
    /// </summary>
    public async Task<AstmProcessOutcome?> ReleaseHeldAsync(long messageId, string? reason, CancellationToken ct = default)
    {
        var existing = await _messages.GetByIdAsync(messageId, ct);
        if (existing is null)
        {
            return null;
        }

        var check = InstrumentMessageFollowUpRule.EvaluateRelease(existing.Status, existing.ResolvedUtc, existing.ErrorDetail, reason);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return new AstmProcessOutcome(false, existing, null, [$"{check.Code}: {check.Message}"]) { Refused = true };
        }

        var by = string.IsNullOrWhiteSpace(_currentUser?.UserName) ? "system" : _currentUser.UserName;
        var outcome = await ProcessCoreAsync(existing.RawMessage, existing.InterfaceId, isReplay: true, releasedBy: by, ct);
        if (outcome.Accepted)
        {
            Close(existing, InstrumentMessageFollowUpRule.ReleaseResolutionNote(reason!), outcome.Log.Id);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return outcome;
    }

    /// <summary>Closes an open failed message that was handled outside replay, with the reason recorded and audited.</summary>
    public async Task<(InstrumentMessageLog? Message, string? Error)> ResolveAsync(long messageId, string? reason, CancellationToken ct = default)
    {
        var existing = await _messages.GetByIdAsync(messageId, ct);
        if (existing is null)
        {
            return (null, $"Message {messageId} not found.");
        }

        var check = InstrumentMessageFollowUpRule.EvaluateResolve(existing.Status, existing.ResolvedUtc, reason);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return (null, $"{check.Code}: {check.Message}");
        }

        Close(existing, reason!.Trim(), replayLogId: null);
        await _unitOfWork.SaveChangesAsync(ct);
        return (existing, null);
    }

    private void Close(InstrumentMessageLog message, string note, long? replayLogId)
    {
        message.ResolvedUtc = _clock.UtcNow;
        message.ResolvedBy = string.IsNullOrWhiteSpace(_currentUser?.UserName) ? "system" : _currentUser.UserName;
        message.ResolutionNote = note.Length <= 500 ? note : note[..500];
        _messages.Update(message);
        _audit?.Record(
            AuditEventType.Interface,
            nameof(InstrumentMessageLog),
            message.Id,
            oldValue: new { message.Status, ResolvedUtc = (DateTime?)null },
            newValue: new { message.ResolvedUtc, message.ResolvedBy, message.ResolutionNote, ReplayLogId = replayLogId, message.SpecimenId },
            reason: message.ResolutionNote);
    }

    private async Task HandleResultsAsync(
        InstrumentInterface iface,
        AstmMessage message,
        InstrumentMessageLog log,
        List<string> notes,
        CancellationToken ct,
        string? releasedBy = null)
    {
        var paths = AstmFieldAccessor.Index(iface.FieldMappings);
        var ruleTrace = new List<InstrumentRuleFire>();
        var groups = GroupResults(message.Records);
        var translator = InstrumentValueTranslator.From(iface.Translations);
        // Error and flag codes act only once the instrument has error code translations (OCD-066); until then they are
        // read but, as before, do not change what posts.
        var checkErrors = translator.HasRows(InstrumentTranslationKeys.ErrorCode);

        foreach (var group in groups)
        {
            var assayCode = Read(paths, [group.Order], InstrumentDataItemKeys.TestCode)
                            ?? group.Order.GetComponent(5, 4);
            var specimenId = Read(paths, [group.Order], InstrumentDataItemKeys.SpecimenId)
                             ?? group.Order.GetField(3);
            var patientId = Read(paths, [group.Patient], InstrumentDataItemKeys.PatientId)
                            ?? group.Patient.GetField(3);
            log.SpecimenId ??= specimenId;
            log.PatientId ??= patientId;

            var orderCodes = await ResolveOrderTestCodesAsync(specimenId, ct);
            var appTest = translator.ToLocal(InstrumentTranslationKeys.ResultTestCode, assayCode, orderedTests: orderCodes);
            if (appTest is null)
            {
                translator.RecordMiss(InstrumentTranslationKeys.ResultTestCode, assayCode, InterfaceTranslationDirection.Inbound);
                Fail(log, notes, $"Instrument code {assayCode} has no translation to a test ({InstrumentTranslationKeys.UnknownCode}): not posted. "
                    + "Add it to the instrument's result test codes, then replay.");
                continue;
            }

            // The patient ID the instrument sent must be the specimen's patient. End-user testing, 2026-11-12: a screen
            // sent with another patient's MRN posted to this specimen without a word. A blank patient ID is not checked.
            if (await PatientMismatchAsync(specimenId, patientId, ct) is { } mismatch)
            {
                Fail(log, notes, $"{mismatch} ({InstrumentTranslationKeys.PatientMismatchCode}): {assayCode} not posted. "
                    + "Check the tube label and the instrument's patient record before replaying.");
                continue;
            }

            var subtests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? interp = null;
            string? units = null;
            string? unknownAnalyte = null;
            var holds = new List<string>();
            var warnings = new List<string>();
            var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in group.Results)
            {
                var rawValue = Read(paths, [result], InstrumentDataItemKeys.SubtestResult) ?? result.GetComponent(4, 1);
                var valueOutcome = translator.Inbound(InstrumentTranslationKeys.ResultValue, rawValue, appTest);
                var isInterpretation = InstrumentInterpretationClassifier.IsInterpretation(
                    Read(paths, [result], InstrumentDataItemKeys.SubtestCode) ?? result.GetComponent(3, 4), assayCode, iface);
                var rawInterp = isInterpretation ? (Read(paths, [result], InstrumentDataItemKeys.TestInterp) ?? result.GetField(4)) : null;
                var interpOutcome = translator.Inbound(InstrumentTranslationKeys.Interpretation, rawInterp, appTest);
                var bag = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [InstrumentDataItemKeys.PatientId] = patientId,
                    [InstrumentDataItemKeys.SpecimenId] = specimenId,
                    [InstrumentDataItemKeys.TestCode] = assayCode,
                    [InstrumentDataItemKeys.SubtestCode] = Read(paths, [result], InstrumentDataItemKeys.SubtestCode)
                        ?? result.GetComponent(3, 4),
                    [InstrumentDataItemKeys.SubtestResult] = CellReactionGradeValue.NormalizeDisplay(valueOutcome.Value),
                    [InstrumentDataItemKeys.TestInterp] = isInterpretation ? interpOutcome.Value : null,
                    [InstrumentDataItemKeys.ResultFlag] = Read(paths, [result], InstrumentDataItemKeys.ResultFlag) ?? result.GetField(7),
                    [InstrumentDataItemKeys.ResultStatus] = Read(paths, [result], InstrumentDataItemKeys.ResultStatus)
                        ?? result.GetField(9),
                    [InstrumentDataItemKeys.ResultDt] = Read(paths, [result], InstrumentDataItemKeys.ResultDt)
                        ?? result.GetField(13),
                    [InstrumentDataItemKeys.InstrumentId] = Read(paths, [result], InstrumentDataItemKeys.InstrumentId)
                        ?? result.GetField(14),
                    [InstrumentDataItemKeys.Error] = Read(paths, message.Records, InstrumentDataItemKeys.Error)
                };

                var (applied, fires) = InstrumentRuleEngine.Apply(bag, iface.Rules);
                ruleTrace.AddRange(fires);

                if (checkErrors)
                {
                    var codes = new[] { applied.GetValueOrDefault(InstrumentDataItemKeys.Error), applied.GetValueOrDefault(InstrumentDataItemKeys.ResultFlag) }
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c!.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase);
                    foreach (var code in codes)
                    {
                        // The instrument's error code table decides each code's action (OCD-066); notes carry its meaning.
                        var (action, _) = translator.ErrorAction(code, appTest);
                        var label = translator.Describe(code, appTest);
                        if (action == InstrumentErrorAction.Hold && !holds.Contains(label, StringComparer.OrdinalIgnoreCase))
                        {
                            holds.Add(label);
                        }
                        else if (action == InstrumentErrorAction.Warn && !warnings.Contains(label, StringComparer.OrdinalIgnoreCase))
                        {
                            warnings.Add(label);
                        }
                    }
                }

                var analyte = applied.GetValueOrDefault(InstrumentDataItemKeys.SubtestCode) ?? bag[InstrumentDataItemKeys.SubtestCode];
                var value = applied.GetValueOrDefault(InstrumentDataItemKeys.SubtestResult);
                var mappedInterp = applied.GetValueOrDefault(InstrumentDataItemKeys.TestInterp);
                if (!string.IsNullOrWhiteSpace(mappedInterp)
                    && InstrumentInterpretationClassifier.IsInterpretation(analyte, assayCode, iface))
                {
                    // Listed only when this instrument translates interpretations and neither a row nor a rule changed it.
                    if (!interpOutcome.Translated && translator.HasRows(InstrumentTranslationKeys.Interpretation)
                        && string.Equals(mappedInterp, rawInterp, StringComparison.Ordinal))
                    {
                        translator.RecordMiss(InstrumentTranslationKeys.Interpretation, rawInterp, InterfaceTranslationDirection.Inbound);
                    }

                    interp = mappedInterp;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(analyte) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!valueOutcome.Translated && translator.HasRows(InstrumentTranslationKeys.ResultValue) && !IsRecognizedValue(value))
                {
                    translator.RecordMiss(InstrumentTranslationKeys.ResultValue, rawValue, InterfaceTranslationDirection.Inbound);
                }

                var appSub = translator.ToLocal(InstrumentTranslationKeys.SubtestCode, analyte);
                if (appSub is null)
                {
                    translator.RecordMiss(InstrumentTranslationKeys.SubtestCode, analyte, InterfaceTranslationDirection.Inbound);
                    unknownAnalyte ??= analyte;
                    continue;
                }

                if (string.Equals(appSub, InstrumentTranslationKeys.Ignore, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var key = string.Equals(appSub, InstrumentTranslationKeys.Keep, StringComparison.OrdinalIgnoreCase) ? analyte : appSub;
                // Two instrument readings for one panel subtest (the Echo's Anti-D1 and Anti-D2 both read as Anti-D): when
                // they differ, the verifier is told both rather than seeing only the last (end-user testing, 2026-11-12).
                if (subtests.TryGetValue(key, out var earlier) && sources.TryGetValue(key, out var earlierAnalyte)
                    && !string.Equals(earlier, value, StringComparison.OrdinalIgnoreCase))
                {
                    var differ = $"{earlierAnalyte} {earlier} / {analyte} {value} → {key} {value}";
                    if (!warnings.Contains(differ, StringComparer.OrdinalIgnoreCase))
                    {
                        warnings.Add(differ);
                    }
                }

                subtests[key] = value;
                sources[key] = analyte;
            }

            if (string.IsNullOrWhiteSpace(specimenId))
            {
                notes.Add($"Assay {assayCode} had no specimen id.");
                continue;
            }

            if (unknownAnalyte is not null)
            {
                Fail(log, notes, $"Analyte {unknownAnalyte} on {assayCode} has no subtest translation ({InstrumentTranslationKeys.UnknownCode}): "
                    + $"{appTest} not posted. Add it to the instrument's subtest codes, then replay.");
                continue;
            }

            if (holds.Count > 0 && releasedBy is not null)
            {
                // Reviewed and released: post it, with the held codes kept beside the result for the verifier.
                warnings.AddRange(holds.Select(h => $"{h} held, released by {releasedBy}"));
                holds.Clear();
            }

            if (holds.Count > 0)
            {
                Fail(log, notes, $"Instrument error {string.Join(", ", holds)} (Hold): {appTest} for {specimenId} held for review, not posted "
                    + $"({InstrumentTranslationKeys.HeldCode}). Check the result on the instrument, then resolve with a reason or re-run it.");
                continue;
            }

            var posted = await PostAssayAsync(appTest, specimenId, subtests, interp, units, notes, ct, warnings);
            if (!posted)
            {
                log.ErrorDetail = notes.LastOrDefault() ?? log.ErrorDetail;
            }
        }

        log.RuleTraceJson = JsonSerializer.Serialize(ruleTrace, JsonOptions);
        await RecordMissesAsync(translator, iface.Id, log, ct);
    }

    private async Task<string?> PatientMismatchAsync(string? specimenId, string? patientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(specimenId))
        {
            return null;
        }

        var specimen = await _specimens.FirstOrDefaultAsync(s => s.AccessionNumber == specimenId, ct);
        if (specimen is null)
        {
            return null;
        }

        var patient = await _patients.GetByIdAsync(specimen.PatientId, ct);
        return patient is null || string.Equals(patient.MedicalRecordNumber?.Trim(), patientId.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Patient ID {patientId.Trim()} on the result is not the patient of specimen {specimenId} (MRN {patient.MedicalRecordNumber})";
    }

    private static void Fail(InstrumentMessageLog log, List<string> notes, string message)
    {
        notes.Add(message);
        log.ErrorDetail = message;
    }

    // A grade the LIS reads, a number, or NT needs no translation, so it is not listed as unmapped.
    private static bool IsRecognizedValue(string value) =>
        CellReactionGradeValue.TryParseCode(value, out _)
        || decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _);

    private async Task RecordMissesAsync(InstrumentValueTranslator translator, long instrumentId, InstrumentMessageLog log, CancellationToken ct)
    {
        if (_unmapped is null || translator.Misses.Count == 0)
        {
            return;
        }

        try
        {
            await _unmapped.RecordInstrumentAsync(translator.Misses, instrumentId, log.SpecimenId is { } s ? $"Specimen {s}" : null, ct);
        }
        catch (Exception ex)
        {
            // The list is for follow-up; failing to update it must not change what the message did.
            _logger.LogWarning(ex, "Could not record unmapped instrument values for interface {Id}.", instrumentId);
        }
    }

    private async Task<string?> HandleQueryAsync(
        InstrumentInterface iface,
        AstmMessage message,
        InstrumentMessageLog log,
        List<string> notes,
        CancellationToken ct)
    {
        var replies = new List<string>();
        var paths = AstmFieldAccessor.Index(iface.FieldMappings);
        foreach (var query in message.Records.Where(r => r.Type == 'Q'))
        {
            var accession = Read(paths, [query], InstrumentDataItemKeys.SpecimenId) ?? query.GetField(3);
            log.SpecimenId ??= accession;
            if (string.IsNullOrWhiteSpace(accession))
            {
                notes.Add("Query was missing an accession number.");
                continue;
            }

            var specimen = await _specimens.FirstOrDefaultAsync(s => s.AccessionNumber == accession, ct);
            if (specimen is null)
            {
                notes.Add($"No specimen matched accession {accession}.");
                continue;
            }

            var links = await _orderSpecimens.ListAsync(l => l.SpecimenId == specimen.Id, ct);
            var orderIds = links.Select(l => l.OrderId).Distinct().ToList();
            var lines = orderIds.Count == 0
                ? []
                : await _orderLines.ListAsync(l => orderIds.Contains(l.OrderId) && l.IsActive, ct);
            var patient = await _patients.GetByIdAsync(specimen.PatientId, ct);
            if (patient is null)
            {
                notes.Add($"Specimen {accession} has no patient.");
                continue;
            }

            var translator = InstrumentValueTranslator.From(iface.Translations);
            replies.Add(AstmOrderBuilder.Build(patient, specimen, lines, translator, out var notSent));
            foreach (var test in notSent)
            {
                translator.RecordMiss(InstrumentTranslationKeys.OrderTestCode, test, InterfaceTranslationDirection.Outbound);
                notes.Add($"{test} not sent to the instrument for {accession}: it has no instrument code.");
            }

            await RecordMissesAsync(translator, iface.Id, log, ct);
        }

        return replies.Count == 0 ? null : string.Join(string.Empty, replies);
    }

    private async Task<bool> PostAssayAsync(
        string appTest,
        string specimenId,
        Dictionary<string, string> subtests,
        string? interp,
        string? units,
        List<string> notes,
        CancellationToken ct,
        IReadOnlyList<string>? warnings = null)
    {
        var specimen = await _specimens.FirstOrDefaultAsync(s => s.AccessionNumber == specimenId, ct);
        if (specimen is null)
        {
            notes.Add($"No specimen matched accession {specimenId}.");
            return false;
        }

        var links = await _orderSpecimens.ListAsync(l => l.SpecimenId == specimen.Id, ct);
        var orderIds = links.Select(l => l.OrderId).Distinct().ToList();
        var lines = orderIds.Count == 0
            ? []
            : await _orderLines.ListAsync(l => orderIds.Contains(l.OrderId) && l.IsActive, ct);
        // Only the line for the translated test: a result is never filed against another test's order.
        var line = lines.FirstOrDefault(l =>
            string.Equals(l.TestCode, appTest, StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            notes.Add($"Test {appTest} is not ordered on specimen {specimenId} ({InstrumentTranslationKeys.NotOrderedCode}): not posted.");
            return false;
        }

        var (value, interpretation) = EncodeResult(appTest, subtests, interp);
        if (string.IsNullOrWhiteSpace(value))
        {
            notes.Add($"Assay {appTest} produced no postable value.");
            return false;
        }

        var reference = $"ASTM:{specimenId}:{appTest}";
        if (warnings is { Count: > 0 })
        {
            // The verifier sees the instrument's flag beside the result's source.
            reference += $" · instrument flag {string.Join(", ", warnings)} (Warn)";
            reference = reference.Length <= 200 ? reference : reference[..200];
        }

        var result = await _results.EnterFromInstrumentAsync(
            specimen.Id, line.OrderId, appTest, value, units, interpretation,
            sourceReference: reference, ct);
        if (!result.Succeeded)
        {
            notes.Add(result.Error ?? $"Failed to post {appTest}.");
            return false;
        }

        notes.Add(warnings is { Count: > 0 }
            ? $"Posted {appTest} as pending verification, with instrument flag {string.Join(", ", warnings)} for the verifier."
            : $"Posted {appTest} as pending verification.");
        return true;
    }

    private async Task<IReadOnlyList<string>> ResolveOrderTestCodesAsync(string? accession, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accession))
        {
            return [];
        }

        var specimen = await _specimens.FirstOrDefaultAsync(s => s.AccessionNumber == accession, ct);
        if (specimen is null)
        {
            return [];
        }

        var links = await _orderSpecimens.ListAsync(l => l.SpecimenId == specimen.Id, ct);
        var orderIds = links.Select(l => l.OrderId).Distinct().ToList();
        if (orderIds.Count == 0)
        {
            return [];
        }

        var lines = await _orderLines.ListAsync(l => orderIds.Contains(l.OrderId) && l.IsActive, ct);
        return lines.Select(l => l.TestCode).Where(c => !string.IsNullOrWhiteSpace(c)).Cast<string>().ToList();
    }

    private static (string? Value, string? Interpretation) EncodeResult(
        string appTest,
        IReadOnlyDictionary<string, string> subtests,
        string? interp)
    {
        if (string.Equals(appTest, "ABORH", StringComparison.OrdinalIgnoreCase)
            || appTest.StartsWith("ABORH-RETYPE", StringComparison.OrdinalIgnoreCase))
        {
            TryParseAboRh(interp, out var abo, out var rh);
            return (AboRhResultValue.FormatPanel(new AboRhPanelResult(abo, rh, subtests)), interp);
        }

        if (string.Equals(appTest, "ABSC", StringComparison.OrdinalIgnoreCase))
        {
            var phased = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, grade) in subtests)
            {
                if (code.Contains('|', StringComparison.Ordinal))
                {
                    phased[code] = grade;
                }
                else
                {
                    phased[PhaseResultKeys.Compose(code, "AHG")] = grade;
                }
            }

            return (PanelResultValue.Format(phased), interp);
        }

        if (string.Equals(appTest, "XM", StringComparison.OrdinalIgnoreCase)
            || string.Equals(appTest, "CXM", StringComparison.OrdinalIgnoreCase))
        {
            var value = string.IsNullOrWhiteSpace(interp) ? "Incompatible" : interp;
            if (subtests.Count > 0)
            {
                return (PanelResultValue.Format(subtests), value);
            }

            return (value, value);
        }

        if (string.Equals(appTest, "DAT", StringComparison.OrdinalIgnoreCase))
        {
            return (interp ?? subtests.Values.FirstOrDefault() ?? string.Empty, interp);
        }

        if (subtests.Count > 0)
        {
            return (PanelResultValue.Format(subtests), interp);
        }

        return (interp, interp);
    }

    private static bool TryParseAboRh(string? interp, out AboGroup abo, out RhType rh)
    {
        abo = AboGroup.Unknown;
        rh = RhType.Unknown;
        if (string.IsNullOrWhiteSpace(interp))
        {
            return false;
        }

        if (AboRhResultValue.TryParse(interp, out var parsed))
        {
            abo = parsed.Abo;
            rh = parsed.Rh;
            return true;
        }

        var text = interp.Replace("Pos", "Positive", StringComparison.OrdinalIgnoreCase)
            .Replace("Neg", "Negative", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "|", StringComparison.Ordinal);
        return AboRhResultValue.TryParse(text, out parsed) && Assign(parsed, out abo, out rh);
    }

    private static bool Assign(AboRh parsed, out AboGroup abo, out RhType rh)
    {
        abo = parsed.Abo;
        rh = parsed.Rh;
        return true;
    }

    private static string? Read(
        IReadOnlyDictionary<string, InstrumentAstmPath> paths,
        IEnumerable<AstmRecord> records,
        string key) =>
        AstmFieldAccessor.Read(paths, records, key);

    private static List<ResultGroup> GroupResults(IReadOnlyList<AstmRecord> records)
    {
        var groups = new List<ResultGroup>();
        AstmRecord? patient = null;
        AstmRecord? order = null;
        List<AstmRecord> results = [];

        void Flush()
        {
            if (patient is not null && order is not null && results.Count > 0)
            {
                groups.Add(new ResultGroup(patient, order, results.ToList()));
            }

            results = [];
        }

        foreach (var record in records)
        {
            switch (record.Type)
            {
                case 'P':
                    Flush();
                    patient = record;
                    order = null;
                    break;
                case 'O':
                    Flush();
                    order = record;
                    break;
                case 'R':
                    results.Add(record);
                    break;
            }
        }

        Flush();
        return groups;
    }

    private sealed record ResultGroup(AstmRecord Patient, AstmRecord Order, List<AstmRecord> Results);
}
