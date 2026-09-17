using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Billing;

/// <summary>
/// Facts about a clinical event that may generate charges. Assembled by the billing
/// service after the clinical action has committed (docs B.2).
/// </summary>
public readonly record struct BillingTriggerContext(
    BillingTriggerType TriggerType,
    string TriggerEntityType,
    long TriggerEntityId,
    DateTime ServiceDateUtc,
    long? PatientId,
    string? Key,
    string? IsbtProductCode = null,
    string? PerformingLocationCode = null);

/// <summary>
/// Event-driven charge capture (docs/printing-billing.md Part B). Translates triggers
/// into <see cref="BillingEvent"/> rows via data-driven <see cref="ChargeRule"/>s and
/// the test/service and product billing catalogs. Each event carries a deterministic
/// dedupe key so a repeated trigger cannot create a duplicate charge. Creation and
/// cancellation are audited; no charge is silently made or removed. Charges are only
/// captured for actions that committed, never blocked ones. Newly created events also
/// queue a standard outbound DFT.
/// </summary>
public sealed class BillingService
{
    private readonly IRepository<BillingEvent> _events;
    private readonly IRepository<ChargeRule> _rules;
    private readonly IRepository<ChargeCode> _codes;
    private readonly IRepository<TestServiceBilling> _testBillings;
    private readonly IRepository<ProductBilling> _productBillings;
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IBillingInterfacePublisher _publisher;
    private readonly IPermissionEvaluator? _permissionEvaluator;
    private readonly IRepository<Patient>? _patients;
    private readonly IRepository<Hl7MessageLog>? _messages;

    public BillingService(
        IRepository<BillingEvent> events,
        IRepository<ChargeRule> rules,
        IRepository<ChargeCode> codes,
        IRepository<TestServiceBilling> testBillings,
        IRepository<ProductBilling> productBillings,
        IRepository<TestResult> results,
        IRepository<Issue> issues,
        IRepository<BloodUnit> units,
        IRepository<ProductType> productTypes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IBillingInterfacePublisher publisher,
        IPermissionEvaluator? permissionEvaluator = null,
        IRepository<Patient>? patients = null,
        IRepository<Hl7MessageLog>? messages = null)
    {
        _events = events;
        _rules = rules;
        _codes = codes;
        _testBillings = testBillings;
        _productBillings = productBillings;
        _results = results;
        _issues = issues;
        _units = units;
        _productTypes = productTypes;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _publisher = publisher;
        _permissionEvaluator = permissionEvaluator;
        _patients = patients;
        _messages = messages;
    }

    /// <summary>Captures charges for a verified result. Safe to call repeatedly (idempotent).</summary>
    public async Task<OperationResult<IReadOnlyList<BillingEvent>>> CaptureForResultAsync(long resultId, CancellationToken ct = default)
    {
        var result = await _results.GetByIdAsync(resultId, ct);
        if (result is null)
        {
            return OperationResult<IReadOnlyList<BillingEvent>>.Fail($"Result {resultId} not found.");
        }

        if (result.Status != ResultStatus.Verified)
        {
            return OperationResult<IReadOnlyList<BillingEvent>>.Fail("Charges are only captured for verified results.");
        }

        var context = new BillingTriggerContext(
            BillingTriggerType.TestVerified,
            nameof(TestResult),
            result.Id,
            result.VerifiedUtc ?? _clock.UtcNow,
            result.PatientId,
            NormalizeKey(result.TestCode));

        return await CaptureAsync(context, ct);
    }

    /// <summary>Captures charges for an issued unit. Safe to call repeatedly (idempotent).</summary>
    public Task<OperationResult<IReadOnlyList<BillingEvent>>> CaptureForIssueAsync(
        long issueId, CancellationToken ct = default) =>
        CaptureForUnitEventAsync(issueId, BillingTriggerType.UnitIssued, ct);

    /// <summary>
    /// SoftBank / SafeTrace administration charge: captured when transfusion is
    /// documented complete. Other dispositions do not bill. Idempotent.
    /// </summary>
    public async Task<OperationResult<IReadOnlyList<BillingEvent>>> CaptureForTransfusionAsync(
        long issueId, TransfusionDisposition disposition, CancellationToken ct = default)
    {
        if (disposition != TransfusionDisposition.Completed)
        {
            return OperationResult<IReadOnlyList<BillingEvent>>.Ok(Array.Empty<BillingEvent>());
        }

        return await CaptureForUnitEventAsync(issueId, BillingTriggerType.UnitTransfused, ct);
    }

