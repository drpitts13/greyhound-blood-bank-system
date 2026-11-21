using System.Text.Json;
using BloodBankLIS.Application.Common;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-322: a request the API cannot read gets a plain 400 message that names the field and never echoes the value.
public class RequestReadErrorTests
{
    private enum Kind { Standard = 0, Emergency = 1 }

    private sealed record Body(long UnitId, Kind Kind, Nested? Scan = null);

    private sealed record Nested(string Din, int Count);

    private static Exception ReadFailure(string json)
    {
        try
        {
            JsonSerializer.Deserialize<Body>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            // Minimal APIs wrap the serializer failure the same way.
            return new InvalidOperationException("Failed to read parameter \"Body body\" from the request body as JSON.", ex);
        }

        throw new InvalidOperationException("Expected the body to fail.");
    }

    [Fact]
    public void EnumWrittenAsText_NamesTheField_AndDoesNotEchoTheValue()
    {
        var message = RequestReadError.Describe(ReadFailure("""{"unitId":13,"kind":"SecretValue"}"""));

        Assert.Contains("'kind'", message);
        Assert.Contains("not one of the allowed values", message);
        Assert.DoesNotContain("SecretValue", message);
        Assert.DoesNotContain("Kind", message.Replace("'kind'", ""));
    }

    [Fact]
    public void NestedField_UsesItsPath()
    {
        var message = RequestReadError.Describe(ReadFailure("""{"unitId":1,"kind":0,"scan":{"din":"X","count":"many"}}"""));

        Assert.Contains("'scan.count'", message);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"unitId":1,""")]
    [InlineData("""{"unitId":""")]
    public void BrokenJson_SaysTheBodyIsNotValid(string json)
    {
        var message = RequestReadError.Describe(ReadFailure(json));

        Assert.Equal("The request body is not valid JSON.", message);
    }

    [Theory]
    [InlineData(null, "The request body is not valid JSON.")]
    [InlineData("$", "The request body is not valid JSON.")]
    [InlineData("$.issueType", "The value for 'issueType' is missing a required part, is the wrong type, or is not one of the allowed values.")]
    [InlineData("$.items[2].code", "The value for 'items[2].code' is missing a required part, is the wrong type, or is not one of the allowed values.")]
    public void JsonPath_IsDescribed(string? path, string expected) =>
        Assert.Equal(expected, RequestReadError.DescribeJsonPath(path));

    [Theory]
    [InlineData("Failed to bind parameter \"long id\" from \"abc\".", "id")]
    [InlineData("Failed to bind parameter \"Nullable<DateTime> fromUtc\" from \"yesterday\".", "fromUtc")]
    public void RouteOrQueryBindFailure_NamesTheParameter_NotTheValue(string aspNetMessage, string name)
    {
        var message = RequestReadError.Describe(new InvalidOperationException(aspNetMessage));

        Assert.Equal($"The value for '{name}' in the address or query is not in the expected format.", message);
        Assert.DoesNotContain("abc", message);
        Assert.DoesNotContain("yesterday", message);
    }

    [Fact]
    public void MissingBody_SaysSo()
    {
        var message = RequestReadError.Describe(new InvalidOperationException(
            "Implicit body inferred for parameter \"request\" but no body was provided. Did you mean to use a Service instead?"));

        Assert.Equal("The request body is missing or is not JSON.", message);
    }

    [Fact]
    public void UnknownFailure_GetsAGenericMessage()
    {
        Assert.Equal("The request is not in the expected format.", RequestReadError.Describe(new InvalidOperationException("something internal")));
        Assert.Equal("The request is not in the expected format.", RequestReadError.Describe(null));
    }
}
