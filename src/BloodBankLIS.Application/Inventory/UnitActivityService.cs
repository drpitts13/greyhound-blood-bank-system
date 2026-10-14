using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Composed unit timeline from status history, allocation, crossmatch, issue,
/// return, transfusion, and modification tables.
/// </summary>
public sealed class UnitActivityService
{
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<Return> _returns;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<OrderAction> _orderActions;
    private readonly IRepository<ActionDefinition> _actionDefinitions;
    private readonly IRepository<UnitModificationUnit> _modificationUnits;
    private readonly IRepository<UnitModification> _modifications;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<InventoryLocation>? _locations;

    public UnitActivityService(
        IInventoryRepository inventory,
        IRepository<Allocation> allocations,
        IRepository<Crossmatch> crossmatches,
        IRepository<Issue> issues,
        IRepository<Return> returns,
        IRepository<TransfusionEvent> transfusions,
        IRepository<OrderAction> orderActions,
        IRepository<ActionDefinition> actionDefinitions,
        IRepository<UnitModificationUnit> modificationUnits,
        IRepository<UnitModification> modifications,
        IRepository<Patient> patients,
        IRepository<InventoryLocation>? locations = null)
    {
        _inventory = inventory;
        _allocations = allocations;
        _crossmatches = crossmatches;
        _issues = issues;
        _returns = returns;
        _transfusions = transfusions;
        _orderActions = orderActions;
        _actionDefinitions = actionDefinitions;
        _modificationUnits = modificationUnits;
        _modifications = modifications;
        _patients = patients;
        _locations = locations;
    }

