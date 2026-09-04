using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Printing.Rendering;
using BloodBankLIS.Printing.Templates;

namespace BloodBankLIS.Printing;

/// <summary>
/// Orchestrates label/tag printing. It assembles the data model from audited domain
/// records, renders via the selected <see cref="ILabelRenderer"/>, and records a
/// <see cref="PrintJob"/> with the payload and rendered output. Reprints are a
/// dangerous action: they require a reason and write a Reprint audit event
/// (see docs/printing-billing.md A.3 and docs/safety-rules.md).
/// </summary>
public sealed class PrintService
{
    private readonly IRepository<PrintJob> _printJobs;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IReadOnlyDictionary<LabelFormat, ILabelRenderer> _renderers;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public PrintService(
        IRepository<PrintJob> printJobs,
        IRepository<Specimen> specimens,
        IRepository<Patient> patients,
        IRepository<Issue> issues,
        IRepository<BloodUnit> units,
        IRepository<ProductType> productTypes,
        IRepository<Crossmatch> crossmatches,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IEnumerable<ILabelRenderer> renderers,
        IPermissionEvaluator? permissions = null)
    {
        _printJobs = printJobs;
        _specimens = specimens;
        _patients = patients;
        _issues = issues;
        _units = units;
        _productTypes = productTypes;
        _crossmatches = crossmatches;
        _bloodTypes = bloodTypes;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _renderers = renderers.ToDictionary(r => r.Format);
        _permissions = permissions;
    }

    public async Task<OperationResult<PrintJob>> PrintSpecimenLabelAsync(long specimenId, PrintRequest request, CancellationToken ct = default)
    {
        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null)
        {
            return OperationResult<PrintJob>.Fail($"Specimen {specimenId} not found.");
        }

        var patient = await _patients.GetByIdAsync(specimen.PatientId, ct);
        if (patient is null)
        {
            return OperationResult<PrintJob>.Fail($"Patient {specimen.PatientId} not found.");
        }

        var model = new SpecimenLabelModel(
            specimen.AccessionNumber,
            PatientName(patient),
            patient.MedicalRecordNumber,
            patient.DateOfBirth,
            specimen.SpecimenType,
            specimen.CollectedUtc,
            specimen.DrawLocation);

        var templateCode = request.TemplateCode ?? SpecimenLabelTemplate.TemplateCode;
        var document = SpecimenLabelTemplate.Build(model);

