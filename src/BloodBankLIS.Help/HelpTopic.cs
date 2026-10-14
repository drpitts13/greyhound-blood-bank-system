namespace BloodBankLIS.Help;

/// <summary>One topic shared by in-app help and a printed manual.</summary>
public sealed record HelpTopic(
    string Guide,
    string Slug,
    string Title,
    int Order,
    string Chapter,
    string Summary,
    IReadOnlyList<string> Routes,
    string Markdown,
    string Html,
    string PlainText)
{
    /// <summary>A route a user can open without substituting an id.</summary>
    public string? ConcreteRoute =>
        Routes.FirstOrDefault(route =>
            route.IndexOf('{') < 0
            && route.IndexOf('?') < 0);
}
