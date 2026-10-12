using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/patients").WithTags("Patients").RequireAuthenticatedUser();

        group.MapGet("/", async (PatientService patients, CancellationToken ct) =>
            Results.Ok(await patients.ListDtosAsync(ct)));

        group.MapGet("/{id:long}", async (long id, PatientService patients, CancellationToken ct) =>
        {
            var patient = await patients.GetDtoAsync(id, ct);
            if (patient is null)
            {
                return Results.NotFound();
            }

            await patients.RecordAccessAsync(id, ct);
            return Results.Ok(patient);
        });

        group.MapPost("/", async (CreatePatientRequest request, PatientService patients, CancellationToken ct) =>
            EndpointResults.Created(await patients.CreateAsync(request, ct),
                p => ($"/api/patients/{p.Id}", (object)PatientDto.From(p))))
            .RequirePermission(PermissionCodes.PatientWrite);

        group.MapPut("/{id:long}", async (
            long id,
            UpdatePatientRequest request,
            PatientService patients,
            CancellationToken ct) =>
        {
            var result = await patients.UpdateAsync(id, request, ct);
            return EndpointResults.From(result, p => PatientDto.From(p));
        }).RequirePermission(PermissionCodes.PatientWrite);

        group.MapPost("/{id:long}/merge", async (
            long id,
            MergePatientsRequest request,
            PatientMergeService merges,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.DuplicateMrn))
            {
                return Results.BadRequest(new { error = "Duplicate patient MRN is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "A reason is required to merge patient records." });
            }

            var duplicate = await merges.FindByMrnAsync(request.DuplicateMrn, followMerge: false, ct);
            if (duplicate is null)
            {
                return Results.NotFound(new { error = $"Duplicate patient '{request.DuplicateMrn.Trim()}' was not found." });
            }

            var result = await merges.MergeAsync(id, duplicate.Id, request.Reason, ct);
            return EndpointResults.From(result, p => PatientDto.From(p));
        }).RequirePermission(PermissionCodes.PatientMerge);

        group.MapPost("/{id:long}/unmerge", async (
            long id,
            UnmergePatientRequest request,
            PatientMergeService merges,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "A reason is required to undo a patient merge." });
            }

            var result = await merges.UnmergeAsync(id, request.Reason, ct);
            return EndpointResults.From(result, p => PatientDto.From(p));
        }).RequirePermission(PermissionCodes.PatientMerge);
    }
}
