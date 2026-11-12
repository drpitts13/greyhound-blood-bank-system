using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record EquipmentCheckStatusDto(
    long CheckId,
    string Name,
    int IntervalDays,
    string? Instructions,
    bool IsActive,
    string State,
    DateOnly? DueOn,
    bool NeedsAction,
    string StatusText,
    DateTime? LastPerformedUtc,
    string? LastPerformedBy,
    bool? LastPassed,
    string? LastNote);

public sealed record EquipmentDto(
    long Id,
    string Code,
    string Name,
    string Kind,
    string? Location,
    string? SerialNumber,
    bool IsActive,
    DateTime? OutOfServiceUtc,
    string? OutOfServiceReason,
    string? OutOfServiceBy,
    IReadOnlyList<EquipmentCheckStatusDto> Checks)
{
    public bool InService => IsActive && OutOfServiceUtc is null;

    /// <summary>Checks needing action on equipment in service (out-of-service equipment is not expected to be checked).</summary>
    public int ChecksNeedingAction => InService ? Checks.Count(c => c.IsActive && c.NeedsAction) : 0;
}

public sealed record EquipmentCheckRecordDto(
    long Id,
    long EquipmentId,
    string EquipmentCode,
    long CheckId,
    string CheckName,
    DateTime PerformedUtc,
    string PerformedBy,
    bool Passed,
    string? Note);

/// <summary>A check to do now, for the worklist and the home card.</summary>
public sealed record EquipmentCheckDueDto(
    long EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    string? Location,
    long CheckId,
    string CheckName,
    string State,
    DateOnly? DueOn,
    string StatusText);

public sealed record SaveEquipmentRequest(string Code, string Name, string Kind, string? Location, string? SerialNumber);

public sealed record SaveEquipmentCheckRequest(string Name, int IntervalDays, string? Instructions);

public sealed record RecordEquipmentCheckRequest(bool Passed, string? Note);

/// <summary>Correct equipment details (the code, its asset tag, does not change); a reason is required (Cycle 712).</summary>
public sealed record UpdateEquipmentRequest(string Name, string Kind, string? Location, string? SerialNumber, string? Reason);

/// <summary>Change a scheduled check (for example a new interval after an SOP revision); a reason is required (Cycle 712).</summary>
public sealed record UpdateEquipmentCheckRequest(string Name, int IntervalDays, string? Instructions, string? Reason);

/// <summary>
/// The equipment register (Cycle 708): equipment, the checks the facility schedules for each, and every time a check
/// was performed. Recording a check and taking equipment out of service or back are bench work; adding equipment and
/// its checks is configuration. Every change is audited. Nothing is deleted.
/// </summary>
public sealed class EquipmentService
{
    private readonly IRepository<Equipment> _equipment;
    private readonly IRepository<EquipmentCheck> _checks;
    private readonly IRepository<EquipmentCheckRecord> _records;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    public EquipmentService(
        IRepository<Equipment> equipment,
        IRepository<EquipmentCheck> checks,
        IRepository<EquipmentCheckRecord> records,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _equipment = equipment;
        _checks = checks;
        _records = records;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<EquipmentDto>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var equipment = includeInactive ? await _equipment.ListAsync(ct) : await _equipment.ListAsync(e => e.IsActive, ct);
        if (equipment.Count == 0)
        {
            return [];
        }

        var ids = equipment.Select(e => e.Id).ToList();
        var checks = await _checks.ListAsync(c => ids.Contains(c.EquipmentId), ct);
        var records = await _records.ListAsync(r => ids.Contains(r.EquipmentId), ct);
        var now = _clock.UtcNow;
        return equipment
            .OrderBy(e => e.IsActive ? 0 : 1)
            .ThenBy(e => e.Kind)
            .ThenBy(e => e.Code)
            .Select(e => ToDto(e, checks.Where(c => c.EquipmentId == e.Id), records, now))
            .ToList();
    }

    public async Task<EquipmentDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var equipment = await _equipment.GetByIdAsync(id, ct);
        if (equipment is null)
        {
            return null;
        }

