namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which antibody panel lots need replacing soon, so a new lot is ordered and entered before the bench is left without
/// one (a workup cannot start without an active, unexpired lot). Operational only: the warning window is the reagent
/// lots' (<see cref="ReagentLotRule.ExpiringSoonDays"/>), not a clinical rule, and this does not identify antibodies.
/// </summary>
public static class AntibodyPanelLotSupplyRule
{
    public const string NoPanel = "no-panel";
    public const string NoSelected = "no-selected";
    public const string Expiring = "expiring";

    public sealed record Lot(long Id, string PanelName, string LotNumber, string ManufacturerName, DateOnly ExpiresOn, bool IsSelectedCellLot, bool IsActive);

    /// <param name="LotId">The expiring lot; null when no lot of the kind is current.</param>
    /// <param name="Replacement">For an expiring lot: a current lot of the same kind that expires later, if any.</param>
    public sealed record Item(string Kind, bool IsSelectedCellLot, Lot? Lot, Lot? Replacement, int? DaysLeft, string Message)
    {
        /// <summary>Needs someone to act: no current lot, or a lot expiring with nothing entered to follow it.</summary>
        public bool NeedsAction => Kind != Expiring || Replacement is null;
    }

    public static IReadOnlyList<Item> Review(IEnumerable<Lot> lots, DateOnly today)
    {
        var all = lots.ToList();
        var items = new List<Item>();
        foreach (var selected in new[] { false, true })
        {
            var kind = all.Where(l => l.IsSelectedCellLot == selected).ToList();
            var current = kind.Where(l => l.IsActive && l.ExpiresOn >= today).OrderBy(l => l.ExpiresOn).ToList();
            var noun = selected ? "selected-cell lot" : "primary panel lot";
            if (current.Count == 0)
            {
                // A facility that has never entered a selected-cell lot is not told it lacks one.
                if (!selected || kind.Count > 0)
                {
                    items.Add(new Item(selected ? NoSelected : NoPanel, selected, null, null, null,
                        $"No active, unexpired {noun}: {(selected ? "selected cells cannot be added to a workup" : "an antibody workup cannot be started")} until one is entered."));
                }

                continue;
            }

            foreach (var lot in current.Where(l => l.ExpiresOn.DayNumber - today.DayNumber <= ReagentLotRule.ExpiringSoonDays))
            {
                var daysLeft = lot.ExpiresOn.DayNumber - today.DayNumber;
                var replacement = current.Where(l => l.Id != lot.Id && l.ExpiresOn > lot.ExpiresOn)
                    .OrderByDescending(l => l.ExpiresOn)
                    .FirstOrDefault();
                var when = daysLeft switch { 0 => "today", 1 => "tomorrow", _ => $"in {daysLeft} days" };
                var message = $"{lot.PanelName} lot {lot.LotNumber} expires {when} ({lot.ExpiresOn:yyyy-MM-dd}); " +
                    (replacement is null
                        ? $"no other {noun} is entered to follow it."
                        : $"{replacement.PanelName} lot {replacement.LotNumber} follows it (expires {replacement.ExpiresOn:yyyy-MM-dd}).");
                items.Add(new Item(Expiring, selected, lot, replacement, daysLeft, message));
            }
        }

        return items.OrderByDescending(i => i.NeedsAction).ThenBy(i => i.DaysLeft ?? -1).ToList();
    }
}
