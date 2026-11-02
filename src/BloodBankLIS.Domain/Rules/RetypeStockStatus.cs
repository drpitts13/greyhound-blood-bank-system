using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Where a unit goes when it comes back to usable stock (return, quarantine release, hold release). A product that
/// requires an ABO/Rh retype and has no verified matching retype goes back to Received, where the retype worklist
/// lists it and the retype can be recorded. Sending it to Available would strand it: retype is recorded only while a
/// unit is Received, and allocation and routine issue refuse an unretyped unit.
/// </summary>
public static class RetypeStockStatus
{
    public const string PendingReason = "ABO/Rh retype not yet confirmed; back to Received for retype";

    public static UnitStatus ForStock(bool requiresRetype, bool retypeConfirmed) =>
        requiresRetype && !retypeConfirmed ? UnitStatus.Received : UnitStatus.Available;
}
