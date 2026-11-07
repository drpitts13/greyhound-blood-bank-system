using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// An Rh immune globulin (RhIG) evaluation for a patient after a pregnancy event: the facts the blood bank gathered
/// (infant Rh, fetal bleed screen and quantitation) and the decision and dose the reviewer recorded. The application
/// shows the facts on file and checks the record is complete; it does not decide candidacy or calculate a dose
/// (OCD-069).
/// </summary>
public class RhigEvaluation : BaseEntity
{
    public long PatientId { get; set; }

    public RhigContext Context { get; set; } = RhigContext.Postpartum;

    /// <summary>When the delivery or other exposure happened.</summary>
    public DateTime EventUtc { get; set; }

    /// <summary>Infant (or fetal) Rh type when known; Unknown when not tested.</summary>
    public RhType InfantRh { get; set; } = RhType.Unknown;

    public FetalBleedScreen FetalBleedScreen { get; set; } = FetalBleedScreen.NotDone;

    /// <summary>Fetal cells in maternal blood (%) from quantitation, when done.</summary>
    public decimal? FetalBleedPercent { get; set; }

    public RhigDecision Decision { get; set; } = RhigDecision.Undetermined;

    /// <summary>Vials the reviewer recommends, from the facility's dosing procedure.</summary>
    public int? VialsRecommended { get; set; }

    /// <summary>Why the patient is not a candidate, or any note on the decision.</summary>
    public string? DecisionNote { get; set; }

    public string RecordedBy { get; set; } = string.Empty;

    public DateTime RecordedUtc { get; set; }

    public string? CompletedBy { get; set; }

    public DateTime? CompletedUtc { get; set; }
}
