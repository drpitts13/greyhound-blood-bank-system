using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// How long ago the pregnancy event was, on an RhIG evaluation still awaiting RhIG (end-user testing pass 222: Noor's
/// chart showed "Delivery · 2026-11-07 13:27 · Open" ten days later, while the RhIG worklist said how long ago it was).
/// Null once RhIG is given, the record is completed, or the patient is not a candidate.
/// </summary>
public static class RhigEventAgeText
{
    public static string? For(RhigEvaluationDto e, DateTime nowUtc) =>
        e.IsCompleted || e.GivenUtc is not null || e.Decision == RhigDecision.NotCandidate
            ? null
            : $"{RhigEvaluationRule.AgeText(nowUtc - e.EventUtc)} ago, RhIG not given";
}
