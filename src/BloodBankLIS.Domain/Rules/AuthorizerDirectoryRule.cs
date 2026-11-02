using System.Text.RegularExpressions;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Matches the typed "authorized by" name on an override against active ordering providers and LIS users
/// (OCD-041: a physician or a supervisor). The result is recorded with the override so a reviewer can see
/// whether the authorizer is a known person. It never blocks: an emergency must not wait on a directory gap.
/// </summary>
public static partial class AuthorizerDirectoryRule
{
    /// <summary>A directory entry: provider id or user name, and the display name.</summary>
    public sealed record Entry(string Id, string? Name);

    public sealed record Result(AuthorizerDirectoryMatch Match, string? Reference);

    /// <summary>
    /// A match on the id, the name, or the "Name (id)" suggestion text, ignoring case and extra spaces.
    /// One provider wins over a user with the same text (the authorizing physician may also have an LIS account).
    /// More than one hit in the same list is ambiguous and records no reference.
    /// </summary>
    public static Result Match(string? text, IReadOnlyCollection<Entry> providers, IReadOnlyCollection<Entry> users)
    {
        var typed = Normalize(text);
        if (typed.Length == 0)
        {
            return new Result(AuthorizerDirectoryMatch.NotInDirectory, null);
        }

        var providerHits = Hits(typed, providers);
        if (providerHits.Count > 1)
        {
            return new Result(AuthorizerDirectoryMatch.Ambiguous, null);
        }

        if (providerHits.Count == 1)
        {
            return new Result(AuthorizerDirectoryMatch.OrderingProvider, providerHits[0].Id.Trim());
        }

        var userHits = Hits(typed, users);
        return userHits.Count switch
        {
            0 => new Result(AuthorizerDirectoryMatch.NotInDirectory, null),
            1 => new Result(AuthorizerDirectoryMatch.User, userHits[0].Id.Trim()),
            _ => new Result(AuthorizerDirectoryMatch.Ambiguous, null)
        };
    }

    /// <summary>The text offered in the authorizer suggestions, which <see cref="Match"/> accepts back.</summary>
    public static string Suggestion(Entry entry) =>
        string.IsNullOrWhiteSpace(entry.Name) || Normalize(entry.Name) == Normalize(entry.Id)
            ? entry.Id.Trim()
            : $"{entry.Name.Trim()} ({entry.Id.Trim()})";

    /// <summary>Plain words for the review screens.</summary>
    public static string Describe(AuthorizerDirectoryMatch? match, string? reference) => match switch
    {
        AuthorizerDirectoryMatch.OrderingProvider => $"provider {reference}",
        AuthorizerDirectoryMatch.User => $"LIS user {reference}",
        AuthorizerDirectoryMatch.Ambiguous => "matches more than one directory entry",
        AuthorizerDirectoryMatch.NotInDirectory => "not in the provider or user directory",
        _ => "not checked against the directory"
    };

    private static List<Entry> Hits(string typed, IEnumerable<Entry> entries) =>
        entries
            .Where(e => Normalize(e.Id) == typed
                || (!string.IsNullOrWhiteSpace(e.Name)
                    && (Normalize(e.Name) == typed || Normalize($"{e.Name} ({e.Id})") == typed)))
            .GroupBy(e => Normalize(e.Id))
            .Select(g => g.First())
            .ToList();

    private static string Normalize(string? value) =>
        Spaces().Replace((value ?? string.Empty).Trim(), " ").ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
