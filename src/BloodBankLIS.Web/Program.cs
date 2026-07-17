using BloodBankLIS.Web.Components;
using BloodBankLIS.Web.Services;

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
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<IdentityHeaderHandler>();

// Typed client over the HTTP API. Default to the API's HTTP endpoint to avoid dev-cert
// friction for server-to-server calls; override with Api:BaseUrl in configuration.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5177/";
builder.Services.AddHttpClient<BloodBankApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
