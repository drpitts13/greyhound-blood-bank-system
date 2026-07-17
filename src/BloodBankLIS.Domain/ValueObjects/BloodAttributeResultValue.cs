using System.Text.Json;

using System.Text.Json.Serialization;

using BloodBankLIS.Domain.Enums;



namespace BloodBankLIS.Domain.ValueObjects;



/// <summary>One antigen/antibody result row in a BloodAttribute test result value.</summary>

public sealed record BloodAttributeResultRow(string Code, AntigenResult Result);



public static class BloodAttributeResultValue

{

    private static readonly JsonSerializerOptions JsonOptions = new()

    {

        PropertyNameCaseInsensitive = true,

        Converters = { new JsonStringEnumConverter() }

    };



    public static bool TryParse(string? value, out IReadOnlyList<BloodAttributeResultRow> rows)

    {

        rows = Array.Empty<BloodAttributeResultRow>();

        if (string.IsNullOrWhiteSpace(value))

        {

            return false;

        }



        try

        {

            var raw = JsonSerializer.Deserialize<List<RowJson>>(value, JsonOptions);

            if (raw is null || raw.Count == 0)

            {

                return false;

            }



            rows = raw

                .Where(r => !string.IsNullOrWhiteSpace(r.Code))

                .Select(r => new BloodAttributeResultRow(r.Code!.Trim(), r.Result))

                .ToList();

            return rows.Count > 0;

        }

        catch (JsonException)

        {

            return false;

        }

    }



    public static string Serialize(IEnumerable<BloodAttributeResultRow> rows)

    {

        var list = rows

            .Where(r => !string.IsNullOrWhiteSpace(r.Code))

            .Select(r => new RowJson { Code = r.Code.Trim(), Result = r.Result })

            .ToList();

        return JsonSerializer.Serialize(list, JsonOptions);

    }



    private sealed class RowJson

    {

        [JsonPropertyName("code")]

        public string? Code { get; set; }



        [JsonPropertyName("result")]

        public AntigenResult Result { get; set; }

    }

}

