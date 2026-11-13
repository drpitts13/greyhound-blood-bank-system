using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

/// <summary>
/// Orders the transfusion reaction workup: the pre-transfusion specimen (collected before the unit was issued) and
/// the post-transfusion specimen (drawn after the reaction) each get the tests of the Test Group the settings map
/// them to, as a Stat reaction workup order linked to that specimen. The tests are then resulted and verified on the
/// bench like any other. Each specimen is set once; it is not swapped after its order exists.
/// </summary>
public sealed class ReactionWorkupService
{
    private readonly IRepository<ReactionInvestigation> _investigations;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<Encounter> _encounters;
    private readonly IRepository<OrderingLocation> _locations;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly OrderService _orderService;
    private readonly SpecimenService _specimenService;
    private readonly ReactionWorkupSettingsReader _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public ReactionWorkupService(
        IRepository<ReactionInvestigation> investigations,
        IRepository<TransfusionEvent> transfusions,
        IRepository<Issue> issues,
        IRepository<Specimen> specimens,
        IRepository<Order> orders,
        IRepository<Encounter> encounters,
        IRepository<OrderingLocation> locations,
        IRepository<Crossmatch> crossmatches,
        OrderService orderService,
        SpecimenService specimenService,
        ReactionWorkupSettingsReader settings,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _investigations = investigations;
        _transfusions = transfusions;
        _issues = issues;
        _specimens = specimens;
        _orders = orders;
        _encounters = encounters;
        _locations = locations;
        _crossmatches = crossmatches;
        _orderService = orderService;
        _specimenService = specimenService;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<OperationResult<ReactionWorkupOptionsDto>> ListOptionsAsync(long investigationId, CancellationToken ct = default)
    {
        var denied = await DeniedAsync(ct);
        if (denied is not null)
        {
            return OperationResult<ReactionWorkupOptionsDto>.Fail(denied);
        }

        var context = await LoadContextAsync(investigationId, ct);
        if (context.Error is not null)
        {
            return OperationResult<ReactionWorkupOptionsDto>.Fail(context.Error);
        }

        var row = context.Row!;
        var issuedUtc = context.IssuedUtc;
        var startUtc = context.StartUtc;
        var settings = await _settings.GetActiveAsync(ct);
        var now = _clock.UtcNow;
        var specimens = (await _specimens.ListAsync(s => s.PatientId == row.PatientId, ct))
            .Where(s => s.Status is not (SpecimenStatus.Rejected or SpecimenStatus.Cancelled))
            .OrderByDescending(s => s.CollectedUtc)
            .ToList();

        // The tube the unit was crossmatched on is the natural pre-transfusion specimen.
        var crossmatchSpecimenId = (await _crossmatches.ListAsync(
                x => x.PatientId == row.PatientId && x.BloodProductId == row.BloodProductId, ct))
            .OrderByDescending(x => x.PerformedUtc)
            .Select(x => (long?)x.SpecimenId)
            .FirstOrDefault();
        var pre = specimens
            .Where(s => s.Status is SpecimenStatus.Accepted or SpecimenStatus.Expired
                && CrossmatchSpecimenRule.IsPreTransfusionSpecimen(s.CollectedUtc, issuedUtc))
            .ToList();
        var suggestedPre = pre.FirstOrDefault(s => s.Id == crossmatchSpecimenId)?.Id ?? pre.FirstOrDefault()?.Id;
        var post = specimens
            .Where(s => s.Status == SpecimenStatus.Accepted && s.CollectedUtc >= startUtc && s.Id != row.PreTransfusionSpecimenId)
            .ToList();

        ReactionWorkupSpecimenOptionDto Option(Specimen s, bool suggested) => new(
            s.Id, s.AccessionNumber, s.SpecimenType, s.CollectedUtc, s.ExpiresUtc, s.Status,
            s.ExpiresUtc is DateTime expires && expires <= now, suggested);

        return OperationResult<ReactionWorkupOptionsDto>.Ok(new ReactionWorkupOptionsDto(
            issuedUtc,
            startUtc,
            settings?.PreTransfusionTestGroupCode,
            settings?.PostTransfusionTestGroupCode,
            pre.Select(s => Option(s, s.Id == suggestedPre)).ToList(),
            post.Select(s => Option(s, false)).ToList()));
    }

    public Task<OperationResult<ReactionInvestigation>> AttachPreTransfusionSpecimenAsync(
        long investigationId, AttachWorkupSpecimenRequest request, CancellationToken ct = default) =>
        AttachAsync(investigationId, request?.SpecimenId ?? 0, preTransfusion: true, ct);

    public Task<OperationResult<ReactionInvestigation>> AttachPostTransfusionSpecimenAsync(
        long investigationId, AttachWorkupSpecimenRequest request, CancellationToken ct = default) =>
        AttachAsync(investigationId, request?.SpecimenId ?? 0, preTransfusion: false, ct);

    /// <summary>Accessions the post-transfusion specimen drawn for the workup, then orders its Test Group on it.</summary>
    public async Task<OperationResult<ReactionInvestigation>> AccessionPostTransfusionSpecimenAsync(
        long investigationId, AccessionPostTransfusionSpecimenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await DeniedAsync(ct);
        if (denied is not null)
        {
            return OperationResult<ReactionInvestigation>.Fail(denied);
        }

        var context = await LoadContextAsync(investigationId, ct);
        if (context.Error is not null)
        {
            return OperationResult<ReactionInvestigation>.Fail(context.Error);
        }

        var precheck = Precheck(context.Row!, preTransfusion: false, await _settings.GetActiveAsync(ct),
            await ExistingOrderWithdrawnAsync(context.Row!, preTransfusion: false, ct));
        if (precheck.Error is not null)
        {
            return OperationResult<ReactionInvestigation>.Fail(precheck.Error);
        }

        if (request.CollectedUtc < context.StartUtc)
        {
            return OperationResult<ReactionInvestigation>.Fail(
                $"{ReactionWorkupSpecimenRule.PostCode}: The post-transfusion specimen must be collected after the transfusion began at {context.StartUtc:yyyy-MM-dd HH:mm} UTC.");
        }

        var accessioned = await _specimenService.AccessionAsync(new AccessionSpecimenRequest(
            request.AccessionNumber,
            context.Row!.PatientId,
            request.SpecimenType,
            request.CollectedUtc,
            request.Barcode,
            Identifier1Value: string.IsNullOrWhiteSpace(request.Identifier1Value) ? null : request.Identifier1Value.Trim(),
            Identifier2Value: string.IsNullOrWhiteSpace(request.Identifier2Value) ? null : request.Identifier2Value.Trim(),
            Comment: $"Post-transfusion specimen for transfusion reaction investigation {context.Row.Id}."), ct);
        if (!accessioned.Succeeded || accessioned.Value is null)
        {
            return OperationResult<ReactionInvestigation>.Fail(accessioned.Error ?? "The specimen could not be accessioned.");
        }

        return await AttachAsync(investigationId, accessioned.Value.Id, preTransfusion: false, ct);
    }

    private async Task<OperationResult<ReactionInvestigation>> AttachAsync(
        long investigationId, long specimenId, bool preTransfusion, CancellationToken ct)
    {
        var denied = await DeniedAsync(ct);
        if (denied is not null)
        {
            return OperationResult<ReactionInvestigation>.Fail(denied);
        }

        var context = await LoadContextAsync(investigationId, ct);
        if (context.Error is not null)
        {
            return OperationResult<ReactionInvestigation>.Fail(context.Error);
        }

        var row = context.Row!;
        var replacing = await ExistingOrderWithdrawnAsync(row, preTransfusion, ct);
        var (groupCode, error) = Precheck(row, preTransfusion, await _settings.GetActiveAsync(ct), replacing);
        if (error is not null)
        {
            return OperationResult<ReactionInvestigation>.Fail(error);
        }

        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Specimen not found.");
        }

        var rule = preTransfusion
            ? ReactionWorkupSpecimenRule.EvaluatePre(
                row.PatientId, specimen.PatientId, specimen.Status, specimen.CollectedUtc, context.IssuedUtc, specimen.AccessionNumber)
            : ReactionWorkupSpecimenRule.EvaluatePost(
                row.PatientId, specimen.PatientId, specimen.Status, specimen.CollectedUtc, context.StartUtc,
                specimen.Id, row.PreTransfusionSpecimenId, specimen.AccessionNumber);
        if (rule.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReactionInvestigation>.Fail($"{rule.Code}: {rule.Message}");
        }

        var encounterId = context.Issue?.EncounterId ?? specimen.EncounterId
            ?? (await _encounters.ListAsync(e => e.PatientId == row.PatientId, ct))
                .OrderByDescending(e => e.AdmitUtc ?? DateTime.MinValue)
                .ThenByDescending(e => e.Id)
                .Select(e => (long?)e.Id)
                .FirstOrDefault();
        if (encounterId is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("The patient has no visit to order the workup on.");
        }

        var issueOrder = context.Issue?.OrderId is long issueOrderId ? await _orders.GetByIdAsync(issueOrderId, ct) : null;
        var locationId = issueOrder?.OrderingLocationId
            ?? (await _locations.ListAsync(l => l.IsActive, ct)).OrderBy(l => l.Id).Select(l => (long?)l.Id).FirstOrDefault();
        if (locationId is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("No active ordering location is configured; cannot order the workup.");
        }

        var role = preTransfusion ? ReactionWorkupSpecimenDto.PreRole : ReactionWorkupSpecimenDto.PostRole;
        // RXN-{id}-PRE / -POST; a replacement after a cancelled order takes the next free number (-2, -3, ...).
        var baseNumber = $"RXN-{row.Id}-{(preTransfusion ? "PRE" : "POST")}";
        var orderNumber = baseNumber;
        for (var n = 2; await _orders.AnyAsync(o => o.OrderNumber == orderNumber, ct); n++)
        {
            orderNumber = $"{baseNumber}-{n}";
        }

        var created = await _orderService.CreateForReactionWorkupAsync(
            row.PatientId,
            new CreateOrderRequest(
                encounterId.Value,
                locationId.Value,
                orderNumber,
                [new OrderLineInputDto(OrderCategory.Test, groupCode!, null)],
                OrderPriority.Stat,
                _clock.UtcNow,
                issueOrder?.OrderingProviderId,
                OrderSource.Manual,
                null,
                _currentUser.UserName,
                specimen.Id,
                $"Transfusion reaction investigation {row.Id}: {role.ToLowerInvariant()} specimen."),
            $"Transfusion reaction workup - {role.ToLowerInvariant()}",
            ct);
        if (!created.Succeeded || created.Value is null)
        {
            return OperationResult<ReactionInvestigation>.Fail(created.Error ?? "The workup order could not be created.");
        }

        var tracked = await _investigations.GetByIdAsync(row.Id, ct) ?? row;
        var replaced = replacing
            ? new
            {
                SpecimenId = preTransfusion ? tracked.PreTransfusionSpecimenId : tracked.PostTransfusionSpecimenId,
                OrderId = preTransfusion ? tracked.PreTransfusionOrderId : tracked.PostTransfusionOrderId
            }
            : null;
        if (preTransfusion)
        {
            tracked.PreTransfusionSpecimenId = specimen.Id;
            tracked.PreTransfusionOrderId = created.Value.Id;
        }
        else
        {
            tracked.PostTransfusionSpecimenId = specimen.Id;
            tracked.PostTransfusionOrderId = created.Value.Id;
        }

        _investigations.Update(tracked);
        _audit.Record(
            AuditEventType.ReactionInvestigation,
            nameof(ReactionInvestigation),
            tracked.Id,
            oldValue: replaced,
            newValue: new
            {
                Role = role,
                SpecimenId = specimen.Id,
                specimen.AccessionNumber,
                OrderId = created.Value.Id,
                created.Value.OrderNumber,
                TestGroup = groupCode,
                SpecimenExpired = specimen.ExpiresUtc is DateTime expires && expires <= _clock.UtcNow
            },
            reason: replacing
                ? $"{role} specimen replaced after its workup order was cancelled; Test Group {groupCode} ordered."
                : $"{role} specimen added to the workup; Test Group {groupCode} ordered.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(tracked);
    }

    /// <summary>Open case, role not yet set, Test Group configured: the group code, or why not.</summary>
    /// <summary>True when the role already has an order and that order was cancelled or discontinued.</summary>
    private async Task<bool> ExistingOrderWithdrawnAsync(ReactionInvestigation row, bool preTransfusion, CancellationToken ct)
    {
        var orderId = preTransfusion ? row.PreTransfusionOrderId : row.PostTransfusionOrderId;
        if (orderId is not long id)
        {
            return false;
        }

        var order = await _orders.GetByIdAsync(id, ct);
        return order is not null && ReactionWorkupReader.IsWithdrawn(order.Status);
    }

    private static (string? GroupCode, string? Error) Precheck(
        ReactionInvestigation row, bool preTransfusion, Domain.Entities.Configuration.ReactionWorkupSettings? settings, bool replacing)
    {
        var role = preTransfusion ? "pre-transfusion" : "post-transfusion";
        if (row.Status == ReactionInvestigationStatus.Closed)
        {
            return (null, $"{ReactionInvestigationLockRule.LockedCode}: The investigation is closed. Reopen it to change the workup.");
        }

        // A specimen is set once; only a cancelled or discontinued workup order frees the role for another tube.
        var existing = preTransfusion ? row.PreTransfusionOrderId : row.PostTransfusionOrderId;
        if (existing is not null && !replacing)
        {
            return (null, $"The {role} specimen is already on this investigation; its tests are on the workup order.");
        }

        var group = preTransfusion ? settings?.PreTransfusionTestGroupCode : settings?.PostTransfusionTestGroupCode;
        return string.IsNullOrWhiteSpace(group)
            ? (null, $"No Test Group is chosen for the {role} specimen. Choose one in Administration, Reaction Workup.")
            : (group, null);
    }

    private sealed record WorkupContext(ReactionInvestigation? Row, DateTime IssuedUtc, DateTime StartUtc, Issue? Issue, string? Error = null);

    private async Task<WorkupContext> LoadContextAsync(long investigationId, CancellationToken ct)
    {
        var row = await _investigations.GetByIdAsync(investigationId, ct);
        if (row is null)
        {
            return new WorkupContext(null, default, default, null, "Investigation not found.");
        }

        var transfusion = await _transfusions.GetByIdAsync(row.TransfusionEventId, ct);
        var issue = transfusion is null ? null : await _issues.GetByIdAsync(transfusion.IssueId, ct);
        var issuedUtc = issue?.IssuedUtc ?? transfusion?.StartUtc ?? row.ReportedUtc;
        var startUtc = transfusion?.StartUtc ?? issuedUtc;
        return new WorkupContext(row, issuedUtc, startUtc, issue);
    }

    private async Task<string?> DeniedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.ReactionInvestigate, ct);
        var auth = ReactionAuthorizationRule.EvaluateInvestigate(allowed);
        return auth.Severity == RuleSeverity.HardStop ? auth.Message : null;
    }
}
