using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Application.Search;

/// <summary>
/// The top-bar "scan anything" lookup. One field takes a unit DIN or number, a specimen accession or tube barcode,
/// an order number, an MRN (current or prior), or part of a patient name, and returns the matching records.
/// Exact identifier matches win; partial matches are offered only when nothing matched exactly.
/// Read-only: it opens nothing itself, and the patient chart audits the access when the user opens it.
/// </summary>
public sealed class UniversalLookupService
{
    public const int PartialLimit = 20;
    private const int MinimumPartialLength = 2;
    private const int MaxMergeHops = 5;

    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientIdentifier> _identifiers;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _products;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<PatientBloodTypeHistory>? _bloodTypes;
    private readonly IRepository<AntibodyHistory>? _antibodies;
    private readonly IRepository<SpecialTransfusionRequirement>? _requirements;
    private readonly IClock? _clock;

    public UniversalLookupService(
        IRepository<Patient> patients,
        IRepository<PatientIdentifier> identifiers,
        IRepository<BloodUnit> units,
        IRepository<ProductType> products,
        IRepository<Specimen> specimens,
        IRepository<Order> orders,
        IRepository<PatientBloodTypeHistory>? bloodTypes = null,
        IRepository<AntibodyHistory>? antibodies = null,
        IRepository<SpecialTransfusionRequirement>? requirements = null,
        IClock? clock = null)
    {
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _requirements = requirements;
        _clock = clock;
        _patients = patients;
        _identifiers = identifiers;
        _units = units;
        _products = products;
        _specimens = specimens;
        _orders = orders;
    }

    public async Task<LookupResultDto> LookupAsync(string? query, CancellationToken ct = default)
    {
        var sanitized = ScannerInputSanitizer.Sanitize(query).Sanitized.Trim();
        if (sanitized.Length == 0)
        {
            return LookupResultDto.Empty(string.Empty);
        }

        var compact = sanitized.Replace(" ", string.Empty);
        var upper = compact.ToUpperInvariant();

        // A scanner DIN (leading '=') is unambiguous: report a malformed scan instead of searching names for it.
        string? din = null;
        var dinParse = DinParser.Parse(sanitized);
        if (dinParse.Success)
        {
            din = dinParse.Value!.Din;
        }
        else if (sanitized.StartsWith('='))
        {
            return LookupResultDto.Empty(sanitized,
                $"That scan is not a readable unit number (DIN). {dinParse.Errors.FirstOrDefault()?.Message} Scan the unit number barcode again.");
        }

        var hits = new List<LookupHitDto>();
        hits.AddRange(await ExactUnitsAsync(upper, din?.ToUpperInvariant(), ct));
        if (!sanitized.StartsWith('='))
        {
            hits.AddRange(await ExactSpecimensAsync(upper, ct));
            hits.AddRange(await ExactOrdersAsync(upper, ct));
            hits.AddRange(await ExactPatientsAsync(upper, ct));
        }

        if (hits.Count > 0)
        {
            return new LookupResultDto(sanitized, hits, false, null);
        }

        if (sanitized.StartsWith('=') || sanitized.Length < MinimumPartialLength)
        {
            return LookupResultDto.Empty(sanitized, NoMatch(sanitized));
        }

        var (patients, patientsCapped) = await PartialPatientsAsync(sanitized, ct);
        var (units, unitsCapped) = await PartialUnitsAsync(upper, ct);
        var partial = patients.Concat(units).ToList();
        if (partial.Count == 0)
        {
            return LookupResultDto.Empty(sanitized, NoMatch(sanitized));
        }

        var truncated = patientsCapped || unitsCapped;
        return new LookupResultDto(
            sanitized,
            partial,
            truncated,
            truncated ? $"Showing the first {PartialLimit} of each kind. Type more of the name or number to narrow the list." : null);
    }

    private static string NoMatch(string query) =>
        $"No patient, unit, specimen, or order matches \"{query}\".";

    private async Task<IReadOnlyList<LookupHitDto>> ExactUnitsAsync(string upper, string? din, CancellationToken ct)
    {
        var units = await _units.ListAsync(u =>
            u.UnitNumber.ToUpper() == upper
            || (u.ComponentIdentity != null && u.ComponentIdentity.ToUpper() == upper)
            || (din != null && (u.UnitNumber.ToUpper() == din || (u.Din != null && u.Din.ToUpper() == din))), ct);
        return await ToUnitHitsAsync(units, exact: true, ct);
    }

