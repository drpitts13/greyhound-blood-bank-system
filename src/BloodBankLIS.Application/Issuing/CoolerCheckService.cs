using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Issuing;

/// <summary>The cooler named on the issues, and the units scanned into it so far. <see cref="Finish"/> records the check.</summary>
public sealed record CoolerCheckRequest(string CoolerId, IReadOnlyList<string> Scans, bool Finish = false);

public enum CoolerCheckOutcome
{
    /// <summary>Issued to this cooler and scanned into it.</summary>
    Packed = 0,
    /// <summary>Issued to this cooler, not scanned.</summary>
    Missing = 1,
    /// <summary>Scanned into this cooler, but issued (and still in transit) under another cooler or none.</summary>
    OtherCooler = 2,
    /// <summary>Scanned into this cooler, but not issued: it must not leave the blood bank.</summary>
    NotIssued = 3
}

public sealed record CoolerCheckLineDto(
    long UnitId,
    string UnitNumber,
    string? ProductName,
    string BloodType,
    UnitStatus Status,
    CoolerCheckOutcome Outcome,
    long? IssueId,
    string? PatientName,
    string? MedicalRecordNumber,
    string? IssuedToLocation,
    string? RecordedCooler);

public sealed record CoolerCheckProblemDto(string Scan, string Message);

public sealed record CoolerCheckDto(
    string CoolerId,
    int Expected,
    int PatientCount,
    IReadOnlyList<CoolerCheckLineDto> Packed,
    IReadOnlyList<CoolerCheckLineDto> Missing,
    IReadOnlyList<CoolerCheckLineDto> OtherCooler,
    IReadOnlyList<CoolerCheckLineDto> NotIssued,
    IReadOnlyList<CoolerCheckProblemDto> Problems,
    bool Recorded)
{
    /// <summary>Every unit issued to the cooler is in it, and nothing else is.</summary>
    public bool Ready => Expected > 0 && Missing.Count == 0 && OtherCooler.Count == 0 && NotIssued.Count == 0 && Problems.Count == 0;
}

/// <summary>A cooler with issued units still in transit, for choosing it without typing its id.</summary>
public sealed record OpenCoolerDto(
    string CoolerId,
    int Units,
    IReadOnlyList<string> Patients,
    IReadOnlyList<string> Destinations,
    DateTime FirstIssuedUtc,
    DateTime? LastCheckedUtc,
    bool? LastCheckReady);

/// <summary>
/// Checks a cooler before it leaves the blood bank: the units scanned into it against the units issued under its id
/// and not yet received on the ward. A unit issued under another cooler (possibly another patient's), or not issued at
/// all, is caught here. Changes nothing; finishing records the check against each issue in the cooler.
/// </summary>
public sealed class CoolerCheckService
{
    private const string CheckReasonPrefix = "Cooler check: ";

    private readonly IRepository<Issue> _issues;
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<Patient> _patients;
    private readonly IPermissionEvaluator _permissions;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditQuery? _auditQuery;

    public CoolerCheckService(
        IRepository<Issue> issues,
        IInventoryRepository inventory,
        IRepository<BloodUnit> units,
        IRepository<ProductType> productTypes,
        IRepository<Patient> patients,
        IPermissionEvaluator permissions,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IUnitOfWork unitOfWork,
        IAuditQuery? auditQuery = null)
    {
        _issues = issues;
        _inventory = inventory;
        _units = units;
        _productTypes = productTypes;
        _patients = patients;
        _permissions = permissions;
        _currentUser = currentUser;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _auditQuery = auditQuery;
    }

    private static string Normalize(string? cooler) => (cooler ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<string?> DeniedAsync(CancellationToken ct)
    {
        return await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueCreate, ct)
            ? null
            : "Checking a cooler requires the issue.create permission.";
    }

    private async Task<List<Issue>> InTransitAsync(CancellationToken ct) =>
        (await _issues.ListAsync(i => i.Status == IssueStatus.Issued && i.WardReceivedUtc == null, ct)).ToList();

    /// <summary>Coolers with units in transit, oldest first, with when each was last checked.</summary>
    public async Task<OperationResult<IReadOnlyList<OpenCoolerDto>>> ListOpenAsync(CancellationToken ct = default)
    {
        if (await DeniedAsync(ct) is { } denied)
        {
            return OperationResult<IReadOnlyList<OpenCoolerDto>>.Fail(denied);
        }

        return OperationResult<IReadOnlyList<OpenCoolerDto>>.Ok(await SummarizeOpenAsync(ct));
    }

