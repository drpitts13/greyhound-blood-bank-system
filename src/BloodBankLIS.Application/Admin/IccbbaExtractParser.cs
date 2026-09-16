using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Parses facility-exported ICCBBA extracts (JSON, CSV, TSV). Does not invent codes
/// and does not read licensed Access/Excel databases (ST-010).
/// </summary>
public static class IccbbaExtractParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsUnsupportedNativeDatabase(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".mdb" or ".accdb" or ".xlsx" or ".xls";
    }

    public static string NativeDatabaseMessage =>
        "Microsoft Access and Excel ICCBBA databases are not parsed. Export product and ABO/RhD tables to CSV, TSV, or JSON first (OCD-004 / ST-010).";

    public static IccbbaExtractKind Classify(string fileName)
    {
        if (IsUnsupportedNativeDatabase(fileName))
            return IccbbaExtractKind.UnsupportedNativeDatabase;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".json")
            return IccbbaExtractKind.CatalogJson;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (stem.Contains("abo", StringComparison.OrdinalIgnoreCase)
            || stem.Contains("rhd", StringComparison.OrdinalIgnoreCase)
            || stem.Contains("bloodgroup", StringComparison.OrdinalIgnoreCase))
        {
            return IccbbaExtractKind.AboRhdDelimited;
        }

        return IccbbaExtractKind.ProductDelimited;
    }

    public static bool IsAllowedExtractExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".csv" or ".tsv" or ".txt" or ".json";
    }

    public static IccbbaExtractParseResult Parse(string fileName, string content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return IccbbaExtractParseResult.Fail("Extract file name is required.");

        var kind = Classify(fileName);
        if (kind == IccbbaExtractKind.UnsupportedNativeDatabase)
            return IccbbaExtractParseResult.Fail(NativeDatabaseMessage);

        if (string.IsNullOrWhiteSpace(content))
            return IccbbaExtractParseResult.Fail($"Extract file '{fileName}' is empty.");

        return kind switch
        {
            IccbbaExtractKind.CatalogJson => ParseJson(content),
            IccbbaExtractKind.AboRhdDelimited => ParseDelimitedAbo(fileName, content),
            _ => ParseDelimitedProducts(fileName, content)
        };
    }

    public static IccbbaExtractParseResult Merge(IEnumerable<IccbbaExtractParseResult> parts)
    {
        var products = new List<LicensedIsbtProductCodeRow>();
        var abo = new List<LicensedIsbtAboRhdRow>();
        foreach (var part in parts)
        {
            if (!part.Succeeded)
                return part;
            products.AddRange(part.ProductCodes);
            abo.AddRange(part.AboRhdCodes);
        }

        return IccbbaExtractParseResult.Ok(products, abo);
    }

    private static IccbbaExtractParseResult ParseJson(string content)
    {
        try
        {
            var rows = JsonSerializer.Deserialize<LicensedExtractJson>(content, JsonOptions);
            if (rows is null)
                return IccbbaExtractParseResult.Fail("Licensed extract JSON could not be parsed.");

            return IccbbaExtractParseResult.Ok(
                rows.ProductCodes ?? [],
                rows.AboRhdCodes ?? []);
        }
        catch (JsonException ex)
        {
            return IccbbaExtractParseResult.Fail($"Licensed extract JSON could not be parsed: {ex.Message}");
        }
    }

    private static IccbbaExtractParseResult ParseDelimitedProducts(string fileName, string content)
    {
        if (!TryReadTable(content, out var headers, out var rows, out var error))
            return IccbbaExtractParseResult.Fail($"{fileName}: {error}");

        var map = IndexHeaders(headers);
        if (!TryHeader(map, out var pdcIdx, "productdescriptioncode", "productcode", "pdc", "code")
            || !TryHeader(map, out var descIdx, "description", "productdescription"))
        {
            return IccbbaExtractParseResult.Fail(
                $"{fileName}: product extracts require ProductDescriptionCode (or Product Code / PDC / Code) and Description columns.");
        }

        TryHeader(map, out var classIdx, "componentclass", "class");
        TryHeader(map, out var modifierIdx, "modifier");
        TryHeader(map, out var storageIdx, "storagerequirements", "storage");
        TryHeader(map, out var extIdx, "requiresextendeddivision", "extendeddivision");
        TryHeader(map, out var effectiveIdx, "effectivedate");
        TryHeader(map, out var retiredIdx, "retireddate");

        var products = new List<LicensedIsbtProductCodeRow>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var pdc = Cell(row, pdcIdx);
            var description = Cell(row, descIdx);
            if (string.IsNullOrWhiteSpace(pdc) && string.IsNullOrWhiteSpace(description))
                continue;
            if (string.IsNullOrWhiteSpace(pdc) || string.IsNullOrWhiteSpace(description))
            {
                return IccbbaExtractParseResult.Fail(
                    $"{fileName}: row {i + 2} must include ProductDescriptionCode and Description from the licensed extract.");
            }

            products.Add(new LicensedIsbtProductCodeRow(
                pdc,
                description,
                MapComponentClass(Cell(row, classIdx)),
                EmptyToNull(Cell(row, modifierIdx)),
                EmptyToNull(Cell(row, storageIdx)),
                ParseBool(Cell(row, extIdx)),
                ParseDate(Cell(row, effectiveIdx)),
                ParseDate(Cell(row, retiredIdx))));
        }

        return IccbbaExtractParseResult.Ok(products, []);
    }

    private static IccbbaExtractParseResult ParseDelimitedAbo(string fileName, string content)
    {
        if (!TryReadTable(content, out var headers, out var rows, out var error))
            return IccbbaExtractParseResult.Fail($"{fileName}: {error}");

        var map = IndexHeaders(headers);
        if (!TryHeader(map, out var codeIdx, "code", "abocode", "bloodgroupcode")
            || !TryHeader(map, out var aboIdx, "abo")
            || !TryHeader(map, out var rhIdx, "rhd", "rh"))
        {
            return IccbbaExtractParseResult.Fail(
                $"{fileName}: ABO/RhD extracts require Code (or ABO Code), Abo, and RhD (or Rh) columns.");
        }

        TryHeader(map, out var collectionIdx, "collectiontype");
        TryHeader(map, out var specialIdx, "specialmessage");
        TryHeader(map, out var phenoIdx, "additionalphenotype");
        TryHeader(map, out var effectiveIdx, "effectivedate");
        TryHeader(map, out var retiredIdx, "retireddate");

        var aboRows = new List<LicensedIsbtAboRhdRow>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var code = Cell(row, codeIdx);
            var abo = Cell(row, aboIdx);
            var rh = Cell(row, rhIdx);
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(abo) && string.IsNullOrWhiteSpace(rh))
                continue;
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(abo) || string.IsNullOrWhiteSpace(rh))
            {
                return IccbbaExtractParseResult.Fail(
                    $"{fileName}: row {i + 2} must include Code plus Abo and RhD values from the licensed extract.");
            }

            aboRows.Add(new LicensedIsbtAboRhdRow(
                code,
                abo,
                rh,
                EmptyToNull(Cell(row, collectionIdx)),
                EmptyToNull(Cell(row, specialIdx)),
                EmptyToNull(Cell(row, phenoIdx)),
                ParseDate(Cell(row, effectiveIdx)),
                ParseDate(Cell(row, retiredIdx))));
        }

        return IccbbaExtractParseResult.Ok([], aboRows);
    }

    private static bool TryReadTable(
        string content,
        out IReadOnlyList<string> headers,
        out IReadOnlyList<IReadOnlyList<string>> rows,
        out string? error)
    {
        headers = [];
        rows = [];
        error = null;

        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(static l => l.TrimEnd())
            .Where(static l => l.Length > 0)
            .ToList();
        if (lines.Count == 0)
        {
            error = "header row is required.";
            return false;
        }

        var delimiter = DetectDelimiter(lines[0]);
        headers = SplitDelimited(lines[0], delimiter);
        if (headers.Count == 0 || headers.All(string.IsNullOrWhiteSpace))
        {
            error = "header row is required.";
            return false;
        }

        var body = new List<IReadOnlyList<string>>();
        for (var i = 1; i < lines.Count; i++)
            body.Add(SplitDelimited(lines[i], delimiter));
        rows = body;
        return true;
    }

    private static char DetectDelimiter(string headerLine)
    {
        var tabs = headerLine.Count(static c => c == '\t');
        var commas = headerLine.Count(static c => c == ',');
        return tabs > commas ? '\t' : ',';
    }

    private static List<string> SplitDelimited(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                fields.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString().Trim());
        return fields;
    }

    private static Dictionary<string, int> IndexHeaders(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var key = NormalizeHeader(headers[i]);
            if (key.Length > 0 && !map.ContainsKey(key))
                map[key] = i;
        }

        return map;
    }

    private static string NormalizeHeader(string header)
    {
        var sb = new StringBuilder(header.Length);
        foreach (var c in header.Trim())
        {
            if (c is ' ' or '_' or '-' or '/')
                continue;
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private static bool TryHeader(Dictionary<string, int> map, out int index, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (map.TryGetValue(alias, out index))
                return true;
        }

        index = -1;
        return false;
    }

    private static string Cell(IReadOnlyList<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ParseBool(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || value.Equals("1", StringComparison.OrdinalIgnoreCase);

    private static DateOnly? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;
        return null;
    }

    public static string MapComponentClass(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Other";

        var trimmed = raw.Trim();
        if (Enum.TryParse<Domain.Enums.ComponentClass>(trimmed, ignoreCase: true, out var parsed)
            && parsed != Domain.Enums.ComponentClass.Other)
        {
            return parsed.ToString();
        }

        var compact = NormalizeHeader(trimmed);
        if (compact.Contains("redblood") || compact is "rbc" or "rbcs")
            return nameof(Domain.Enums.ComponentClass.RedBloodCells);
        if (compact.Contains("plasma"))
            return nameof(Domain.Enums.ComponentClass.Plasma);
        if (compact.Contains("platelet"))
            return nameof(Domain.Enums.ComponentClass.Platelets);
        if (compact.Contains("cryo"))
            return nameof(Domain.Enums.ComponentClass.Cryoprecipitate);
        if (compact.Contains("wholeblood") || compact is "wb")
            return nameof(Domain.Enums.ComponentClass.WholeBlood);
        if (compact.Contains("granulocyte"))
            return nameof(Domain.Enums.ComponentClass.Granulocytes);
        return nameof(Domain.Enums.ComponentClass.Other);
    }

    private sealed class LicensedExtractJson
    {
        public List<LicensedIsbtProductCodeRow>? ProductCodes { get; set; }
        public List<LicensedIsbtAboRhdRow>? AboRhdCodes { get; set; }
    }
}

public enum IccbbaExtractKind
{
    CatalogJson = 0,
    ProductDelimited = 1,
    AboRhdDelimited = 2,
    UnsupportedNativeDatabase = 3
}

public sealed record IccbbaExtractParseResult(
    bool Succeeded,
    string? Error,
    IReadOnlyList<LicensedIsbtProductCodeRow> ProductCodes,
    IReadOnlyList<LicensedIsbtAboRhdRow> AboRhdCodes)
{
    public static IccbbaExtractParseResult Ok(
        IReadOnlyList<LicensedIsbtProductCodeRow> products,
        IReadOnlyList<LicensedIsbtAboRhdRow> abo) =>
        new(true, null, products, abo);

    public static IccbbaExtractParseResult Fail(string error) =>
        new(false, error, [], []);
}
