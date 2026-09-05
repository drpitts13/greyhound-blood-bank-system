using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Issuing;

public sealed record InterfaceTransfusionRequest(
    string Mrn,
    string? UnitNumber,
    string? Din,
    DateTime? StartUtc,
    DateTime? StopUtc,
    decimal? VolumeTransfused,
    string? Location,
    string? Transfusionist,
    bool ReactionSuspected);

/// <summary>
/// Documents a transfusion from an inbound BPAM (RAS/BPS) message. Bedside scan is
/// not required here because the EHR already captured administration.
/// </summary>
public sealed class InterfaceTransfusionService
{
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IInventoryRepository _inventory;
    private readonly ReactionInvestigationService _reactions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public InterfaceTransfusionService(
        IRepository<Patient> patients,
        IRepository<BloodUnit> units,
        IRepository<Issue> issues,
        IRepository<TransfusionEvent> transfusions,
        IInventoryRepository inventory,
        ReactionInvestigationService reactions,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _patients = patients;
        _units = units;
        _issues = issues;
        _transfusions = transfusions;
        _inventory = inventory;
        _reactions = reactions;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<OperationResult<string>> DocumentAsync(InterfaceTransfusionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_permissions is not null)
        {
            var allowed = await _permissions.HasPermissionAsync(
                _currentUser.UserName, PermissionCodes.TransfusionDocument, ct);
            var auth = IssueAuthorizationRule.EvaluateInterfaceDocument(allowed);
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<string>.Fail(auth.Message);
            }
        }

        try
        {
            return OperationResult<string>.Ok(await DocumentCoreAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult<string>.Fail(ex.Message);
        }
    }

    public Task<string> DocumentFromHl7Async(InterfaceTransfusionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DocumentCoreAsync(request, ct);
    }

    private async Task<string> DocumentCoreAsync(InterfaceTransfusionRequest request, CancellationToken ct)
    {

        if (string.IsNullOrWhiteSpace(request.Mrn))
        {
            throw new InvalidOperationException("BPAM message has no patient identifier.");
        }

        var unitKey = request.UnitNumber ?? request.Din;
        if (string.IsNullOrWhiteSpace(unitKey))
        {
            throw new InvalidOperationException("BPAM message has no unit number or DIN.");
        }

        var patient = await _patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == request.Mrn, ct)
            ?? throw new InvalidOperationException($"No patient found for MRN '{request.Mrn}'.");

        var unit = await FindUnitAsync(request.UnitNumber, request.Din, ct)
            ?? throw new InvalidOperationException($"No inventory unit matches '{unitKey}'.");

        var issueMatch = (await _issues.ListAsync(
                i => i.PatientId == patient.Id && i.BloodProductId == unit.Id
                    && (i.Status == IssueStatus.Issued || i.Status == IssueStatus.Transfused),
                ct))
            .OrderByDescending(i => i.IssuedUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No issued unit '{unitKey}' found for patient {request.Mrn}.");

        var issue = await _issues.GetByIdAsync(issueMatch.Id, ct)
            ?? throw new InvalidOperationException($"No issued unit '{unitKey}' found for patient {request.Mrn}.");

        var now = _clock.UtcNow;
        var existing = await _transfusions.FirstOrDefaultAsync(t => t.IssueId == issue.Id, ct);
        if (existing is not null)
        {
            existing.StartUtc = request.StartUtc ?? existing.StartUtc;
            existing.StopUtc = request.StopUtc ?? existing.StopUtc ?? now;
            existing.VolumeTransfused = request.VolumeTransfused ?? existing.VolumeTransfused;
            existing.Location = request.Location ?? existing.Location;
            existing.Transfusionist = request.Transfusionist ?? existing.Transfusionist;
            existing.ReactionSuspected = existing.ReactionSuspected || request.ReactionSuspected;
            existing.DocumentedBy = _currentUser.UserName;
            if (request.ReactionSuspected)
            {
                await _reactions.OpenForTransfusionAsync(existing, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return $"Transfusion for unit {unitKey} updated from BPAM.";
        }

        if (issue.Status != IssueStatus.Issued)
        {
            throw new InvalidOperationException($"Issue {issue.Id} is {issue.Status} and cannot be transfused.");
        }

        if (issue.WardReceivedUtc is null)
        {
            issue.WardReceivedUtc = now;
            issue.WardReceivedBy = request.Transfusionist ?? request.Location ?? "HL7-BPAM";
            issue.WardVisualAcceptable = true;
        }

        var trackedUnit = await _inventory.GetUnitAsync(unit.Id, ct)
            ?? throw new InvalidOperationException("Unit not found.");

        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = trackedUnit.Id,
            PatientId = patient.Id,
            StartUtc = request.StartUtc ?? now,
            StopUtc = request.StopUtc,
            VolumeTransfused = request.VolumeTransfused,
            Transfusionist = request.Transfusionist,
            ReactionSuspected = request.ReactionSuspected,
            FinalDisposition = request.ReactionSuspected ? TransfusionDisposition.Stopped : TransfusionDisposition.Completed,
            DocumentedBy = _currentUser.UserName,
            Location = request.Location,
            PatientIdentificationMethod = "HL7-BPAM",
            UnitIdentificationMethod = "HL7-BPAM",
            WorkstationId = _currentUser.Workstation
        };
        await _transfusions.AddAsync(transfusion, ct);

        var terminal = transfusion.FinalDisposition == TransfusionDisposition.Stopped
            ? UnitStatus.TransfusionStopped
            : UnitStatus.Transfused;

        if (trackedUnit.Status == UnitStatus.Issued)
        {
            AppendStatus(trackedUnit, UnitStatus.TransfusionStarted, "Transfusion started (HL7 BPAM)", now, nameof(TransfusionEvent), issue.Id);
        }

        issue.Status = IssueStatus.Transfused;
        AppendStatus(trackedUnit, terminal, $"Transfusion {transfusion.FinalDisposition} (HL7 BPAM)", now, nameof(TransfusionEvent), issue.Id);

        _audit.Record(
            AuditEventType.Transfusion,
            nameof(BloodUnit),
            trackedUnit.Id,
            oldValue: new { Status = UnitStatus.Issued },
            newValue: new { Status = terminal, Source = "HL7-BPAM", request.ReactionSuspected },
            reason: "Transfusion documented from inbound BPAM.");

        await _unitOfWork.SaveChangesAsync(ct);

        if (request.ReactionSuspected)
        {
            await _reactions.OpenForTransfusionAsync(transfusion, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return $"Transfusion for unit {unitKey} documented from BPAM.";
    }

    private async Task<BloodUnit?> FindUnitAsync(string? unitNumber, string? din, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(unitNumber))
        {
            var byNumber = await _units.FirstOrDefaultAsync(u => u.UnitNumber == unitNumber, ct);
            if (byNumber is not null)
            {
                return byNumber;
            }
        }

        if (!string.IsNullOrWhiteSpace(din))
        {
            return await _units.FirstOrDefaultAsync(u => u.Din == din || u.Isbt128DonationId == din, ct);
        }

        return null;
    }

    private void AppendStatus(BloodUnit unit, UnitStatus toStatus, string reason, DateTime whenUtc, string relatedType, long relatedId)
    {
        var fromStatus = unit.Status;
        unit.Status = toStatus;
        _inventory.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = reason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = whenUtc,
            RelatedEntityType = relatedType,
            RelatedEntityId = relatedId
        });
    }
}
