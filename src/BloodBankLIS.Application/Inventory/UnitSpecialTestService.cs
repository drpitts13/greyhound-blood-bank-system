using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

public sealed record RecordUnitSpecialTestRequest(string TestCode, string Result, string? Reason = null);

/// <summary>
/// One recording of a unit test. <see cref="IsCurrent"/> marks the latest per test code, which is the unit's result.
/// </summary>
public sealed record UnitSpecialTestDto(
    long Id,
    long BloodUnitId,
    string TestCode,
    string? Result,
    DateTime RecordedUtc,
    string RecordedBy,
    bool IsCurrent);

/// <summary>
/// Records test results on a unit after it arrived (CMV, HbS), so a unit typing in a patient's special requirements
/// can be met by the unit's own test. Earlier recordings stay as history; a correction needs a reason.
/// </summary>
public sealed class UnitSpecialTestService
{
    private readonly IRepository<BloodComponentSpecialTest> _tests;
    private readonly IInventoryRepository _units;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public UnitSpecialTestService(
        IRepository<BloodComponentSpecialTest> tests,
        IInventoryRepository units,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _tests = tests;
        _units = units;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    /// <summary>The latest recording per test code: the unit's result.</summary>
    public static IEnumerable<BloodComponentSpecialTest> Current(IEnumerable<BloodComponentSpecialTest> rows) =>
        rows.GroupBy(t => UnitSpecialTestRule.NormalizeCode(t.TestCode))
            .Select(g => g.OrderByDescending(t => t.CreatedUtc).ThenByDescending(t => t.Id).First());

    public async Task<IReadOnlyList<UnitSpecialTestDto>> ListAsync(long unitId, CancellationToken ct = default)
    {
        var rows = await _tests.ListAsync(t => t.BloodProductId == unitId, ct);
        var current = Current(rows).Select(t => t.Id).ToHashSet();
        return rows
            .OrderBy(t => UnitSpecialTestRule.NormalizeCode(t.TestCode))
            .ThenByDescending(t => t.CreatedUtc)
            .ThenByDescending(t => t.Id)
            .Select(t => new UnitSpecialTestDto(
                t.Id, t.BloodProductId, t.TestCode, t.Result, t.CreatedUtc, t.CreatedBy, current.Contains(t.Id)))
            .ToList();
    }

    public async Task<OperationResult<UnitSpecialTestDto>> RecordAsync(
        long unitId, RecordUnitSpecialTestRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_permissions is not null)
        {
            var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.InventoryReceive, ct);
            var auth = InventoryAuthorizationRule.EvaluateSaveAttribute(allowed);
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<UnitSpecialTestDto>.Fail(auth.Message);
            }
        }

        if (await _units.GetUnitAsync(unitId, ct) is null)
        {
            return OperationResult<UnitSpecialTestDto>.Fail("Unit not found.");
        }

        var code = UnitSpecialTestRule.NormalizeCode(request.TestCode);
        var existing = await _tests.ListAsync(t => t.BloodProductId == unitId, ct);
        var previous = Current(existing).FirstOrDefault(t => UnitSpecialTestRule.NormalizeCode(t.TestCode) == code);
        var check = UnitSpecialTestRule.Evaluate(code, request.Result, previous?.Result, request.Reason);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<UnitSpecialTestDto>.Fail(check.Message);
        }

        var row = new BloodComponentSpecialTest
        {
            BloodProductId = unitId,
            TestCode = code,
            Result = UnitSpecialTestRule.NormalizeResult(request.Result)
        };
        await _tests.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(
            previous is null ? AuditEventType.Create : AuditEventType.Correct,
            nameof(BloodComponentSpecialTest),
            row.Id,
            oldValue: previous is null ? null : new { previous.Id, BloodUnitId = unitId, previous.TestCode, previous.Result },
            newValue: new { row.Id, BloodUnitId = unitId, row.TestCode, row.Result },
            reason: previous is null ? null : request.Reason!.Trim());
        await _unitOfWork.SaveChangesAsync(ct);

        return OperationResult<UnitSpecialTestDto>.Ok(new UnitSpecialTestDto(
            row.Id, unitId, row.TestCode, row.Result, row.CreatedUtc, row.CreatedBy, IsCurrent: true));
    }
}