    private async Task<(IReadOnlyList<LookupHitDto> Hits, bool Capped)> PartialUnitsAsync(string upper, CancellationToken ct)
    {
        var units = (await _units.ListAsync(u => u.UnitNumber.ToUpper().Contains(upper), ct))
            .OrderBy(u => u.UnitNumber)
            .Take(PartialLimit + 1)
            .ToList();
        var hits = await ToUnitHitsAsync(units.Take(PartialLimit).ToList(), exact: false, ct);
        return (hits, units.Count > PartialLimit);
    }

    private async Task<IReadOnlyList<LookupHitDto>> ToUnitHitsAsync(IReadOnlyList<BloodUnit> units, bool exact, CancellationToken ct)
    {
        if (units.Count == 0)
        {
            return [];
        }

        var productIds = units.Select(u => u.ProductTypeId).Distinct().ToList();
        var products = (await _products.ListAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        return units
            .OrderBy(u => u.UnitNumber)
            .Select(u =>
            {
                var product = products.TryGetValue(u.ProductTypeId, out var p) && !string.IsNullOrWhiteSpace(p.Name)
                    ? p.Name
                    : $"Product {u.ProductTypeId}";
                return new LookupHitDto(
                    LookupHitKind.Unit,
                    u.Id,
                    exact,
                    u.UnitNumber,
                    product,
                    $"{AboRhLabel(u.Abo, u.RhD)} · {u.Status} · expires {u.ExpiresUtc:yyyy-MM-dd HH:mm} UTC",
                    $"/inventory/units/{u.Id}");
            })
            .ToList();
    }

    private async Task<IReadOnlyList<LookupHitDto>> ExactSpecimensAsync(string upper, CancellationToken ct)
    {
        var specimens = await _specimens.ListAsync(s =>
            s.AccessionNumber.ToUpper() == upper || (s.Barcode != null && s.Barcode.ToUpper() == upper), ct);
        if (specimens.Count == 0)
        {
            return [];
        }

        var patients = await PatientsByIdAsync(specimens.Select(s => s.PatientId), ct);
        return specimens
            .Select(s =>
            {
                var owner = patients.GetValueOrDefault(s.PatientId);
                return new LookupHitDto(
                    LookupHitKind.Specimen,
                    s.Id,
                    true,
                    s.AccessionNumber,
                    $"Specimen for {PatientLabel(owner, s.PatientId)}",
                    $"{s.SpecimenType} · {s.Status} · collected {s.CollectedUtc:yyyy-MM-dd HH:mm} UTC",
                    $"/patients/{s.PatientId}?tab=specimens&specimen={s.Id}");
            })
            .ToList();
    }

    private async Task<IReadOnlyList<LookupHitDto>> ExactOrdersAsync(string upper, CancellationToken ct)
    {
        var orders = await _orders.ListAsync(o => o.OrderNumber.ToUpper() == upper, ct);
        if (orders.Count == 0)
        {
            return [];
        }

        var patients = await PatientsByIdAsync(orders.Select(o => o.PatientId), ct);
        return orders
            .Select(o => new LookupHitDto(
                LookupHitKind.Order,
                o.Id,
                true,
                o.OrderNumber,
                $"{(string.IsNullOrWhiteSpace(o.OrderName) ? "Order" : o.OrderName)} for {PatientLabel(patients.GetValueOrDefault(o.PatientId), o.PatientId)}",
                $"{o.Priority} · {o.Status}",
                $"/patients/{o.PatientId}?tab=orders"))
            .ToList();
    }

    private async Task<IReadOnlyList<LookupHitDto>> ExactPatientsAsync(string upper, CancellationToken ct)
    {
        var byMrn = await _patients.ListAsync(p => p.MedicalRecordNumber.ToUpper() == upper, ct);
        var priorMrn = await _identifiers.ListAsync(i =>
            i.IsActive
            && i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber
            && i.Value.ToUpper() == upper, ct);

        var hits = new List<LookupHitDto>();
        foreach (var patient in byMrn)
        {
            hits.Add(await PatientHitAsync(patient, exact: true, matchedNote: null, ct));
        }

        var priorOwners = await PatientsByIdAsync(priorMrn.Select(i => i.PatientId), ct);
        foreach (var owner in priorOwners.Values)
        {
            hits.Add(await PatientHitAsync(owner, exact: true, matchedNote: $"Prior MRN {upper}", ct));
        }

        return Distinct(hits);
    }

    private async Task<(IReadOnlyList<LookupHitDto> Hits, bool Capped)> PartialPatientsAsync(string term, CancellationToken ct)
    {
        var upper = term.ToUpperInvariant();
        var direct = (await _patients.ListAsync(p => p.MedicalRecordNumber.ToUpper().Contains(upper)
                || p.LastName.ToUpper().Contains(upper)
                || p.FirstName.ToUpper().Contains(upper)
                || (p.LastName + ", " + p.FirstName).ToUpper().Contains(upper), ct))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id)
            .Take(PartialLimit + 1)
            .ToList();
        var aliasOwnerIds = (await _identifiers.ListAsync(i => i.IsActive && i.Value.ToUpper().Contains(upper), ct))
            .Select(i => i.PatientId)
            .Distinct()
            .Take(PartialLimit + 1)
            .ToList();
        var aliasOwners = await PatientsByIdAsync(aliasOwnerIds.Where(id => direct.All(p => p.Id != id)), ct);

        var all = direct.Concat(aliasOwners.Values).ToList();
        var hits = new List<LookupHitDto>();
        foreach (var patient in all.Take(PartialLimit))
        {
            var note = aliasOwners.ContainsKey(patient.Id) ? "Matched a prior name or MRN" : null;
            hits.Add(await PatientHitAsync(patient, exact: false, note, ct));
        }

        return (Distinct(hits), all.Count > PartialLimit);
    }

