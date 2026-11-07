using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Issuing;

public sealed record ActivateMtpRequest(string? RequestedBy, string? Reason, string? Location = null, long? PackDefinitionId = null);

public sealed record CloseMtpRequest(string? Reason);

/// <summary>Units issued under one activation, per component class. Counts only: ratios are facility policy.</summary>
public sealed record MtpComponentCountDto(ComponentClass ComponentClass, int Issued, int Returned, int Transfused);

public sealed record MtpIssuedUnitDto(
    long IssueId,
    long BloodUnitId,
    string UnitNumber,
    string ProductName,
    ComponentClass ComponentClass,
    string BloodType,
    DateTime IssuedUtc,
    IssueStatus IssueStatus,
    // The pack (container) the unit went into; null while not yet packed.
    long? PackRecordId = null);

/// <summary>One pack component: units per round, sent (issued and not returned), and sent in the current round.</summary>
public sealed record MtpPackComponentDto(ComponentClass ComponentClass, int PerRound, int Sent, int InCurrentRound);

/// <summary>Round guidance for a facility pack. Never blocks an issue.</summary>
public sealed record MtpPackProgressDto(
    long PackDefinitionId,
    string Code,
    string Name,
    int CompletedRounds,
    int CurrentRound,
    IReadOnlyList<MtpPackComponentDto> Components);

public sealed record MtpActivationDto(
    long Id,
    long PatientId,
    string? MedicalRecordNumber,
    string PatientName,
    MtpActivationStatus Status,
    string RequestedBy,
    string Reason,
    string? Location,
    DateTime ActivatedUtc,
    string ActivatedBy,
    DateTime? ClosedUtc,
    string? ClosedBy,
    string? CloseReason,
    int MinutesActive,
    IReadOnlyList<MtpComponentCountDto> Counts,
    IReadOnlyList<MtpIssuedUnitDto> Units,
    MtpPackProgressDto? Pack = null);

/// <summary>
/// Massive transfusion protocol activation: record who called it and why, track every unit issued under it, and
/// close it with a reason. Activation and close need <see cref="PermissionCodes.IssueEmergencyRelease"/> (OCD-012).
/// Both are audited.
/// </summary>
public sealed class MtpActivationService
{
    private readonly IRepository<MtpActivation> _activations;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<MtpPackDefinition>? _packs;

    public MtpActivationService(
        IRepository<MtpActivation> activations,
        IRepository<Patient> patients,
        IRepository<Issue> issues,
        IRepository<BloodUnit> units,
        IRepository<ProductType> products,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        IRepository<MtpPackDefinition>? packs = null)
    {
        _activations = activations;
        _patients = patients;
        _issues = issues;
        _units = units;
        _products = products;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
        _packs = packs;
    }

    public async Task<EvaluationResult<MtpActivationDto>> ActivateAsync(long patientId, ActivateMtpRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await DenyAsync(ct);
        if (denied is not null)
        {
            return EvaluationResult<MtpActivationDto>.Blocked(denied);
        }

        var patient = (await _patients.ListAsync(p => p.Id == patientId, ct)).FirstOrDefault();
        if (patient is null)
        {
            return EvaluationResult<MtpActivationDto>.Fail("Patient not found.");
        }

        var alreadyActive = await _activations.AnyAsync(a => a.PatientId == patientId && a.Status == MtpActivationStatus.Active, ct);
        var evaluation = MtpActivationRule.EvaluateActivate(request.RequestedBy, request.Reason, alreadyActive, patient.Status);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<MtpActivationDto>.Blocked(evaluation);
        }

