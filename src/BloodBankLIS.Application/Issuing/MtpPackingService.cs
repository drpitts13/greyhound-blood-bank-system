using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Issuing;

public sealed record PackMtpRequest(
    long TransportContainerId,
    string? ContainerLabel,
    decimal? TemperatureC,
    IReadOnlyList<long> IssueIds,
    string? Comment = null);

public sealed record MtpPackSummaryDto(
    long Id,
    int PackNumber,
    DateTime PackedUtc,
    string PackedBy,
    string ContainerName,
    string? ContainerLabel,
    decimal TemperatureC,
    bool TemperatureOutsideRange,
    int UnitCount);

public sealed record MtpPackReportUnitDto(
    long IssueId,
    string UnitNumber,
    string ProductName,
    string ProductCode,
    string? IsbtProductCode,
    string BloodType,
    DateTime ExpiresUtc,
    bool Uncrossmatched,
    // What the issue recorded: never inferred, so plasma is not printed as "crossmatched".
    string Crossmatch,
    DateTime? ExpiresLocal = null);

/// <summary>Everything printed on the pack report, including the blanks the receiving clinicians fill in.</summary>
public sealed record MtpPackReportDto(
    long PackRecordId,
    int PackNumber,
    long MtpActivationId,
    long PatientId,
    string PatientName,
    string? MedicalRecordNumber,
    DateOnly? DateOfBirth,
    string RequestedBy,
    string ActivationReason,
    string? ActivationLocation,
    DateTime ActivatedUtc,
    string? PackDefinitionCode,
    string? PackDefinitionName,
    DateTime PackedUtc,
    string PackedBy,
    string ContainerCode,
    string ContainerName,
    TransportContainerType ContainerType,
    string? ContainerLabel,
    string ContainerRange,
    int? ValidatedHours,
    decimal TemperatureC,
    bool TemperatureOutsideRange,
    string? Comment,
    IReadOnlyList<MtpPackReportUnitDto> Units,
    IReadOnlyList<string> Disclaimers,
    // Bedside sheets print local time (product owner, 2026-11-01); the UTC fields above stay the record.
    string TimeZoneLabel = "UTC",
    DateTime? ActivatedLocal = null,
    DateTime? PackedLocal = null);

/// <summary>
/// Records each physical pack sent during a massive transfusion protocol (container, temperature, who and when,
/// which issued units) and builds its printable report. Packing needs <see cref="PermissionCodes.IssueCreate"/>.
/// Packing and printing are audited. A pack never changes a unit's status; the units were already issued.
/// </summary>
public sealed class MtpPackingService
{
    private readonly IRepository<MtpPackRecord> _packs;
    private readonly IRepository<MtpActivation> _activations;
    private readonly IRepository<TransportContainer> _containers;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _products;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<MtpPackDefinition> _packDefinitions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IFacilityTimeZone? _timeZone;

