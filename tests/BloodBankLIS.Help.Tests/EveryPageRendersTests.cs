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
    public Task Page_RendersWithAnEmptyApi(string route) =>
        RenderAsync(route, RoutablePages().Single(p => p.Route == route).Page, new Dictionary<string, object?>());


    /// <summary>
    /// Pages opened with a record id (and every patient chart tab), each with the record the server cannot find: the page
    /// must say so, not fail. Route values are given as the router would bind them.
    /// </summary>
    public static TheoryData<string> RecordRoutes()
    {
        var data = new TheoryData<string>();
        foreach (var route in RecordPages.Keys)
        {
            data.Add(route);
        }

        return data;
    }

    private static readonly string[] ChartTabs =
        ["overview", "timeline", "visits", "orders", "specimens", "tests", "test-history", "products", "history", "rhig", "dat-elution"];

    private static readonly Dictionary<string, (Type Page, Dictionary<string, object?> Values)> RecordPages =
        new Dictionary<string, (Type, Dictionary<string, object?>)>
        {
            ["/admin/reports/design/1"] = (typeof(Web.Components.Pages.ReportDesign), new() { ["Id"] = 1L }),
            ["/help/clinical/blood-orders"] = (typeof(Web.Components.Pages.HelpArticle), new() { ["Guide"] = "clinical", ["Slug"] = "blood-orders" }),
            ["/inventory/deliveries/1"] = (typeof(Web.Components.Pages.Delivery), new() { ["Id"] = 1L }),
            ["/inventory/deliveries/1/receive"] = (typeof(Web.Components.Pages.DeliveryReceive), new() { ["Id"] = 1L }),
            ["/inventory/units/1"] = (typeof(Web.Components.Pages.UnitDetail), new() { ["UnitId"] = 1L }),
            ["/mtp/pack-records/1"] = (typeof(Web.Components.Pages.MtpPackReport), new() { ["PackRecordId"] = 1L }),
            ["/patients/1/antibody-card"] = (typeof(Web.Components.Pages.AntibodyCard), new() { ["PatientId"] = 1L }),
            ["/patients/1/antibody-id"] = (typeof(Web.Components.Pages.AntibodyIdentification), new() { ["PatientId"] = 1L }),
            ["/patients/1/antibody-id/1"] = (typeof(Web.Components.Pages.AntibodyIdentification), new() { ["PatientId"] = 1L, ["WorkupId"] = 1L }),
            ["/patients/1/unit-antigen-typing/1"] = (typeof(Web.Components.Pages.UnitAntigenTyping), new() { ["PatientId"] = 1L, ["BloodUnitId"] = 1L }),
            ["/reactions/1/report"] = (typeof(Web.Components.Pages.ReactionReport), new() { ["Id"] = 1L }),
            ["/reports/1"] = (typeof(Web.Components.Pages.ReportRun), new() { ["Id"] = 1L })
        }
        .Concat(ChartTabs.Select(tab => new KeyValuePair<string, (Type, Dictionary<string, object?>)>(
            $"/patients/1?tab={tab}", (typeof(Web.Components.Pages.PatientDetail), new() { ["Id"] = 1L }))))
        .ToDictionary();

    // TEST-BB-663
    [Theory]
    [MemberData(nameof(RecordRoutes))]
    public Task RecordPage_RendersWhenTheRecordIsNotFound(string route) =>
        RenderAsync(route, RecordPages[route].Page, RecordPages[route].Values);


    public static TheoryData<string> ChartTabNames()
    {
        var data = new TheoryData<string>();
        foreach (var tab in ChartTabs)
        {
            data.Add(tab);
        }

        return data;
    }

    // TEST-BB-664: every chart tab draws for a patient who has nothing else on file (every other request 404).
    [Theory]
    [MemberData(nameof(ChartTabNames))]
    public async Task ChartTab_RendersForAPatientWithNothingOnFile(string tab)
    {
        var patient = new BloodBankLIS.Application.Patients.PatientDto(
            1, "MRN-EMPTY", "Empty", "Chart", null, new DateOnly(1970, 1, 1), BloodBankLIS.Domain.Enums.Sex.Unknown,
            BloodBankLIS.Domain.Enums.PatientStatus.Active, DateTime.UtcNow, "test");
        var markup = await RenderAsync($"/patients/1?tab={tab}", typeof(Web.Components.Pages.PatientDetail), new() { ["Id"] = 1L },
            new Dictionary<string, object> { ["/api/patients/1"] = patient });
        Assert.Contains("MRN-EMPTY", markup);
    }

    [Fact]
    public void RecordRoutes_CoverEveryParameterizedPage()
    {
        var covered = RecordPages.Values.Select(p => p.Page).ToHashSet();
        var parameterized = typeof(BloodBankLIS.Web.Components.Pages.BloodOrders).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<RouteAttribute>().Any(r => r.Template.Contains('{')))
            .ToList();
        Assert.All(parameterized, t => Assert.True(covered.Contains(t), $"{t.Name} has a route with a parameter but no render case."));
    }

    private static async Task<string> RenderAsync(string route, Type page, Dictionary<string, object?> values,
        Dictionary<string, object>? responses = null)
    {
        await using var context = new BunitContext();
        CannedApi.Register(context, responses ?? new Dictionary<string, object>());
        context.Services.AddSingleton(new ReportDownloadCache(new MemoryCache(new MemoryCacheOptions())));
        context.Services.AddSingleton(HelpCatalog.LoadEmbedded());
        context.Services.AddSingleton(new DevModeState { Enabled = true });
        context.Services.AddSingleton(new DesktopHostLifetime(new DesktopHostOptions(), new StoppedLifetime(), NullLogger<DesktopHostLifetime>.Instance));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(route);

        var rendered = context.Render<DynamicComponent>(p => p.Add(c => c.Type, page).Add(c => c.Parameters, values));
        // Let startup calls (answered 404 at once) finish, then surface anything they threw while drawing.
        await Task.Delay(100);
        rendered.Render();
        Assert.False(string.IsNullOrWhiteSpace(rendered.Markup), $"{route} drew nothing.");
        return rendered.Markup;
    }

    private sealed class StoppedLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