        if (request.PackDefinitionId is long packId
            && (_packs is null || !await _packs.AnyAsync(p => p.Id == packId && p.IsActive, ct)))
        {
            return EvaluationResult<MtpActivationDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(MtpPackRule.Code, "Choose an active pack, or activate without one.")]));
        }

        var now = _clock.UtcNow;
        var activation = new MtpActivation
        {
            PatientId = patientId,
            Status = MtpActivationStatus.Active,
            RequestedBy = request.RequestedBy!.Trim(),
            Reason = request.Reason!.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            PackDefinitionId = request.PackDefinitionId,
            ActivatedUtc = now,
            ActivatedBy = _currentUser.UserName
        };
        await _activations.AddAsync(activation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _audit.Record(
            AuditEventType.MassiveTransfusionProtocol,
            nameof(MtpActivation),
            activation.Id,
            newValue: new { Action = "Activated", activation.PatientId, activation.RequestedBy, activation.Reason, activation.Location, activation.PackDefinitionId },
            reason: activation.Reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<MtpActivationDto>.Ok(await ToDtoAsync(activation, patient, ct));
    }

    public async Task<EvaluationResult<MtpActivationDto>> CloseAsync(long activationId, CloseMtpRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await DenyAsync(ct);
        if (denied is not null)
        {
            return EvaluationResult<MtpActivationDto>.Blocked(denied);
        }

        var activation = await _activations.GetByIdAsync(activationId, ct);
        if (activation is null)
        {
            return EvaluationResult<MtpActivationDto>.Fail("Massive transfusion protocol activation not found.");
        }

        var evaluation = MtpActivationRule.EvaluateClose(request.Reason, activation.Status);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<MtpActivationDto>.Blocked(evaluation);
        }

        activation.Status = MtpActivationStatus.Closed;
        activation.ClosedUtc = _clock.UtcNow;
        activation.ClosedBy = _currentUser.UserName;
        activation.CloseReason = request.Reason!.Trim();
        _activations.Update(activation);
        _audit.Record(
            AuditEventType.MassiveTransfusionProtocol,
            nameof(MtpActivation),
            activation.Id,
            oldValue: new { Status = MtpActivationStatus.Active },
            newValue: new { Action = "Closed", activation.PatientId, Status = MtpActivationStatus.Closed, activation.CloseReason },
            reason: activation.CloseReason);
        await _unitOfWork.SaveChangesAsync(ct);

        var patient = (await _patients.ListAsync(p => p.Id == activation.PatientId, ct)).FirstOrDefault();
        return EvaluationResult<MtpActivationDto>.Ok(await ToDtoAsync(activation, patient, ct));
    }

    /// <summary>Every active protocol, oldest first, for the issue desk board.</summary>
    public async Task<IReadOnlyList<MtpActivationDto>> ListActiveAsync(CancellationToken ct = default)
    {
        var rows = await _activations.ListAsync(a => a.Status == MtpActivationStatus.Active, ct);
        return await ToDtosAsync(rows.OrderBy(a => a.ActivatedUtc), ct);
    }

    /// <summary>All of one patient's activations, newest first.</summary>
    public async Task<IReadOnlyList<MtpActivationDto>> ListForPatientAsync(long patientId, CancellationToken ct = default)
    {
        var rows = await _activations.ListAsync(a => a.PatientId == patientId, ct);
        return await ToDtosAsync(rows.OrderByDescending(a => a.ActivatedUtc), ct);
    }

    private async Task<RuleEvaluation?> DenyAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var auth = IssueAuthorizationRule.EvaluateEmergency(
            IssueType.MassiveTransfusion,
            await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueEmergencyRelease, ct));
        return auth.Severity == RuleSeverity.HardStop ? new RuleEvaluation([auth]) : null;
    }

    private async Task<IReadOnlyList<MtpActivationDto>> ToDtosAsync(IEnumerable<MtpActivation> rows, CancellationToken ct)
    {
        var list = new List<MtpActivationDto>();
        foreach (var a in rows)
        {
            var patient = (await _patients.ListAsync(p => p.Id == a.PatientId, ct)).FirstOrDefault();
            list.Add(await ToDtoAsync(a, patient, ct));
        }

        return list;
    }

    private async Task<MtpActivationDto> ToDtoAsync(MtpActivation a, Patient? patient, CancellationToken ct)
    {
        var issues = await _issues.ListAsync(i => i.MtpActivationId == a.Id, ct);
        var unitIds = issues.Select(i => i.BloodProductId).Distinct().ToList();
        var units = (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id);
        var productIds = units.Values.Select(u => u.ProductTypeId).Distinct().ToList();
        var products = (await _products.ListAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);

        var issued = issues
            .OrderBy(i => i.IssuedUtc)
            .Select(i =>
            {
                units.TryGetValue(i.BloodProductId, out var u);
                ProductType? product = null;
                if (u is not null)
                {
                    products.TryGetValue(u.ProductTypeId, out product);
                }

                return new MtpIssuedUnitDto(
                    i.Id,
                    i.BloodProductId,
                    u?.UnitNumber ?? $"Unit {i.BloodProductId}",
                    product?.Name ?? (u is null ? "Unknown product" : $"Product {u.ProductTypeId}"),
                    product?.ComponentClass ?? ComponentClass.Other,
                    u is null ? "Unknown" : $"{u.Abo} {RhText(u.RhD)}",
                    i.IssuedUtc,
                    i.Status,
                    i.MtpPackRecordId);
            })
            .ToList();

        var counts = issued
            .GroupBy(u => u.ComponentClass)
            .OrderBy(g => g.Key)
            .Select(g => new MtpComponentCountDto(
                g.Key,
                g.Count(),
                g.Count(u => u.IssueStatus == IssueStatus.Returned),
                g.Count(u => u.IssueStatus == IssueStatus.Transfused)))
            .ToList();

        MtpPackProgressDto? pack = null;
        if (a.PackDefinitionId is long packDefinitionId && _packs is not null
            && (await _packs.ListAsync(p => p.Id == packDefinitionId, ct)).FirstOrDefault() is { } definition)
        {
            // Sent = issued and not returned: a returned unit did not reach the patient.
            var sent = issued
                .Where(u => u.IssueStatus != IssueStatus.Returned)
                .GroupBy(u => u.ComponentClass)
                .ToDictionary(g => g.Key, g => g.Count());
            var progress = MtpPackRule.Progress(definition, sent);
            pack = new MtpPackProgressDto(
                definition.Id, definition.Code, definition.Name, progress.CompletedRounds, progress.CurrentRound,
                progress.Components.Select(c => new MtpPackComponentDto(c.ComponentClass, c.PerRound, c.Sent, c.InCurrentRound)).ToList());
        }

        var end = a.ClosedUtc ?? _clock.UtcNow;
        var name = patient is null
            ? $"Patient {a.PatientId}"
            : string.IsNullOrWhiteSpace(patient.FirstName) ? patient.LastName : $"{patient.LastName}, {patient.FirstName}";

        return new MtpActivationDto(
            a.Id, a.PatientId, patient?.MedicalRecordNumber, name, a.Status, a.RequestedBy, a.Reason, a.Location,
            a.ActivatedUtc, a.ActivatedBy, a.ClosedUtc, a.ClosedBy, a.CloseReason,
            (int)Math.Max(0, (end - a.ActivatedUtc).TotalMinutes),
            counts, issued, pack);
    }

    private static string RhText(RhType rh) => rh switch
    {
        RhType.Positive => "Pos",
        RhType.Negative => "Neg",
        _ => "Rh unknown"
    };
}
