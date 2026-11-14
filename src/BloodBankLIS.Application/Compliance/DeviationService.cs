using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record CreateDeviationRequest(
    string Title,
    string Description,
    DeviationSeverity Severity,
    string? ContextType = null,
    long? ContextId = null);

/// <summary>The investigation fields of a deviation update (end-user testing pass 73); null leaves them as they are.</summary>
public sealed record DeviationInvestigationUpdate(
    string? RootCause,
    DeviationFdaReportability FdaReportability,
    DateOnly? FdaReportedOn,
    string? FdaReportNumber);

public sealed record DeviationDto(
    long Id,
    string Title,
    string Description,
    DeviationSeverity Severity,
    DeviationStatus Status,
    string? ContextType,
    long? ContextId,
    string? CorrectiveAction,
    string ReportedBy,
    DateTime ReportedUtc,
    string? ClosedBy = null,
    DateTime? ClosedUtc = null)
{
    /// <summary>
    /// The related record as a person reads it (unit number, patient and MRN, accession, location and reading), so the
    /// list never shows an internal id (end-user testing pass 24: "Blood unit #1"). Null when it cannot be named.
    /// </summary>
    public string? ContextName { get; init; }

    /// <summary>Root cause and the FDA reportability determination (end-user testing pass 73).</summary>
    public string? RootCause { get; init; }
    public DeviationFdaReportability FdaReportability { get; init; }
    public DateOnly? FdaReportedOn { get; init; }
    public string? FdaReportNumber { get; init; }

    public static DeviationDto From(Deviation d) => new(
        d.Id, d.Title, d.Description, d.Severity, d.Status, d.ContextType, d.ContextId,
        d.CorrectiveAction, d.ReportedBy, d.ReportedUtc, d.ClosedBy, d.ClosedUtc)
    {
        RootCause = d.RootCause,
        FdaReportability = d.FdaReportability,
        FdaReportedOn = d.FdaReportedOn,
        FdaReportNumber = d.FdaReportNumber
    };
}

public sealed class DeviationService
{
    private readonly IRepository<Deviation> _deviations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<BloodUnit>? _units;
    private readonly IRepository<Patient>? _patients;
    private readonly IRepository<Specimen>? _specimens;
    private readonly IRepository<StorageTemperatureReading>? _readings;
    private readonly IRepository<InventoryLocation>? _locations;

    public DeviationService(
        IRepository<Deviation> deviations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        IRepository<BloodUnit>? units = null,
        IRepository<Patient>? patients = null,
        IRepository<Specimen>? specimens = null,
        IRepository<StorageTemperatureReading>? readings = null,
        IRepository<InventoryLocation>? locations = null)
    {
        _units = units;
        _patients = patients;
        _specimens = specimens;
        _readings = readings;
        _locations = locations;
        _deviations = deviations;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public Task<IReadOnlyList<Deviation>> ListAsync(CancellationToken ct = default) =>
        _deviations.ListAsync(ct);

    /// <summary>The list (all, or one record's) with each related record named.</summary>
    public async Task<IReadOnlyList<DeviationDto>> ListDtosAsync(string? contextType = null, long? contextId = null, CancellationToken ct = default)
    {
        var rows = !string.IsNullOrWhiteSpace(contextType) && contextId is long id
            ? await ListForRecordAsync(contextType.Trim(), id, ct)
            : await ListAsync(ct);
        var result = new List<DeviationDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(DeviationDto.From(row) with { ContextName = await NameAsync(row.ContextType, row.ContextId, ct) });
        }

        return result;
    }