    /// <summary>A merged duplicate opens the surviving chart, and says so, so nobody works on the retired record.</summary>
    private async Task<LookupHitDto> PatientHitAsync(Patient patient, bool exact, string? matchedNote, CancellationToken ct)
    {
        var target = patient;
        string? mergedNote = null;
        for (var hop = 0; hop < MaxMergeHops && target.MergedIntoPatientId is long survivorId; hop++)
        {
            var survivor = (await _patients.ListAsync(p => p.Id == survivorId, ct)).FirstOrDefault();
            if (survivor is null)
            {
                break;
            }

            mergedNote = $"MRN {patient.MedicalRecordNumber} was merged into MRN {survivor.MedicalRecordNumber}";
            target = survivor;
        }

        var detail = string.Join(" · ", new[]
        {
            $"DOB {target.DateOfBirth:yyyy-MM-dd}",
            target.Sex.ToString(),
            mergedNote,
            matchedNote
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var targetId = target.Id;
        var bloodType = _bloodTypes is null
            ? null
            : PatientBloodBankFlags.CurrentBloodType(await _bloodTypes.ListAsync(t => t.PatientId == targetId, ct));
        var antibodies = _antibodies is null
            ? []
            : PatientBloodBankFlags.ActiveAntibodies(await _antibodies.ListAsync(a => a.PatientId == targetId, ct));
        var requirements = _requirements is null
            ? 0
            : PatientBloodBankFlags.RequirementsInEffect(
                await _requirements.ListAsync(r => r.PatientId == targetId, ct), _clock?.UtcNow ?? DateTime.UtcNow);

        return new LookupHitDto(
            LookupHitKind.Patient,
            target.Id,
            exact,
            target.MedicalRecordNumber,
            PatientName(target),
            detail,
            $"/patients/{target.Id}",
            bloodType,
            antibodies,
            requirements);
    }

    private static IReadOnlyList<LookupHitDto> Distinct(IEnumerable<LookupHitDto> hits) =>
        hits.GroupBy(h => (h.Kind, h.Id)).Select(g => g.First()).ToList();

    private async Task<Dictionary<long, Patient>> PatientsByIdAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        return (await _patients.ListAsync(p => wanted.Contains(p.Id), ct)).ToDictionary(p => p.Id);
    }

    private static string PatientName(Patient p)
    {
        var name = $"{p.LastName}, {p.FirstName}".Trim().Trim(',').Trim();
        return string.IsNullOrWhiteSpace(name) ? $"Patient {p.Id}" : name;
    }

    private static string PatientLabel(Patient? p, long patientId) =>
        p is null ? $"patient {patientId}" : $"{PatientName(p)} (MRN {p.MedicalRecordNumber})";

    private static string AboRhLabel(AboGroup abo, RhType rh)
    {
        var aboText = abo == AboGroup.Unknown ? "ABO unknown" : abo.ToString();
        var rhText = rh switch
        {
            RhType.Positive => "Pos",
            RhType.Negative => "Neg",
            _ => "Rh unknown"
        };
        return $"{aboText} {rhText}";
    }
}
