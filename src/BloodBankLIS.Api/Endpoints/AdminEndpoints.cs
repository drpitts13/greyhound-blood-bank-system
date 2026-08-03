using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Admin configuration endpoints (versioned, validated, audited). All routes are
/// permission-gated: reads require <c>admin.config.view</c>; writes require the
/// area-specific manage permission; activation requires <c>admin.config.activate</c>.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        MapTests(app);
        MapBloodAttributes(app);
        MapSpecimenTypes(app);
        MapSubtests(app);
        MapTestGroupers(app);
        MapReflexRules(app);
        MapRules(app);
        MapProducts(app);
        MapModificationRules(app);
        MapIsbtProductCodes(app);
        MapProviders(app);
        MapLocations(app);
        MapExceptions(app);
        MapHl7(app);
        MapUsersAndRoles(app);
        MapHistory(app);
    }

    private static void MapTests(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/tests").WithTags("Admin: Tests").RequireAuthenticatedUser();

        group.MapGet("", async (TestDefinitionAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, TestDefinitionAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Test definition not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveTestDefinitionRequest req, TestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPut("/{id:long}", async (long id, SaveTestDefinitionRequest req, TestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, TestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, TestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/clone", async (long id, CloneRequest req, TestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.CloneAsync(id, req.NewCode, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);
    }

    private static void MapBloodAttributes(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/blood-attributes").WithTags("Admin: Blood Attributes").RequireAuthenticatedUser();

        group.MapGet("", async (BloodAttributeAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, BloodAttributeAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Blood attribute definition not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveBloodAttributeDefinitionRequest req, BloodAttributeAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPut("/{id:long}", async (long id, SaveBloodAttributeDefinitionRequest req, BloodAttributeAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, BloodAttributeAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, BloodAttributeAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapSpecimenTypes(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/specimen-types").WithTags("Admin: Specimen Types").RequireAuthenticatedUser();

        group.MapGet("", async (SpecimenTypeAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, SpecimenTypeAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Specimen type definition not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveSpecimenTypeDefinitionRequest req, SpecimenTypeAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPut("/{id:long}", async (long id, SaveSpecimenTypeDefinitionRequest req, SpecimenTypeAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, SpecimenTypeAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, SpecimenTypeAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapSubtests(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/subtests").WithTags("Admin: Subtests").RequireAuthenticatedUser();

        group.MapGet("", async (SubtestDefinitionAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, SubtestDefinitionAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Subtest definition not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveSubtestDefinitionRequest req, SubtestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPut("/{id:long}", async (long id, SaveSubtestDefinitionRequest req, SubtestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, SubtestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, SubtestDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapTestGroupers(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/test-groupers").WithTags("Admin: Test Groupers").RequireAuthenticatedUser();

        group.MapGet("", async (TestGrouperAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, TestGrouperAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Test grouper not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveTestGrouperRequest req, TestGrouperAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPut("/{id:long}", async (long id, SaveTestGrouperRequest req, TestGrouperAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, TestGrouperAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, TestGrouperAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapReflexRules(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/reflex-rules").WithTags("Admin: Reflex Rules").RequireAuthenticatedUser();

        group.MapGet("", async (ReflexRuleAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, ReflexRuleAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Reflex rule not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveReflexRuleRequest req, ReflexRuleAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPut("/{id:long}", async (long id, SaveReflexRuleRequest req, ReflexRuleAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, ReflexRuleAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, ReflexRuleAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapRules(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/rules").WithTags("Admin: Order and Test Rules").RequireAuthenticatedUser();

        group.MapGet("", async (RuleDefinitionAdminService svc, bool? includeInactive, RuleLevel? level, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, level, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        // Vocabulary is authoring metadata, so it is mapped before the id route.
        group.MapGet("/vocabulary", (RuleLevel? level) =>
            Results.Ok(RuleDefinitionAdminService.Vocabulary(level ?? RuleLevel.Order)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, RuleDefinitionAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Rule not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("/validate", async (ValidateRuleRequest req, RuleDefinitionAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ValidateAsync(req, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveRuleDefinitionRequest req, RuleDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPut("/{id:long}", async (long id, SaveRuleDefinitionRequest req, RuleDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminTestsManage);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, RuleDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, RuleDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapProducts(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/products").WithTags("Admin: Products").RequireAuthenticatedUser();

        group.MapGet("/attributes", async (ProductAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAttributesAsync(ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("", async (ProductAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, ProductAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Product not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveProductDefinitionRequest req, ProductAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminProductsManage);

        group.MapPut("/{id:long}", async (long id, SaveProductDefinitionRequest req, ProductAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminProductsManage);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, ProductAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, ProductAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapModificationRules(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/modification-rules").WithTags("Admin: Modification Rules").RequireAuthenticatedUser();

        group.MapGet("", async (ModificationRuleAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, ModificationRuleAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Modification rule not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveModificationRuleRequest req, ModificationRuleAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminModificationRulesManage);

        group.MapPut("/{id:long}", async (long id, SaveModificationRuleRequest req, ModificationRuleAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminModificationRulesManage);

        group.MapPost("/{id:long}/activate", async (long id, ReasonOnlyRequest? req, ModificationRuleAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.ActivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ReasonOnlyRequest? req, ModificationRuleAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.DeactivateAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapIsbtProductCodes(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/isbt-product-codes").WithTags("Admin: ISBT Product Codes").RequireAuthenticatedUser();

        group.MapGet("", async (IsbtProductCodeAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);
    }

    private static void MapProviders(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/providers").WithTags("Admin: Providers").RequireAuthenticatedUser();

        group.MapGet("", async (OrderingProviderAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, OrderingProviderAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Provider not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveOrderingProviderRequest req, OrderingProviderAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPut("/{id:long}", async (long id, SaveOrderingProviderRequest req, OrderingProviderAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/{id:long}/activate", async (long id, OrderingProviderAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, true, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, OrderingProviderAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, false, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapExceptions(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/exceptions").WithTags("Admin: Exceptions").RequireAuthenticatedUser();

        group.MapGet("", async (ExceptionDefinitionAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/by-code/{ruleCode}", async (string ruleCode, ExceptionDefinitionAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetByRuleCodeAsync(ruleCode, ct);
            return dto is null ? Results.NotFound(new { error = "Exception definition not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, ExceptionDefinitionAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Exception definition not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveExceptionDefinitionRequest req, ExceptionDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPut("/{id:long}", async (long id, SaveExceptionDefinitionRequest req, ExceptionDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/{id:long}/activate", async (long id, ExceptionDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, true, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, ExceptionDefinitionAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, false, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapLocations(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/locations").WithTags("Admin: Ordering Locations").RequireAuthenticatedUser();

        group.MapGet("", async (OrderingLocationAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, OrderingLocationAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Location not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveOrderingLocationRequest req, OrderingLocationAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPut("/{id:long}", async (long id, SaveOrderingLocationRequest req, OrderingLocationAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/{id:long}/activate", async (long id, OrderingLocationAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, true, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);

        group.MapPost("/{id:long}/deactivate", async (long id, OrderingLocationAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetActiveAsync(id, false, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigActivate);
    }

    private static void MapHl7(WebApplication app)
    {
        var group = app.MapGroup("/api/admin/hl7/endpoints").WithTags("Admin: HL7").RequireAuthenticatedUser();

        group.MapGet("", async (Hl7ConfigAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        group.MapGet("/{id:long}", async (long id, Hl7ConfigAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Endpoint not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        group.MapPost("", async (SaveHl7EndpointRequest req, Hl7ConfigAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminHl7Manage);

        group.MapPut("/{id:long}", async (long id, SaveHl7EndpointRequest req, Hl7ConfigAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminHl7Manage);

        group.MapPost("/{id:long}/enable", async (long id, ReasonOnlyRequest? req, Hl7ConfigAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetEnabledAsync(id, true, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminHl7Manage);

        group.MapPost("/{id:long}/disable", async (long id, ReasonOnlyRequest? req, Hl7ConfigAdminService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetEnabledAsync(id, false, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminHl7Manage);
    }

    private static void MapUsersAndRoles(WebApplication app)
    {
        var users = app.MapGroup("/api/admin/users").WithTags("Admin: Users").RequireAuthenticatedUser();

        users.MapGet("", async (UserAdminService svc, bool? includeInactive, CancellationToken ct) =>
            Results.Ok(await svc.ListUsersAsync(includeInactive ?? true, ct)))
            .RequirePermission(PermissionCodes.AdminUsersManage);

        users.MapGet("/{id:long}", async (long id, UserAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetUserAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "User not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminUsersManage);

        users.MapPost("", async (SaveUserRequest req, UserAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.CreateUserAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminUsersManage);

        users.MapPut("/{id:long}", async (long id, SaveUserRequest req, UserAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.UpdateUserAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminUsersManage);

        users.MapPost("/{id:long}/roles", async (long id, AssignRolesRequest req, UserAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.AssignRolesAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminUsersManage);

        users.MapPost("/{id:long}/active", async (long id, SetActiveRequest req, UserAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.SetActiveAsync(id, req.Active, req.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminUsersManage);

        users.MapPost("/{id:long}/lock", async (long id, SetActiveRequest req, UserAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.SetLockedAsync(id, req.Active, req.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminUsersManage);

        users.MapPost("/{id:long}/reset-password", async (long id, ReasonOnlyRequest? req, UserAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.RequestPasswordResetAsync(id, req?.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminUsersManage);

        var roles = app.MapGroup("/api/admin/roles").WithTags("Admin: Roles").RequireAuthenticatedUser();

        roles.MapGet("", async (UserAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRolesAsync(ct)))
            .RequirePermission(PermissionCodes.AdminConfigView);

        roles.MapGet("/{id:long}", async (long id, UserAdminService svc, CancellationToken ct) =>
        {
            var dto = await svc.GetRoleAsync(id, ct);
            return dto is null ? Results.NotFound(new { error = "Role not found." }) : Results.Ok(dto);
        }).RequirePermission(PermissionCodes.AdminConfigView);

        roles.MapPost("", async (SaveRoleRequest req, UserAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.CreateRoleAsync(req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminRolesManage);

        roles.MapPut("/{id:long}", async (long id, SaveRoleRequest req, UserAdminService svc, CancellationToken ct) =>
            EndpointResults.From(await svc.UpdateRoleAsync(id, req, ct), d => d))
            .RequirePermission(PermissionCodes.AdminRolesManage);

        app.MapGet("/api/admin/permissions", async (UserAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPermissionCodesAsync(ct)))
            .RequireAuthenticatedUser()
            .RequirePermission(PermissionCodes.AdminConfigView)
            .WithTags("Admin: Roles");
    }

    private static void MapHistory(WebApplication app)
    {
        app.MapGet("/api/admin/history", async (
            IConfigurationHistoryReader reader, string? entityType, long? entityId, int? max, CancellationToken ct) =>
        {
            var limit = Math.Clamp(max ?? 100, 1, 500);
            var rows = entityType is not null && entityId is not null
                ? await reader.GetForEntityAsync(entityType, entityId.Value, limit, ct)
                : await reader.RecentAsync(entityType, limit, ct);
            return Results.Ok(rows);
        })
        .RequireAuthenticatedUser()
        .RequirePermission(PermissionCodes.AdminAuditReview)
        .WithTags("Admin: History");
    }
}