    private async Task<string?> NameAsync(string? type, long? id, CancellationToken ct)
    {
        if (id is not long key || string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        switch (type)
        {
            case "BloodUnit" or "Unit" when _units is not null:
                return (await _units.GetByIdAsync(key, ct))?.UnitNumber;
            case "Patient" when _patients is not null:
                return await _patients.GetByIdAsync(key, ct) is { } p ? $"{p.LastName}, {p.FirstName} ({p.MedicalRecordNumber})" : null;
            case "Specimen" when _specimens is not null:
                return (await _specimens.GetByIdAsync(key, ct))?.AccessionNumber;
            case "StorageTemperatureReading" when _readings is not null:
                var reading = await _readings.GetByIdAsync(key, ct);
                if (reading is null)
                {
                    return null;
                }

                var location = _locations is null ? null : await _locations.GetByIdAsync(reading.InventoryLocationId, ct);
                return $"{location?.Code ?? "location " + reading.InventoryLocationId} {reading.ReadingC.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} °C";
            default:
                return null;
        }
    }

    /// <summary>
    /// Deviations linked to one record (a unit, a patient), for that record's page. "Unit" and "Hl7Message" are older
    /// spellings of "BloodUnit" and "Hl7" and are matched too.
    /// </summary>
    public async Task<IReadOnlyList<Deviation>> ListForRecordAsync(string contextType, long contextId, CancellationToken ct = default)
    {
        var types = DeviationRecordTypes.Aliases(contextType);
        return (await _deviations.ListAsync(d => d.ContextId == contextId && d.ContextType != null && types.Contains(d.ContextType), ct))
            .OrderBy(d => d.Status == DeviationStatus.Closed ? 1 : 0)
            .ThenByDescending(d => d.ReportedUtc)
            .ToList();
    }

    public async Task<OperationResult<Deviation>> CreateAsync(CreateDeviationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return OperationResult<Deviation>.Fail("Title and description are required.");
        }

        var row = new Deviation
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity,
            ContextType = request.ContextType,
            ContextId = request.ContextId,
            ReportedBy = _currentUser.UserName,
            ReportedUtc = _clock.UtcNow
        };
        await _deviations.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(
            AuditEventType.Deviation,
            nameof(Deviation),
            row.Id,
            newValue: new { row.Title, row.Severity, row.Status, row.ContextType, row.ContextId },
            reason: "Deviation created.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Deviation>.Ok(row);
    }

    public async Task<OperationResult<Deviation>> UpdateStatusAsync(
        long id, DeviationStatus status, string? correctiveAction, CancellationToken ct = default,
        DeviationInvestigationUpdate? investigation = null)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _deviations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<Deviation>.Fail("Deviation not found.");
        }

        var check = DeviationStatusRule.Evaluate(status, row.CorrectiveAction, correctiveAction);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<Deviation>.Fail(check.Message);
        }

        if (investigation is not null)
        {
            var fda = DeviationFdaReportRule.Evaluate(investigation.FdaReportability, investigation.FdaReportedOn, DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc), TimeZoneInfo.Local)));
            if (fda.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<Deviation>.Fail(fda.Message);
            }
        }

        var oldInvestigation = new { row.RootCause, row.FdaReportability, row.FdaReportedOn, row.FdaReportNumber };
        if (investigation is not null)
        {
            row.RootCause = string.IsNullOrWhiteSpace(investigation.RootCause) ? null : investigation.RootCause.Trim();
            row.FdaReportability = investigation.FdaReportability;
            row.FdaReportedOn = investigation.FdaReportability == DeviationFdaReportability.Reportable ? investigation.FdaReportedOn : null;
            row.FdaReportNumber = investigation.FdaReportability == DeviationFdaReportability.Reportable && !string.IsNullOrWhiteSpace(investigation.FdaReportNumber)
                ? investigation.FdaReportNumber.Trim()
                : null;
        }

        var oldStatus = row.Status;
        var oldAction = row.CorrectiveAction;
        var oldClosedBy = row.ClosedBy;
        var oldClosedUtc = row.ClosedUtc;
        row.Status = status;
        if (correctiveAction is not null)
        {
            row.CorrectiveAction = correctiveAction.Trim();
        }

        if (status == DeviationStatus.Closed && oldStatus != DeviationStatus.Closed)
        {
            row.ClosedBy = _currentUser.UserName;
            row.ClosedUtc = _clock.UtcNow;
        }
        else if (status != DeviationStatus.Closed)
        {
            // Reopened: the earlier close stays in the audit trail below.
            row.ClosedBy = null;
            row.ClosedUtc = null;
        }

        _audit.Record(
            AuditEventType.Deviation,
            nameof(Deviation),
            row.Id,
            oldValue: new { Status = oldStatus, CorrectiveAction = oldAction, ClosedBy = oldClosedBy, ClosedUtc = oldClosedUtc, Investigation = oldInvestigation },
            newValue: new { row.Status, row.CorrectiveAction, row.ClosedBy, row.ClosedUtc, Investigation = new { row.RootCause, row.FdaReportability, row.FdaReportedOn, row.FdaReportNumber } },
            reason: "Deviation status updated.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<Deviation>.Ok(row);
    }

    private async Task<OperationResult<Deviation>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.DeviationManage, ct);
        var auth = DeviationAuthorizationRule.EvaluateManage(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<Deviation>.Fail(auth.Message)
            : null;
    }
}
