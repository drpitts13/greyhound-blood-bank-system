using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Compliance;

public sealed record LookbackUnitDto(
    long BloodProductId,
    string? Din,
    string UnitNumber,
    UnitStatus Status,
    bool IsDerivedComponent);

public sealed record LookbackRecipientDto(
    long BloodProductId,
    long? PatientId,
    long? IssueId,
    long? TransfusionEventId,
    string? IssuedToLocation,
    DateTime? IssuedUtc);

public sealed record LookbackReportDto(
    string Din,
    IReadOnlyList<LookbackUnitDto> Units,
    IReadOnlyList<LookbackRecipientDto> Recipients,
    IReadOnlyList<LookbackNotificationDto> Notifications);

public sealed record LookbackNotificationDto(
    long Id,
    string Din,
    long BloodProductId,
    long? PatientId,
    long? IssueId,
    LookbackNotificationStatus Status,
    string? PhysicianOfRecord,
    DateTime? AttemptedUtc,
    string? AttemptedBy,
    string? Notes)
{
    public static LookbackNotificationDto From(LookbackNotification n) => new(
        n.Id, n.Din, n.BloodProductId, n.PatientId, n.IssueId, n.Status,
        n.PhysicianOfRecord, n.AttemptedUtc, n.AttemptedBy, n.Notes);
}

public sealed record RecordLookbackAttemptRequest(string? PhysicianOfRecord, string? Notes, LookbackNotificationStatus Status);

public sealed class LookbackService
{
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<UnitModificationUnit> _modificationUnits;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<LookbackNotification> _notifications;
    private readonly InventoryService _inventoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    public LookbackService(
        IInventoryRepository inventory,
        IRepository<BloodUnit> units,
        IRepository<UnitModificationUnit> modificationUnits,
        IRepository<Issue> issues,
        IRepository<TransfusionEvent> transfusions,
        IRepository<LookbackNotification> notifications,
        InventoryService inventoryService,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _inventory = inventory;
        _units = units;
        _modificationUnits = modificationUnits;
        _issues = issues;
        _transfusions = transfusions;
        _notifications = notifications;
        _inventoryService = inventoryService;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<OperationResult<LookbackReportDto>> FindByDinAsync(string din, CancellationToken ct = default)
    {
        var normalized = NormalizeDin(din);
        if (normalized.Length != 13)
        {
            return OperationResult<LookbackReportDto>.Fail("DIN must be 13 characters.");
        }

        var units = await CollectUnitsForDinAsync(normalized, ct);
        var recipients = new List<LookbackRecipientDto>();
        foreach (var unit in units)
        {
            var issues = await _issues.ListAsync(i => i.BloodProductId == unit.Id, ct);
            foreach (var issue in issues)
            {
                var tx = await _transfusions.FirstOrDefaultAsync(t => t.IssueId == issue.Id, ct);
                recipients.Add(new LookbackRecipientDto(
                    unit.Id, issue.PatientId, issue.Id, tx?.Id, issue.IssuedToLocation, issue.IssuedUtc));
            }
        }

        var notes = await _notifications.ListAsync(n => n.Din == normalized, ct);
        _audit.Record(AuditEventType.Lookback, nameof(BloodUnit), units.FirstOrDefault()?.Id, newValue: new { Din = normalized, UnitCount = units.Count }, reason: "Lookback search");
        await _unitOfWork.SaveChangesAsync(ct);

        return OperationResult<LookbackReportDto>.Ok(new LookbackReportDto(
            normalized,
            units.Select(u => new LookbackUnitDto(u.Id, u.Din, u.UnitNumber, u.Status, u.Din != normalized)).ToList(),
            recipients,
            notes.Select(LookbackNotificationDto.From).ToList()));
    }

    public async Task<OperationResult<LookbackReportDto>> RecallByDinAsync(string din, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<LookbackReportDto>.Fail("A reason is required to recall by DIN.");
        }

        var find = await FindByDinAsync(din, ct);
        if (!find.Succeeded || find.Value is null)
        {
            return find;
        }

        var normalized = find.Value.Din;
        var units = await CollectUnitsForDinAsync(normalized, ct);

        foreach (var unit in units)
        {
            if (unit.Status is UnitStatus.Transfused or UnitStatus.TransfusionStarted or UnitStatus.TransfusionStopped)
            {
                var issue = await _issues.FirstOrDefaultAsync(i => i.BloodProductId == unit.Id, ct);
                await _notifications.AddAsync(new LookbackNotification
                {
                    Din = normalized,
                    BloodProductId = unit.Id,
                    PatientId = issue?.PatientId,
                    IssueId = issue?.Id,
                    Status = LookbackNotificationStatus.Pending,
                    Reason = reason.Trim()
                }, ct);
                continue;
            }

            if (unit.Status != UnitStatus.Recalled && unit.Status != UnitStatus.Discarded)
            {
                await _inventoryService.RecallAsync(unit.Id, reason, ct);
            }
        }

        _audit.Record(AuditEventType.Lookback, nameof(BloodUnit), null, newValue: new { Din = normalized }, reason: reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return await FindByDinAsync(normalized, ct);
    }

    public async Task<OperationResult<LookbackNotification>> RecordAttemptAsync(
        long notificationId, RecordLookbackAttemptRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = await _notifications.GetByIdAsync(notificationId, ct);
        if (row is null)
        {
            return OperationResult<LookbackNotification>.Fail("Lookback notification not found.");
        }

        row.Status = request.Status;
        row.PhysicianOfRecord = request.PhysicianOfRecord;
        row.Notes = request.Notes;
        row.AttemptedUtc = _clock.UtcNow;
        row.AttemptedBy = _currentUser.UserName;
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<LookbackNotification>.Ok(row);
    }

    private async Task<List<BloodUnit>> CollectUnitsForDinAsync(string din13, CancellationToken ct)
    {
        var direct = (await _units.ListAsync(u => u.Din == din13, ct)).ToList();
        var found = new Dictionary<long, BloodUnit>();
        foreach (var unit in direct)
        {
            found[unit.Id] = unit;
        }

        foreach (var source in direct)
        {
            var sourceLinks = await _modificationUnits.ListAsync(
                l => l.BloodProductId == source.Id && l.Role == ModificationUnitRole.Source, ct);
            foreach (var link in sourceLinks)
            {
                var results = await _modificationUnits.ListAsync(
                    l => l.UnitModificationId == link.UnitModificationId && l.Role == ModificationUnitRole.Result, ct);
                foreach (var result in results)
                {
                    var child = await _inventory.GetUnitAsync(result.BloodProductId, ct);
                    if (child is not null)
                    {
                        found[child.Id] = child;
                    }
                }
            }
        }

        return found.Values.ToList();
    }

    private static string NormalizeDin(string din) =>
        new string((din ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