        var checks = await _checks.ListAsync(c => c.EquipmentId == id, ct);
        var records = await _records.ListAsync(r => r.EquipmentId == id, ct);
        return ToDto(equipment, checks, records, _clock.UtcNow);
    }

    /// <summary>Active checks on active, in-service equipment that need doing now: failed, overdue, due today, never done.</summary>
    public async Task<IReadOnlyList<EquipmentCheckDueDto>> ListDueAsync(CancellationToken ct = default) =>
        (await ListAsync(includeInactive: false, ct))
            .Where(e => e.InService)
            .SelectMany(e => e.Checks.Where(c => c.IsActive && c.NeedsAction)
                .Select(c => new EquipmentCheckDueDto(e.Id, e.Code, e.Name, e.Location, c.CheckId, c.Name, c.State, c.DueOn, c.StatusText)))
            .OrderBy(d => Rank(d.State))
            .ThenBy(d => d.DueOn)
            .ThenBy(d => d.EquipmentCode)
            .ToList();

    /// <summary>The longest look ahead offered for checks coming up (Cycle 716).</summary>
    public const int MaxUpcomingDays = 366;

    /// <summary>
    /// Checks on equipment in service that are current now but fall due within the next <paramref name="days"/> days
    /// (Cycle 716), soonest first, so a calibration or vendor visit can be booked before it is due. Checks already due
    /// are on the due list instead.
    /// </summary>
    public async Task<IReadOnlyList<EquipmentCheckDueDto>> ListUpcomingAsync(int days, CancellationToken ct = default)
    {
        var horizon = DateOnly.FromDateTime(_clock.UtcNow).AddDays(Math.Clamp(days, 1, MaxUpcomingDays));
        return (await ListAsync(includeInactive: false, ct))
            .Where(e => e.InService)
            .SelectMany(e => e.Checks.Where(c => c.IsActive && !c.NeedsAction && c.DueOn is { } due && due <= horizon)
                .Select(c => new EquipmentCheckDueDto(e.Id, e.Code, e.Name, e.Location, c.CheckId, c.Name, c.State, c.DueOn, c.StatusText)))
            .OrderBy(d => d.DueOn)
            .ThenBy(d => d.EquipmentCode)
            .ToList();
    }

    public async Task<IReadOnlyList<EquipmentCheckRecordDto>> ListRecordsAsync(long equipmentId, int take = 50, CancellationToken ct = default)
    {
        var equipment = await _equipment.GetByIdAsync(equipmentId, ct);
        if (equipment is null)
        {
            return [];
        }

        var checks = (await _checks.ListAsync(c => c.EquipmentId == equipmentId, ct)).ToDictionary(c => c.Id, c => c.Name);
        return (await _records.ListAsync(r => r.EquipmentId == equipmentId, ct))
            .OrderByDescending(r => r.PerformedUtc)
            .ThenByDescending(r => r.Id)
            .Take(Math.Clamp(take, 1, 500))
            .Select(r => new EquipmentCheckRecordDto(r.Id, r.EquipmentId, equipment.Code, r.EquipmentCheckId,
                checks.GetValueOrDefault(r.EquipmentCheckId) ?? "", r.PerformedUtc, r.PerformedBy, r.Passed, r.Note))
            .ToList();
    }

    public async Task<EvaluationResult<EquipmentDto>> CreateAsync(SaveEquipmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = request.Code?.Trim().ToUpperInvariant() ?? "";
        var inUse = code.Length > 0 && await _equipment.AnyAsync(e => e.Code == code, ct);
        var stops = EquipmentCheckRule.EvaluateSave(code, request.Name, request.Kind, inUse);
        if (stops.Count > 0)
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation(stops));
        }

        var equipment = new Equipment
        {
            Code = code,
            Name = request.Name.Trim(),
            Kind = request.Kind.Trim(),
            Location = Trimmed(request.Location),
            SerialNumber = Trimmed(request.SerialNumber),
            IsActive = true
        };
        await _equipment.AddAsync(equipment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(AuditEventType.Create, nameof(Equipment), equipment.Id, null,
            new { equipment.Code, equipment.Name, equipment.Kind, equipment.Location, equipment.SerialNumber }, "Equipment registered.");
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentDto>.Ok((await GetAsync(equipment.Id, ct))!);
    }

    public async Task<EvaluationResult<EquipmentDto>> AddCheckAsync(long equipmentId, SaveEquipmentCheckRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var equipment = await _equipment.GetByIdAsync(equipmentId, ct);
        if (equipment is null)
        {
            return EvaluationResult<EquipmentDto>.Fail("Equipment was not found.");
        }

        var check = EquipmentCheckRule.EvaluateCheck(request.Name, request.IntervalDays);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation([check]));
        }

        var name = request.Name.Trim();
        if (await _checks.AnyAsync(c => c.EquipmentId == equipmentId && c.IsActive && c.Name == name, ct))
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation(
                [RuleResult.HardStop(EquipmentCheckRule.CheckCode, $"{equipment.Code} already has an active check named {name}.")]));
        }

        var row = new EquipmentCheck
        {
            EquipmentId = equipmentId,
            Name = name,
            IntervalDays = request.IntervalDays,
            Instructions = Trimmed(request.Instructions),
            IsActive = true
        };
        await _checks.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(AuditEventType.Create, nameof(EquipmentCheck), row.Id, null,
            new { equipment.Code, row.Name, row.IntervalDays, row.Instructions }, $"Check scheduled for {equipment.Code}.");
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentDto>.Ok((await GetAsync(equipmentId, ct))!);
    }

    public async Task<EvaluationResult<EquipmentDto>> UpdateAsync(long id, UpdateEquipmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var equipment = await _equipment.GetByIdAsync(id, ct);
        if (equipment is null)
        {
            return EvaluationResult<EquipmentDto>.Fail("Equipment was not found.");
        }

        var stops = EquipmentCheckRule.EvaluateSave(equipment.Code, request.Name, request.Kind, codeInUse: false).ToList();
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            stops.Add(RuleResult.HardStop(EquipmentCheckRule.SaveCode, "Say why the details change (for example, moved to Bench 2, name corrected)."));
        }

        if (stops.Count > 0)
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation(stops));
        }

        var before = new { equipment.Name, equipment.Kind, equipment.Location, equipment.SerialNumber };
        equipment.Name = request.Name.Trim();
        equipment.Kind = request.Kind.Trim();
        equipment.Location = Trimmed(request.Location);
        equipment.SerialNumber = Trimmed(request.SerialNumber);
        _equipment.Update(equipment);
        _audit.Record(AuditEventType.Update, nameof(Equipment), equipment.Id, before,
            new { equipment.Name, equipment.Kind, equipment.Location, equipment.SerialNumber }, request.Reason!.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentDto>.Ok((await GetAsync(id, ct))!);
    }

    /// <summary>
    /// Change an active check's name, interval or instructions, keeping its records: its due date follows from its last
    /// pass and the new interval.
    /// </summary>
    public async Task<EvaluationResult<EquipmentDto>> UpdateCheckAsync(long checkId, UpdateEquipmentCheckRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = await _checks.GetByIdAsync(checkId, ct);
        if (check is null || !check.IsActive)
        {
            return EvaluationResult<EquipmentDto>.Fail("Active check was not found.");
        }

        var stops = new List<RuleResult>();
        var rule = EquipmentCheckRule.EvaluateCheck(request.Name, request.IntervalDays);
        if (rule.Severity == RuleSeverity.HardStop)
        {
            stops.Add(rule);
        }

        var name = request.Name?.Trim() ?? "";
        if (name.Length > 0 && await _checks.AnyAsync(c => c.EquipmentId == check.EquipmentId && c.IsActive && c.Id != checkId && c.Name == name, ct))
        {
            stops.Add(RuleResult.HardStop(EquipmentCheckRule.CheckCode, $"This equipment already has an active check named {name}."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            stops.Add(RuleResult.HardStop(EquipmentCheckRule.CheckCode, "Say why the check changes (for example, SOP revised to monthly)."));
        }

        if (stops.Count > 0)
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation(stops));
        }

        var before = new { check.Name, check.IntervalDays, check.Instructions };
        check.Name = name;
        check.IntervalDays = request.IntervalDays;
        check.Instructions = Trimmed(request.Instructions);
        _checks.Update(check);
        _audit.Record(AuditEventType.Update, nameof(EquipmentCheck), check.Id, before,
            new { check.Name, check.IntervalDays, check.Instructions }, request.Reason!.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentDto>.Ok((await GetAsync(check.EquipmentId, ct))!);
    }

    /// <summary>Stop scheduling a check (its records are kept); a reason is required.</summary>
    public async Task<EvaluationResult<EquipmentDto>> RetireCheckAsync(long checkId, string? reason, CancellationToken ct = default)
    {
        var check = await _checks.GetByIdAsync(checkId, ct);
        if (check is null || !check.IsActive)
        {
            return EvaluationResult<EquipmentDto>.Fail("Active check was not found.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation(
                [RuleResult.HardStop(EquipmentCheckRule.CheckCode, "Say why the check is no longer scheduled.")]));
        }

        check.IsActive = false;
        _checks.Update(check);
        _audit.Record(AuditEventType.Deactivate, nameof(EquipmentCheck), check.Id, new { check.Name, IsActive = true },
            new { check.Name, IsActive = false }, reason.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentDto>.Ok((await GetAsync(check.EquipmentId, ct))!);
    }

    public async Task<EvaluationResult<EquipmentCheckRecordDto>> RecordAsync(long checkId, RecordEquipmentCheckRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = await _checks.GetByIdAsync(checkId, ct);
        var equipment = check is null ? null : await _equipment.GetByIdAsync(check.EquipmentId, ct);
        if (check is null || equipment is null)
        {
            return EvaluationResult<EquipmentCheckRecordDto>.Fail("Equipment check was not found.");
        }

        var rule = EquipmentCheckRule.EvaluateRecord(equipment.IsActive, check.IsActive, request.Passed, request.Note);
        if (rule.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<EquipmentCheckRecordDto>.Blocked(new RuleEvaluation([rule]));
        }

        var row = new EquipmentCheckRecord
        {
            EquipmentCheckId = check.Id,
            EquipmentId = equipment.Id,
            PerformedUtc = _clock.UtcNow,
            PerformedBy = _currentUser.UserName,
            Passed = request.Passed,
            Note = Trimmed(request.Note)
        };
        await _records.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(AuditEventType.Create, nameof(EquipmentCheckRecord), row.Id, null,
            new { equipment.Code, Check = check.Name, row.Passed, row.Note, row.PerformedBy },
            row.Passed ? $"{check.Name} passed on {equipment.Code}." : $"{check.Name} failed on {equipment.Code}.");
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentCheckRecordDto>.Ok(new EquipmentCheckRecordDto(row.Id, equipment.Id, equipment.Code, check.Id,
            check.Name, row.PerformedUtc, row.PerformedBy, row.Passed, row.Note));
    }

    /// <summary>Take equipment out of service, or return it, with a reason. Its checks stop being due while it is out.</summary>
    public async Task<EvaluationResult<EquipmentDto>> SetInServiceAsync(long id, bool inService, string? reason, CancellationToken ct = default)
    {
        var equipment = await _equipment.GetByIdAsync(id, ct);
        if (equipment is null || !equipment.IsActive)
        {
            return EvaluationResult<EquipmentDto>.Fail("Active equipment was not found.");
        }

        var rule = EquipmentCheckRule.EvaluateService(reason, takingOut: !inService,
            alreadyThatWay: inService == (equipment.OutOfServiceUtc is null));
        if (rule.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation([rule]));
        }

        var before = new { equipment.OutOfServiceUtc, equipment.OutOfServiceReason, equipment.OutOfServiceBy };
        if (inService)
        {
            equipment.OutOfServiceUtc = null;
            equipment.OutOfServiceReason = null;
            equipment.OutOfServiceBy = null;
        }
        else
        {
            equipment.OutOfServiceUtc = _clock.UtcNow;
            equipment.OutOfServiceReason = reason!.Trim();
            equipment.OutOfServiceBy = _currentUser.UserName;
        }

        _equipment.Update(equipment);
        _audit.Record(inService ? AuditEventType.Activate : AuditEventType.Deactivate, nameof(Equipment), equipment.Id, before,
            new { equipment.OutOfServiceUtc, equipment.OutOfServiceReason, equipment.OutOfServiceBy },
            (inService ? "Returned to service: " : "Out of service: ") + reason!.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentDto>.Ok((await GetAsync(id, ct))!);
    }

    /// <summary>Retire equipment from the register (kept with its records); a reason is required.</summary>
    public async Task<EvaluationResult<EquipmentDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var equipment = await _equipment.GetByIdAsync(id, ct);
        if (equipment is null || !equipment.IsActive)
        {
            return EvaluationResult<EquipmentDto>.Fail("Active equipment was not found.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation(
                [RuleResult.HardStop(EquipmentCheckRule.SaveCode, "Say why the equipment is retired (for example, replaced, sent back).")]));
        }

        equipment.IsActive = false;
        _equipment.Update(equipment);
        _audit.Record(AuditEventType.Deactivate, nameof(Equipment), equipment.Id, new { IsActive = true }, new { IsActive = false }, reason.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentDto>.Ok((await GetAsync(id, ct))!);
    }

    /// <summary>Bring retired equipment back into the register (in the service state it had), with a reason (Cycle 715).</summary>
    public async Task<EvaluationResult<EquipmentDto>> RestoreAsync(long id, string? reason, CancellationToken ct = default)
    {
        var equipment = await _equipment.GetByIdAsync(id, ct);
        if (equipment is null || equipment.IsActive)
        {
            return EvaluationResult<EquipmentDto>.Fail("Retired equipment was not found.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return EvaluationResult<EquipmentDto>.Blocked(new RuleEvaluation(
                [RuleResult.HardStop(EquipmentCheckRule.SaveCode, "Say why the equipment is back (for example, retired in error, returned from loan).")]));
        }

        equipment.IsActive = true;
        _equipment.Update(equipment);
        _audit.Record(AuditEventType.Activate, nameof(Equipment), equipment.Id, new { IsActive = false }, new { IsActive = true }, reason.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<EquipmentDto>.Ok((await GetAsync(id, ct))!);
    }

    private static EquipmentDto ToDto(Equipment e, IEnumerable<EquipmentCheck> checks, IReadOnlyList<EquipmentCheckRecord> records, DateTime now) =>
        new(e.Id, e.Code, e.Name, e.Kind, e.Location, e.SerialNumber, e.IsActive, e.OutOfServiceUtc, e.OutOfServiceReason, e.OutOfServiceBy,
            checks.OrderBy(c => c.IsActive ? 0 : 1).ThenBy(c => c.IntervalDays).ThenBy(c => c.Name)
                .Select(c =>
                {
                    var mine = records.Where(r => r.EquipmentCheckId == c.Id).OrderByDescending(r => r.PerformedUtc).ThenByDescending(r => r.Id).ToList();
                    var latest = mine.FirstOrDefault();
                    var lastPassed = mine.FirstOrDefault(r => r.Passed);
                    var status = EquipmentCheckRule.Evaluate(c.IntervalDays, lastPassed?.PerformedUtc, latest is { Passed: false }, now);
                    return new EquipmentCheckStatusDto(c.Id, c.Name, c.IntervalDays, c.Instructions, c.IsActive, status.State.ToString(),
                        status.DueOn, status.NeedsAction, EquipmentCheckRule.Describe(status), latest?.PerformedUtc, latest?.PerformedBy,
                        latest?.Passed, latest?.Note);
                })
                .ToList());

    private static int Rank(string state) => state switch
    {
        nameof(EquipmentCheckRule.State.Failed) => 0,
        nameof(EquipmentCheckRule.State.Overdue) => 1,
        nameof(EquipmentCheckRule.State.NeverDone) => 2,
        _ => 3
    };

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
