using System.Text.Json;

using System.Text.Json.Serialization;



namespace BloodBankLIS.Domain.ValueObjects;



/// <summary>Catalog code included in a BloodAttribute test definition scope.</summary>

public sealed record BloodAttributeScopeEntry(string Code);



public static class BloodAttributeScope

{

    private static readonly JsonSerializerOptions JsonOptions = new()

    {

        PropertyNameCaseInsensitive = true

    };



    public static IReadOnlyList<BloodAttributeScopeEntry> Parse(string? json)

    {

        if (string.IsNullOrWhiteSpace(json))

        {

            return Array.Empty<BloodAttributeScopeEntry>();

        }



        try

        {

            var raw = JsonSerializer.Deserialize<List<ScopeJsonRow>>(json, JsonOptions);

            if (raw is null || raw.Count == 0)

            {

                return Array.Empty<BloodAttributeScopeEntry>();

            }



            return raw

                .Where(r => !string.IsNullOrWhiteSpace(r.Code))

                .Select(r => new BloodAttributeScopeEntry(r.Code!.Trim()))

                .ToList();

        }

        catch (JsonException)

        {

            return Array.Empty<BloodAttributeScopeEntry>();

        }

    }



    public static string Serialize(IEnumerable<BloodAttributeScopeEntry> entries)

    {

        var rows = entries

            .Where(e => !string.IsNullOrWhiteSpace(e.Code))

            .Select(e => new ScopeJsonRow { Code = e.Code.Trim() })

            .ToList();

        return JsonSerializer.Serialize(rows, JsonOptions);

    }



    private sealed class ScopeJsonRow

    {

        [JsonPropertyName("code")]

        public string? Code { get; set; }

    }

}

