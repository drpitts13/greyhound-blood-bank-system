namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Place phrase for one status-history row. A row whose locations match, including both unassigned, says nothing about a move.
/// A missing name stays the location id. A missing destination is not invented.
/// </summary>
public static class StatusHistoryLocationCue
{
    public static string? Format(
        long? fromLocationId,
        long? toLocationId,
        IReadOnlyDictionary<long, string?>? namesById)
    {
        if (fromLocationId == toLocationId)
        {
            return null;
        }

        var to = toLocationId is null ? null : StorageLocationLabel.Format(toLocationId, namesById);
        var from = fromLocationId is null ? null : StorageLocationLabel.Format(fromLocationId, namesById);
        if (from is null)
        {
            return to is null ? null : $"to {to}";
        }

        return to is null ? $"from {from}" : $"from {from} to {to}";
    }
}
