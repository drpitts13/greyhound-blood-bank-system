using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Application.Search;

/// <summary>
/// The top-bar "scan anything" lookup. One field takes a unit DIN or number, a specimen accession or tube barcode,
/// an order number, an MRN (current or prior) or other patient identifier, a visit or account number, or part of a
/// patient name, and returns the matching records.
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
    private readonly IRepository<SpecialRequirementDefinition>? _requirementDefinitions;
    private readonly IRepository<Encounter>? _encounters;

    private readonly IRepository<RhigEvaluation>? _rhig;

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
        IClock? clock = null,
        IRepository<SpecialRequirementDefinition>? requirementDefinitions = null,
        IRepository<Encounter>? encounters = null,
        IRepository<RhigEvaluation>? rhig = null)
    {
        _rhig = rhig;
        _encounters = encounters;
        _requirementDefinitions = requirementDefinitions;
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
            hits.AddRange(await ExactVisitsAsync(upper, ct));
            hits.AddRange(await ExactRhigLotsAsync(upper, ct));
        }

        if (hits.Count > 0)
        {
            // A patient found by both MRN and visit number is one record.
            return new LookupResultDto(sanitized, Distinct(hits), false, null);
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

    /// <summary>A scanned RhIG lot recorded as given: one hit that opens the lot trace (who received it).</summary>
    private async Task<IReadOnlyList<LookupHitDto>> ExactRhigLotsAsync(string upper, CancellationToken ct)
    {
        if (_rhig is null)
        {
            return [];
        }

        var given = await _rhig.ListAsync(e => e.GivenLot != null && e.GivenLot.ToUpper() == upper, ct);
        if (given.Count == 0)
        {
            return [];
        }

        var lot = given[0].GivenLot!;
        var patients = given.Select(e => e.PatientId).Distinct().Count();
        var last = given.Max(e => e.GivenUtc) ?? DateTime.MinValue;
        return
        [
            new LookupHitDto(
                LookupHitKind.RhigLot,
                given.Min(e => e.Id),
                true,
                lot,
                $"RhIG lot given to {patients} patient{(patients == 1 ? "" : "s")}",
                $"Last given {last:yyyy-MM-dd HH:mm} UTC",
                $"/rhig?lot={Uri.EscapeDataString(lot)}")
        ];
    }

    private static string NoMatch(string query) =>
        $"No patient, unit, specimen, order, visit, or RhIG lot matches \"{query}\".";

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
                    $"{s.SpecimenType} · {s.Status} · collected {s.CollectedUtc:yyyy-MM-dd HH:mm} UTC"
                        + (string.IsNullOrWhiteSpace(s.StorageLocation) ? string.Empty : $" · stored in {s.StorageLocation}")
                        + (s.DiscardedUtc is DateTime discarded ? $" · discarded {discarded:yyyy-MM-dd}" : string.Empty),
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
                OrderHref(o.PatientId, o.OrderNumber)))
            .ToList();
    }

    private async Task<IReadOnlyList<LookupHitDto>> ExactPatientsAsync(string upper, CancellationToken ct)
    {
        var byMrn = await _patients.ListAsync(p => p.MedicalRecordNumber.ToUpper() == upper, ct);
        // Scannable identifiers only: a prior MRN, an account number, or another recorded id. Names and dates of birth
        // are not identifiers to scan, and stay with the partial search.
        var identifiers = await _identifiers.ListAsync(i =>
            i.IsActive
            && (i.IdentifierType == IdentityTokenType.PriorMedicalRecordNumber
                || i.IdentifierType == IdentityTokenType.AccountNumber
                || i.IdentifierType == IdentityTokenType.Other)
            && i.Value.ToUpper() == upper, ct);

        var hits = new List<LookupHitDto>();
        foreach (var patient in byMrn)
        {
            hits.Add(await PatientHitAsync(patient, exact: true, matchedNote: null, ct));
        }

        var owners = await PatientsByIdAsync(identifiers.Select(i => i.PatientId), ct);
        foreach (var identifier in identifiers.Where(i => owners.ContainsKey(i.PatientId)))
        {
            hits.Add(await PatientHitAsync(owners[identifier.PatientId], exact: true, IdentifierNote(identifier.IdentifierType, upper), ct));
        }

        return Distinct(hits);
    }

    private static string IdentifierNote(IdentityTokenType type, string value) => type switch
    {
        IdentityTokenType.PriorMedicalRecordNumber => $"Prior MRN {value}",
        IdentityTokenType.AccountNumber => $"Account number {value}",
        _ => $"Identifier {value}"
    };

    /// <summary>
    /// A visit (or its account) number from a requisition or wristband opens that patient on the visit's orders, and says
    /// whether the visit is still open, so an old visit is not mistaken for the current one.
    /// </summary>
    private async Task<IReadOnlyList<LookupHitDto>> ExactVisitsAsync(string upper, CancellationToken ct)
    {
        if (_encounters is null)
        {
            return [];
        }

        var visits = await _encounters.ListAsync(e =>
            e.VisitNumber.ToUpper() == upper || (e.AccountNumber != null && e.AccountNumber.ToUpper() == upper), ct);
        if (visits.Count == 0)
        {
            return [];
        }

        var owners = await PatientsByIdAsync(visits.Select(v => v.PatientId), ct);
        var hits = new List<LookupHitDto>();
        foreach (var visit in visits.Where(v => owners.ContainsKey(v.PatientId)).OrderByDescending(v => v.AdmitUtc ?? DateTime.MinValue))
        {
            hits.Add(await PatientHitAsync(owners[visit.PatientId], exact: true, VisitNote(visit), ct, visit.Id));
        }

        return Distinct(hits);
    }

    /// <summary>"Visit V-1 · Inpatient · ICU 4 · open": the recorded parts, then whether the visit is still open.</summary>
    public static string VisitNote(Encounter visit)
    {
        var state = visit.Status switch
        {
            EncounterStatus.Cancelled => "cancelled",
            _ when visit.DischargeUtc is DateTime discharged => $"discharged {discharged:yyyy-MM-dd}",
            EncounterStatus.Discharged => "discharged",
            EncounterStatus.Historical => "historical",
            EncounterStatus.PreAdmit => "pre-admit",
            _ => "open"
        };
        return string.Join(" · ", new[]
        {
            $"Visit {visit.VisitNumber}",
            visit.EncounterType == EncounterType.Unknown ? null : visit.EncounterType.ToString(),
            (visit.CurrentLocation ?? visit.AdmissionLocation)?.Trim() is { Length: > 0 } place ? place : null,
            state
        }.Where(p => p is not null));
    }

    /// <summary>The Orders tab searching for this order, the same link the chart builds.</summary>
    public static string OrderHref(long patientId, string? orderNumber) =>
        string.IsNullOrWhiteSpace(orderNumber)
            ? $"/patients/{patientId}?tab=orders"
            : $"/patients/{patientId}?tab=orders&order={Uri.EscapeDataString(orderNumber.Trim())}";

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
    private async Task<LookupHitDto> PatientHitAsync(Patient patient, bool exact, string? matchedNote, CancellationToken ct, long? visitId = null)
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

        var targetId = target.Id;
        // Where the patient is now (the chart header's current visit), unless the row is for that same visit.
        string? whereNow = null;
        if (_encounters is not null)
        {
            var (current, otherOpen) = CurrentVisit.For(await _encounters.ListAsync(e => e.PatientId == targetId, ct));
            if (current is not null && current.Id != visitId)
            {
                var where = CurrentVisit.Where(current);
                whereNow = $"Now {(where.Length > 0 ? where : $"visit {current.VisitNumber}")}{(otherOpen > 0 ? $" (+{otherOpen} more open)" : string.Empty)}";
            }
        }

        var detail = string.Join(" · ", new[]
        {
            $"DOB {target.DateOfBirth:yyyy-MM-dd}",
            target.Sex.ToString(),
            whereNow,
            mergedNote,
            matchedNote
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var bloodType = _bloodTypes is null
            ? null
            : PatientBloodBankFlags.CurrentBloodType(await _bloodTypes.ListAsync(t => t.PatientId == targetId, ct));
        var antibodies = _antibodies is null
            ? []
            : PatientBloodBankFlags.ActiveAntibodies(await _antibodies.ListAsync(a => a.PatientId == targetId, ct));
        var rows = _requirements is null
            ? []
            : await _requirements.ListAsync(r => r.PatientId == targetId, ct);
        var informational = _requirementDefinitions is null || rows.Count == 0
            ? null
            : PatientBloodBankFlags.InformationalIds(await _requirementDefinitions.ListAsync(ct));
        var now = _clock?.UtcNow ?? DateTime.UtcNow;
        var requirements = PatientBloodBankFlags.RequirementsInEffect(rows, now, informational);
        var notes = PatientBloodBankFlags.NotesInEffect(rows, now, informational);

        return new LookupHitDto(
            LookupHitKind.Patient,
            target.Id,
            exact,
            target.MedicalRecordNumber,
            PatientName(target),
            detail,
            // A visit opens its own orders, unless the visit's patient was merged away (the survivor opens plainly).
            visitId is long id && target.Id == patient.Id ? $"/patients/{target.Id}?tab=orders&visit={id}" : $"/patients/{target.Id}",
            bloodType,
            antibodies,
            requirements,
            notes);
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
