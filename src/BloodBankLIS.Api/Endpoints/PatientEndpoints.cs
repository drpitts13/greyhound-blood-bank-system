using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Compliance;
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

        group.MapGet("/possible-duplicates/pairs", async (PatientService patients, CancellationToken ct) =>
            EndpointResults.From(await patients.ListPossibleDuplicatePairsAsync(ct), d => d))
            .RequirePermission(PermissionCodes.PatientMerge);

        group.MapPost("/possible-duplicates/dismiss", async (DismissPossibleDuplicateRequest request, PatientService patients, CancellationToken ct) =>
            EndpointResults.From(await patients.DismissPossibleDuplicateAsync(request, ct), d => d))
            .RequirePermission(PermissionCodes.PatientMerge);

        group.MapGet("/possible-duplicates", async (string? lastName, string? firstName, DateOnly dateOfBirth, PatientService patients, CancellationToken ct) =>
            EndpointResults.From(await patients.ListPossibleDuplicatesAsync(lastName, firstName, dateOfBirth, ct), d => d))
            .RequirePermission(PermissionCodes.PatientWrite);

        // Other charts this one may duplicate, so the chart can warn that the blood bank history may be elsewhere.
        group.MapGet("/{id:long}/possible-duplicates", async (long id, PatientService patients, CancellationToken ct) =>
            Results.Ok(await patients.PossibleDuplicatesOfAsync(id, ct)));

        // Mother and infant charts: each shows the other's type and antibodies (information only).
        group.MapGet("/{id:long}/family", async (long id, PatientService patients, CancellationToken ct) =>
            await patients.GetFamilyAsync(id, ct) is { } family ? Results.Ok(family) : Results.NotFound());

        group.MapPost("/{id:long}/mother", async (long id, LinkMotherRequest request, PatientService patients, CancellationToken ct) =>
            EndpointResults.From(await patients.LinkMotherAsync(id, request, ct), p => PatientDto.From(p)))
            .RequirePermission(PermissionCodes.PatientWrite);

        group.MapPost("/{id:long}/mother/unlink", async (long id, UnlinkMotherRequest request, PatientService patients, CancellationToken ct) =>
            EndpointResults.From(await patients.UnlinkMotherAsync(id, request, ct), p => PatientDto.From(p)))
            .RequirePermission(PermissionCodes.PatientWrite);

        // Unidentified emergency patients: register in one step, list those not yet identified, merge once identified.
        group.MapGet("/unidentified", async (PatientService patients, CancellationToken ct) =>
            Results.Ok(await patients.ListUnidentifiedAsync(ct)));

        group.MapPost("/unidentified", async (CreateUnidentifiedPatientRequest request, PatientService patients, CancellationToken ct) =>
            EndpointResults.Created(await patients.CreateUnidentifiedAsync(request, ct),
                p => ($"/api/patients/{p.Id}", (object)PatientDto.From(p))))
            .RequirePermission(PermissionCodes.PatientWrite);

        // Units still out that were issued under another MRN (after a merge): the wristband may differ from the chart.
        group.MapGet("/{id:long}/issued-under-earlier-identity", async (long id, PatientMergeService merges, CancellationToken ct) =>
            Results.Ok(await merges.ListIssuedUnderEarlierIdentityAsync(id, ct)));

        // A reaction still under investigation (Cycle 632): the chart warns before another unit is issued or started.
        // Read with the chart, so it needs no reaction-investigation permission; it carries only the number and time.
        group.MapGet("/{id:long}/reaction-under-investigation", async (long id, ReactionInvestigationService reactions, CancellationToken ct) =>
            Results.Ok(await reactions.OpenForPatientAsync(id, ct)));

        group.MapPost("/{id:long}/identify", async (long id, IdentifyPatientRequest request, PatientMergeService merges, CancellationToken ct) =>
            EndpointResults.From(await merges.IdentifyAsync(id, request, ct), p => PatientDto.From(p)))
            .RequirePermission(PermissionCodes.PatientMerge);

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
