using System.Reflection;
using BloodBankLIS.Help;
using BloodBankLIS.Web.Hosting;
using BloodBankLIS.Web.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Every page a user can open by address (routes without parameters) draws without throwing when the API has nothing to
/// give, including after its startup calls finish. A page that fails here would fail for a user on an empty or
/// unreachable API instead of saying there is nothing to show.
/// </summary>
public class EveryPageRendersTests
{
    public static TheoryData<string> Routes()
    {
        var data = new TheoryData<string>();
        foreach (var route in RoutablePages().Select(p => p.Route).Order(StringComparer.Ordinal))
        {
            data.Add(route);
        }

        return data;
    }

    private static IEnumerable<(string Route, Type Page)> RoutablePages() =>
        typeof(BloodBankLIS.Web.Components.Pages.BloodOrders).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>().Select(r => (Route: r.Template, Page: t)))
            .Where(p => !p.Route.Contains('{'))
            .GroupBy(p => p.Route)
            .Select(g => g.First());

    // TEST-BB-662
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Page_RendersWithAnEmptyApi(string route)
    {
        var page = RoutablePages().Single(p => p.Route == route).Page;
        await using var context = new BunitContext();
        CannedApi.Register(context, new Dictionary<string, object>());
        context.Services.AddSingleton(new ReportDownloadCache(new MemoryCache(new MemoryCacheOptions())));
        context.Services.AddSingleton(HelpCatalog.LoadEmbedded());
        context.Services.AddSingleton(new DevModeState { Enabled = true });
        context.Services.AddSingleton(new DesktopHostLifetime(new DesktopHostOptions(), new StoppedLifetime(), NullLogger<DesktopHostLifetime>.Instance));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(route);

        var rendered = context.Render<DynamicComponent>(p => p.Add(c => c.Type, page));
        // Let startup calls (answered 404 at once) finish, then surface anything they threw while drawing.
        await Task.Delay(100);
        rendered.Render();
        Assert.False(string.IsNullOrWhiteSpace(rendered.Markup), $"{route} drew nothing.");
    }

    private sealed class StoppedLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
