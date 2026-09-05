using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class InventoryLocationAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(InventoryLocation);

    private readonly IRepository<InventoryLocation> _locations;

    public InventoryLocationAdminService(
        IRepository<InventoryLocation> locations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _locations = locations;
    }

    public async Task<IReadOnlyList<InventoryLocationAdminDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _locations.ListAsync(ct)
            : await _locations.ListAsync(l => l.IsActive, ct);
        return list.OrderBy(l => l.Code).Select(InventoryLocationAdminDto.From).ToList();
    }

    public async Task<InventoryLocationAdminDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _locations.GetByIdAsync(id, ct);
        return entity is null ? null : InventoryLocationAdminDto.From(entity);
    }

    public async Task<EvaluationResult<InventoryLocationAdminDto>> CreateAsync(
        SaveInventoryLocationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new InventoryLocation { IsActive = true };
        Apply(entity, request, applyTypeDefaults: request.ApplyTypeDefaults || IsUnspecifiedStorage(request));

        var duplicate = await _locations.AnyAsync(l => l.Code == entity.Code, ct);
        var validation = InventoryLocationValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<InventoryLocationAdminDto>.Blocked(validation);
        }

        await _locations.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = InventoryLocationAdminDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<InventoryLocationAdminDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<InventoryLocationAdminDto>> UpdateAsync(
        long id,
        SaveInventoryLocationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _locations.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<InventoryLocationAdminDto>.Fail("Inventory location not found.");
        }

        var old = InventoryLocationAdminDto.From(entity);
        Apply(entity, request, applyTypeDefaults: request.ApplyTypeDefaults);

        var duplicate = await _locations.AnyAsync(l => l.Code == entity.Code && l.Id != id, ct);
        var validation = InventoryLocationValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<InventoryLocationAdminDto>.Blocked(validation);
        }

        _locations.Update(entity);
        var dto = InventoryLocationAdminDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<InventoryLocationAdminDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<InventoryLocationAdminDto>> SetActiveAsync(
        long id,
        bool active,
        CancellationToken ct = default)
    {
        var entity = await _locations.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<InventoryLocationAdminDto>.Fail("Inventory location not found.");
        }

        var old = InventoryLocationAdminDto.From(entity);
        entity.IsActive = active;
        _locations.Update(entity);
        var dto = InventoryLocationAdminDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<InventoryLocationAdminDto>.Ok(dto, new RuleEvaluation([]));
    }

    private static void Apply(InventoryLocation entity, SaveInventoryLocationRequest request, bool applyTypeDefaults)
    {
        entity.Code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        entity.Name = request.Name?.Trim() ?? string.Empty;
        entity.LocationType = request.LocationType;
        entity.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
        entity.RequiresSecondVerifier = request.RequiresSecondVerifier;
        entity.StorageTempMinC = request.StorageTempMinC;
        entity.StorageTempMaxC = request.StorageTempMaxC;
        entity.DefaultInTransitHours = request.DefaultInTransitHours;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (applyTypeDefaults)
        {
            InventoryLocationPolicyRule.ApplyTypeDefaults(entity);
        }

        if (request.AllowsIssue is { } allowsIssue) entity.AllowsIssue = allowsIssue;
        if (request.AllowsRemoteIssue is { } allowsRemote) entity.AllowsRemoteIssue = allowsRemote;
        if (request.AllowsElectronicIssue is { } allowsExm) entity.AllowsElectronicIssue = allowsExm;
        if (request.IsSatellite is { } satellite) entity.IsSatellite = satellite;
        if (request.AllowsRbc is { } rbc) entity.AllowsRbc = rbc;
        if (request.AllowsPlasma is { } plasma) entity.AllowsPlasma = plasma;
        if (request.AllowsPlatelets is { } plt) entity.AllowsPlatelets = plt;
        if (request.AllowsCryo is { } cryo) entity.AllowsCryo = cryo;
        if (request.AllowsWholeBlood is { } wb) entity.AllowsWholeBlood = wb;
    }

    private static bool IsUnspecifiedStorage(SaveInventoryLocationRequest request) =>
        request.AllowsRbc is null
        && request.AllowsPlasma is null
        && request.AllowsPlatelets is null
        && request.AllowsCryo is null
        && request.AllowsWholeBlood is null;
}
