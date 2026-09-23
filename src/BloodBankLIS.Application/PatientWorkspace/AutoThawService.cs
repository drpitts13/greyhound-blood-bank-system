using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// Thaws an allocated frozen product in place: same unit and product code,
/// expiration becomes 24 hours after the chosen thaw time, and the seeded THAW charge is sent.
/// </summary>
public sealed class AutoThawService
{
    public const string ThawActionCode = "THAW";

    public static readonly TimeSpan ThawedShelfLife = TimeSpan.FromHours(24);

    private readonly IRepository<AutoThawProduct> _products;
    private readonly IRepository<AutoThawRequest> _requests;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<OrderAction> _orderActions;
    private readonly IRepository<ActionDefinition> _actionDefinitions;
    private readonly IRepository<ActionBilling> _actionBillings;
    private readonly BillingService _billing;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public AutoThawService(
        IRepository<AutoThawProduct> products,
        IRepository<AutoThawRequest> requests,
        IRepository<Allocation> allocations,
        IRepository<BloodUnit> units,
        IRepository<ProductType> productTypes,
        IRepository<OrderAction> orderActions,
        IRepository<ActionDefinition> actionDefinitions,
        IRepository<ActionBilling> actionBillings,
        BillingService billing,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _products = products;
        _requests = requests;
        _allocations = allocations;
        _units = units;
        _productTypes = productTypes;
        _orderActions = orderActions;
        _actionDefinitions = actionDefinitions;
        _actionBillings = actionBillings;
        _billing = billing;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<bool> IsEligibleAsync(long productTypeId, CancellationToken ct = default) =>
        await _products.AnyAsync(p => p.IsActive && p.ProductTypeId == productTypeId, ct);

    public async Task<IReadOnlySet<long>> PendingAllocationIdsAsync(
        IReadOnlyCollection<long> allocationIds,
        CancellationToken ct = default)
    {
        if (allocationIds.Count == 0)
        {
            return new HashSet<long>();
        }

        var rows = await _requests.ListAsync(
            r => allocationIds.Contains(r.AllocationId) && r.Status == AutoThawRequestStatus.Pending, ct);
        return rows.Select(r => r.AllocationId).ToHashSet();
    }

    public async Task<OperationResult<long>> DeclineAsync(long patientId, long allocationId, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var allocation = await _allocations.GetByIdAsync(allocationId, ct);
        if (allocation is null || allocation.PatientId != patientId)
        {
            return OperationResult<long>.Fail("Allocation not found.");
        }

        if (allocation.Status != AllocationStatus.Reserved)
        {
            return OperationResult<long>.Fail("Only a reserved allocation can defer auto-thaw.");
        }

        var unit = await _units.GetByIdAsync(allocation.BloodProductId, ct);
        if (unit is null)
        {
            return OperationResult<long>.Fail("Unit not found.");
        }

        if (!await IsEligibleAsync(unit.ProductTypeId, ct))
        {
            return OperationResult<long>.Fail("This product code is not in the auto-thaw list.");
        }

        if (await _requests.AnyAsync(
                r => r.BloodUnitId == unit.Id && r.Status == AutoThawRequestStatus.Completed, ct))
        {
            return OperationResult<long>.Fail("This unit has already been auto-thawed.");
        }

        var existing = await _requests.FirstOrDefaultAsync(r => r.AllocationId == allocationId, ct);
        if (existing is not null)
        {
            return OperationResult<long>.Ok(existing.Id);
        }

        var request = new AutoThawRequest
        {
            AllocationId = allocation.Id,
            BloodUnitId = unit.Id,
            Status = AutoThawRequestStatus.Pending
        };
        await _requests.AddAsync(request, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<long>.Ok(request.Id);
    }

    public async Task<OperationResult<AutoThawResultDto>> ApplyAsync(
        long patientId,
        long allocationId,
        DateTime thawUtc,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return OperationResult<AutoThawResultDto>.Fail(denied.Error ?? "Not authorized.");
        }

        var thaw = ToUtc(thawUtc);
        if (thaw > _clock.UtcNow.AddMinutes(1))
        {
            return OperationResult<AutoThawResultDto>.Fail("Thaw date/time cannot be in the future.");
        }

        var allocation = await _allocations.GetByIdAsync(allocationId, ct);
        if (allocation is null || allocation.PatientId != patientId)
        {
            return OperationResult<AutoThawResultDto>.Fail("Allocation not found.");
        }

        if (allocation.Status != AllocationStatus.Reserved)
        {
            return OperationResult<AutoThawResultDto>.Fail("Only a reserved allocation can be auto-thawed.");
        }

        if (allocation.OrderId is null)
        {
            return OperationResult<AutoThawResultDto>.Fail(
                "An open product order is required so the thaw billing message can be sent.");
        }

        var unit = await _units.GetByIdAsync(allocation.BloodProductId, ct);
        if (unit is null)
        {
            return OperationResult<AutoThawResultDto>.Fail("Unit not found.");
        }

        if (unit.Status is UnitStatus.Issued or UnitStatus.Transfused or UnitStatus.TransfusionStarted)
        {
            return OperationResult<AutoThawResultDto>.Fail("An issued unit cannot be auto-thawed.");
        }

        var product = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        if (product is null || !await IsEligibleAsync(product.Id, ct))
        {
            return OperationResult<AutoThawResultDto>.Fail("This product code is not in the auto-thaw list.");
        }

        if (await _requests.AnyAsync(
                r => r.BloodUnitId == unit.Id && r.Status == AutoThawRequestStatus.Completed, ct))
        {
            return OperationResult<AutoThawResultDto>.Fail("This unit has already been auto-thawed.");
        }

        var actionDefinition = await _actionDefinitions.FirstOrDefaultAsync(
            a => a.Code == ThawActionCode && a.IsActive, ct);
        if (actionDefinition is null)
        {
            return OperationResult<AutoThawResultDto>.Fail("The THAW action is not configured.");
        }

        var billingReady = await _actionBillings.AnyAsync(
            b => b.IsActive
                 && b.ActionDefinitionId == actionDefinition.Id
                 && b.Trigger == BillingTriggerType.ActionCompleted, ct);
        if (!billingReady)
        {
            return OperationResult<AutoThawResultDto>.Fail("Thaw billing is not configured.");
        }

        var previousExpires = unit.ExpiresUtc;
        var newExpires = DateTime.SpecifyKind(thaw.Add(ThawedShelfLife), DateTimeKind.Utc);
        var productTypeId = unit.ProductTypeId;
        var unitNumber = unit.UnitNumber;

        unit.ExpiresUtc = newExpires;
        _units.Update(unit);

        var orderAction = new OrderAction
        {
            OrderId = allocation.OrderId.Value,
            ActionDefinitionId = actionDefinition.Id,
            Status = OrderActionStatus.Completed,
            SourceBloodUnitId = unit.Id,
            ResultBloodUnitId = unit.Id,
            CompletedUtc = thaw,
            CompletedBy = _currentUser.UserName
        };
        await _orderActions.AddAsync(orderAction, ct);

        var offer = await _requests.FirstOrDefaultAsync(r => r.AllocationId == allocation.Id, ct);
        var offerIsNew = offer is null;
        offer ??= new AutoThawRequest
        {
            AllocationId = allocation.Id,
            BloodUnitId = unit.Id
        };

        offer.Status = AutoThawRequestStatus.Completed;
        offer.ThawUtc = thaw;
        offer.PreviousExpiresUtc = previousExpires;
        offer.NewExpiresUtc = newExpires;
        offer.CompletedUtc = _clock.UtcNow;
        offer.CompletedBy = _currentUser.UserName;
        if (offerIsNew)
        {
            await _requests.AddAsync(offer, ct);
        }
        else
        {
            _requests.Update(offer);
        }

        _audit.Record(
            AuditEventType.Modify,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { ExpiresUtc = previousExpires, ProductTypeId = productTypeId, UnitNumber = unitNumber },
            newValue: new { ExpiresUtc = newExpires, ProductTypeId = productTypeId, UnitNumber = unitNumber, ThawUtc = thaw },
            reason: "Auto-thaw");

        await _unitOfWork.SaveChangesAsync(ct);

        offer.OrderActionId = orderAction.Id;
        _requests.Update(offer);
        await _unitOfWork.SaveChangesAsync(ct);

        var billed = await _billing.CaptureForActionAsync(orderAction.Id, ct);
        if (!billed.Succeeded)
        {
            return OperationResult<AutoThawResultDto>.Fail(billed.Error ?? "Thaw billing message was not sent.");
        }

        if (billed.Value is null || billed.Value.Count == 0)
        {
            return OperationResult<AutoThawResultDto>.Fail("Thaw billing message was not created.");
        }

        return OperationResult<AutoThawResultDto>.Ok(new AutoThawResultDto(
            allocation.Id,
            unit.Id,
            product.ProductCode,
            previousExpires,
            newExpires));
    }

    private async Task<OperationResult<long>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.CompatibilityAllocate, ct);
        var auth = AutoThawAuthorizationRule.EvaluateApply(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<long>.Fail(auth.Message)
            : null;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
