using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A piece of blood bank equipment that needs scheduled checks: a centrifuge, cell washer, heat block, irradiator,
/// thawer, platelet agitator, thermometer or pipette (Cycle 708). Storage refrigerators and freezers keep their own
/// temperature log. Nothing is deleted: equipment is deactivated, and taken out of service with a reason.
/// </summary>
public class Equipment : BaseEntity
{
    /// <summary>Asset tag or short code, upper case; what a modification's device field is matched against.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Centrifuge, Cell washer, Heat block, Irradiator, ... (free text; the page offers the common kinds).</summary>
    public string Kind { get; set; } = string.Empty;

    public string? Location { get; set; }

    public string? SerialNumber { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Set while the equipment must not be used; cleared when it is returned to service.</summary>
    public DateTime? OutOfServiceUtc { get; set; }

    public string? OutOfServiceReason { get; set; }

    public string? OutOfServiceBy { get; set; }
}

/// <summary>
/// A check the facility schedules for one piece of equipment (daily QC, a monthly timer check, an annual calibration),
/// due again a set number of days after it last passed. The interval is the facility's, from its SOP or the
/// manufacturer: none is built in.
/// </summary>
public class EquipmentCheck : BaseEntity
{
    public long EquipmentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int IntervalDays { get; set; }

    /// <summary>What to do or what passes, from the SOP; shown when the check is recorded.</summary>
    public string? Instructions { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>One time a check was performed: who, when, passed or failed, and what was seen or done.</summary>
public class EquipmentCheckRecord : BaseEntity
{
    public long EquipmentCheckId { get; set; }

    public long EquipmentId { get; set; }

    public DateTime PerformedUtc { get; set; }

    public string PerformedBy { get; set; } = string.Empty;

    public bool Passed { get; set; }

    /// <summary>The reading or result (for example "3400 rpm", "37.1 °C"), and for a failure what was found or done.</summary>
    public string? Note { get; set; }
}
