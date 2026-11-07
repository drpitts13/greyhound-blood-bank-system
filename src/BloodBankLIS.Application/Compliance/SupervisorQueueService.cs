using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record SupervisorQueueItemDto(
    string Kind,
    string Title,
    string Detail,
    string Href,
    string NextAction,
    long? PatientId = null,
    long? ContextId = null,
    string? Mrn = null,
    bool DueSoon = false);

/// <summary>
/// Aggregates supervisor-scoped work already owned by ABID review, result
/// verification, and quarantine release. Does not change clinical gates.
/// </summary>
public sealed class SupervisorQueueService
{
    private readonly AntibodyIdentificationService? _abid;
    private readonly TestWorklistService? _tests;
    private readonly InventoryService? _inventory;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionEvaluator? _permissions;
    private readonly OverrideReviewService? _overrides;

    public SupervisorQueueService(
        ICurrentUser currentUser,
        AntibodyIdentificationService? abid = null,
        TestWorklistService? tests = null,
        InventoryService? inventory = null,
        IPermissionEvaluator? permissions = null,
        OverrideReviewService? overrides = null)
    {
        _overrides = overrides;
        _currentUser = currentUser;
        _abid = abid;
        _tests = tests;
        _inventory = inventory;
        _permissions = permissions;
    }

    public async Task<OperationResult<IReadOnlyList<SupervisorQueueItemDto>>> ListAsync(
        CancellationToken ct = default)
    {
        var immuno = await HasAsync(PermissionCodes.ImmunoOverride, ct);
        var verify = await HasAsync(PermissionCodes.ResultVerify, ct);
        var release = await HasAsync(PermissionCodes.InventoryRelease, ct);
        var approve = await HasAsync(PermissionCodes.OverrideApprove, ct);
        var access = SupervisorQueueRule.EvaluateAccess(immuno, verify, release, approve);
        if (access.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<IReadOnlyList<SupervisorQueueItemDto>>.Fail(access.Message);
        }

        var items = new List<SupervisorQueueItemDto>();

        if (SupervisorQueueRule.IncludeAntibodyReview(immuno) && _abid is not null)
        {
            var workups = await _abid.ListOpenWorkupsAsync(ct);
            foreach (var w in workups.Where(w => w.NextAction == AntibodyIdWorklistNextAction.Review))
            {
                var name = string.IsNullOrWhiteSpace(w.PatientName) ? "Patient" : w.PatientName!;
                var mrn = w.PatientMrn ?? "—";
                items.Add(new SupervisorQueueItemDto(
                    SupervisorQueueRule.KindAntibodyReview,
                    $"Antibody ID review — {name} ({mrn})",
                    $"Lot {w.LotNumber}. Interpretation waiting for supervisor Accept or Reject.",
                    $"/patients/{w.PatientId}/antibody-id/{w.Id}",
                    "Review",
                    w.PatientId,
                    w.Id,
                    w.PatientMrn));
            }
        }

        if (SupervisorQueueRule.IncludeResultVerify(verify) && _tests is not null)
        {
            var pending = await _tests.ListPendingGlobalAsync(ct);
            foreach (var row in pending.Where(r =>
                         r.CurrentResultStatus == ResultStatus.PendingVerification
                         && r.CurrentResultId is not null
                         && r.CanEnterResults
                         && !r.IsAboSelfVerifyBlocked(_currentUser.UserName)))
            {
                var detail = $"{row.CurrentResultSource}: {row.CurrentResultValue ?? "—"}. Posted value needs verification.";
                items.Add(new SupervisorQueueItemDto(
                    SupervisorQueueRule.KindResultVerify,
                    $"Verify {row.TestCode} — {row.PatientName} ({row.Mrn})",
                    row.VerificationDeadlineNotice is null ? detail : $"{detail} {row.VerificationDeadlineNotice}",
                    $"/test-worklist?patientId={row.PatientId}&orderLineId={row.OrderLineId}",
                    "Verify",
                    row.PatientId,
                    row.CurrentResultId,
                    row.Mrn,
                    DueSoon: row.VerificationDeadlineNotice is not null));
            }
        }

        if (SupervisorQueueRule.IncludeQuarantineRelease(release) && _inventory is not null)
        {
            var quarantine = await _inventory.ListQuarantineAsync(ct);
            foreach (var u in quarantine)
            {
                var product = string.IsNullOrWhiteSpace(u.ProductCode) ? "unit" : u.ProductCode!;
                var reason = u.ReasonCode == UnitQuarantineReason.Unspecified
                    ? (u.Notes ?? "Quarantine")
                    : u.ReasonCode.ToString();
                items.Add(new SupervisorQueueItemDto(
                    SupervisorQueueRule.KindQuarantineRelease,
                    $"Quarantine release — {u.UnitNumber}",
                    $"{product} {u.Abo} {u.RhD}. {reason}.",
                    $"/inventory/units/{u.UnitId}",
                    "Release",
                    ContextId: u.UnitId));
            }
        }

        if (SupervisorQueueRule.IncludeDiscrepancy(release) && _inventory is not null)
        {
            foreach (var u in await _inventory.ListDiscrepancyAsync(ct))
            {
                var missing = u.Status == UnitStatus.Missing;
                var product = string.IsNullOrWhiteSpace(u.ProductCode) ? "unit" : u.ProductCode!;
                var reason = string.IsNullOrWhiteSpace(u.Reason) ? "No reason recorded" : u.Reason!.Trim().TrimEnd('.');
                items.Add(new SupervisorQueueItemDto(
                    SupervisorQueueRule.KindDiscrepancy,
                    $"{(missing ? "Missing" : "Damaged")} — {u.UnitNumber}",
                    $"{product} {u.Abo} {u.RhD}. {reason}. Last recorded at {u.LocationLabel}.",
                    $"/inventory/units/{u.UnitId}",
                    missing ? "Locate" : "Inspect",
                    ContextId: u.UnitId));
            }
        }

        if (SupervisorQueueRule.IncludeOverrideReview(approve) && _overrides is not null)
        {
            foreach (var o in await _overrides.ListPendingAsync(ct))
            {
                var what = OverrideActionRule.Describe(o.Action);
                items.Add(new SupervisorQueueItemDto(
                    SupervisorQueueRule.KindOverrideReview,
                    $"{what} — {o.ContextType} {o.ContextId}",
                    $"{o.OverriddenUtc:yyyy-MM-dd HH:mm} UTC. Rule {o.RuleCode}. Reason: {o.Reason}. Authorized by {o.AuthorizedBy} ({AuthorizerDirectoryRule.Describe(o.AuthorizerMatch, o.AuthorizerReference)}), performed by {o.PerformedBy}.",
                    o.ContextHref ?? "/supervisor",
                    "Review",
                    ContextId: o.Id));
            }
        }

        return OperationResult<IReadOnlyList<SupervisorQueueItemDto>>.Ok(
            items
                .OrderBy(i => SupervisorQueueRule.KindSortOrder(i.Kind))
                .ThenBy(i => i.DueSoon ? 0 : 1)
                .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private async Task<bool> HasAsync(string permission, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return true;
        }

        return await _permissions.HasPermissionAsync(_currentUser.UserName, permission, ct);
    }
}
