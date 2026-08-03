using BloodBankLIS.Api.Auth;
using BloodBankLIS.Api.Endpoints;
using BloodBankLIS.Api.Hosting;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.HL7;
using BloodBankLIS.Infrastructure;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Printing;
using BloodBankLIS.Security;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var dbProvider = DatabaseOptions.ResolveProvider(builder.Configuration, builder.Environment.IsDevelopment());
var connectionString = DatabaseOptions.ResolveConnectionString(builder.Configuration, builder.Environment.IsDevelopment());

// Dev mode (no-login). Hard-fail if it is ever enabled outside Development.
var devMode = builder.Configuration.GetSection(DevModeOptions.SectionName).Get<DevModeOptions>() ?? new DevModeOptions();
if (devMode.Enabled && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        $"DevMode:Enabled is true in the '{builder.Environment.EnvironmentName}' environment. " +
        "No-login dev mode is permitted only in Development. Disable it before deploying.");
}

var effectiveDevMode = devMode.Enabled && builder.Environment.IsDevelopment();
devMode.Enabled = effectiveDevMode;
builder.Services.AddSingleton(devMode);

builder.Services.AddInfrastructure(connectionString, dbProvider);

builder.Services.AddHl7Interfaces();
builder.Services.AddPrinting();
builder.Services.AddSecurity();
builder.Services.AddHostedService<MllpListenerService>();

// Real environment descriptor for audit stamping (overrides the infrastructure default).
builder.Services.AddSingleton<IEnvironmentInfo>(
    new StaticEnvironmentInfo(builder.Environment.EnvironmentName, effectiveDevMode));

// Replace the default identity with the request-scoped, header-based resolver so audit
// metadata and authorization reflect the calling user (registered after AddInfrastructure
// so this registration wins). Falls back to the system account outside an HTTP request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

var app = builder.Build();

if (effectiveDevMode)
{
    app.Logger.LogWarning(
        "DEV MODE ENABLED: unauthenticated requests run as '{DevUser}' with full permissions. " +
        "This must never be used in a production environment.", devMode.UserName);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseExceptionHandler(exceptionHandlerApp =>
    {
        exceptionHandlerApp.Run(async context =>
        {
            var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            if (ex is Microsoft.Data.SqlClient.SqlException or Microsoft.Data.Sqlite.SqliteException)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Database unavailable",
                    detail = "The application could not use the database. In Development, SQLite is stored under %LOCALAPPDATA%\\BloodBankLIS\\bloodbank.dev.db. Stop the API and restart it so migrations can recreate an out-of-date file, or delete bloodbank.dev.db (and -wal/-shm) manually if the problem persists.",
                    status = 503
                });
                return;
            }

            // Surface the deepest message — EF wraps SQLite/SQL errors as DbUpdateException
            // with the actionable detail on InnerException.
            var detail = ex?.InnerException?.Message ?? ex?.Message;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "An unexpected error occurred.",
                detail,
                status = 500
            });
        });
    });

    app.Logger.LogInformation(
        "Database provider: {Provider}. Connection: {Connection}",
        dbProvider,
        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? connectionString
            : "(SQL Server — connection string hidden)");

    // Convenience for local development: apply migrations and seed demo data.
    if (app.Configuration.GetValue("Database:AutoMigrate", true))
    {
        await ApplyMigrationsAndSeedAsync(app, effectiveDevMode, dbProvider);
    }
}

app.UseHttpsRedirection();

app.MapPatientEndpoints();
app.MapPatientWorkspaceEndpoints();
app.MapInventoryEndpoints();
app.MapModificationEndpoints();
app.MapIsbtEndpoints();
app.MapSpecimenEndpoints();
app.MapResultEndpoints();
app.MapTestWorklistEndpoints();
app.MapImmunohematologyEndpoints();
app.MapCompatibilityEndpoints();
app.MapIssuingEndpoints();
app.MapHl7Endpoints();
app.MapPrintEndpoints();
app.MapBillingEndpoints();
app.MapSignatureEndpoints();
app.MapReferenceEndpoints();
app.MapMeEndpoints();
app.MapAdminEndpoints();
app.MapAuditEndpoints();

app.Run();

static async Task ApplyMigrationsAndSeedAsync(WebApplication app, bool seedDevAdmin, string dbProvider)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<BloodBankDbContext>();

        if (string.Equals(dbProvider, DatabaseOptions.Sqlite, StringComparison.OrdinalIgnoreCase)
            && app.Environment.IsDevelopment())
        {
            await DevelopmentSqliteBootstrap.InitializeAsync(context, logger);
            logger.LogInformation(
                "SQLite path: {Path}",
                DatabaseOptions.ResolveConnectionString(app.Configuration, isDevelopment: true));
        }
        else
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrated.");
        }

        await DatabaseSeeder.SeedAsync(context, seedDevAdmin);
        logger.LogInformation("Database seeded.");
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Database migrate/seed failed. For SQLite dev, stop the API, delete %LOCALAPPDATA%\\BloodBankLIS\\bloodbank.dev.db (and -wal/-shm if present), then restart.");
        throw;
    }
}

// Exposed for integration/functional testing.
public partial class Program;
