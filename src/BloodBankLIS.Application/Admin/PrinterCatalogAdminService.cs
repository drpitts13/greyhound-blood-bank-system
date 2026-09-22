using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class PrinterCatalogAdminService : ConfigAdminServiceBase
{
    private static readonly int[] AllowedDpi = [203, 300, 600];

    private readonly IRepository<PdfPrinter> _pdfPrinters;
    private readonly IRepository<ZebraPrinter> _zebraPrinters;
    private readonly IInstalledPrinterCatalog _installed;
    private readonly IPermissionEvaluator? _permissions;

    public PrinterCatalogAdminService(
        IRepository<PdfPrinter> pdfPrinters,
        IRepository<ZebraPrinter> zebraPrinters,
        IInstalledPrinterCatalog installed,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissions = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _pdfPrinters = pdfPrinters;
        _zebraPrinters = zebraPrinters;
        _installed = installed;
        _permissions = permissions;
    }

    public IReadOnlyList<string> ListInstalledPrinters() => _installed.List();

    public async Task<IReadOnlyList<PdfPrinterDto>> ListPdfAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _pdfPrinters.ListAsync(ct)
            : await _pdfPrinters.ListAsync(p => p.IsActive, ct);
        return list.OrderBy(p => p.Name).Select(PdfPrinterDto.From).ToList();
    }

    public async Task<IReadOnlyList<PrinterChoiceDto>> ListActivePdfChoicesAsync(CancellationToken ct = default)
    {
        var list = await _pdfPrinters.ListAsync(p => p.IsActive, ct);
        return list.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name)
            .Select(p => new PrinterChoiceDto(p.Id, p.Name, p.IsDefault)).ToList();
    }

    public async Task<IReadOnlyList<ZebraPrinterDto>> ListZebraAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _zebraPrinters.ListAsync(ct)
            : await _zebraPrinters.ListAsync(p => p.IsActive, ct);
        return list.OrderBy(p => p.Name).Select(ZebraPrinterDto.From).ToList();
    }

    public async Task<IReadOnlyList<PrinterChoiceDto>> ListActiveZebraChoicesAsync(CancellationToken ct = default)
    {
        var list = await _zebraPrinters.ListAsync(p => p.IsActive, ct);
        return list.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name)
            .Select(p => new PrinterChoiceDto(p.Id, p.Name, p.IsDefault)).ToList();
    }

    public async Task<EvaluationResult<PdfPrinterDto>> CreatePdfAsync(SavePdfPrinterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectAsync<PdfPrinterDto>(PermissionCodes.AdminConfigEdit, PrinterAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var error = ValidatePdf(request);
        if (error is not null)
        {
            return EvaluationResult<PdfPrinterDto>.Fail(error);
        }

        var entity = new PdfPrinter { IsActive = true };
        ApplyPdf(entity, request);
        await ClearOtherPdfDefaultsAsync(entity, ct);
        await _pdfPrinters.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = PdfPrinterDto.From(entity);
        RecordChange("PdfPrinter", entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<PdfPrinterDto>.Ok(dto);
    }

    public async Task<EvaluationResult<PdfPrinterDto>> UpdatePdfAsync(long id, SavePdfPrinterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectAsync<PdfPrinterDto>(PermissionCodes.AdminConfigEdit, PrinterAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _pdfPrinters.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<PdfPrinterDto>.Fail("PDF printer not found.");
        }

        var error = ValidatePdf(request);
        if (error is not null)
        {
            return EvaluationResult<PdfPrinterDto>.Fail(error);
        }

        var old = PdfPrinterDto.From(entity);
        ApplyPdf(entity, request);
        await ClearOtherPdfDefaultsAsync(entity, ct);
        _pdfPrinters.Update(entity);
        var dto = PdfPrinterDto.From(entity);
        RecordChange("PdfPrinter", entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<PdfPrinterDto>.Ok(dto);
    }

    public async Task<EvaluationResult<PdfPrinterDto>> SetPdfActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectAsync<PdfPrinterDto>(
            PermissionCodes.AdminConfigActivate,
            active ? PrinterAuthorizationRule.EvaluateActivate : PrinterAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _pdfPrinters.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<PdfPrinterDto>.Fail("PDF printer not found.");
        }

        var old = PdfPrinterDto.From(entity);
        entity.IsActive = active;
        if (!active)
        {
            entity.IsDefault = false;
        }

        _pdfPrinters.Update(entity);
        var dto = PdfPrinterDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange("PdfPrinter", entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<PdfPrinterDto>.Ok(dto);
    }

    public async Task<EvaluationResult<ZebraPrinterDto>> CreateZebraAsync(SaveZebraPrinterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectAsync<ZebraPrinterDto>(PermissionCodes.AdminConfigEdit, PrinterAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var error = ValidateZebra(request);
        if (error is not null)
        {
            return EvaluationResult<ZebraPrinterDto>.Fail(error);
        }

        var entity = new ZebraPrinter { IsActive = true };
        ApplyZebra(entity, request);
        await ClearOtherZebraDefaultsAsync(entity, ct);
        await _zebraPrinters.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = ZebraPrinterDto.From(entity);
        RecordChange("ZebraPrinter", entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ZebraPrinterDto>.Ok(dto);
    }

    public async Task<EvaluationResult<ZebraPrinterDto>> UpdateZebraAsync(long id, SaveZebraPrinterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectAsync<ZebraPrinterDto>(PermissionCodes.AdminConfigEdit, PrinterAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _zebraPrinters.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ZebraPrinterDto>.Fail("Zebra printer not found.");
        }

        var error = ValidateZebra(request);
        if (error is not null)
        {
            return EvaluationResult<ZebraPrinterDto>.Fail(error);
        }

        var old = ZebraPrinterDto.From(entity);
        ApplyZebra(entity, request);
        await ClearOtherZebraDefaultsAsync(entity, ct);
        _zebraPrinters.Update(entity);
        var dto = ZebraPrinterDto.From(entity);
        RecordChange("ZebraPrinter", entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ZebraPrinterDto>.Ok(dto);
    }

    public async Task<EvaluationResult<ZebraPrinterDto>> SetZebraActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectAsync<ZebraPrinterDto>(
            PermissionCodes.AdminConfigActivate,
            active ? PrinterAuthorizationRule.EvaluateActivate : PrinterAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _zebraPrinters.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ZebraPrinterDto>.Fail("Zebra printer not found.");
        }

        var old = ZebraPrinterDto.From(entity);
        entity.IsActive = active;
        if (!active)
        {
            entity.IsDefault = false;
        }

        _zebraPrinters.Update(entity);
        var dto = ZebraPrinterDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange("ZebraPrinter", entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ZebraPrinterDto>.Ok(dto);
    }

    private async Task ClearOtherPdfDefaultsAsync(PdfPrinter entity, CancellationToken ct)
    {
        if (!entity.IsDefault)
        {
            return;
        }

        var others = await _pdfPrinters.ListAsync(p => p.IsDefault && p.Id != entity.Id, ct);
        foreach (var other in others)
        {
            var tracked = await _pdfPrinters.GetByIdAsync(other.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            tracked.IsDefault = false;
            _pdfPrinters.Update(tracked);
        }
    }

    private async Task ClearOtherZebraDefaultsAsync(ZebraPrinter entity, CancellationToken ct)
    {
        if (!entity.IsDefault)
        {
            return;
        }

        var others = await _zebraPrinters.ListAsync(p => p.IsDefault && p.Id != entity.Id, ct);
        foreach (var other in others)
        {
            var tracked = await _zebraPrinters.GetByIdAsync(other.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            tracked.IsDefault = false;
            _zebraPrinters.Update(tracked);
        }
    }

    private static string? ValidatePdf(SavePdfPrinterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Display name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.WindowsPrinterName))
        {
            return "Windows printer name is required.";
        }

        return null;
    }

    private static string? ValidateZebra(SaveZebraPrinterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Display name is required.";
        }

        if (!AllowedDpi.Contains(request.Dpi))
        {
            return "DPI must be 203, 300, or 600.";
        }

        return request.ConnectionType switch
        {
            ZebraConnectionType.NetworkTcp when string.IsNullOrWhiteSpace(request.Host) =>
                "Host is required for a network Zebra printer.",
            ZebraConnectionType.NetworkTcp when request.Port is < 1 or > 65535 =>
                "Port must be between 1 and 65535.",
            ZebraConnectionType.WindowsDriver or ZebraConnectionType.Usb when string.IsNullOrWhiteSpace(request.WindowsPrinterName) =>
                "Windows printer name is required for a USB or driver-connected Zebra.",
            ZebraConnectionType.Serial or ZebraConnectionType.Bluetooth when string.IsNullOrWhiteSpace(request.ComPort) =>
                "COM port is required for a serial or Bluetooth Zebra printer.",
            ZebraConnectionType.Serial or ZebraConnectionType.Bluetooth when request.BaudRate <= 0 =>
                "Baud rate must be greater than zero.",
            ZebraConnectionType.Serial or ZebraConnectionType.Bluetooth when request.DataBits != 7 && request.DataBits != 8 =>
                "Data bits must be 7 or 8.",
            _ => null
        };
    }

    private static void ApplyPdf(PdfPrinter entity, SavePdfPrinterRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.WindowsPrinterName = request.WindowsPrinterName.Trim();
        entity.IsDefault = request.IsDefault;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private static void ApplyZebra(ZebraPrinter entity, SaveZebraPrinterRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.ConnectionType = request.ConnectionType;
        entity.Host = string.IsNullOrWhiteSpace(request.Host) ? null : request.Host.Trim();
        entity.Port = request.Port == 0 ? 9100 : request.Port;
        entity.WindowsPrinterName = string.IsNullOrWhiteSpace(request.WindowsPrinterName) ? null : request.WindowsPrinterName.Trim();
        entity.ComPort = string.IsNullOrWhiteSpace(request.ComPort) ? null : request.ComPort.Trim();
        entity.BaudRate = request.BaudRate == 0 ? 9600 : request.BaudRate;
        entity.DataBits = request.DataBits == 0 ? 8 : request.DataBits;
        entity.Parity = request.Parity;
        entity.StopBits = request.StopBits;
        entity.Dpi = request.Dpi;
        entity.IsDefault = request.IsDefault;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private async Task<EvaluationResult<T>?> RejectAsync<T>(
        string permissionCode, Func<bool, RuleResult> evaluate, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<T>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