        return await CreateJobAsync(PrintJobType.SpecimenLabel, templateCode, request, model, document,
            nameof(Specimen), specimen.Id, ct);
    }

    public async Task<OperationResult<PrintJob>> PrintCompatibilityTagAsync(long issueId, PrintRequest request, CancellationToken ct = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return OperationResult<PrintJob>.Fail($"Issue {issueId} not found.");
        }

        var unit = await _units.GetByIdAsync(issue.BloodProductId, ct);
        if (unit is null)
        {
            return OperationResult<PrintJob>.Fail($"Blood unit {issue.BloodProductId} not found.");
        }

        var patient = await _patients.GetByIdAsync(issue.PatientId, ct);
        if (patient is null)
        {
            return OperationResult<PrintJob>.Fail($"Patient {issue.PatientId} not found.");
        }

        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        var crossmatch = (await _crossmatches.ListAsync(
                x => x.BloodProductId == unit.Id && x.PatientId == patient.Id, ct))
            .OrderByDescending(x => x.PerformedUtc)
            .FirstOrDefault();

        var currentType = (await _bloodTypes.ListAsync(b => b.PatientId == patient.Id && b.IsCurrent, ct))
            .FirstOrDefault();

        var model = new CompatibilityTagModel(
            PatientName: PatientName(patient),
            MedicalRecordNumber: patient.MedicalRecordNumber,
            DateOfBirth: patient.DateOfBirth,
            PatientBloodType: currentType?.BloodType.ToString() ?? "Unknown",
            UnitNumber: unit.UnitNumber,
            UnitBloodType: unit.BloodType.ToString(),
            ProductName: productType?.Name ?? "Unknown",
            CrossmatchMethod: crossmatch?.Method.ToString() ?? "None",
            CrossmatchResult: crossmatch?.Result.ToString() ?? "None",
            UnitExpiresUtc: unit.ExpiresUtc,
            IssuedUtc: issue.IssuedUtc,
            IssuedBy: issue.IssuedBy,
            IsEmergency: issue.IssueType != IssueType.Standard,
            TestsIncomplete: issue.TestsIncompleteAtIssue);

        var templateCode = request.TemplateCode ?? CompatibilityTagTemplate.TemplateCode;
        var document = CompatibilityTagTemplate.Build(model);

        return await CreateJobAsync(PrintJobType.CompatibilityTag, templateCode, request, model, document,
            nameof(Issue), issue.Id, ct);
    }

    public async Task<OperationResult<PrintJob>> PrintComponentLabelAsync(long unitId, PrintRequest request, CancellationToken ct = default)
    {
        var unit = await _units.GetByIdAsync(unitId, ct);
        if (unit is null)
        {
            return OperationResult<PrintJob>.Fail($"Blood unit {unitId} not found.");
        }

        var productType = await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        var model = new ComponentLabelModel(
            UnitNumber: unit.UnitNumber,
            Din: unit.Din,
            ProductCodeData: unit.ProductCodeData ?? unit.Isbt128ProductCode,
            AboRhdCode: unit.AboRhdCode,
            UnitBloodType: unit.BloodType.ToString(),
            ProductName: productType?.Name ?? "Unknown",
            ExpiresUtc: unit.ExpiresUtc,
            CollectionFacility: unit.CollectionFacility);

        var templateCode = request.TemplateCode ?? ComponentLabelTemplate.TemplateCode;
        var document = ComponentLabelTemplate.Build(model);

        return await CreateJobAsync(PrintJobType.ProductLabel, templateCode, request, model, document,
            nameof(BloodUnit), unit.Id, ct);
    }

    public async Task<OperationResult<PrintJob>> ReprintAsync(long printJobId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<PrintJob>.Fail("A reason is required to reprint.");
        }

        var unauthorized = await RejectUnauthorizedReprintAsync(ct);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var original = await _printJobs.GetByIdAsync(printJobId, ct);
        if (original is null)
        {
            return OperationResult<PrintJob>.Fail($"Print job {printJobId} not found.");
        }

        // Re-render from the stored payload so the reprint matches what was printed.
        var document = RebuildDocument(original);
        if (document is null)
        {
            return OperationResult<PrintJob>.Fail($"Cannot reprint job type {original.JobType}.");
        }

        if (!_renderers.TryGetValue(original.Format, out var renderer))
        {
            return OperationResult<PrintJob>.Fail($"No renderer registered for format {original.Format}.");
        }

        var now = _clock.UtcNow;
        var reprint = new PrintJob
        {
            JobType = original.JobType,
            TemplateCode = original.TemplateCode,
            Format = original.Format,
            TargetPrinter = original.TargetPrinter,
            ContextType = original.ContextType,
            ContextId = original.ContextId,
            PayloadJson = original.PayloadJson,
            RenderedZpl = renderer.Render(document),
            Status = PrintJobStatus.Printed,
            IsReprint = true,
            ReprintReason = reason,
            PrintedBy = _currentUser.UserName,
            PrintedUtc = now
        };

        await _printJobs.AddAsync(reprint, ct);

        _audit.Record(
            AuditEventType.Reprint,
            nameof(PrintJob),
            original.Id,
            oldValue: new { original.Id, original.JobType },
            newValue: new { ReprintOfJobId = original.Id, reprint.JobType },
            reason: reason);

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<PrintJob>.Ok(reprint);
    }

    public async Task<PrintJob?> GetAsync(long id, CancellationToken ct = default) =>
        await _printJobs.GetByIdAsync(id, ct);

    public async Task<IReadOnlyList<PrintJob>> ListAsync(CancellationToken ct = default) =>
        await _printJobs.ListAsync(ct);

    private async Task<OperationResult<PrintJob>> CreateJobAsync(
        PrintJobType jobType,
        string templateCode,
        PrintRequest request,
        object model,
        LabelDocument document,
        string contextType,
        long contextId,
        CancellationToken ct)
    {
        var unauthorized = await RejectUnauthorizedLabelAsync(ct);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        if (!_renderers.TryGetValue(request.Format, out var renderer))
        {
            return OperationResult<PrintJob>.Fail($"No renderer registered for format {request.Format}.");
        }

        var now = _clock.UtcNow;
        var job = new PrintJob
        {
            JobType = jobType,
            TemplateCode = templateCode,
            Format = request.Format,
            TargetPrinter = request.TargetPrinter,
            ContextType = contextType,
            ContextId = contextId,
            PayloadJson = JsonSerializer.Serialize(model, model.GetType(), JsonOptions),
            RenderedZpl = renderer.Render(document),
            Status = PrintJobStatus.Printed,
            PrintedBy = _currentUser.UserName,
            PrintedUtc = now
        };

        await _printJobs.AddAsync(job, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<PrintJob>.Ok(job);
    }

    private static LabelDocument? RebuildDocument(PrintJob job) => job.JobType switch
    {
        PrintJobType.SpecimenLabel => SpecimenLabelTemplate.Build(
            JsonSerializer.Deserialize<SpecimenLabelModel>(job.PayloadJson, JsonOptions)!),
        PrintJobType.CompatibilityTag => CompatibilityTagTemplate.Build(
            JsonSerializer.Deserialize<CompatibilityTagModel>(job.PayloadJson, JsonOptions)!),
        PrintJobType.ProductLabel => ComponentLabelTemplate.Build(
            JsonSerializer.Deserialize<ComponentLabelModel>(job.PayloadJson, JsonOptions)!),
        _ => null
    };

    private static string PatientName(Patient patient) =>
        string.IsNullOrWhiteSpace(patient.MiddleName)
            ? $"{patient.LastName}, {patient.FirstName}"
            : $"{patient.LastName}, {patient.FirstName} {patient.MiddleName}";

    private Task<OperationResult<PrintJob>?> RejectUnauthorizedLabelAsync(CancellationToken ct) =>
        RejectUnauthorizedAsync(PermissionCodes.PrintLabel, PrintAuthorizationRule.EvaluateLabel, ct);

    private Task<OperationResult<PrintJob>?> RejectUnauthorizedReprintAsync(CancellationToken ct) =>
        RejectUnauthorizedAsync(PermissionCodes.PrintReprint, PrintAuthorizationRule.EvaluateReprint, ct);

    private async Task<OperationResult<PrintJob>?> RejectUnauthorizedAsync(
        string permissionCode, Func<bool, RuleResult> evaluate, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<PrintJob>.Fail(auth.Message)
            : null;
    }
}
