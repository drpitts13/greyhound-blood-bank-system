using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Rules.PatientWorkspace;

public static class EncounterValidator
{
    public static RuleEvaluation Validate(Encounter e)
    {
        var results = new List<RuleResult>();

        if (e.PatientId <= 0)
        {
            results.Add(RuleResult.HardStop("ENCOUNTER.PATIENT.REQUIRED", "Patient is required."));
        }

        if (string.IsNullOrWhiteSpace(e.VisitNumber))
        {
            results.Add(RuleResult.HardStop("ENCOUNTER.VISITNUMBER.REQUIRED", "Hospital visit number is required."));
        }

        if (e.AdmitUtc.HasValue && e.DischargeUtc.HasValue && e.DischargeUtc < e.AdmitUtc)
        {
            results.Add(RuleResult.HardStop("ENCOUNTER.DISCHARGE.BEFORE.ADMIT", "Discharge date/time cannot be before admission date/time."));
        }

        if (e.Status == EncounterStatus.Active && e.DischargeUtc.HasValue && e.AdmitUtc.HasValue && e.DischargeUtc < e.AdmitUtc)
        {
            results.Add(RuleResult.HardStop("ENCOUNTER.ACTIVE.INVALID.DATES", "Active visit cannot have discharge before admission."));
        }

        return new RuleEvaluation(results);
    }
}
