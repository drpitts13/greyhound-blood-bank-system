using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Create, activate, or deactivate antibody-identification panel lots.
/// Create requires cells and typed antigens. Does not identify antibodies.
/// </summary>
public sealed class AntibodyPanelLotAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(AntibodyPanelLot);

    private readonly IRepository<AntibodyPanelLot> _lots;
    private readonly IRepository<AntibodyPanelManufacturer> _manufacturers;
    private readonly IRepository<AntibodyPanelCell> _cells;
    private readonly IRepository<AntibodyPanelCellAntigen> _cellAntigens;
    private readonly IRepository<BloodAttributeDefinition> _attributes;
    private readonly IRepository<AntibodyIdentificationWorkup> _workups;
    private readonly IRepository<AntibodyIdentificationWorkupLot> _workupLots;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<AntibodyIdentificationFinding> _findings;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public AntibodyPanelLotAdminService(
        IRepository<AntibodyPanelLot> lots,
        IRepository<AntibodyPanelManufacturer> manufacturers,
        IRepository<AntibodyPanelCell> cells,
        IRepository<AntibodyPanelCellAntigen> cellAntigens,
        IRepository<BloodAttributeDefinition> attributes,
        IRepository<AntibodyIdentificationWorkup> workups,
        IRepository<AntibodyIdentificationWorkupLot> workupLots,
        IRepository<Patient> patients,
        IRepository<AntibodyIdentificationFinding> findings,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _lots = lots;
        _manufacturers = manufacturers;
        _cells = cells;
        _cellAntigens = cellAntigens;
        _attributes = attributes;
        _workups = workups;
        _workupLots = workupLots;
        _patients = patients;
        _findings = findings;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<AntibodyPanelLotListItemDto>> ListAsync(
        bool includeInactive,
        CancellationToken ct = default)
    {
        var lots = includeInactive
            ? await _lots.ListAsync(ct)
            : await _lots.ListAsync(l => l.IsActive, ct);
        var manufacturers = (await _manufacturers.ListAsync(ct)).ToDictionary(m => m.Id);
        var today = DateOnly.FromDateTime(Clock.UtcNow);
        var counts = await WorkupCountsByLotAsync(ct);
        return lots
            .Select(l => ToDto(
                l,
                manufacturers.GetValueOrDefault(l.ManufacturerId)?.Name ?? "",
                today,
                counts.Open.GetValueOrDefault(l.Id),
                counts.Completed.GetValueOrDefault(l.Id),
                counts.PostedHistory.GetValueOrDefault(l.Id)))
            .OrderByDescending(l => l.OpenWorkupCount)
            .ThenByDescending(l => l.PostedHistoryWorkupCount)
            .ThenByDescending(l => l.CompletedWorkupCount)
            .ThenBy(l => l.PanelName)
            .ThenBy(l => l.LotNumber)
            .ToList();
    }

    public async Task<AntibodyPanelLotDetailDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var lot = await _lots.GetByIdAsync(id, ct);
        if (lot is null)
        {
            return null;
        }

        var manufacturer = await _manufacturers.GetByIdAsync(lot.ManufacturerId, ct);
        var today = DateOnly.FromDateTime(Clock.UtcNow);
        var cells = (await _cells.ListAsync(c => c.LotId == id, ct))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.CellNumber)
            .ToList();
        var cellIds = cells.Select(c => c.Id).ToList();
        var antigens = cellIds.Count == 0
            ? []
            : await _cellAntigens.ListAsync(a => cellIds.Contains(a.CellId), ct);
        var attributes = (await _attributes.ListAsync(ct)).ToDictionary(a => a.Id);
        var antigensByCell = antigens
            .GroupBy(a => a.CellId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var cellDtos = cells.Select(cell =>
        {
            var typed = antigensByCell.GetValueOrDefault(cell.Id) ?? [];
            return new AntibodyPanelLotCellDetailDto(
                cell.Id,
                cell.CellNumber,
                cell.Role,
                cell.SortOrder,
                typed
                    .OrderBy(a => attributes.GetValueOrDefault(a.BloodAttributeDefinitionId)?.SortOrder ?? int.MaxValue)
                    .Select(a =>
                    {
                        attributes.TryGetValue(a.BloodAttributeDefinitionId, out var attr);
                        return new AntibodyPanelLotAntigenDetailDto(
                            a.BloodAttributeDefinitionId,
                            attr?.Code ?? "",
                            attr?.AntibodyName ?? "",
                            a.Expression);
                    })
                    .ToList());
        }).ToList();

        var attached = await ListWorkupsForLotAsync(id, RecallWorkup, ct);
        var openWorkups = attached.Where(w => AntibodyIdentificationHistoryPostRule.IsOpen(w.Status)).ToList();
        var completedWorkups = attached.Where(w => AntibodyIdentificationHistoryPostRule.IsCompleted(w.Status)).ToList();
        return new AntibodyPanelLotDetailDto(
            ToDto(lot, manufacturer?.Name ?? "", today, openWorkups.Count, completedWorkups.Count,
                completedWorkups.Count(w => w.PostedHistoryCount > 0)),
            cellDtos,
            openWorkups,
            completedWorkups);
    }

    public async Task<IReadOnlyList<AntibodyPanelManufacturerListItemDto>> ListManufacturersAsync(
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        var manufacturers = includeInactive
            ? await _manufacturers.ListAsync(ct)
            : await _manufacturers.ListAsync(m => m.IsActive, ct);
        var counts = await WorkupCountsByManufacturerAsync(ct);
        return manufacturers
            .Select(m => ToManufacturerDto(
                m,
                counts.Open.GetValueOrDefault(m.Id),
                counts.Completed.GetValueOrDefault(m.Id),
                counts.PostedHistory.GetValueOrDefault(m.Id)))
            .OrderByDescending(m => m.OpenWorkupCount)
            .ThenByDescending(m => m.PostedHistoryWorkupCount)
            .ThenByDescending(m => m.CompletedWorkupCount)
            .ThenBy(m => m.Name)
            .ToList();
    }

    public async Task<AntibodyPanelManufacturerDetailDto?> GetManufacturerAsync(
        long id,
        CancellationToken ct = default)
    {
        var manufacturer = await _manufacturers.GetByIdAsync(id, ct);
        if (manufacturer is null)
        {
            return null;
        }

        var attached = await ListWorkupsForManufacturerAsync(id, RecallWorkup, ct);
        var openWorkups = attached.Where(w => AntibodyIdentificationHistoryPostRule.IsOpen(w.Status)).ToList();
        var completedWorkups = attached.Where(w => AntibodyIdentificationHistoryPostRule.IsCompleted(w.Status)).ToList();
        return new AntibodyPanelManufacturerDetailDto(
            ToManufacturerDto(manufacturer, openWorkups.Count, completedWorkups.Count,
                completedWorkups.Count(w => w.PostedHistoryCount > 0)),
            openWorkups,
            completedWorkups);
    }

    public async Task<EvaluationResult<AntibodyPanelManufacturerListItemDto>> CreateManufacturerAsync(
        CreateAntibodyPanelManufacturerRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedEvalAsync<AntibodyPanelManufacturerListItemDto>(
            PermissionCodes.AdminConfigEdit, AntibodyPanelManufacturerAdminRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var code = request.Code?.Trim() ?? "";
        var name = request.Name?.Trim() ?? "";
        var duplicate = await _manufacturers.AnyAsync(m => m.Code == code, ct);
        var stops = AntibodyPanelManufacturerAdminRule.EvaluateCreateDraft(code, name, duplicate);
        if (stops.Count > 0)
        {
            return EvaluationResult<AntibodyPanelManufacturerListItemDto>.Blocked(new RuleEvaluation(stops));
        }

        var entity = new AntibodyPanelManufacturer
        {
            Code = code,
            Name = name,
            IsActive = true,
            IsDraft = false,
            Version = 1,
            EffectiveUtc = Clock.UtcNow
        };
        await _manufacturers.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = ToManufacturerDto(entity);
        RecordChange(nameof(AntibodyPanelManufacturer), entity.Id, entity.Version, ConfigChangeAction.Create,
            AuditEventType.TestChange, oldValue: null, newValue: dto,
            reason: "Create antibody panel manufacturer. Does not identify antibodies.");
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<AntibodyPanelManufacturerListItemDto>.Ok(dto);
    }

    public async Task<EvaluationResult<AntibodyPanelManufacturerListItemDto>> ActivateManufacturerAsync(
        long id,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync<AntibodyPanelManufacturerListItemDto>(
            PermissionCodes.AdminConfigActivate, AntibodyPanelManufacturerAdminRule.EvaluateActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _manufacturers.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<AntibodyPanelManufacturerListItemDto>.Fail("Antibody panel manufacturer was not found.");
        }

        var before = ToManufacturerDto(entity);
        entity.IsActive = true;
        entity.IsDraft = false;
        entity.RetiredUtc = null;
        entity.EffectiveUtc ??= Clock.UtcNow;
        _manufacturers.Update(entity);

        RecordChange(nameof(AntibodyPanelManufacturer), entity.Id, entity.Version, ConfigChangeAction.Activate,
            AuditEventType.Activate, oldValue: before, newValue: ToManufacturerDto(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<AntibodyPanelManufacturerListItemDto>.Ok(ToManufacturerDto(entity));
    }

    public async Task<EvaluationResult<AntibodyPanelManufacturerListItemDto>> DeactivateManufacturerAsync(
        long id,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync<AntibodyPanelManufacturerListItemDto>(
            PermissionCodes.AdminConfigActivate, AntibodyPanelManufacturerAdminRule.EvaluateDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var reasonCheck = AntibodyPanelManufacturerAdminRule.EvaluateDeactivateReason(reason);
        if (reasonCheck.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyPanelManufacturerListItemDto>.Blocked(new RuleEvaluation([reasonCheck]));
        }

        var entity = await _manufacturers.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<AntibodyPanelManufacturerListItemDto>.Fail("Antibody panel manufacturer was not found.");
        }

        var before = ToManufacturerDto(entity);
        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason?.Trim();
        _manufacturers.Update(entity);

        RecordChange(nameof(AntibodyPanelManufacturer), entity.Id, entity.Version, ConfigChangeAction.Deactivate,
            AuditEventType.Deactivate, oldValue: before, newValue: ToManufacturerDto(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<AntibodyPanelManufacturerListItemDto>.Ok(ToManufacturerDto(entity));
    }

    public async Task<EvaluationResult<AntibodyPanelLotListItemDto>> CreateAsync(
        CreateAntibodyPanelLotRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedEvalAsync<AntibodyPanelLotListItemDto>(
            PermissionCodes.AdminConfigEdit, AntibodyPanelLotAdminRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var manufacturer = await _manufacturers.GetByIdAsync(request.ManufacturerId, ct);
        var today = DateOnly.FromDateTime(Clock.UtcNow);
        var lotNumber = request.LotNumber?.Trim() ?? "";
        var duplicate = manufacturer is not null
            && await _lots.AnyAsync(
                l => l.ManufacturerId == request.ManufacturerId && l.LotNumber == lotNumber, ct);
        var activeAttributeIds = (await _attributes.ListAsync(a => a.IsActive, ct))
            .Select(a => a.Id)
            .ToHashSet();
        var cells = (request.Cells ?? [])
            .Select(c => new AntibodyPanelLotCreateCellDraft(
                c.CellNumber,
                c.Role,
                (c.Antigens ?? [])
                    .Select(a => new AntibodyPanelLotCreateAntigenDraft(
                        a.BloodAttributeDefinitionId, a.Expression))
                    .ToList()))
            .ToList();
        var draft = new AntibodyPanelLotCreateDraft(
            manufacturer is { IsActive: true },
            lotNumber,
            request.PanelName,
            request.ExpiresOn,
            today,
            duplicate,
            request.IsSelectedCellLot,
            cells,
            activeAttributeIds);
        var stops = AntibodyPanelLotAdminRule.EvaluateCreateDraft(draft);
        if (stops.Count > 0)
        {
            return EvaluationResult<AntibodyPanelLotListItemDto>.Blocked(new RuleEvaluation(stops));
        }

        var lot = new AntibodyPanelLot
        {
            ManufacturerId = request.ManufacturerId,
            LotNumber = lotNumber,
            PanelName = request.PanelName.Trim(),
            ExpiresOn = request.ExpiresOn,
            IsSelectedCellLot = request.IsSelectedCellLot,
            IsActive = true
        };
        await _lots.AddAsync(lot, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        foreach (var incoming in request.Cells)
        {
            var cell = new AntibodyPanelCell
            {
                LotId = lot.Id,
                CellNumber = incoming.CellNumber.Trim(),
                Role = incoming.Role,
                SortOrder = incoming.SortOrder
            };
            await _cells.AddAsync(cell, ct);
            await UnitOfWork.SaveChangesAsync(ct);

            if (incoming.Role == PanelCellRole.Autocontrol)
            {
                continue;
            }

            foreach (var antigen in incoming.Antigens ?? [])
            {
                if (antigen.Expression == AntigenExpression.NotTested)
                {
                    continue;
                }

                await _cellAntigens.AddAsync(new AntibodyPanelCellAntigen
                {
                    CellId = cell.Id,
                    BloodAttributeDefinitionId = antigen.BloodAttributeDefinitionId,
                    Expression = antigen.Expression
                }, ct);
            }
        }

        await UnitOfWork.SaveChangesAsync(ct);
        var dto = ToDto(lot, manufacturer?.Name ?? "", today);
        RecordChange(EntityType, lot.Id, 1, ConfigChangeAction.Create, AuditEventType.TestChange,
            oldValue: null, newValue: dto, reason: "Create antibody panel lot. Does not identify antibodies.");
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<AntibodyPanelLotListItemDto>.Ok(dto);
    }

    public async Task<EvaluationResult<AntibodyPanelLotListItemDto>> ActivateAsync(
        long id,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync<AntibodyPanelLotListItemDto>(
            PermissionCodes.AdminConfigActivate, AntibodyPanelLotAdminRule.EvaluateActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var lot = await _lots.GetByIdAsync(id, ct);
        if (lot is null)
        {
            return EvaluationResult<AntibodyPanelLotListItemDto>.Fail("Antibody panel lot was not found.");
        }

        var before = await ToDtoAsync(lot, ct);
        lot.IsActive = true;
        _lots.Update(lot);

        RecordChange(EntityType, lot.Id, 1, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: before, newValue: await ToDtoAsync(lot, ct), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyPanelLotListItemDto>.Ok(await ToDtoAsync(lot, ct));
    }

    public async Task<EvaluationResult<AntibodyPanelLotListItemDto>> DeactivateAsync(
        long id,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync<AntibodyPanelLotListItemDto>(
            PermissionCodes.AdminConfigActivate, AntibodyPanelLotAdminRule.EvaluateDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var reasonCheck = AntibodyPanelLotAdminRule.EvaluateDeactivateReason(reason);
        if (reasonCheck.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<AntibodyPanelLotListItemDto>.Blocked(new RuleEvaluation([reasonCheck]));
        }

        var lot = await _lots.GetByIdAsync(id, ct);
        if (lot is null)
        {
            return EvaluationResult<AntibodyPanelLotListItemDto>.Fail("Antibody panel lot was not found.");
        }

        var before = await ToDtoAsync(lot, ct);
        lot.IsActive = false;
        _lots.Update(lot);

        RecordChange(EntityType, lot.Id, 1, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: before, newValue: await ToDtoAsync(lot, ct), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<AntibodyPanelLotListItemDto>.Ok(await ToDtoAsync(lot, ct));
    }

    private async Task<AntibodyPanelLotListItemDto> ToDtoAsync(AntibodyPanelLot lot, CancellationToken ct)
    {
        var manufacturer = await _manufacturers.GetByIdAsync(lot.ManufacturerId, ct);
        var counts = await WorkupCountsByLotAsync(ct);
        return ToDto(lot, manufacturer?.Name ?? "", DateOnly.FromDateTime(Clock.UtcNow),
            counts.Open.GetValueOrDefault(lot.Id),
            counts.Completed.GetValueOrDefault(lot.Id),
            counts.PostedHistory.GetValueOrDefault(lot.Id));
    }

    private static AntibodyPanelLotListItemDto ToDto(
        AntibodyPanelLot lot,
        string manufacturer,
        DateOnly today,
        int openWorkupCount = 0,
        int completedWorkupCount = 0,
        int postedHistoryWorkupCount = 0) =>
        new(lot.Id, lot.ManufacturerId, manufacturer, lot.LotNumber, lot.ExpiresOn, lot.PanelName,
            lot.IsSelectedCellLot, lot.IsActive, lot.ExpiresOn < today, openWorkupCount, completedWorkupCount,
            postedHistoryWorkupCount);

    private static bool RecallWorkup(AntibodyWorkupStatus status) =>
        AntibodyIdentificationHistoryPostRule.IsOpen(status)
        || AntibodyIdentificationHistoryPostRule.IsCompleted(status);

    private async Task<WorkupCounts> WorkupCountsByLotAsync(CancellationToken ct)
    {
        var open = new Dictionary<long, int>();
        var completed = new Dictionary<long, int>();
        var posted = new Dictionary<long, int>();
        var postedIds = await PostedHistoryWorkupIdsAsync(ct);
        foreach (var workup in await ListWorkupEntitiesAsync(RecallWorkup, ct))
        {
            var dest = AntibodyIdentificationHistoryPostRule.IsOpen(workup.Status) ? open : completed;
            var lotIds = await AttachedLotIdsAsync(workup, ct);
            foreach (var lotId in lotIds)
            {
                dest[lotId] = dest.GetValueOrDefault(lotId) + 1;
            }

            if (AntibodyIdentificationHistoryPostRule.IsCompleted(workup.Status) && postedIds.Contains(workup.Id))
            {
                foreach (var lotId in lotIds)
                {
                    posted[lotId] = posted.GetValueOrDefault(lotId) + 1;
                }
            }
        }

        return new WorkupCounts(open, completed, posted);
    }

    private async Task<IReadOnlyList<AntibodyPanelLotOpenWorkupDto>> ListWorkupsForLotAsync(
        long lotId,
        Func<AntibodyWorkupStatus, bool> match,
        CancellationToken ct)
    {
        var candidates = await ListWorkupEntitiesAsync(match, ct);
        var matches = new List<AntibodyIdentificationWorkup>();
        foreach (var workup in candidates)
        {
            if ((await AttachedLotIdsAsync(workup, ct)).Contains(lotId))
            {
                matches.Add(workup);
            }
        }

        return await MapOpenWorkupsAsync(matches, ct);
    }

    private async Task<IReadOnlyList<AntibodyPanelLotOpenWorkupDto>> ListWorkupsForManufacturerAsync(
        long manufacturerId,
        Func<AntibodyWorkupStatus, bool> match,
        CancellationToken ct)
    {
        var manufacturerLots = (await _lots.ListAsync(l => l.ManufacturerId == manufacturerId, ct))
            .ToDictionary(l => l.Id);
        if (manufacturerLots.Count == 0)
        {
            return [];
        }

        var candidates = await ListWorkupEntitiesAsync(match, ct);
        var matches = new List<(AntibodyIdentificationWorkup Workup, string LotNumber)>();
        foreach (var workup in candidates)
        {
            var attached = await AttachedLotIdsAsync(workup, ct);
            var usedLots = attached
                .Select(id => manufacturerLots.GetValueOrDefault(id))
                .OfType<AntibodyPanelLot>()
                .Select(l => l.LotNumber)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (usedLots.Count > 0)
            {
                matches.Add((workup, string.Join(", ", usedLots)));
            }
        }

        return await MapOpenWorkupsAsync(matches.Select(m => m.Workup).ToList(), ct,
            w => matches.First(m => m.Workup.Id == w.Id).LotNumber);
    }

    private async Task<IReadOnlyList<AntibodyPanelLotOpenWorkupDto>> MapOpenWorkupsAsync(
        IReadOnlyList<AntibodyIdentificationWorkup> matches,
        CancellationToken ct,
        Func<AntibodyIdentificationWorkup, string?>? lotNumber = null)
    {
        var patientIds = matches.Select(w => w.PatientId).Distinct().ToList();
        var patients = patientIds.Count == 0
            ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var workupIds = matches.Select(w => w.Id).ToList();
        var postedCounts = workupIds.Count == 0
            ? new Dictionary<long, int>()
            : (await _findings.ListAsync(f => workupIds.Contains(f.WorkupId) && f.PostedToHistory, ct))
                .GroupBy(f => f.WorkupId)
                .ToDictionary(g => g.Key, g => g.Count());

        return matches
            .Select(w =>
            {
                patients.TryGetValue(w.PatientId, out var patient);
                var name = patient is null ? null : $"{patient.LastName}, {patient.FirstName}";
                return new AntibodyPanelLotOpenWorkupDto(
                    w.Id,
                    w.PatientId,
                    patient?.MedicalRecordNumber,
                    name,
                    w.Status,
                    AntibodyIdentificationWorklistRule.NextAction(w.Status),
                    lotNumber?.Invoke(w),
                    postedCounts.GetValueOrDefault(w.Id));
            })
            .OrderByDescending(w => w.PostedHistoryCount)
            .ThenByDescending(w => w.Status == AntibodyWorkupStatus.Completed)
            .ThenByDescending(w => matches.First(m => m.Id == w.WorkupId).CreatedUtc)
            .ToList();
    }

    private async Task<IReadOnlyList<AntibodyIdentificationWorkup>> ListWorkupEntitiesAsync(
        Func<AntibodyWorkupStatus, bool> match,
        CancellationToken ct)
    {
        var workups = await _workups.ListAsync(ct);
        return workups.Where(w => match(w.Status)).ToList();
    }

    private async Task<IReadOnlyList<long>> AttachedLotIdsAsync(
        AntibodyIdentificationWorkup workup,
        CancellationToken ct)
    {
        var links = await _workupLots.ListAsync(l => l.WorkupId == workup.Id, ct);
        var ids = links.Select(l => l.LotId).Distinct().ToList();
        if (ids.Count == 0)
        {
            ids.Add(workup.PrimaryLotId);
        }

        return ids;
    }

    private static AntibodyPanelManufacturerListItemDto ToManufacturerDto(
        AntibodyPanelManufacturer manufacturer,
        int openWorkupCount = 0,
        int completedWorkupCount = 0,
        int postedHistoryWorkupCount = 0) =>
        new(manufacturer.Id, manufacturer.Code, manufacturer.Name, manufacturer.IsActive,
            openWorkupCount, completedWorkupCount, postedHistoryWorkupCount);

    private async Task<HashSet<long>> PostedHistoryWorkupIdsAsync(CancellationToken ct)
    {
        var posted = await _findings.ListAsync(f => f.PostedToHistory, ct);
        return posted.Select(f => f.WorkupId).ToHashSet();
    }

    private async Task<WorkupCounts> WorkupCountsByManufacturerAsync(CancellationToken ct)
    {
        var lots = (await _lots.ListAsync(ct)).ToDictionary(l => l.Id);
        var open = new Dictionary<long, int>();
        var completed = new Dictionary<long, int>();
        var posted = new Dictionary<long, int>();
        var postedIds = await PostedHistoryWorkupIdsAsync(ct);
        foreach (var workup in await ListWorkupEntitiesAsync(RecallWorkup, ct))
        {
            var dest = AntibodyIdentificationHistoryPostRule.IsOpen(workup.Status) ? open : completed;
            var manufacturerIds = new HashSet<long>();
            foreach (var lotId in await AttachedLotIdsAsync(workup, ct))
            {
                if (lots.TryGetValue(lotId, out var lot))
                {
                    manufacturerIds.Add(lot.ManufacturerId);
                }
            }

            foreach (var manufacturerId in manufacturerIds)
            {
                dest[manufacturerId] = dest.GetValueOrDefault(manufacturerId) + 1;
            }

            if (AntibodyIdentificationHistoryPostRule.IsCompleted(workup.Status) && postedIds.Contains(workup.Id))
            {
                foreach (var manufacturerId in manufacturerIds)
                {
                    posted[manufacturerId] = posted.GetValueOrDefault(manufacturerId) + 1;
                }
            }
        }

        return new WorkupCounts(open, completed, posted);
    }

    private sealed record WorkupCounts(
        Dictionary<long, int> Open,
        Dictionary<long, int> Completed,
        Dictionary<long, int> PostedHistory);

    private async Task<EvaluationResult<T>?> RejectUnauthorizedEvalAsync<T>(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<T>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
