using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;
using BloodBankLIS.Domain.Isbt128.Validation;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Isbt128;

/// <summary>
/// Scan-session accumulator for one-at-a-time, rapid, concatenated, or Data Matrix multi-structure input.
/// </summary>
public sealed class ScanSessionService
{
    private static readonly IsbtDataStructureKind[] DefaultExpected =
    [
        IsbtDataStructureKind.DonationIdentificationNumber,
        IsbtDataStructureKind.AboRhd,
        IsbtDataStructureKind.ProductCode,
        IsbtDataStructureKind.ExpirationDate,
        IsbtDataStructureKind.ExpirationDateTime
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IRepository<BloodComponentScanSession> _sessions;
    private readonly IRepository<BloodComponentScanSessionLine> _lines;
    private readonly IsbtLookupCatalog _lookups;
    private readonly IDinCheckCharacterValidator _dinCheck;
    private readonly IInventoryRepository _inventory;
    private readonly InventoryService _inventoryService;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;

    public ScanSessionService(
        IRepository<BloodComponentScanSession> sessions,
        IRepository<BloodComponentScanSessionLine> lines,
        IsbtLookupCatalog lookups,
        IDinCheckCharacterValidator dinCheck,
        IInventoryRepository inventory,
        InventoryService inventoryService,
        IUnitOfWork uow,
        IClock clock,
        ICurrentUser user)
    {
        _sessions = sessions;
        _lines = lines;
        _lookups = lookups;
        _dinCheck = dinCheck;
        _inventory = inventory;
        _inventoryService = inventoryService;
        _uow = uow;
        _clock = clock;
        _user = user;
    }

    public async Task<OperationResult<ScanSessionDto>> StartAsync(StartScanSessionRequest request, CancellationToken ct = default)
    {
        var expected = request.ExpectedStructures is { Count: > 0 }
            ? request.ExpectedStructures.ToList()
            : DefaultExpected.ToList();

        var session = new BloodComponentScanSession
        {
            SessionKey = Guid.NewGuid(),
            ExpectedStructuresJson = JsonSerializer.Serialize(expected, JsonOptions),
            ReceivedStructuresJson = "[]",
            DraftJson = JsonSerializer.Serialize(new CanonicalComponentDraft
            {
                Source = ComponentEntrySource.Scanner,
                EnteredBy = _user.UserName,
                EnteredAt = _clock.UtcNow
            }, JsonOptions),
            StartedAt = _clock.UtcNow,
            LastScanAt = _clock.UtcNow,
            StartedBy = _user.UserName
        };

        await _sessions.AddAsync(session, ct);
        await _uow.SaveChangesAsync(ct);
        return OperationResult<ScanSessionDto>.Ok(await ToDtoAsync(session, ct));
    }

    public async Task<OperationResult<ScanSessionDto>> AddScanAsync(AddScanRequest request, CancellationToken ct = default)
    {
        var session = await _sessions.FirstOrDefaultAsync(s => s.SessionKey == request.SessionKey, ct);
        if (session is null)
            return OperationResult<ScanSessionDto>.Fail("Scan session not found.");
        if (session.IsCompleted)
            return OperationResult<ScanSessionDto>.Fail("Scan session is already completed.");

        var draft = JsonSerializer.Deserialize<CanonicalComponentDraft>(session.DraftJson, JsonOptions)
                    ?? new CanonicalComponentDraft();
        var expected = JsonSerializer.Deserialize<List<IsbtDataStructureKind>>(session.ExpectedStructuresJson, JsonOptions)
                       ?? DefaultExpected.ToList();
        var received = JsonSerializer.Deserialize<List<IsbtDataStructureKind>>(session.ReceivedStructuresJson, JsonOptions)
                       ?? new List<IsbtDataStructureKind>();

        var segments = CompoundIsbtPayloadSplitter.Split(request.Value);
        if (segments.Count == 0)
        {
            return OperationResult<ScanSessionDto>.Fail(
                IsbtErrorCodes.UnsupportedDataStructure + ": No recognizable ISBT structures.");
        }

        var aboLookup = await _lookups.GetAboLookupAsync(ct);
        var productLookup = await _lookups.GetProductLookupAsync(ct);

        foreach (var segment in segments)
        {
            var isExactDuplicate = await _lines.AnyAsync(
                l => l.ScanSessionId == session.Id && l.SanitizedValue == segment.Value && !l.WasConflict, ct);

            if (isExactDuplicate)
            {
                await _lines.AddAsync(new BloodComponentScanSessionLine
                {
                    ScanSessionId = session.Id,
                    StructureKind = segment.Kind,
                    OriginalValue = request.Value ?? string.Empty,
                    SanitizedValue = segment.Value,
                    WasDuplicate = true,
                    ScannedAt = _clock.UtcNow
                }, ct);
                continue;
            }

            if (!expected.Contains(segment.Kind)
                && segment.Kind is not (IsbtDataStructureKind.ExpirationDate or IsbtDataStructureKind.ExpirationDateTime
                    or IsbtDataStructureKind.CollectionDate or IsbtDataStructureKind.CollectionDateTime))
            {
                return OperationResult<ScanSessionDto>.Fail(
                    $"{IsbtErrorCodes.UnsupportedDataStructure}: Unexpected structure {segment.Kind}.");
            }

            var apply = await ApplySegmentAsync(draft, segment, aboLookup, productLookup, ct);
            if (!apply.Success)
                return OperationResult<ScanSessionDto>.Fail(string.Join("; ", apply.Errors));

            if (!received.Contains(NormalizeExpirationKind(segment.Kind)))
                received.Add(NormalizeExpirationKind(segment.Kind));

            await _lines.AddAsync(new BloodComponentScanSessionLine
            {
                ScanSessionId = session.Id,
                StructureKind = segment.Kind,
                OriginalValue = request.Value ?? string.Empty,
                SanitizedValue = segment.Value,
                WasDuplicate = false,
                WasConflict = false,
                ScannedAt = _clock.UtcNow
            }, ct);
        }

        draft.EnteredBy = _user.UserName;
        draft.EnteredAt = _clock.UtcNow;
        draft.RebuildIdentity();
        session.DraftJson = JsonSerializer.Serialize(draft, JsonOptions);
        session.ReceivedStructuresJson = JsonSerializer.Serialize(received, JsonOptions);
        session.LastScanAt = _clock.UtcNow;
        _sessions.Update(session);
        await _uow.SaveChangesAsync(ct);

        return OperationResult<ScanSessionDto>.Ok(await ToDtoAsync(session, ct));
    }

    public async Task<InventoryActionResult> CompleteAsync(CompleteScanSessionRequest request, CancellationToken ct = default)
    {
        var session = await _sessions.FirstOrDefaultAsync(s => s.SessionKey == request.SessionKey, ct);
        if (session is null)
            return InventoryActionResult.Fail("Scan session not found.");
        if (session.IsCompleted)
            return InventoryActionResult.Fail("Scan session is already completed.");

        var draft = JsonSerializer.Deserialize<CanonicalComponentDraft>(session.DraftJson, JsonOptions)
                    ?? new CanonicalComponentDraft();
        draft.RebuildIdentity();

        var identityExists = !string.IsNullOrEmpty(draft.ComponentIdentity)
            && await _inventory.ComponentIdentityKeyExistsAsync(
                ComponentIdentityBuilder.BuildUniquenessKey(
                    draft.Din!.Din, draft.Product!.ProductCodeData, draft.Product.ExtendedDivisionCode), ct);

        var validation = ComponentCrossFieldValidator.Validate(draft, identityExists, _clock.UtcNow);
        if (!validation.Valid)
        {
            return InventoryActionResult.Fail(string.Join("; ", validation.Errors.Select(e => $"{e.Code}: {e.Message}")));
        }

        var result = await _inventoryService.ReceiveNormalizedComponentAsync(
            draft,
            request.ProductTypeId,
            request.LocationId,
            request.Supplier,
            request.ShipmentId,
            request.CollectionFacility,
            request.Volume,
            request.ReleaseToAvailable,
            request.VisualInspectionAcceptable,
            request.VisualInspectionNotes,
            request.Appearance,
            request.SecondVerifier,
            request.ReceiveTemperatureCelsius,
            ct);

        if (result.Succeeded)
        {
            session.IsCompleted = true;
            session.CompletedComponentIdentity = draft.ComponentIdentity;
            _sessions.Update(session);
            await _uow.SaveChangesAsync(ct);
        }

        return result;
    }

    private async Task<(bool Success, IReadOnlyList<string> Errors)> ApplySegmentAsync(
        CanonicalComponentDraft draft,
        CompoundIsbtPayloadSplitter.Segment segment,
        IReadOnlyDictionary<string, AboRhdParser.LookupRow> aboLookup,
        IReadOnlyDictionary<string, ProductParser.LookupRow> productLookup,
        CancellationToken ct)
    {
        await Task.CompletedTask;
        var errors = new List<string>();

        switch (segment.Kind)
        {
            case IsbtDataStructureKind.DonationIdentificationNumber:
            {
                var parsed = DinParser.Parse(segment.Value, _dinCheck);
                if (!parsed.Success)
                    return (false, parsed.Errors.Select(e => $"{e.Code}: {e.Message}").ToList());
                if (draft.Din is not null && draft.Din.Din != parsed.Value!.Din)
                    return (false, new[] { $"{IsbtErrorCodes.MixedUnitSession}: Conflicting DIN in session." });
                if (draft.Din is not null && draft.Din.Din == parsed.Value!.Din && draft.Din.Flags != parsed.Value.Flags)
                    return (false, new[] { $"{IsbtErrorCodes.ConflictingScan}: Conflicting DIN flags." });
                draft.Din = parsed.Value;
                break;
            }
            case IsbtDataStructureKind.AboRhd:
            {
                var parsed = AboRhdParser.ParseScanner(segment.Value, aboLookup);
                if (!parsed.Success)
                    return (false, parsed.Errors.Select(e => $"{e.Code}: {e.Message}").ToList());
                if (draft.AboRhd is not null && draft.AboRhd.AboRhdCode != parsed.Value!.AboRhdCode)
                    return (false, new[] { $"{IsbtErrorCodes.ConflictingScan}: Conflicting ABO/RhD." });
                draft.AboRhd = parsed.Value;
                break;
            }
            case IsbtDataStructureKind.ProductCode:
            {
                var parsed = ProductParser.ParseScanner(segment.Value, productLookup, isNewManufactureOrRelabel: true);
                if (!parsed.Success)
                    return (false, parsed.Errors.Select(e => $"{e.Code}: {e.Message}").ToList());
                if (draft.Product is not null && draft.Product.ProductCodeData != parsed.Value!.ProductCodeData)
                    return (false, new[] { $"{IsbtErrorCodes.ConflictingScan}: Conflicting product data." });
                draft.Product = parsed.Value;
                break;
            }
            case IsbtDataStructureKind.ExpirationDate:
            case IsbtDataStructureKind.ExpirationDateTime:
            {
                var parsed = ExpirationParser.Parse(segment.Value);
                if (!parsed.Success)
                    return (false, parsed.Errors.Select(e => $"{e.Code}: {e.Message}").ToList());
                if (draft.Expiration is not null && draft.Expiration.ExpirationEncoded != parsed.Value!.ExpirationEncoded)
                    return (false, new[] { $"{IsbtErrorCodes.ConflictingScan}: Conflicting expiration." });
                draft.Expiration = parsed.Value;
                break;
            }
            default:
                errors.Add($"{IsbtErrorCodes.UnsupportedDataStructure}: {segment.Kind}");
                return (false, errors);
        }

        return (true, Array.Empty<string>());
    }

    private static IsbtDataStructureKind NormalizeExpirationKind(IsbtDataStructureKind kind) =>
        kind is IsbtDataStructureKind.ExpirationDateTime ? IsbtDataStructureKind.ExpirationDate : kind;

    private async Task<ScanSessionDto> ToDtoAsync(BloodComponentScanSession session, CancellationToken ct)
    {
        var draft = JsonSerializer.Deserialize<CanonicalComponentDraft>(session.DraftJson, JsonOptions)
                    ?? new CanonicalComponentDraft();
        var expected = JsonSerializer.Deserialize<List<IsbtDataStructureKind>>(session.ExpectedStructuresJson, JsonOptions)
                       ?? new List<IsbtDataStructureKind>();
        var received = JsonSerializer.Deserialize<List<IsbtDataStructureKind>>(session.ReceivedStructuresJson, JsonOptions)
                       ?? new List<IsbtDataStructureKind>();

        var identityExists = false;
        if (draft.Din is not null && draft.Product is not null)
        {
            identityExists = await _inventory.ComponentIdentityKeyExistsAsync(
                ComponentIdentityBuilder.BuildUniquenessKey(
                    draft.Din.Din, draft.Product.ProductCodeData, draft.Product.ExtendedDivisionCode), ct);
        }

        var validation = ComponentCrossFieldValidator.Validate(draft, identityExists, _clock.UtcNow);
        return new ScanSessionDto(
            session.SessionKey,
            session.StartedAt,
            session.LastScanAt,
            session.IsCompleted,
            expected,
            received,
            CanonicalComponentMapper.ToSummary(draft),
            validation);
    }
}