    public MtpPackingService(
        IRepository<MtpPackRecord> packs,
        IRepository<MtpActivation> activations,
        IRepository<TransportContainer> containers,
        IRepository<Issue> issues,
        IRepository<BloodUnit> units,
        IRepository<ProductType> products,
        IRepository<Patient> patients,
        IRepository<MtpPackDefinition> packDefinitions,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        IFacilityTimeZone? timeZone = null)
    {
        _timeZone = timeZone;
        _packs = packs;
        _activations = activations;
        _containers = containers;
        _issues = issues;
        _units = units;
        _products = products;
        _patients = patients;
        _packDefinitions = packDefinitions;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<EvaluationResult<MtpPackSummaryDto>> PackAsync(long activationId, PackMtpRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_permissions is not null)
        {
            var auth = IssueAuthorizationRule.EvaluateCreate(
                await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.IssueCreate, ct));
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<MtpPackSummaryDto>.Blocked(new RuleEvaluation([auth]));
            }
        }

        var activation = (await _activations.ListAsync(a => a.Id == activationId, ct)).FirstOrDefault();
        if (activation is null)
        {
            return EvaluationResult<MtpPackSummaryDto>.Fail("Massive transfusion protocol activation not found.");
        }

        var container = (await _containers.ListAsync(c => c.Id == request.TransportContainerId, ct)).FirstOrDefault();
        var issueIds = (request.IssueIds ?? []).Distinct().ToList();
        var issues = new List<Issue>();
        var ineligible = new List<string>();
        foreach (var issueId in issueIds)
        {
            var issue = await _issues.GetByIdAsync(issueId, ct);
            if (issue is { Status: IssueStatus.Issued, MtpPackRecordId: null } && issue.MtpActivationId == activationId)
            {
                issues.Add(issue);
            }
            else
            {
                ineligible.Add(issue is null ? $"issue {issueId}" : await UnitNumberAsync(issue.BloodProductId, ct));
            }
        }

        var evaluation = TransportContainerRule.EvaluatePacking(activation.Status, container, request.TemperatureC, issueIds.Count, ineligible);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<MtpPackSummaryDto>.Blocked(evaluation);
        }

        var existing = await _packs.ListAsync(p => p.MtpActivationId == activationId, ct);
        var record = new MtpPackRecord
        {
            MtpActivationId = activationId,
            PackNumber = existing.Count == 0 ? 1 : existing.Max(p => p.PackNumber) + 1,
            TransportContainerId = container!.Id,
            ContainerLabel = string.IsNullOrWhiteSpace(request.ContainerLabel) ? null : request.ContainerLabel.Trim(),
            TemperatureC = request.TemperatureC!.Value,
            TemperatureOutsideRange = TransportContainerRule.IsOutsideRange(container, request.TemperatureC.Value),
            PackedUtc = _clock.UtcNow,
            PackedBy = _currentUser.UserName,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim()
        };
        await _packs.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var unitNumbers = new List<string>();
        foreach (var issue in issues)
        {
            issue.MtpPackRecordId = record.Id;
            _issues.Update(issue);
            unitNumbers.Add(await UnitNumberAsync(issue.BloodProductId, ct));
        }

        _audit.Record(
            AuditEventType.MassiveTransfusionProtocol,
            nameof(MtpPackRecord),
            record.Id,
            newValue: new
            {
                Action = "Packed",
                record.MtpActivationId,
                activation.PatientId,
                record.PackNumber,
                Container = container.Code,
                record.ContainerLabel,
                record.TemperatureC,
                record.TemperatureOutsideRange,
                Units = unitNumbers
            },
            reason: record.TemperatureOutsideRange ? "Packed; temperature outside the container's facility range." : "Packed.");
        await _unitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<MtpPackSummaryDto>.Ok(
            new MtpPackSummaryDto(record.Id, record.PackNumber, record.PackedUtc, record.PackedBy, container.Name,
                record.ContainerLabel, record.TemperatureC, record.TemperatureOutsideRange, issues.Count),
            evaluation);
    }

    public async Task<IReadOnlyList<MtpPackSummaryDto>> ListForActivationAsync(long activationId, CancellationToken ct = default)
    {
        var packs = await _packs.ListAsync(p => p.MtpActivationId == activationId, ct);
        if (packs.Count == 0)
        {
            return [];
        }

        var containerIds = packs.Select(p => p.TransportContainerId).Distinct().ToList();
        var containers = (await _containers.ListAsync(c => containerIds.Contains(c.Id), ct)).ToDictionary(c => c.Id);
        var packIds = packs.Select(p => (long?)p.Id).ToList();
        var counts = (await _issues.ListAsync(i => packIds.Contains(i.MtpPackRecordId), ct))
            .GroupBy(i => i.MtpPackRecordId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        return packs
            .OrderBy(p => p.PackNumber)
            .Select(p => new MtpPackSummaryDto(
                p.Id, p.PackNumber, p.PackedUtc, p.PackedBy,
                containers.TryGetValue(p.TransportContainerId, out var c) ? c.Name : $"Container {p.TransportContainerId}",
                p.ContainerLabel, p.TemperatureC, p.TemperatureOutsideRange,
                counts.TryGetValue(p.Id, out var n) ? n : 0))
            .ToList();
    }

    public async Task<MtpPackReportDto?> GetReportAsync(long packRecordId, CancellationToken ct = default)
    {
        var record = (await _packs.ListAsync(p => p.Id == packRecordId, ct)).FirstOrDefault();
        if (record is null)
        {
            return null;
        }

        var activation = (await _activations.ListAsync(a => a.Id == record.MtpActivationId, ct)).First();
        var patient = (await _patients.ListAsync(p => p.Id == activation.PatientId, ct)).FirstOrDefault();
        var container = (await _containers.ListAsync(c => c.Id == record.TransportContainerId, ct)).FirstOrDefault();
        MtpPackDefinition? definition = null;
        if (activation.PackDefinitionId is long definitionId)
        {
            definition = (await _packDefinitions.ListAsync(d => d.Id == definitionId, ct)).FirstOrDefault();
        }

        var issues = await _issues.ListAsync(i => i.MtpPackRecordId == record.Id, ct);
        var unitIds = issues.Select(i => i.BloodProductId).Distinct().ToList();
        var units = (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id);
        var productIds = units.Values.Select(u => u.ProductTypeId).Distinct().ToList();
        var products = (await _products.ListAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);

        var lines = issues
            .OrderBy(i => i.IssuedUtc)
            .Select(i =>
            {
                units.TryGetValue(i.BloodProductId, out var u);
                ProductType? product = null;
                if (u is not null)
                {
                    products.TryGetValue(u.ProductTypeId, out product);
                }

                return new MtpPackReportUnitDto(
                    i.Id,
                    u?.UnitNumber ?? $"Unit {i.BloodProductId}",
                    product?.Name ?? "Unknown product",
                    product?.ProductCode ?? "—",
                    u?.Isbt128ProductCode ?? product?.Isbt128ProductCode,
                    u is null ? "Unknown" : $"{u.Abo} {RhText(u.RhD)}",
                    u?.ExpiresUtc ?? i.UnitExpirationAtIssueUtc ?? DateTime.MinValue,
                    i.TestsIncompleteAtIssue,
                    CrossmatchText(i),
                    Local(u?.ExpiresUtc ?? i.UnitExpirationAtIssueUtc ?? DateTime.MinValue));
            })
            .ToList();

        var name = patient is null
            ? $"Patient {activation.PatientId}"
            : string.IsNullOrWhiteSpace(patient.FirstName) ? patient.LastName : $"{patient.LastName}, {patient.FirstName}";

        return new MtpPackReportDto(
            record.Id, record.PackNumber, activation.Id, activation.PatientId, name, patient?.MedicalRecordNumber, patient?.DateOfBirth,
            activation.RequestedBy, activation.Reason, activation.Location, activation.ActivatedUtc,
            definition?.Code, definition?.Name,
            record.PackedUtc, record.PackedBy,
            container?.Code ?? "—", container?.Name ?? $"Container {record.TransportContainerId}",
            container?.ContainerType ?? TransportContainerType.Other, record.ContainerLabel,
            container is null ? "No range set" : Admin.TransportContainerDto.From(container).RangeText,
            container?.ValidatedHours,
            record.TemperatureC, record.TemperatureOutsideRange, record.Comment,
            lines,
            MtpPackReportText.Disclaimers(lines.Any(l => l.Uncrossmatched), container?.ValidatedHours, record.TemperatureOutsideRange),
            _timeZone?.Label ?? "UTC",
            Local(activation.ActivatedUtc),
            Local(record.PackedUtc));
    }

    /// <summary>UTC when no facility time zone is configured (for example in tests that pass none).</summary>
    private DateTime Local(DateTime utc) =>
        utc == DateTime.MinValue || _timeZone is null ? utc : _timeZone.ToLocal(utc);

    /// <summary>Audits that the pack report was printed (it carries patient data off the unit).</summary>
    public async Task<bool> RecordPrintedAsync(long packRecordId, CancellationToken ct = default)
    {
        var record = (await _packs.ListAsync(p => p.Id == packRecordId, ct)).FirstOrDefault();
        if (record is null)
        {
            return false;
        }

        _audit.Record(
            AuditEventType.Print,
            nameof(MtpPackRecord),
            record.Id,
            newValue: new { Document = "MTP pack report", record.MtpActivationId, record.PackNumber },
            reason: "MTP pack report printed.");
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public static string CrossmatchText(Issue i) => i.TestsIncompleteAtIssue
        ? "Uncrossmatched"
        : i.CrossmatchStatus switch
        {
            CrossmatchClinicalStatus.Compatible => "Compatible",
            CrossmatchClinicalStatus.NotCrossmatchedEmergency => "Uncrossmatched",
            CrossmatchClinicalStatus.Incompatible => "Incompatible",
            CrossmatchClinicalStatus.Expired => "Crossmatch expired",
            _ => "Not crossmatched"
        };

    private async Task<string> UnitNumberAsync(long unitId, CancellationToken ct) =>
        (await _units.ListAsync(u => u.Id == unitId, ct)).FirstOrDefault()?.UnitNumber ?? $"unit {unitId}";

    private static string RhText(RhType rh) => rh switch
    {
        RhType.Positive => "Pos",
        RhType.Negative => "Neg",
        _ => "Rh unknown"
    };
}
