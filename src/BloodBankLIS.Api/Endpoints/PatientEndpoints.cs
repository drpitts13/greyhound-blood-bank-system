using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Services;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/patients").WithTags("Patients").RequireAuthenticatedUser();

        group.MapGet("/", async (EntityCrudService<Patient> service, CancellationToken ct) =>
        {
            var patients = await service.ListAsync(ct);
            return Results.Ok(patients.Select(PatientDto.From));
        });

        group.MapGet("/{id:long}", async (long id, EntityCrudService<Patient> service, CancellationToken ct) =>
        {
            var patient = await service.GetAsync(id, ct);
            return patient is null ? Results.NotFound() : Results.Ok(PatientDto.From(patient));
        });

        group.MapPost("/", async (CreatePatientRequest request, EntityCrudService<Patient> service, CancellationToken ct) =>
        {
            var patient = new Patient
            {
                MedicalRecordNumber = request.MedicalRecordNumber,
                LastName = request.LastName,
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                DateOfBirth = request.DateOfBirth,
                Sex = request.Sex
            };

            await service.CreateAsync(patient, ct);
            return Results.Created($"/api/patients/{patient.Id}", PatientDto.From(patient));
        }).RequirePermission(PermissionCodes.PatientWrite);

        group.MapPut("/{id:long}", async (long id, UpdatePatientRequest request, EntityCrudService<Patient> service, CancellationToken ct) =>
        {
            var patient = await service.GetAsync(id, ct);
            if (patient is null)
            {
                return Results.NotFound();
            }

            patient.LastName = request.LastName;
            patient.FirstName = request.FirstName;
            patient.MiddleName = request.MiddleName;
            patient.DateOfBirth = request.DateOfBirth;
            patient.Sex = request.Sex;
            patient.Status = request.Status;

            await service.UpdateAsync(patient, ct);
            return Results.Ok(PatientDto.From(patient));
        }).RequirePermission(PermissionCodes.PatientWrite);
    }
}
