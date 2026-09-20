using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Instruments;

namespace BloodBankLIS.Astm.Parsing;

/// <summary>Reads mapped catalog values from ASTM records using configured paths.</summary>
public static class AstmFieldAccessor
{
    public static string? Get(AstmRecord record, InstrumentAstmPath path)
    {
        if (char.ToUpperInvariant(record.Type) != path.RecordType)
        {
            return null;
        }

        return path.Component is int component
            ? NullIfEmpty(record.GetComponent(path.Field, component))
            : NullIfEmpty(record.GetField(path.Field));
    }

    public static string? Get(IEnumerable<AstmRecord> records, InstrumentAstmPath path) =>
        records.Select(r => Get(r, path)).FirstOrDefault(v => v is not null);

    public static Dictionary<string, InstrumentAstmPath> Index(IEnumerable<InstrumentFieldMapping> mappings)
    {
        var map = new Dictionary<string, InstrumentAstmPath>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings)
        {
            if (InstrumentAstmPath.TryParse(mapping.AstmPath, out var path))
            {
                map[mapping.DataItemKey] = path;
            }
        }

        return map;
    }

    public static string? Read(IReadOnlyDictionary<string, InstrumentAstmPath> index, IEnumerable<AstmRecord> records, string key) =>
        index.TryGetValue(key, out var path) ? Get(records, path) : null;

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
