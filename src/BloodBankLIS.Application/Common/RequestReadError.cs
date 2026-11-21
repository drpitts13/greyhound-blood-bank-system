using System.Text.Json;
using System.Text.RegularExpressions;

namespace BloodBankLIS.Application.Common;

/// <summary>
/// Plain-language text for a request the API could not read: a body that is not JSON, a field of the wrong type or
/// not an allowed value, or an address or query value in the wrong format. The text names the field but never echoes
/// the submitted value, type names, or stack details.
/// </summary>
public static partial class RequestReadError
{
    public const string Title = "The request could not be read.";

    /// <summary>Describes the first <see cref="JsonException"/> or bind failure found in the exception chain.</summary>
    public static string Describe(Exception? exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is JsonException json)
            {
                // A syntax error comes from the reader (an internal JsonReaderException); the path then points at
                // where reading stopped, not at a field with a wrong value.
                return json.InnerException?.GetType().Name == "JsonReaderException"
                    ? DescribeJsonPath(null)
                    : DescribeJsonPath(json.Path);
            }
        }

        var message = exception?.Message ?? string.Empty;
        var bind = BindFailure().Match(message);
        if (bind.Success)
        {
            return $"The value for '{bind.Groups["name"].Value}' in the address or query is not in the expected format.";
        }

        if (message.Contains("body was provided", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            return "The request body is missing or is not JSON.";
        }

        return "The request is not in the expected format.";
    }

    /// <summary>
    /// <c>$.issueType</c> → the issueType field; <c>$</c>, an empty path, or no path means the body itself is not
    /// valid JSON (or is empty).
    /// </summary>
    public static string DescribeJsonPath(string? path)
    {
        var field = path is null ? string.Empty : path.TrimStart('$').TrimStart('.');
        return field.Length == 0
            ? "The request body is not valid JSON."
            : $"The value for '{field}' is missing a required part, is the wrong type, or is not one of the allowed values.";
    }

    // ASP.NET Core: Failed to bind parameter "long id" from "abc".
    [GeneratedRegex("""Failed to bind parameter "(?:[^"\s]+\s)?(?<name>[A-Za-z_][A-Za-z0-9_]*)" """)]
    private static partial Regex BindFailure();
}
