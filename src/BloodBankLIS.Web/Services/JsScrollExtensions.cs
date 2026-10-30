using Microsoft.JSInterop;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// Brings a form or result message into view after a button changes what is shown far
/// from where the user clicked (for example an editor rendered below a long table).
/// </summary>
public static class JsScrollExtensions
{
    /// <summary>Focus a field and select its text, so the next scan or keystroke replaces it.</summary>
    public static async Task FocusSelectAsync(this IJSRuntime js, string elementId, bool preventScroll = false)
    {
        try
        {
            await Task.Yield();
            await js.InvokeVoidAsync("bbUi.focusSelect", elementId, preventScroll);
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public static async Task ScrollIntoViewAsync(this IJSRuntime js, string elementId)
    {
        try
        {
            // Let the pending render reach the browser first so a just-shown element exists.
            await Task.Yield();
            await js.InvokeVoidAsync("bbUi.scrollIntoView", elementId);
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
            // Prerendering: no browser yet.
        }
    }
}