    /// <summary>
    /// The open-cooler list without its own permission check, for summaries (shift handoff) whose caller has already
    /// been authorized for its own action. Read-only.
    /// </summary>
    public async Task<IReadOnlyList<OpenCoolerDto>> SummarizeOpenAsync(CancellationToken ct = default)
    {
        var open = (await InTransitAsync(ct)).Where(i => !string.IsNullOrWhiteSpace(i.CoolerId)).ToList();
        var patients = await PatientsAsync(open, ct);
        var result = new List<OpenCoolerDto>();
        foreach (var group in open.GroupBy(i => Normalize(i.CoolerId)))
        {
            var (checkedUtc, ready) = await LastCheckAsync(group.ToList(), ct);
            result.Add(new OpenCoolerDto(
                group.First().CoolerId!.Trim(),
                group.Count(),
                group.Select(i => patients.TryGetValue(i.PatientId, out var p) ? PatientName(p) : $"patient {i.PatientId}").Distinct().ToList(),
                group.Select(i => i.IssuedToLocation?.Trim()).Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList()!,
                group.Min(i => i.IssuedUtc),
                checkedUtc,
                ready));
        }

        return result.OrderBy(c => c.FirstIssuedUtc).ToList();
    }

    public async Task<OperationResult<CoolerCheckDto>> CheckAsync(CoolerCheckRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await DeniedAsync(ct) is { } denied)
        {
            return OperationResult<CoolerCheckDto>.Fail(denied);
        }

        var cooler = Normalize(request.CoolerId);
        if (cooler.Length == 0)
        {
            return OperationResult<CoolerCheckDto>.Fail("Scan or choose the cooler.");
        }

        var inTransit = await InTransitAsync(ct);
        var expected = inTransit.Where(i => Normalize(i.CoolerId) == cooler).ToList();
        if (request.Finish && expected.Count == 0)
        {
            return OperationResult<CoolerCheckDto>.Fail($"No issued units in transit are recorded in cooler {request.CoolerId.Trim()}.");
        }

