using System.Text.RegularExpressions;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// An element focused in the same handler that makes it appear (a desk, a step, a field shown by the click) is not on
/// the page yet: the reference is unset and the focus throws, which on Blazor Server ends the session. Issue on a Ready
/// to issue row did that (Cycle 627) and the sign-in code field relied on the same timing (Cycle 628). Focus goes in
/// OnAfterRenderAsync, after the render, unless the element is always on the page.
/// </summary>
public partial class FocusTimingTests
{
    // Elements rendered unconditionally on their page, safe to focus from a handler.
    private static readonly string[] AlwaysRendered = ["_passwordInput"];

    // TEST-BB-756: every element-reference focus is made after the render, or on an element always on its page.
    [Fact]
    public void ElementFocus_HappensAfterTheRender()
    {
        var root = RepoRoot();
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var afterRender = AfterRenderBody(text);
            foreach (Match m in ReferenceFocus().Matches(text))
            {
                var name = m.Groups[1].Value;
                var insideAfterRender = afterRender is { } body && m.Index >= body.Start && m.Index < body.End;
                if (!insideAfterRender && !AlwaysRendered.Contains(name))
                {
                    var line = text[..m.Index].Count(c => c == '\n') + 1;
                    problems.Add($"{Path.GetRelativePath(root, file)}:{line}: {name}.FocusAsync outside OnAfterRenderAsync");
                }
            }
        }

        Assert.True(problems.Count == 0,
            "An element is focused before it may be on the page (set a flag and focus in OnAfterRenderAsync):" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    // The span of OnAfterRenderAsync's body, by brace counting from its first brace.
    private static (int Start, int End)? AfterRenderBody(string text)
    {
        var at = text.IndexOf("OnAfterRenderAsync(bool", StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var open = text.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return (open, i);
        }

        return null;
    }

    [GeneratedRegex(@"\b(_\w+|input)\.FocusAsync\(")]
    private static partial Regex ReferenceFocus();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}

/// <summary>Sign-in with an authenticator code (Cycle 628): the code field appears after the password, then takes the focus.</summary>
public class SignInCodeFocusTests : Bunit.BunitContext
{
    private sealed class NoLifetime : Microsoft.Extensions.Hosting.IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    // TEST-BB-757: the password accepted with an authenticator challenge shows the code field and focuses it after the
    // render (it was focused before the field was on the page); the click does not throw.
    [Fact]
    public void PasswordAccepted_CodeFieldTakesTheFocus()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/auth/login"] = new BloodBankLIS.Web.Services.LoginResultVm(null, "tech1", "Tech One", 1, [],
                new BloodBankLIS.Web.Services.LoginChallengeVm("Challenge"))
        });
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(Services, new BloodBankLIS.Web.Hosting.DesktopHostOptions());
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Microsoft.Extensions.Hosting.IHostApplicationLifetime>(Services, new NoLifetime());
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(Services,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BloodBankLIS.Web.Hosting.DesktopHostLifetime>.Instance);
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<BloodBankLIS.Web.Hosting.DesktopHostLifetime>(Services);

        var page = Render<BloodBankLIS.Web.Components.Pages.Login>();
        page.Find("input[autocomplete='username']").Input("tech1");
        page.Find("input[autocomplete='current-password']").Input("demo");
        page.Find("input[autocomplete='current-password']").KeyDown("Enter");

        page.WaitForAssertion(() => Assert.NotNull(page.Find("input[autocomplete='one-time-code']")));
        var codeId = ((Microsoft.AspNetCore.Components.ElementReference)typeof(BloodBankLIS.Web.Components.Pages.Login)
            .GetField("_codeInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(page.Instance)!).Id;
        page.WaitForAssertion(() => Assert.Contains(JSInterop.Invocations, i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)
            && i.Arguments.OfType<Microsoft.AspNetCore.Components.ElementReference>().Any(r => r.Id == codeId)));
    }
}
