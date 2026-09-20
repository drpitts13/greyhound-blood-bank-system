using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Astm.Parsing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BloodBankLIS.Astm.Messaging;

public sealed class AstmInboundProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        ILogger<AstmInboundProcessor> logger)
    {
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

    public async Task<AstmProcessOutcome> ProcessAsync(
        string raw,
        long interfaceId,
        bool isReplay = false,
        CancellationToken ct = default)
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
        log.ParsedJson = JsonSerializer.Serialize(message.Records.Select(r => r.Raw), JsonOptions);
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
                await HandleResultsAsync(iface, message, log, notes, ct);
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

    public async Task<AstmProcessOutcome?> ReplayAsync(long messageId, CancellationToken ct = default)
    {
        var existing = await _messages.GetByIdAsync(messageId, ct);
        if (existing is null)
        {
            return null;
        }

        return await ProcessAsync(existing.RawMessage, existing.InterfaceId, isReplay: true, ct);
    }

    private async Task HandleResultsAsync(
        InstrumentInterface iface,
        AstmMessage message,
        InstrumentMessageLog log,
        List<string> notes,
        CancellationToken ct)
    {
        var paths = AstmFieldAccessor.Index(iface.FieldMappings);
        var mappedRows = new List<Dictionary<string, string?>>();
        var ruleTrace = new List<InstrumentRuleFire>();
        var groups = GroupResults(message.Records);

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
            var appTest = InstrumentMappingResolver.ToApplication(
                iface.TestMappings, assayCode, InstrumentMappingKind.Assay, orderCodes)
                ?? assayCode;

            var subtests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? interp = null;
            string? units = null;

            foreach (var result in group.Results)
            {
                var bag = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [InstrumentDataItemKeys.PatientId] = patientId,
                    [InstrumentDataItemKeys.SpecimenId] = specimenId,
                    [InstrumentDataItemKeys.TestCode] = assayCode,
                    [InstrumentDataItemKeys.SubtestCode] = Read(paths, [result], InstrumentDataItemKeys.SubtestCode)
                        ?? result.GetComponent(3, 4),
                    [InstrumentDataItemKeys.SubtestResult] = Read(paths, [result], InstrumentDataItemKeys.SubtestResult)
                        ?? result.GetComponent(4, 1),
                    [InstrumentDataItemKeys.TestInterp] = result.GetComponent(3, 4)
                        .Equals(assayCode, StringComparison.OrdinalIgnoreCase)
                        ? (Read(paths, [result], InstrumentDataItemKeys.TestInterp) ?? result.GetField(4))
                        : null,
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
                mappedRows.Add(applied);

                var analyte = applied.GetValueOrDefault(InstrumentDataItemKeys.SubtestCode) ?? bag[InstrumentDataItemKeys.SubtestCode];
                var value = applied.GetValueOrDefault(InstrumentDataItemKeys.SubtestResult);
                var mappedInterp = applied.GetValueOrDefault(InstrumentDataItemKeys.TestInterp);
                if (!string.IsNullOrWhiteSpace(mappedInterp)
                    && string.Equals(analyte, assayCode, StringComparison.OrdinalIgnoreCase))
                {
                    interp = mappedInterp;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(analyte) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var appSub = InstrumentMappingResolver.ToApplication(
                    iface.TestMappings, analyte, InstrumentMappingKind.Analyte, orderCodes)
                    ?? analyte;
                subtests[appSub] = value;
            }

            if (string.IsNullOrWhiteSpace(specimenId))
            {
                notes.Add($"Assay {assayCode} had no specimen id.");
                continue;
            }

            var posted = await PostAssayAsync(appTest, specimenId, subtests, interp, units, notes, ct);
            if (!posted)
            {
                log.ErrorDetail = notes.LastOrDefault() ?? log.ErrorDetail;
            }
        }

        log.MappedJson = JsonSerializer.Serialize(mappedRows, JsonOptions);
        log.RuleTraceJson = JsonSerializer.Serialize(ruleTrace, JsonOptions);
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

            replies.Add(AstmOrderBuilder.Build(patient, specimen, lines, iface.TestMappings.ToList()));
        }

        log.MappedJson = JsonSerializer.Serialize(new { queries = message.Records.Count(r => r.Type == 'Q') }, JsonOptions);
        return replies.Count == 0 ? null : string.Join(string.Empty, replies);
    }

    private async Task<bool> PostAssayAsync(
        string appTest,
        string specimenId,
        Dictionary<string, string> subtests,
        string? interp,
        string? units,
        List<string> notes,
        CancellationToken ct)
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
        var line = lines.FirstOrDefault(l =>
            string.Equals(l.TestCode, appTest, StringComparison.OrdinalIgnoreCase));
        line ??= lines.FirstOrDefault();
        if (line is null)
        {
            notes.Add($"No active order line for specimen {specimenId} / test {appTest}.");
            return false;
        }

        var (value, interpretation) = EncodeResult(appTest, subtests, interp);
        if (string.IsNullOrWhiteSpace(value))
        {
            notes.Add($"Assay {appTest} produced no postable value.");
            return false;
        }

        var result = await _results.EnterFromInstrumentAsync(
            specimen.Id, line.OrderId, appTest, value, units, interpretation,
            sourceReference: $"ASTM:{specimenId}:{appTest}", ct);
        if (!result.Succeeded)
        {
            notes.Add(result.Error ?? $"Failed to post {appTest}.");
            return false;
        }

        notes.Add($"Posted {appTest} as pending verification.");
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
