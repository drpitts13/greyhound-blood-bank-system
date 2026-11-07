using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Values interfaces needed that had no translation, for the interface team: recorded after each message (counted, not
/// repeated), resolved when a translation for them is saved, or dismissed with a reason.
/// </summary>
public sealed class InterfaceUnmappedValueService
{
    private readonly IRepository<InterfaceUnmappedValue> _values;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser? _currentUser;
    private readonly IAuditWriter? _audit;
    private readonly IRepository<InterfaceEndpoint>? _endpoints;

    public InterfaceUnmappedValueService(
        IRepository<InterfaceUnmappedValue> values,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser? currentUser = null,
        IAuditWriter? audit = null,
        IRepository<InterfaceEndpoint>? endpoints = null)
    {
        _values = values;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _endpoints = endpoints;
    }

    /// <summary>Adds or counts each miss of one message. Saves the changes.</summary>
    public async Task RecordAsync(IReadOnlyList<TranslationMiss> misses, long? endpointId, string? messageControlId, CancellationToken ct = default)
    {
        if (misses.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;
        foreach (var miss in misses)
        {
            var key = miss.DataItemKey;
            var value = Truncate(miss.Value, 200);
            var direction = miss.Direction;
            var open = (await _values.ListAsync(v => v.ResolvedUtc == null && v.EndpointId == endpointId
                    && v.DataItemKey == key && v.Direction == direction, ct))
                .FirstOrDefault(v => string.Equals(v.Value, value, StringComparison.OrdinalIgnoreCase));
            if (open is null)
            {
                await _values.AddAsync(new InterfaceUnmappedValue
                {
                    EndpointId = endpointId,
                    DataItemKey = key,
                    Value = value,
                    Direction = direction,
                    FirstSeenUtc = now,
                    LastSeenUtc = now,
                    Count = 1,
                    LastMessageControlId = Truncate(messageControlId, 100)
                }, ct);
                continue;
            }

            var tracked = await _values.GetByIdAsync(open.Id, ct) ?? open;
            tracked.Count++;
            tracked.LastSeenUtc = now;
            tracked.LastMessageControlId = Truncate(messageControlId, 100) ?? tracked.LastMessageControlId;
            _values.Update(tracked);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<InterfaceUnmappedValueDto>> ListAsync(bool openOnly, CancellationToken ct = default)
    {
        var since = _clock.UtcNow.AddDays(-30);
        var rows = openOnly
            ? await _values.ListAsync(v => v.ResolvedUtc == null, ct)
            : await _values.ListAsync(v => v.ResolvedUtc == null || v.ResolvedUtc >= since, ct);
        var endpoints = _endpoints is null
            ? new Dictionary<long, string>()
            : (await _endpoints.ListAsync(ct)).ToDictionary(e => e.Id, e => e.Name);
        var names = InterfaceDataItemCatalog.Translatable().ToDictionary(i => i.Key, i => i.DisplayName, StringComparer.Ordinal);
        return rows
            .OrderBy(v => v.ResolvedUtc is null ? 0 : 1)
            .ThenByDescending(v => v.LastSeenUtc)
            .Select(v => new InterfaceUnmappedValueDto(
                v.Id, v.EndpointId, v.EndpointId is long id ? endpoints.GetValueOrDefault(id) : null,
                v.DataItemKey, names.GetValueOrDefault(v.DataItemKey, v.DataItemKey), v.Value, v.Direction,
                v.FirstSeenUtc, v.LastSeenUtc, v.Count, v.LastMessageControlId, v.ResolvedUtc, v.ResolvedBy, v.ResolvedNote))
            .ToList();
    }

    public async Task<int> CountOpenAsync(CancellationToken ct = default) =>
        (await _values.ListAsync(v => v.ResolvedUtc == null, ct)).Count;

    /// <summary>Closes an unmapped value without a translation (for example a test code sent in error). Audited.</summary>
    public async Task<OperationResult<InterfaceUnmappedValueDto>> DismissAsync(long id, string? reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<InterfaceUnmappedValueDto>.Fail("Say why this value needs no translation.");
        }

        var row = await _values.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<InterfaceUnmappedValueDto>.Fail("Unmapped value not found.");
        }

        if (row.ResolvedUtc is not null)
        {
            return OperationResult<InterfaceUnmappedValueDto>.Fail("This value is already resolved.");
        }

        Resolve(row, Truncate(reason.Trim(), 500)!);
        _audit?.Record(AuditEventType.Interface, nameof(InterfaceUnmappedValue), row.Id,
            oldValue: null, newValue: new { row.DataItemKey, row.Value, row.Direction, row.EndpointId },
            reason: $"Unmapped interface value dismissed: {row.ResolvedNote}");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<InterfaceUnmappedValueDto>.Ok((await ListAsync(false, ct)).First(v => v.Id == row.Id));
    }

    /// <summary>Resolves the open values a newly saved translation now covers. The caller saves.</summary>
    public async Task ResolveCoveredAsync(string dataItemKey, long? endpointId, IReadOnlyList<InterfaceValueTranslation> rows, CancellationToken ct = default)
    {
        var keys = InterfaceValueSets.IsProcedureKey(dataItemKey)
            ? new[] { InterfaceDataItemKeys.ProcedureCode, InterfaceDataItemKeys.OrderTestCode, InterfaceDataItemKeys.ResultObrTestCode }
            : new[] { dataItemKey };
        var open = await _values.ListAsync(v => v.ResolvedUtc == null && keys.Contains(v.DataItemKey), ct);
        foreach (var value in open.Where(v => endpointId is null || v.EndpointId == endpointId))
        {
            var inbound = value.Direction == InterfaceTranslationDirection.Inbound;
            var covered = rows.Any(r => (inbound ? InterfaceValueTranslator.AppliesInbound(r.Direction) : InterfaceValueTranslator.AppliesOutbound(r.Direction))
                && string.Equals(inbound ? r.ExternalValue : r.InternalValue, value.Value, StringComparison.OrdinalIgnoreCase));
            if (covered)
            {
                var tracked = await _values.GetByIdAsync(value.Id, ct) ?? value;
                Resolve(tracked, "Translation saved.");
            }
        }
    }

    private void Resolve(InterfaceUnmappedValue row, string note)
    {
        row.ResolvedUtc = _clock.UtcNow;
        row.ResolvedBy = _currentUser?.UserName;
        row.ResolvedNote = note;
        _values.Update(row);
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
