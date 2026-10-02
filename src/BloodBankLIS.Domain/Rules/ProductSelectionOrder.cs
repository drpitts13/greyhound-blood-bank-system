using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Explainable order for offering available units on a patient chart.
/// Another patient's autologous or directed unit is omitted. This patient's
/// restricted units come first, then ABO/Rh-identical stock, then a compatible
/// substitute. Antigen-negative units precede untested units, which precede
/// units that already need an antigen-negative override. This does not decide
/// issue eligibility and does not assign a compatibility grade.
/// </summary>
public static class ProductSelectionOrder
{
    public const string PatientAutologousCue = "Patient autologous";
    public const string PatientDirectedCue = "Patient directed";
    public const string IdenticalCue = "ABO/Rh identical";
    public const string SubstituteCue = "Compatible substitute";

    public readonly record struct Placement(int Band, int AntigenBand, string Cue);

    public static bool TryPlace(
        DonationRestriction restriction,
        long? reservedPatientId,
        long patientId,
        AboRh patientType,
        AboRh unitType,
        string? antigenNegFlag,
        out Placement placement)
    {
        placement = default;
        var forThisPatient = reservedPatientId == patientId;
        if (AutologousDirectedRule.RequiresRecipient(restriction) && !forThisPatient)
        {
            return false;
        }

        var identical = patientType.Abo == unitType.Abo && patientType.Rh == unitType.Rh;
        int band;
        string cue;
        if (restriction == DonationRestriction.Autologous && forThisPatient)
        {
            band = 0;
            cue = PatientAutologousCue;
        }
        else if (restriction == DonationRestriction.Directed && forThisPatient)
        {
            band = 1;
            cue = PatientDirectedCue;
        }
        else if (identical)
        {
            band = 2;
            cue = IdenticalCue;
        }
        else
        {
            band = 3;
            cue = SubstituteCue;
        }

        placement = new Placement(band, AntigenBand(antigenNegFlag), cue);
        return true;
    }

    /// <summary>
    /// Ready-to-issue cue when this reserved unit is already this patient's autologous or directed unit.
    /// Another patient's restriction, or allogeneic stock, has no cue. Does not decide issue eligibility.
    /// </summary>
    public static string? RestrictedUnitCue(DonationRestriction restriction, long? reservedPatientId, long patientId)
    {
        if (reservedPatientId != patientId)
        {
            return null;
        }

        return restriction switch
        {
            DonationRestriction.Autologous => PatientAutologousCue,
            DonationRestriction.Directed => PatientDirectedCue,
            _ => null
        };
    }

    public static int AntigenBand(string? antigenNegFlag)
    {
        if (string.IsNullOrEmpty(antigenNegFlag))
        {
            return 0;
        }

        return antigenNegFlag == BloodAttributeCompatibilityRule.UntestedFlag ? 1 : 2;
    }
}
