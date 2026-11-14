using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A lot of Rh immune globulin received by the blood bank (end-user testing, 2026-11-12: the lot recorded as given was
/// free text, checked against nothing, so a mistyped or expired lot was accepted and a recall could miss a recipient).
/// Once any lot is registered, RhIG given must name an active registered lot in date when given. Nothing is deleted.
/// </summary>
public class RhigLot : BaseEntity
{
    public string LotNumber { get; set; } = string.Empty;

    /// <summary>Product and manufacturer as on the carton (for example "RhoGAM 300 µg").</summary>
    public string ProductName { get; set; } = string.Empty;

    public DateOnly ExpiresOn { get; set; }

    public int VialsReceived { get; set; }

    public DateTime ReceivedUtc { get; set; }

    public string ReceivedBy { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string? RetiredReason { get; set; }
}
