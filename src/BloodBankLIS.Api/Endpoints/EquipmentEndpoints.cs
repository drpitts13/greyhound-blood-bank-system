using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>The equipment register (Cycle 708): reading is open to signed-in staff, recording checks and taking
/// equipment out of service is bench work (result.enter), adding equipment and checks is configuration.</summary>
public static class EquipmentEndpoints
{
    public sealed record EquipmentReasonBody(string? Reason);

    public sealed record EquipmentServiceBody(bool InService, string? Reason);

    public static void MapEquipmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/equipment").WithTags("Equipment").RequireAuthenticatedUser();

        group.MapGet("", async (bool? includeInactive, EquipmentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(includeInactive ?? false, ct)));

        group.MapGet("/due", async (EquipmentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListDueAsync(ct)));

        group.MapGet("/upcoming", async (int? days, EquipmentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListUpcomingAsync(days ?? 30, ct)));

        group.MapGet("/{id:long}", async (long id, EquipmentService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } equipment ? Results.Ok(equipment) : Results.NotFound());

        group.MapGet("/{id:long}/records", async (long id, int? take, EquipmentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRecordsAsync(id, take ?? 50, ct)));

        group.MapPost("", async (SaveEquipmentRequest request, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.CreateAsync(request, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPut("/{id:long}", async (long id, UpdateEquipmentRequest request, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateAsync(id, request, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPut("/checks/{checkId:long}", async (long checkId, UpdateEquipmentCheckRequest request, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.UpdateCheckAsync(checkId, request, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/{id:long}/checks", async (long id, SaveEquipmentCheckRequest request, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.AddCheckAsync(id, request, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/checks/{checkId:long}/retire", async (long checkId, EquipmentReasonBody body, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.RetireCheckAsync(checkId, body.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/{id:long}/deactivate", async (long id, EquipmentReasonBody body, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.DeactivateAsync(id, body.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/{id:long}/restore", async (long id, EquipmentReasonBody body, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.RestoreAsync(id, body.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.AdminConfigEdit);

        group.MapPost("/checks/{checkId:long}/records", async (long checkId, RecordEquipmentCheckRequest request, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.RecordAsync(checkId, request, ct), d => d))
            .RequirePermission(PermissionCodes.ResultEnter);

        group.MapPost("/{id:long}/service", async (long id, EquipmentServiceBody body, EquipmentService svc, CancellationToken ct) =>
            EndpointResults.FromEvaluation(await svc.SetInServiceAsync(id, body.InService, body.Reason, ct), d => d))
            .RequirePermission(PermissionCodes.ResultEnter);
    }
}
