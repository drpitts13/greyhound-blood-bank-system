using System.Text.Json.Nodes;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Interop.Fhir;

/// <summary>
/// Read-only FHIR R4 view of blood bank data for the EHR (OCD-043). Nothing here creates or changes clinical data.
/// Every read or search that returns a patient's data is audited as PatientAccess, the same as opening the chart.
/// A read that finds nothing returns null; the endpoint turns that into a 404 OperationOutcome.
/// </summary>
public sealed class FhirReadService
{
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<SpecialTransfusionRequirement> _requirements;
    private readonly IRepository<SpecialRequirementDefinition> _definitions;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter? _audit;
    private readonly IRepository<TestResult>? _results;
    private readonly IRepository<TestDefinition>? _tests;

    public FhirReadService(
        IRepository<Patient> patients,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IRepository<SpecialTransfusionRequirement> requirements,
        IRepository<SpecialRequirementDefinition> definitions,
        IRepository<Order> orders,
        IRepository<BloodUnit> units,
        IRepository<ProductType> products,
        IUnitOfWork unitOfWork,
        IAuditWriter? audit = null,
        IRepository<TestResult>? results = null,
        IRepository<TestDefinition>? tests = null)
    {
        _results = results;
        _tests = tests;
        _patients = patients;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _requirements = requirements;
        _definitions = definitions;
        _orders = orders;
        _units = units;
        _products = products;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<JsonObject?> ReadPatientAsync(long id, CancellationToken ct = default)
    {
        var patient = (await _patients.ListAsync(p => p.Id == id, ct)).FirstOrDefault();
        if (patient is null)
        {
            return null;
        }

        await AuditAsync([patient.Id], ct);
        return await ToPatientAsync(patient, ct);
    }

    /// <summary>Search by MRN. Accepts a bare value or FHIR token form <c>system|value</c>; a foreign system matches nothing.</summary>
    public async Task<JsonObject> SearchPatientsAsync(string? identifier, string selfUrl, CancellationToken ct = default)
    {
        var value = TokenValue(identifier, FhirMapper.MrnSystem);
        if (value is null)
        {
            return FhirMapper.Bundle([], selfUrl);
        }

        var matches = await _patients.ListAsync(p => p.MedicalRecordNumber == value, ct);
        await AuditAsync(matches.Select(p => p.Id), ct);
        var resources = new List<JsonObject>();
        foreach (var p in matches)
        {
            resources.Add(await ToPatientAsync(p, ct));
        }

        return FhirMapper.Bundle(resources, selfUrl);
    }

    /// <summary>
    /// Current ABO/Rh, active antibodies, and verified test results (the current version of each; a corrected result
    /// is sent as corrected). Superseded types, deactivated antibodies, unverified and invalidated results are not sent.
    /// </summary>
    public async Task<JsonObject> SearchObservationsAsync(long patientId, string selfUrl, CancellationToken ct = default)
    {
        var types = await _bloodTypes.ListAsync(b => b.PatientId == patientId && b.IsCurrent, ct);
        var antibodies = await _antibodies.ListAsync(a => a.PatientId == patientId && a.IsActive, ct);
        var resources = types.Select(FhirMapper.BloodType)
            .Concat(antibodies.OrderBy(a => a.CreatedUtc).Select(FhirMapper.Antibody))
            .ToList();
        if (_results is not null)
        {
            var results = await _results.ListAsync(r => r.PatientId == patientId && r.SupersededByResultId == null && r.VerifiedUtc != null
                && (r.Status == ResultStatus.Verified || r.Status == ResultStatus.Corrected), ct);
            var definitions = await DefinitionsAsync(results.Select(r => r.TestCode), ct);
            resources.AddRange(results.OrderBy(r => r.VerifiedUtc ?? r.EnteredUtc).ThenBy(r => r.Id)
                .Select(r => FhirMapper.TestResult(r, definitions.GetValueOrDefault(r.TestCode.ToUpperInvariant()))));
        }
        await AuditIfAnyAsync(patientId, resources.Count, ct);
        return FhirMapper.Bundle(resources, selfUrl);
    }

    /// <summary>Like search, only the current blood type and active antibodies: a superseded type must not reach the EHR as "final".</summary>
    public async Task<JsonObject?> ReadObservationAsync(string id, CancellationToken ct = default)
    {
        if (TryParseId(id, "abo-", out var aboId))
        {
            var b = (await _bloodTypes.ListAsync(x => x.Id == aboId && x.IsCurrent, ct)).FirstOrDefault();
            return b is null ? null : await AuditedAsync(b.PatientId, FhirMapper.BloodType(b), ct);
        }

        // By id, an invalidated result is returned as entered-in-error so the EHR can withdraw it; unverified ones never.
        if (TryParseId(id, "result-", out var resultId) && _results is not null)
        {
            var r = (await _results.ListAsync(x => x.Id == resultId && x.VerifiedUtc != null
                && (x.Status == ResultStatus.Verified || x.Status == ResultStatus.Corrected || x.Status == ResultStatus.Invalidated), ct)).FirstOrDefault();
            if (r is null)
            {
                return null;
            }

            var definition = (await DefinitionsAsync([r.TestCode], ct)).GetValueOrDefault(r.TestCode.ToUpperInvariant());
            return await AuditedAsync(r.PatientId, FhirMapper.TestResult(r, definition), ct);
        }

        if (TryParseId(id, "antibody-", out var antibodyId))
        {
            var a = (await _antibodies.ListAsync(x => x.Id == antibodyId && x.IsActive, ct)).FirstOrDefault();
            return a is null ? null : await AuditedAsync(a.PatientId, FhirMapper.Antibody(a), ct);
        }

        return null;
    }

    private async Task<Dictionary<string, TestDefinition>> DefinitionsAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var wanted = codes.Select(c => c.ToUpperInvariant()).Distinct().ToList();
        if (_tests is null || wanted.Count == 0)
        {
            return new Dictionary<string, TestDefinition>();
        }

        return (await _tests.ListAsync(t => t.IsActive && !t.IsDraft, ct))
            .Where(t => wanted.Contains(t.Code.ToUpperInvariant()))
            .GroupBy(t => t.Code.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>All special transfusion requirements, active and inactive, so the EHR can see when one ended.</summary>
    public async Task<JsonObject> SearchFlagsAsync(long patientId, string selfUrl, CancellationToken ct = default)
    {
        var rows = await _requirements.ListAsync(r => r.PatientId == patientId, ct);
        var names = await DefinitionNamesAsync(ct);
        var resources = rows.OrderBy(r => r.EffectiveUtc).Select(r => FhirMapper.Requirement(r, NameOf(r, names))).ToList();
        await AuditIfAnyAsync(patientId, resources.Count, ct);
        return FhirMapper.Bundle(resources, selfUrl);
    }

    public async Task<JsonObject?> ReadFlagAsync(string id, CancellationToken ct = default)
    {
        if (!TryParseId(id, "requirement-", out var reqId))
        {
            return null;
        }

        var r = (await _requirements.ListAsync(x => x.Id == reqId, ct)).FirstOrDefault();
        if (r is null)
        {
            return null;
        }

        return await AuditedAsync(r.PatientId, FhirMapper.Requirement(r, NameOf(r, await DefinitionNamesAsync(ct))), ct);
    }

    public async Task<JsonObject> SearchServiceRequestsAsync(long patientId, string selfUrl, CancellationToken ct = default)
    {
        var rows = await _orders.ListAsync(o => o.PatientId == patientId, ct);
        var resources = rows.OrderBy(o => o.OrderedUtc).Select(FhirMapper.ServiceRequest).ToList();
        await AuditIfAnyAsync(patientId, resources.Count, ct);
        return FhirMapper.Bundle(resources, selfUrl);
    }

    public async Task<JsonObject?> ReadServiceRequestAsync(long id, CancellationToken ct = default)
    {
        var o = (await _orders.ListAsync(x => x.Id == id, ct)).FirstOrDefault();
        return o is null ? null : await AuditedAsync(o.PatientId, FhirMapper.ServiceRequest(o), ct);
    }

    /// <summary>Inventory, not patient data, so no PatientAccess audit.</summary>
    public async Task<JsonObject?> ReadProductAsync(long id, CancellationToken ct = default)
    {
        var u = (await _units.ListAsync(x => x.Id == id, ct)).FirstOrDefault();
        return u is null ? null : FhirMapper.Product(u, await ProductTypeAsync(u.ProductTypeId, ct));
    }

    /// <summary>Search by unit number or DIN (token: bare value, or either local system).</summary>
    public async Task<JsonObject> SearchProductsAsync(string? identifier, string selfUrl, CancellationToken ct = default)
    {
        var value = TokenValue(identifier, FhirMapper.UnitNumberSystem, FhirMapper.DinSystem);
        if (value is null)
        {
            return FhirMapper.Bundle([], selfUrl);
        }

        var units = await _units.ListAsync(u => u.UnitNumber == value || u.Din == value, ct);
        var resources = new List<JsonObject>();
        foreach (var u in units)
        {
            resources.Add(FhirMapper.Product(u, await ProductTypeAsync(u.ProductTypeId, ct)));
        }

        return FhirMapper.Bundle(resources, selfUrl);
    }

    /// <summary>Accepts <c>Patient/12</c> or <c>12</c>.</summary>
    public static bool TryParsePatientReference(string? reference, out long patientId)
    {
        patientId = 0;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var value = reference.Trim();
        if (value.StartsWith("Patient/", StringComparison.Ordinal))
        {
            value = value["Patient/".Length..];
        }

        return long.TryParse(value, out patientId) && patientId > 0;
    }

    private async Task<JsonObject> ToPatientAsync(Patient patient, CancellationToken ct)
    {
        Patient? survivor = null;
        if (patient.MergedIntoPatientId is long survivorId)
        {
            survivor = (await _patients.ListAsync(p => p.Id == survivorId, ct)).FirstOrDefault();
        }

        return FhirMapper.Patient(patient, survivor);
    }

    private async Task<ProductType?> ProductTypeAsync(long productTypeId, CancellationToken ct) =>
        (await _products.ListAsync(p => p.Id == productTypeId, ct)).FirstOrDefault();

    private async Task<Dictionary<long, string>> DefinitionNamesAsync(CancellationToken ct) =>
        (await _definitions.ListAsync(ct)).ToDictionary(d => d.Id, d => d.Name);

    private static string? NameOf(SpecialTransfusionRequirement r, Dictionary<long, string> names) =>
        r.RequirementDefinitionId is long id && names.TryGetValue(id, out var name) ? name : null;

    private async Task<JsonObject> AuditedAsync(long patientId, JsonObject resource, CancellationToken ct)
    {
        await AuditAsync([patientId], ct);
        return resource;
    }

    private Task AuditIfAnyAsync(long patientId, int count, CancellationToken ct) =>
        count == 0 ? Task.CompletedTask : AuditAsync([patientId], ct);

    private async Task AuditAsync(IEnumerable<long> patientIds, CancellationToken ct)
    {
        if (_audit is null)
        {
            return;
        }

        var any = false;
        foreach (var id in patientIds.Distinct())
        {
            _audit.Record(
                AuditEventType.PatientAccess,
                nameof(Patient),
                id,
                newValue: new { PatientId = id, Channel = "FHIR" },
                reason: "Patient data read through the FHIR API.");
            any = true;
        }

        if (any)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    private static bool TryParseId(string id, string prefix, out long value)
    {
        value = 0;
        return id.StartsWith(prefix, StringComparison.Ordinal) && long.TryParse(id[prefix.Length..], out value);
    }

    private static string? TokenValue(string? token, params string[] systems)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var bar = token.IndexOf('|');
        if (bar < 0)
        {
            return token.Trim();
        }

        var system = token[..bar];
        var value = token[(bar + 1)..].Trim();
        if (value.Length == 0 || (system.Length > 0 && !systems.Contains(system, StringComparer.Ordinal)))
        {
            return null;
        }

        return value;
    }
}