    private async Task<OperationResult<IReadOnlyList<BillingEvent>>> CaptureForUnitEventAsync(
        long issueId, BillingTriggerType triggerType, CancellationToken ct)
    {
        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return OperationResult<IReadOnlyList<BillingEvent>>.Fail($"Issue {issueId} not found.");
        }

        string? key = null;
        string? isbt = null;
        var unit = await _units.GetByIdAsync(issue.BloodProductId, ct);
        if (unit is not null)
        {
            var product = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
            key = product?.ProductCode;
            isbt = FirstNonEmpty(unit.ProductDescriptionCode, unit.Isbt128ProductCode, product?.Isbt128ProductCode);
        }

        var serviceDate = triggerType == BillingTriggerType.UnitTransfused
            ? _clock.UtcNow
            : issue.IssuedUtc;
        var context = new BillingTriggerContext(
            triggerType,
            triggerType == BillingTriggerType.UnitTransfused ? nameof(TransfusionEvent) : nameof(Issue),
            issue.Id,
            serviceDate,
            issue.PatientId,
            NormalizeKey(key),
            NormalizeKey(isbt),
            string.IsNullOrWhiteSpace(issue.IssuedToLocation) ? null : issue.IssuedToLocation.Trim());

        return await CaptureAsync(context, ct);
    }

    public async Task<OperationResult<IReadOnlyList<BillingEvent>>> CaptureAsync(BillingTriggerContext context, CancellationToken ct = default)
    {
        var created = new List<BillingEvent>();

        await CaptureFromChargeRulesAsync(context, created, ct);
        await CaptureFromTestServiceCatalogAsync(context, created, ct);
        await CaptureFromProductCatalogAsync(context, created, ct);

        if (created.Count == 0)
        {
            return OperationResult<IReadOnlyList<BillingEvent>>.Ok(Array.Empty<BillingEvent>());
        }

        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var billingEvent in created)
        {
            var messageId = await _publisher.PublishChargeAsync(billingEvent, ct);
            if (messageId is not null)
            {
                billingEvent.Hl7MessageId = messageId;
                _events.Update(billingEvent);
            }
        }

        if (created.Any(e => e.Hl7MessageId is not null))
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<IReadOnlyList<BillingEvent>>.Ok(created);
    }

    public async Task<OperationResult<BillingEvent>> ReviewAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.BillingReview, BillingAuthorizationRule.EvaluateReview, ct);
        if (denied is not null)
        {
            return denied;
        }

        var billingEvent = await _events.GetByIdAsync(id, ct);
        if (billingEvent is null)
        {
            return OperationResult<BillingEvent>.Fail($"Billing event {id} not found.");
        }

        if (billingEvent.Status != BillingEventStatus.Pending)
        {
            return OperationResult<BillingEvent>.Fail($"Only pending charges can be reviewed (current: {billingEvent.Status}).");
        }

        var previousStatus = billingEvent.Status;
        billingEvent.Status = BillingEventStatus.Reviewed;
        billingEvent.ReviewedBy = _currentUser.UserName;
        billingEvent.ReviewedUtc = _clock.UtcNow;
        _events.Update(billingEvent);

        _audit.Record(
            AuditEventType.Billing,
            nameof(BillingEvent),
            billingEvent.Id,
            oldValue: new { Status = previousStatus },
            newValue: new { Status = BillingEventStatus.Reviewed, billingEvent.ReviewedBy },
            reason: "Charge reviewed.");

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<BillingEvent>.Ok(billingEvent);
    }

    public async Task<OperationResult<BillingEvent>> CancelAsync(long id, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<BillingEvent>.Fail("A reason is required to cancel a charge.");
        }

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.BillingCancel, BillingAuthorizationRule.EvaluateCancel, ct);
        if (denied is not null)
        {
            return denied;
        }

        var billingEvent = await _events.GetByIdAsync(id, ct);
        if (billingEvent is null)
        {
            return OperationResult<BillingEvent>.Fail($"Billing event {id} not found.");
        }

        if (billingEvent.Status is BillingEventStatus.Cancelled or BillingEventStatus.Exported)
        {
            return OperationResult<BillingEvent>.Fail($"A {billingEvent.Status} charge cannot be cancelled.");
        }

        var previousStatus = billingEvent.Status;
        billingEvent.Status = BillingEventStatus.Cancelled;
        billingEvent.CancellationReason = reason;
        _events.Update(billingEvent);

        _audit.Record(
            AuditEventType.Billing,
            nameof(BillingEvent),
            billingEvent.Id,
            oldValue: new { Status = previousStatus },
            newValue: new { Status = BillingEventStatus.Cancelled },
            reason: reason);

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<BillingEvent>.Ok(billingEvent);
    }

    public async Task<OperationResult<BillingEvent>> ExportAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.BillingExport, BillingAuthorizationRule.EvaluateExport, ct);
        if (denied is not null)
        {
            return denied;
        }

        var billingEvent = await _events.GetByIdAsync(id, ct);
        if (billingEvent is null)
        {
            return OperationResult<BillingEvent>.Fail($"Billing event {id} not found.");
        }

        if (billingEvent.Status != BillingEventStatus.Reviewed)
        {
            return OperationResult<BillingEvent>.Fail($"Only reviewed charges can be exported (current: {billingEvent.Status}).");
        }

        var previousStatus = billingEvent.Status;
        billingEvent.Status = BillingEventStatus.Exported;
        billingEvent.ExportedUtc = _clock.UtcNow;
        _events.Update(billingEvent);

        _audit.Record(
            AuditEventType.Export,
            nameof(BillingEvent),
            billingEvent.Id,
            oldValue: new { Status = previousStatus },
            newValue: new { Status = BillingEventStatus.Exported },
            reason: "Charge exported.");

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<BillingEvent>.Ok(billingEvent);
    }

    public async Task<IReadOnlyList<BillingEvent>> GetReviewQueueAsync(CancellationToken ct = default)
    {
        var rows = await _events.ListAsync(
            e => e.Status == BillingEventStatus.Pending || e.Status == BillingEventStatus.Reviewed,
            ct);
        return rows
            .OrderBy(e => e.Status == BillingEventStatus.Pending ? 0 : 1)
            .ThenBy(e => e.ServiceDateUtc)
            .ToList();
    }

    /// <summary>
    /// Pending and reviewed charges with MRN, patient name, test code or unit number,
    /// and queued DFT control id so review-before-export stays on the same worklist.
    /// </summary>
    public async Task<IReadOnlyList<BillingEventDto>> ListReviewQueueDtosAsync(CancellationToken ct = default)
    {
        var queue = await GetReviewQueueAsync(ct);
        if (queue.Count == 0)
        {
            return Array.Empty<BillingEventDto>();
        }

        var patientIds = queue.Select(e => e.PatientId).OfType<long>().Distinct().ToList();
        var patients = _patients is null || patientIds.Count == 0
            ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => patientIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);

        var resultIds = queue
            .Where(e => e.TriggerEntityType == nameof(TestResult))
            .Select(e => e.TriggerEntityId)
            .Distinct()
            .ToList();
        var results = resultIds.Count == 0
            ? new Dictionary<long, TestResult>()
            : (await _results.ListAsync(r => resultIds.Contains(r.Id), ct)).ToDictionary(r => r.Id);

        var issueIds = queue
            .Where(e => e.TriggerEntityType is nameof(Issue) or nameof(TransfusionEvent))
            .Select(e => e.TriggerEntityId)
            .Distinct()
            .ToList();
        var issues = issueIds.Count == 0
            ? new Dictionary<long, Issue>()
            : (await _issues.ListAsync(i => issueIds.Contains(i.Id), ct)).ToDictionary(i => i.Id);

        var unitIds = issues.Values.Select(i => i.BloodProductId).Distinct().ToList();
        var units = unitIds.Count == 0
            ? new Dictionary<long, BloodUnit>()
            : (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id);

        var messageIds = queue.Where(e => e.Hl7MessageId is > 0).Select(e => e.Hl7MessageId!.Value).Distinct().ToList();
        var messages = _messages is null || messageIds.Count == 0
            ? new Dictionary<long, Hl7MessageLog>()
            : (await _messages.ListAsync(m => messageIds.Contains(m.Id), ct)).ToDictionary(m => m.Id);

        return queue.Select(e =>
        {
            patients.TryGetValue(e.PatientId ?? 0, out var patient);
            string? clinical = null;
            if (e.TriggerEntityType == nameof(TestResult) && results.TryGetValue(e.TriggerEntityId, out var result))
            {
                clinical = result.TestCode;
            }
            else if (issues.TryGetValue(e.TriggerEntityId, out var issue)
                     && units.TryGetValue(issue.BloodProductId, out var unit))
            {
                clinical = unit.UnitNumber;
            }

            messages.TryGetValue(e.Hl7MessageId ?? 0, out var message);
            return BillingEventDto.From(
                e,
                patient?.MedicalRecordNumber,
                patient is null ? null : $"{patient.LastName}, {patient.FirstName}",
                clinical,
                message?.MessageControlId);
        }).ToList();
    }

    private async Task CaptureFromChargeRulesAsync(
        BillingTriggerContext context,
        List<BillingEvent> created,
        CancellationToken ct)
    {
        var rules = await _rules.ListAsync(
            r => r.IsActive && r.TriggerType == context.TriggerType
                 && (r.TriggerKey == null || r.TriggerKey == context.Key), ct);

        foreach (var rule in rules)
        {
            var code = await _codes.GetByIdAsync(rule.ChargeCodeId, ct);
            if (code is null || !code.IsActive)
            {
                continue;
            }

            await TryAddEventAsync(
                context,
                BillingChargeSourceKind.ChargeRule,
                rule.Id,
                code,
                created,
                ct);
        }
    }

    private async Task CaptureFromTestServiceCatalogAsync(
        BillingTriggerContext context,
        List<BillingEvent> created,
        CancellationToken ct)
    {
        if (context.TriggerType != BillingTriggerType.TestVerified || string.IsNullOrWhiteSpace(context.Key))
        {
            return;
        }

        var rows = await _testBillings.ListAsync(
            r => r.IsActive && r.Trigger == context.TriggerType && r.TestCode == context.Key, ct);

        foreach (var row in rows)
        {
            var code = await _codes.GetByIdAsync(row.ChargeCodeId, ct);
            if (code is null || !code.IsActive)
            {
                continue;
            }

            await TryAddEventAsync(
                context,
                BillingChargeSourceKind.TestService,
                row.Id,
                code,
                created,
                ct);
        }
    }

    private async Task CaptureFromProductCatalogAsync(
        BillingTriggerContext context,
        List<BillingEvent> created,
        CancellationToken ct)
    {
        if (context.TriggerType is not (BillingTriggerType.UnitIssued or BillingTriggerType.UnitTransfused)
            || string.IsNullOrWhiteSpace(context.IsbtProductCode))
        {
            return;
        }

        var rows = await _productBillings.ListAsync(
            r => r.IsActive && r.Trigger == context.TriggerType && r.IsbtProductCode == context.IsbtProductCode, ct);

        foreach (var row in rows)
        {
            var code = await _codes.GetByIdAsync(row.ChargeCodeId, ct);
            if (code is null || !code.IsActive)
            {
                continue;
            }

            await TryAddEventAsync(
                context,
                BillingChargeSourceKind.Product,
                row.Id,
                code,
                created,
                ct);
        }
    }

    private async Task TryAddEventAsync(
        BillingTriggerContext context,
        BillingChargeSourceKind sourceKind,
        long sourceId,
        ChargeCode code,
        List<BillingEvent> created,
        CancellationToken ct)
    {
        var dedupeKey = BuildDedupeKey(context, sourceKind, sourceId);
        if (await _events.AnyAsync(e => e.DedupeKey == dedupeKey, ct))
        {
            return;
        }

        var billingEvent = new BillingEvent
        {
            ChargeCodeId = code.Id,
            BillingCode = code.Code,
            TriggerType = context.TriggerType,
            TriggerEntityType = context.TriggerEntityType,
            TriggerEntityId = context.TriggerEntityId,
            PatientId = context.PatientId,
            ServiceDateUtc = context.ServiceDateUtc,
            Amount = code.DefaultAmount,
            SourceKind = sourceKind,
            SourceId = sourceId,
            DedupeKey = dedupeKey,
            Status = BillingEventStatus.Pending,
            ProcedureCode = code.CptCode,
            RevenueCode = code.RevenueCode,
            Modifier = code.Modifier,
            Description = code.Description,
            PerformingLocationCode = context.PerformingLocationCode
        };

        await _events.AddAsync(billingEvent, ct);
        created.Add(billingEvent);
    }

    private static string BuildDedupeKey(
        BillingTriggerContext context,
        BillingChargeSourceKind sourceKind,
        long sourceId) =>
        $"{context.TriggerType}|{context.TriggerEntityType}|{context.TriggerEntityId}|{sourceKind}|{sourceId}|{context.ServiceDateUtc:yyyyMMdd}";

    private static string? NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(NormalizeKey).FirstOrDefault(v => v is not null);

    private async Task<OperationResult<BillingEvent>?> RejectUnauthorizedAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            _currentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<BillingEvent>.Fail(auth.Message)
            : null;
    }
}
