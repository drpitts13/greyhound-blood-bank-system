using System.Text.Json.Nodes;
using BloodBankLIS.Api.Auth;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Interop.Fhir;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Read-only FHIR R4 API for the EHR (OCD-043). GET only: there is no create, update, or delete. Signed-in users
/// only; patient reads are audited by <see cref="FhirReadService"/>. Errors are OperationOutcome resources.
/// </summary>
public static class FhirEndpoints
{
    public const string ContentType = "application/fhir+json";

    public static void MapFhirEndpoints(this WebApplication app)
    {
        var fhir = app.MapGroup("/fhir").WithTags("FHIR R4 (read-only)").RequireAuthenticatedUser();

        fhir.MapGet("/metadata", (IClock clock) => Fhir(FhirMapper.Capability(clock.UtcNow)));

        fhir.MapGet("/Patient/{id:long}", async (long id, FhirReadService svc, CancellationToken ct) =>
            Found(await svc.ReadPatientAsync(id, ct), "Patient", id.ToString()));
        fhir.MapGet("/Patient", async (string? identifier, HttpRequest req, FhirReadService svc, CancellationToken ct) =>
            identifier is null
                ? Invalid("Search Patient by identifier (MRN).")
                : Fhir(await svc.SearchPatientsAsync(identifier, Self(req), ct)));

        fhir.MapGet("/Observation/{id}", async (string id, FhirReadService svc, CancellationToken ct) =>
            Found(await svc.ReadObservationAsync(id, ct), "Observation", id));
        fhir.MapGet("/Observation", async (string? patient, HttpRequest req, FhirReadService svc, CancellationToken ct) =>
            FhirReadService.TryParsePatientReference(patient, out var pid)
                ? Fhir(await svc.SearchObservationsAsync(pid, Self(req), ct))
                : Invalid("Search Observation by patient, for example ?patient=Patient/12."));

        fhir.MapGet("/Flag/{id}", async (string id, FhirReadService svc, CancellationToken ct) =>
            Found(await svc.ReadFlagAsync(id, ct), "Flag", id));
        fhir.MapGet("/Flag", async (string? patient, HttpRequest req, FhirReadService svc, CancellationToken ct) =>
            FhirReadService.TryParsePatientReference(patient, out var pid)
                ? Fhir(await svc.SearchFlagsAsync(pid, Self(req), ct))
                : Invalid("Search Flag by patient, for example ?patient=Patient/12."));

        fhir.MapGet("/ServiceRequest/{id:long}", async (long id, FhirReadService svc, CancellationToken ct) =>
            Found(await svc.ReadServiceRequestAsync(id, ct), "ServiceRequest", id.ToString()));
        fhir.MapGet("/ServiceRequest", async (string? patient, HttpRequest req, FhirReadService svc, CancellationToken ct) =>
            FhirReadService.TryParsePatientReference(patient, out var pid)
                ? Fhir(await svc.SearchServiceRequestsAsync(pid, Self(req), ct))
                : Invalid("Search ServiceRequest by patient, for example ?patient=Patient/12."));

        fhir.MapGet("/BiologicallyDerivedProduct/{id:long}", async (long id, FhirReadService svc, CancellationToken ct) =>
            Found(await svc.ReadProductAsync(id, ct), "BiologicallyDerivedProduct", id.ToString()));
        fhir.MapGet("/BiologicallyDerivedProduct", async (string? identifier, HttpRequest req, FhirReadService svc, CancellationToken ct) =>
            identifier is null
                ? Invalid("Search BiologicallyDerivedProduct by identifier (unit number or DIN).")
                : Fhir(await svc.SearchProductsAsync(identifier, Self(req), ct)));
    }

    private static IResult Fhir(JsonObject resource, int status = StatusCodes.Status200OK) =>
        Results.Text(resource.ToJsonString(), ContentType, statusCode: status);

    private static IResult Found(JsonObject? resource, string type, string id) =>
        resource is null
            ? Fhir(FhirMapper.Outcome("not-found", $"{type}/{id} was not found."), StatusCodes.Status404NotFound)
            : Fhir(resource);

    private static IResult Invalid(string message) =>
        Fhir(FhirMapper.Outcome("required", message), StatusCodes.Status400BadRequest);

    private static string Self(HttpRequest req) => $"{req.Path}{req.QueryString}";
}