        var openByUnit = inTransit.GroupBy(i => i.BloodProductId).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.IssuedUtc).First());
        var expectedUnitIds = expected.Select(i => i.BloodProductId).ToHashSet();
        var units = (await _units.ListAsync(u => expectedUnitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id);
        var productNames = (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
        var patients = await PatientsAsync(inTransit, ct);

        CoolerCheckLineDto Line(BloodUnit u, CoolerCheckOutcome outcome, Issue? issue)
        {
            var patient = issue is not null && patients.TryGetValue(issue.PatientId, out var p) ? p : null;
            return new CoolerCheckLineDto(
                u.Id, u.UnitNumber, productNames.GetValueOrDefault(u.ProductTypeId), u.BloodType.ToString(), u.Status, outcome,
                issue?.Id, patient is null ? null : PatientName(patient), patient?.MedicalRecordNumber, issue?.IssuedToLocation,
                issue is null ? null : issue.CoolerId?.Trim());
        }

        var packedUnitIds = new HashSet<long>();
        var otherCooler = new Dictionary<long, CoolerCheckLineDto>();
        var notIssued = new Dictionary<long, CoolerCheckLineDto>();
        var problems = new List<CoolerCheckProblemDto>();
        foreach (var scan in request.Scans.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            var (key, matches, error) = await InventoryScanResolver.ResolveAsync(_inventory, scan, ct);
            if (error is not null)
            {
                problems.Add(new CoolerCheckProblemDto(scan.Trim(), error));
                continue;
            }

            var here = matches.Where(u => expectedUnitIds.Contains(u.Id)).ToList();
            if (matches.Count > 1 && here.Count != 1)
            {
                problems.Add(new CoolerCheckProblemDto(scan.Trim(),
                    $"{key} matches {matches.Count} units ({string.Join(", ", matches.Select(u => u.UnitNumber + (string.IsNullOrWhiteSpace(u.DivisionCode) ? "" : $" division {u.DivisionCode}")))}); scan its product code label next to pick the division."));
                continue;
            }

            var unit = here.Count == 1 ? here[0] : matches[0];
            if (expectedUnitIds.Contains(unit.Id))
            {
                packedUnitIds.Add(unit.Id);
            }
            else if (openByUnit.TryGetValue(unit.Id, out var elsewhere))
            {
                otherCooler[unit.Id] = Line(unit, CoolerCheckOutcome.OtherCooler, elsewhere);
            }
            else
            {
                notIssued[unit.Id] = Line(unit, CoolerCheckOutcome.NotIssued, null);
            }
        }

        var expectedLines = expected
            .Where(i => units.ContainsKey(i.BloodProductId))
            .Select(i => (Issue: i, Unit: units[i.BloodProductId]))
            .OrderBy(x => x.Unit.UnitNumber)
            .ToList();
        var dto = new CoolerCheckDto(
            expected.Count > 0 ? expected[0].CoolerId!.Trim() : request.CoolerId.Trim(),
            expected.Count,
            expected.Select(i => i.PatientId).Distinct().Count(),
            expectedLines.Where(x => packedUnitIds.Contains(x.Unit.Id)).Select(x => Line(x.Unit, CoolerCheckOutcome.Packed, x.Issue)).ToList(),
            expectedLines.Where(x => !packedUnitIds.Contains(x.Unit.Id)).Select(x => Line(x.Unit, CoolerCheckOutcome.Missing, x.Issue)).ToList(),
            otherCooler.Values.OrderBy(l => l.UnitNumber).ToList(),
            notIssued.Values.OrderBy(l => l.UnitNumber).ToList(),
            problems,
            request.Finish);

        if (request.Finish)
        {
            var reason = dto.Ready
                ? $"{CheckReasonPrefix}{dto.CoolerId}: all {dto.Expected} units packed."
                : $"{CheckReasonPrefix}{dto.CoolerId}: {dto.Packed.Count} of {dto.Expected} packed, {dto.Missing.Count} missing, {dto.OtherCooler.Count} issued to another cooler, {dto.NotIssued.Count} not issued, {dto.Problems.Count} unresolved scans.";
            foreach (var (issue, unit) in expectedLines)
            {
                _audit.Record(
                    AuditEventType.Issue,
                    nameof(Issue),
                    issue.Id,
                    newValue: new
                    {
                        Document = "Cooler check",
                        Cooler = dto.CoolerId,
                        IssueId = issue.Id,
                        BloodProductId = unit.Id,
                        unit.UnitNumber,
                        Packed = packedUnitIds.Contains(unit.Id),
                        dto.Ready,
                        Missing = dto.Missing.Select(l => l.UnitNumber).ToList(),
                        OtherCooler = dto.OtherCooler.Select(l => $"{l.UnitNumber} ({l.RecordedCooler ?? "no cooler"}, {l.PatientName})").ToList(),
                        NotIssued = dto.NotIssued.Select(l => $"{l.UnitNumber} ({l.Status})").ToList(),
                        Unresolved = dto.Problems.Select(p => p.Scan).ToList()
                    },
                    reason: reason);
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<CoolerCheckDto>.Ok(dto);
    }

    private async Task<Dictionary<long, Patient>> PatientsAsync(IReadOnlyCollection<Issue> issues, CancellationToken ct)
    {
        var ids = issues.Select(i => i.PatientId).Distinct().ToList();
        return (await _patients.ListAsync(p => ids.Contains(p.Id), ct)).ToDictionary(p => p.Id);
    }

    private static string PatientName(Patient p) => $"{p.LastName}, {p.FirstName}".Trim(' ', ',');

    // The newest finished check on any unit in the cooler; its outcome is whether the reason says all were packed.
    private async Task<(DateTime? CheckedUtc, bool? Ready)> LastCheckAsync(IReadOnlyList<Issue> issues, CancellationToken ct)
    {
        if (_auditQuery is null)
        {
            return (null, null);
        }

        AuditEvent? latest = null;
        foreach (var issue in issues)
        {
            var events = await _auditQuery.ListForEntityAsync(nameof(Issue), issue.Id, ct);
            var check = events.LastOrDefault(e => e.Reason is not null && e.Reason.StartsWith(CheckReasonPrefix, StringComparison.Ordinal));
            if (check is not null && (latest is null || check.OccurredUtc > latest.OccurredUtc))
            {
                latest = check;
            }
        }

        return latest is null ? (null, null) : (latest.OccurredUtc, latest.Reason!.Contains(": all ", StringComparison.Ordinal));
    }
}