    public async Task<IReadOnlyList<UnitActivityDto>> ListAsync(long unitId, CancellationToken ct = default)
    {
        var rows = new List<UnitActivityDto>();
        var history = await _inventory.GetHistoryAsync(unitId, ct);
        var locationNames = await LoadLocationNamesAsync(history, ct);

        foreach (var h in history)
        {
            var from = h.FromStatus?.ToString() ?? "—";
            var place = StatusHistoryLocationCue.Format(h.FromLocationId, h.ToLocationId, locationNames);
            var reason = string.IsNullOrWhiteSpace(h.Reason) ? "" : $" — {h.Reason}";
            var location = place is null ? "" : $" · {place}";
            rows.Add(new UnitActivityDto(
                h.ChangedUtc,
                "Status",
                $"{from} → {h.ToStatus}{location}{reason}",
                h.ChangedBy,
                h.RelatedEntityType,
                h.RelatedEntityId));
        }

        var allocations = await _allocations.ListAsync(x => x.BloodProductId == unitId, ct);
        foreach (var a in allocations)
        {
            rows.Add(new UnitActivityDto(
                a.AllocatedUtc,
                "Allocation",
                $"{a.Status} to patient #{a.PatientId}",
                a.AllocatedBy,
                nameof(Allocation),
                a.Id));
        }

        var allocationOrderIds = allocations
            .Where(a => a.OrderId is long)
            .Select(a => a.OrderId!.Value)
            .Distinct()
            .ToList();

        foreach (var xm in await _crossmatches.ListAsync(x => x.BloodProductId == unitId, ct))
        {
            rows.Add(new UnitActivityDto(
                xm.PerformedUtc,
                "Crossmatch",
                $"{xm.Method} {xm.Result}",
                xm.PerformedBy,
                nameof(Crossmatch),
                xm.Id));
        }

        var issues = await _issues.ListAsync(i => i.BloodProductId == unitId, ct);
        var issueIds = issues.Select(i => i.Id).ToHashSet();
        var patients = issues.Count == 0
            ? new Dictionary<long, Patient>()
            : (await _patients.ListAsync(p => issues.Select(i => i.PatientId).Contains(p.Id), ct))
                .ToDictionary(p => p.Id);
        foreach (var i in issues)
        {
            patients.TryGetValue(i.PatientId, out var patient);
            var who = patient is null ? $"patient #{i.PatientId}" : $"{patient.LastName}, {patient.FirstName}";
            rows.Add(new UnitActivityDto(
                i.IssuedUtc,
                "Issue",
                $"Issued to {who}{(string.IsNullOrWhiteSpace(i.IssuedToLocation) ? "" : $" · {i.IssuedToLocation}")}",
                i.IssuedBy,
                nameof(Issue),
                i.Id));
        }

        foreach (var r in await _returns.ListAsync(x => x.BloodProductId == unitId, ct))
        {
            rows.Add(new UnitActivityDto(
                r.ReturnedUtc,
                "Return",
                r.ReissueEligible ? $"Returned (reissue eligible) — {r.Reason}" : $"Returned — {r.Reason}",
                r.ReturnedBy,
                nameof(Return),
                r.Id));
        }

        if (issueIds.Count > 0)
        {
            foreach (var t in await _transfusions.ListAsync(x => issueIds.Contains(x.IssueId), ct))
            {
                rows.Add(new UnitActivityDto(
                    t.StopUtc ?? t.StartUtc ?? t.CreatedUtc,
                    "Transfusion",
                    t.FinalDisposition.ToString(),
                    t.Transfusionist ?? t.CreatedBy,
                    nameof(TransfusionEvent),
                    t.Id));
            }
        }

        var actions = await _orderActions.ListAsync(
            a => a.SourceBloodUnitId == unitId
                 || a.ResultBloodUnitId == unitId
                 || (allocationOrderIds.Count > 0 && allocationOrderIds.Contains(a.OrderId)),
            ct);
        if (actions.Count > 0)
        {
            var defIds = actions.Select(a => a.ActionDefinitionId).Distinct().ToList();
            var defs = (await _actionDefinitions.ListAsync(d => defIds.Contains(d.Id), ct))
                .ToDictionary(d => d.Id);
            foreach (var action in actions)
            {
                defs.TryGetValue(action.ActionDefinitionId, out var def);
                var name = def?.Name ?? "Modification";
                var when = action.CompletedUtc ?? action.ModifiedUtc ?? action.CreatedUtc;
                rows.Add(new UnitActivityDto(
                    when,
                    "Modification",
                    $"{name} {action.Status}",
                    action.CompletedBy ?? action.ModifiedBy ?? action.CreatedBy,
                    nameof(OrderAction),
                    action.Id));
            }
        }

        var links = await _modificationUnits.ListAsync(u => u.BloodProductId == unitId, ct);
        if (links.Count > 0)
        {
            var modIds = links.Select(l => l.UnitModificationId).Distinct().ToList();
            var mods = (await _modifications.ListAsync(m => modIds.Contains(m.Id), ct))
                .ToDictionary(m => m.Id);
            foreach (var link in links)
            {
                if (!mods.TryGetValue(link.UnitModificationId, out var mod))
                {
                    continue;
                }

                rows.Add(new UnitActivityDto(
                    mod.PerformedUtc,
                    "Modification",
                    $"{mod.ModificationType} ({link.Role}) — {mod.Reason}",
                    mod.PerformedBy,
                    nameof(UnitModification),
                    mod.Id));
            }
        }

        return rows
            .OrderBy(r => r.WhenUtc)
            .ThenBy(r => r.EventType)
            .ToList();
    }

    private async Task<Dictionary<long, string?>> LoadLocationNamesAsync(
        IReadOnlyList<InventoryStatusHistory> history,
        CancellationToken ct)
    {
        var names = new Dictionary<long, string?>();
        if (_locations is null)
        {
            return names;
        }

        var ids = history
            .SelectMany(row => new long?[] { row.FromLocationId, row.ToLocationId })
            .OfType<long>()
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return names;
        }

        foreach (var location in await _locations.ListAsync(location => ids.Contains(location.Id), ct))
        {
            names[location.Id] = location.Name;
        }

        return names;
    }
}
