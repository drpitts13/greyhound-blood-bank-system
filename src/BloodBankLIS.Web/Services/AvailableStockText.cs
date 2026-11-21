using BloodBankLIS.Application.Inventory;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The home screen's Available units tile counts units in Available status; those reserved for a patient are not stock
/// another patient can have, so the tile says how many are (end-user testing pass 297; Minimum stock counts them apart
/// since pass 294).
/// </summary>
public static class AvailableStockText
{
    public static string? ReservedNote(IEnumerable<BloodUnitDto> available)
    {
        var reserved = available.Count(u => u.ReservedForPatientId is not null);
        return reserved == 0 ? null : $"{reserved} reserved for patients";
    }
}
