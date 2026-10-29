using BloodBankLIS.Help;
using BloodBankLIS.Web.Components;
using BloodBankLIS.Web.Hosting;
using BloodBankLIS.Web.Security;
using BloodBankLIS.Web.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Dev mode (no-login). Hard-fail if enabled outside Development.
var devModeEnabled = builder.Configuration.GetValue("DevMode:Enabled", false);
if (devModeEnabled && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        $"DevMode:Enabled is true in the '{builder.Environment.EnvironmentName}' environment. " +
        "No-login dev mode is permitted only in Development. Disable it before deploying.");
}

var devMode = new DevModeState
{
    Enabled = devModeEnabled && builder.Environment.IsDevelopment(),
    UserName = builder.Configuration["DevMode:UserName"] ?? "DEV_ADMIN"
};
builder.Services.AddSingleton(devMode);

// Per-circuit operator identity, and a handler that forwards it to the API.
var desktopHost = builder.Configuration.GetSection(DesktopHostOptions.SectionName).Get<DesktopHostOptions>()
    ?? new DesktopHostOptions();
builder.Services.AddSingleton(desktopHost);
builder.Services.AddSingleton<DesktopHostLifetime>();
builder.Services.AddSingleton<CircuitHandler, DesktopShutdownCircuitHandler>();

builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<IdentityHeaderHandler>();

// Typed client over the HTTP API. Default to the API's HTTP endpoint to avoid dev-cert
// friction for server-to-server calls; override with Api:BaseUrl in configuration.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5177/";
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ReportDownloadCache>();
var manualDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "docs", "user-manual"));
var helpCatalog = builder.Environment.IsDevelopment() && Directory.Exists(manualDirectory)
    ? HelpCatalog.LoadFromDirectory(manualDirectory)
    : HelpCatalog.LoadEmbedded();
builder.Services.AddSingleton(helpCatalog);
builder.Services.AddSingleton<ManualPdfCache>();
builder.Services.AddHttpClient<BloodBankApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromMinutes(2);
    })
    .AddHttpMessageHandler<IdentityHeaderHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseAntiforgery();
app.MapGet("/help-figures/{name}", (string name, HelpCatalog catalog) =>
{
    if (!catalog.TryGetFigure(name, out var bytes))
        return Results.NotFound();
    return Results.File(bytes, "image/png");
});
app.MapGet("/downloads/manuals/{guide}", (string guide, ManualPdfCache cache) =>
{
    if (!HelpGuides.IsKnown(guide))
        return Results.NotFound();
    var canonical = HelpGuides.Canonical(guide);
    return Results.File(cache.Get(canonical), "application/pdf", HelpGuides.FileName(canonical));
});
app.MapGet("/downloads/reports/{token}", (string token, ReportDownloadCache cache) =>
{
    var file = cache.Take(token);
    return file is null
        ? Results.NotFound()
        : Results.File(file.Bytes, file.ContentType, file.FileName);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
