using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Compliance;

/// <summary>
/// Reads the workup specimens and their Test Group results for reaction investigations. The current result of a
/// line is the one not superseded, highest version, the same as the test worklist.
/// </summary>
public static class ReactionWorkupReader
{
    /// <summary>
    /// The workup of one investigation. A role whose order was cancelled or discontinued counts as not chosen
    /// (<see cref="PreCancelled"/>, <see cref="PostCancelled"/>), so the case asks for that specimen again instead of
    /// waiting for tests that will never be resulted.
    /// </summary>
    public sealed record Workup(
        ReactionWorkupSpecimenDto? Pre,
        ReactionWorkupSpecimenDto? Post,
        bool PreCancelled = false,
        bool PostCancelled = false);

    public static bool IsWithdrawn(OrderStatus status) => status is OrderStatus.Cancelled or OrderStatus.Discontinued;

    public static async Task<IReadOnlyDictionary<long, Workup>> LoadAsync(
        IReadOnlyList<ReactionInvestigation> rows,
        IRepository<Specimen>? specimens,
        IRepository<Order>? orders,
        IRepository<OrderLine>? orderLines,
        IRepository<TestResult>? results,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var withWorkup = rows.Where(r => r.PreTransfusionOrderId is not null || r.PostTransfusionOrderId is not null).ToList();
        if (withWorkup.Count == 0 || specimens is null || orders is null || orderLines is null || results is null)
        {
            return new Dictionary<long, Workup>();
        }

        var orderIds = withWorkup
            .SelectMany(r => new[] { r.PreTransfusionOrderId, r.PostTransfusionOrderId })
            .OfType<long>()
            .Distinct()
            .ToList();
        var specimenIds = withWorkup
            .SelectMany(r => new[] { r.PreTransfusionSpecimenId, r.PostTransfusionSpecimenId })
            .OfType<long>()
            .Distinct()
            .ToList();

        var orderById = (await orders.ListAsync(o => orderIds.Contains(o.Id), ct)).ToDictionary(o => o.Id);
        var specimenById = (await specimens.ListAsync(s => specimenIds.Contains(s.Id), ct)).ToDictionary(s => s.Id);
        var lines = (await orderLines.ListAsync(
                l => orderIds.Contains(l.OrderId) && l.IsActive && l.LineCategory == OrderCategory.Test, ct))
            .ToLookup(l => l.OrderId);
        var current = (await results.ListAsync(
                r => r.OrderId != null && orderIds.Contains(r.OrderId.Value) && r.SupersededByResultId == null, ct))
            .GroupBy(r => (r.OrderId!.Value, r.SpecimenId, Code: r.TestCode.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Version).First());

        ReactionWorkupSpecimenDto? Build(string role, long? specimenId, long? orderId)
        {
            if (specimenId is not long sid || orderId is not long oid
                || !specimenById.TryGetValue(sid, out var specimen) || !orderById.TryGetValue(oid, out var order)
                || IsWithdrawn(order.Status))
            {
                return null;
            }

            var workupLines = lines[oid]
                .Where(l => !string.IsNullOrWhiteSpace(l.TestCode))
                .OrderBy(l => l.LineNumber)
                .Select(l =>
                {
                    current.TryGetValue((oid, sid, l.TestCode!.ToUpperInvariant()), out var result);
                    return new ReactionWorkupLineDto(
                        l.Id,
                        l.TestCode!,
                        string.IsNullOrWhiteSpace(l.LineName) ? l.TestCode! : l.LineName,
                        result?.Status,
                        result?.Value,
                        result?.Status == ResultStatus.Verified);
                })
                .ToList();
            return new ReactionWorkupSpecimenDto(
                role,
                specimen.Id,
                specimen.AccessionNumber,
                specimen.CollectedUtc,
                specimen.ExpiresUtc,
                specimen.ExpiresUtc is DateTime expires && expires <= nowUtc,
                order.Id,
                order.OrderNumber,
                workupLines);
        }

        bool Withdrawn(long? orderId) =>
            orderId is long oid && orderById.TryGetValue(oid, out var order) && IsWithdrawn(order.Status);

        return withWorkup.ToDictionary(
            r => r.Id,
            r => new Workup(
                Build(ReactionWorkupSpecimenDto.PreRole, r.PreTransfusionSpecimenId, r.PreTransfusionOrderId),
                Build(ReactionWorkupSpecimenDto.PostRole, r.PostTransfusionSpecimenId, r.PostTransfusionOrderId),
                Withdrawn(r.PreTransfusionOrderId),
                Withdrawn(r.PostTransfusionOrderId)));
    }

    /// <summary>
    /// The close-gate state. An order on the case that cannot be read counts as one test still to verify, so a
    /// missing read never lets a case close.
    /// </summary>
    public static Domain.Rules.ReactionWorkupCompletenessRule.WorkupState StateFor(ReactionInvestigation row, Workup? workup)
    {
        var preLinked = row.PreTransfusionOrderId is not null && workup?.PreCancelled != true;
        var postLinked = row.PostTransfusionOrderId is not null && workup?.PostCancelled != true;
        var postDat = workup?.Post?.VerifiedValue("DAT");
        return new Domain.Rules.ReactionWorkupCompletenessRule.WorkupState(
            preLinked,
            postLinked,
            workup?.Pre?.Pending ?? (preLinked ? 1 : 0),
            workup?.Post?.Pending ?? (postLinked ? 1 : 0),
            string.Equals(postDat?.Trim(), "Positive", StringComparison.OrdinalIgnoreCase));
    }
}
