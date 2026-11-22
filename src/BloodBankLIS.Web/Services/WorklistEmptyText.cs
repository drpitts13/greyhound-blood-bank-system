namespace BloodBankLIS.Web.Services;

/// <summary>
/// What an empty work list says (end-user testing passes 169–170: lists said "No reserved units waiting to issue" while
/// they were still loading, and kept saying so when loading failed, so the issue desk could read a failed read as
/// nothing to do). Until the list is read it says so; a failed read says what failed; only then the empty message.
/// </summary>
public static class WorklistEmptyText
{
    public static (string Text, bool IsError) For(bool loaded, string? error, string emptyText) =>
        !loaded ? ("Loading…", false)
        : error is not null ? ($"This list could not be loaded: {error} Refresh the page before relying on it.", true)
        : (emptyText, false);
}
