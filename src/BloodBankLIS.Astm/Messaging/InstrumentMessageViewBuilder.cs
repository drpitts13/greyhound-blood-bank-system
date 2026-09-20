using BloodBankLIS.Application.Admin;
using BloodBankLIS.Astm.Parsing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Astm.Messaging;

/// <summary>Formats stored ASTM logs for the operations message viewer.</summary>
public static class InstrumentMessageViewBuilder
{
    public static string FormatRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return string.Join(
            '\n',
            raw.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static InstrumentMessageView Build(string raw, InstrumentInterface? iface)
    {
        if (!AstmParser.TryParse(raw, out var message, out _) || message is null)
        {
            return new InstrumentMessageView(FormatRaw(raw), [], []);
        }

        return Build(message, iface);
    }

    public static InstrumentMessageView Build(AstmMessage message, InstrumentInterface? iface)
    {
        var paths = IndexPaths(iface);
        var parsed = new List<InstrumentParsedFieldDto>();
        var mapped = new List<InstrumentMappedValueDto>();
        var seenParsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenMapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddParsed(string dataItem, string? value, bool once = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var display = dataItem is InstrumentDataItemKeys.SubtestResult or InstrumentDataItemKeys.TestInterp
                ? CellReactionGradeValue.NormalizeDisplay(value)
                : value;
            var key = once ? $"{dataItem}={display}" : $"{parsed.Count}:{dataItem}={display}";
            if (!seenParsed.Add(key))
            {
                return;
            }

            parsed.Add(new InstrumentParsedFieldDto(dataItem, display));
        }

        void AddMapped(string? instrument, string? application)
        {
            if (string.IsNullOrWhiteSpace(instrument) && string.IsNullOrWhiteSpace(application))
            {
                return;
            }

            var left = instrument ?? string.Empty;
            var right = string.IsNullOrWhiteSpace(application) ? left : application;
            if (!seenMapped.Add($"{left}\u001f{right}"))
            {
                return;
            }

            mapped.Add(new InstrumentMappedValueDto(left, right));
        }

        if (message.Kind == InstrumentMessageKind.Query)
        {
            foreach (var query in message.Records.Where(r => r.Type == 'Q'))
            {
                AddParsed(
                    InstrumentDataItemKeys.SpecimenId,
                    Read(paths, [query], InstrumentDataItemKeys.SpecimenId) ?? query.GetField(3));
            }

            AddParsed(InstrumentDataItemKeys.Error, Read(paths, message.Records, InstrumentDataItemKeys.Error), once: true);
            return new InstrumentMessageView(FormatRaw(message.Raw), parsed, mapped);
        }

        foreach (var group in GroupResults(message.Records))
        {
            var assayCode = Read(paths, [group.Order], InstrumentDataItemKeys.TestCode)
                            ?? group.Order.GetComponent(5, 4);
            var specimenId = Read(paths, [group.Order], InstrumentDataItemKeys.SpecimenId)
                             ?? group.Order.GetField(3);
            var patientId = Read(paths, [group.Patient], InstrumentDataItemKeys.PatientId)
                            ?? group.Patient.GetField(3);
            var appTest = InstrumentMappingResolver.ToApplication(
                iface?.TestMappings ?? [], assayCode, InstrumentMappingKind.Assay)
                ?? assayCode;

            AddParsed(InstrumentDataItemKeys.PatientId, patientId, once: true);
            AddParsed(
                InstrumentDataItemKeys.PatientLname,
                Read(paths, [group.Patient], InstrumentDataItemKeys.PatientLname),
                once: true);
            AddParsed(
                InstrumentDataItemKeys.PatientFname,
                Read(paths, [group.Patient], InstrumentDataItemKeys.PatientFname),
                once: true);
            AddParsed(
                InstrumentDataItemKeys.PatientGender,
                Read(paths, [group.Patient], InstrumentDataItemKeys.PatientGender),
                once: true);
            AddParsed(
                InstrumentDataItemKeys.PatientDob,
                Read(paths, [group.Patient], InstrumentDataItemKeys.PatientDob),
                once: true);
            AddParsed(
                InstrumentDataItemKeys.OrderId,
                Read(paths, [group.Order], InstrumentDataItemKeys.OrderId),
                once: true);
            AddParsed(InstrumentDataItemKeys.SpecimenId, specimenId, once: true);
            AddParsed(
                InstrumentDataItemKeys.CollectionDt,
                Read(paths, [group.Order], InstrumentDataItemKeys.CollectionDt),
                once: true);
            AddParsed(InstrumentDataItemKeys.TestCode, assayCode);
            AddMapped(assayCode, appTest);

            foreach (var result in group.Results)
            {
                var analyte = Read(paths, [result], InstrumentDataItemKeys.SubtestCode)
                              ?? result.GetComponent(3, 4);
                var rawResult = CellReactionGradeValue.NormalizeDisplay(
                    Read(paths, [result], InstrumentDataItemKeys.SubtestResult) ?? result.GetComponent(4, 1));
                var isInterp = InstrumentInterpretationClassifier.IsInterpretation(analyte, assayCode, iface);
                var rawInterp = isInterp
                    ? (Read(paths, [result], InstrumentDataItemKeys.TestInterp) ?? result.GetField(4))
                    : null;

                var bag = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [InstrumentDataItemKeys.PatientId] = patientId,
                    [InstrumentDataItemKeys.SpecimenId] = specimenId,
                    [InstrumentDataItemKeys.TestCode] = assayCode,
                    [InstrumentDataItemKeys.SubtestCode] = analyte,
                    [InstrumentDataItemKeys.SubtestResult] = rawResult,
                    [InstrumentDataItemKeys.TestInterp] = rawInterp,
                    [InstrumentDataItemKeys.ResultStatus] = Read(paths, [result], InstrumentDataItemKeys.ResultStatus)
                        ?? result.GetField(9),
                    [InstrumentDataItemKeys.ResultDt] = Read(paths, [result], InstrumentDataItemKeys.ResultDt)
                        ?? result.GetField(13),
                    [InstrumentDataItemKeys.InstrumentId] = Read(paths, [result], InstrumentDataItemKeys.InstrumentId)
                        ?? result.GetField(14),
                    [InstrumentDataItemKeys.Error] = Read(paths, message.Records, InstrumentDataItemKeys.Error)
                };

                var (applied, _) = InstrumentRuleEngine.Apply(bag, iface?.Rules ?? []);
                var mappedAnalyte = applied.GetValueOrDefault(InstrumentDataItemKeys.SubtestCode) ?? analyte;
                var mappedResult = applied.GetValueOrDefault(InstrumentDataItemKeys.SubtestResult) ?? rawResult;
                var mappedInterp = applied.GetValueOrDefault(InstrumentDataItemKeys.TestInterp) ?? rawInterp;
                var appSub = InstrumentMappingResolver.ToApplication(
                    iface?.TestMappings ?? [], mappedAnalyte ?? string.Empty, InstrumentMappingKind.Analyte)
                    ?? mappedAnalyte;

                AddParsed(InstrumentDataItemKeys.SubtestCode, analyte);
                if (isInterp)
                {
                    AddParsed(InstrumentDataItemKeys.TestInterp, rawInterp);
                }
                else
                {
                    AddParsed(InstrumentDataItemKeys.SubtestResult, rawResult);
                }

                AddParsed(InstrumentDataItemKeys.ResultStatus, bag[InstrumentDataItemKeys.ResultStatus], once: true);
                AddParsed(InstrumentDataItemKeys.ResultDt, bag[InstrumentDataItemKeys.ResultDt], once: true);
                AddParsed(InstrumentDataItemKeys.InstrumentId, bag[InstrumentDataItemKeys.InstrumentId], once: true);

                if (!isInterp)
                {
                    AddMapped(analyte, appSub);
                    if (LooksLikeNumericCapture(rawResult)
                        && !string.Equals(rawResult, mappedResult, StringComparison.Ordinal))
                    {
                        AddMapped(rawResult, mappedResult);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(rawInterp))
                {
                    AddMapped(rawInterp, mappedInterp);
                }
            }
        }

        AddParsed(InstrumentDataItemKeys.Error, Read(paths, message.Records, InstrumentDataItemKeys.Error), once: true);
        return new InstrumentMessageView(FormatRaw(message.Raw), parsed, mapped);
    }

    private static bool LooksLikeNumericCapture(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.All(c => char.IsAsciiDigit(c) || c == '.');

    private static Dictionary<string, InstrumentAstmPath> IndexPaths(InstrumentInterface? iface)
    {
        var mappings = iface?.FieldMappings is { Count: > 0 } configured
            ? configured
            : InstrumentDataItemCatalog.All.Select(item => new InstrumentFieldMapping
            {
                DataItemKey = item.Key,
                AstmPath = item.DefaultAstmPath
            }).ToList();
        return AstmFieldAccessor.Index(mappings);
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

public sealed record InstrumentMessageView(
    string RawDisplay,
    IReadOnlyList<InstrumentParsedFieldDto> ParsedFields,
    IReadOnlyList<InstrumentMappedValueDto> MappedValues);
