using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Isbt128;

/// <summary>
/// Table-driven, versioned compatibility engine. Does not hardcode clinical tables in UI.
/// Exact compatibility matrices are validated configuration — MEDICAL_DIRECTOR_APPROVAL /
/// ICCBBA_VALIDATION_REQUIRED as applicable. Falls back to existing pure domain rules.
/// </summary>
public sealed class CompatibilityRulesEngine
{
    public sealed record EvaluateRequest(
        ComponentClass ComponentClass,
        AboRh ComponentAboRh,
        AboRh PatientAboRh,
        bool PatientBloodTypeKnown,
        int? PatientAgeYears,
        string? PatientSex,
        bool CurrentAntibodyScreenNegative,
        bool HasHistoricalAntibodies,
        bool RequiresIrradiation,
        bool RequiresLeukoreduction,
        bool RequiresCmvStrategy,
        bool RequiresHbsNegative,
        bool RequiresWashed,
        bool PathogenReduced,
        bool UnitExpired,
        bool EmergencyRelease,
        bool ElectronicCrossmatchEligible);

    public sealed record Decision(
        CompatibilityOutcome Outcome,
        CompatibilityPathway Pathway,
        IReadOnlyList<RuleResult> SatisfiedRules,
        IReadOnlyList<RuleResult> Warnings,
        IReadOnlyList<RuleResult> HardStops,
        IReadOnlyList<string> RequiredApprovals,
        string PolicyVersion,
        string RulesVersion);

    private readonly IRepository<CompatibilityRuleVersion> _versions;
    private readonly IRepository<BloodComponentCompatibilityDecision> _decisions;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;

    public CompatibilityRulesEngine(
        IRepository<CompatibilityRuleVersion> versions,
        IRepository<BloodComponentCompatibilityDecision> decisions,
        IUnitOfWork uow,
        IClock clock,
        ICurrentUser user)
    {
        _versions = versions;
        _decisions = decisions;
        _uow = uow;
        _clock = clock;
        _user = user;
    }

    public async Task<Decision> EvaluateAsync(EvaluateRequest request, CancellationToken ct = default)
    {
        var version = (await _versions.ListAsync(v => v.IsActive, ct))
            .OrderByDescending(v => v.EffectiveDate)
            .FirstOrDefault();

        var policyVersion = version?.PolicyVersion ?? "PLACEHOLDER-POLICY";
        var rulesVersion = version?.Version ?? "PLACEHOLDER-RULES";

        var results = new List<RuleResult>();

        if (request.UnitExpired)
            results.Add(RuleResult.HardStop(IsbtErrorCodes.ComponentExpired, "Component is expired."));

        if (request.PatientBloodTypeKnown && request.ComponentAboRh.IsKnown)
            results.AddRange(AboCompatibilityRule.Evaluate(request.PatientAboRh, request.ComponentAboRh, request.ComponentClass));

        // Rule-family selection — do not assume all products use RBC logic.
        var pathway = SelectPathway(request);
        if (request.EmergencyRelease)
        {
            pathway = CompatibilityPathway.EmergencyRelease;
            results.Add(RuleResult.Warning(
                IsbtErrorCodes.EmergencyReleaseAuthorizationRequired,
                "Emergency release pathway — unit must not be marked Compatible."));
        }

        if (version is not null)
        {
            foreach (var rule in version.Rules.Where(r => r.IsActive && r.ComponentClass == request.ComponentClass))
            {
                // ExpressionJson is facility configuration; placeholder evaluates metadata only.
                // INSTITUTIONAL_POLICY_REVIEW: replace with validated expression evaluator.
                if (rule.Severity.Equals("HardStop", StringComparison.OrdinalIgnoreCase)
                    && rule.ExpressionJson.Contains("\"alwaysFail\":true", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(RuleResult.HardStop(rule.RuleCode, rule.Description));
                }
                else
                {
                    results.Add(RuleResult.Pass(rule.RuleCode, rule.Description));
                }
            }
        }

        var evaluation = new RuleEvaluation(results);
        var hardStops = evaluation.HardStops.ToList();
        var warnings = evaluation.Warnings.ToList();
        var satisfied = evaluation.Results.Where(r => r.Severity == RuleSeverity.Pass).ToList();

        CompatibilityOutcome outcome;
        if (hardStops.Count > 0)
            outcome = CompatibilityOutcome.Incompatible;
        else if (warnings.Count > 0 || request.EmergencyRelease)
            outcome = CompatibilityOutcome.RequiresOverride;
        else
            outcome = CompatibilityOutcome.Compatible;

        var approvals = new List<string>();
        if (request.EmergencyRelease)
            approvals.Add(PermissionCodes.IssueEmergencyRelease);
        if (warnings.Count > 0)
            approvals.Add(PermissionCodes.IssueOverride);

        return new Decision(
            outcome,
            pathway,
            satisfied,
            warnings,
            hardStops,
            approvals,
            policyVersion,
            rulesVersion);
    }

    public async Task PersistDecisionAsync(
        long bloodProductId,
        long patientId,
        long? orderId,
        Decision decision,
        CancellationToken ct = default)
    {
        await _decisions.AddAsync(new BloodComponentCompatibilityDecision
        {
            BloodProductId = bloodProductId,
            PatientId = patientId,
            OrderId = orderId,
            Outcome = decision.Outcome,
            Pathway = decision.Pathway,
            SatisfiedRulesJson = JsonSerializer.Serialize(decision.SatisfiedRules),
            WarningsJson = JsonSerializer.Serialize(decision.Warnings),
            HardStopsJson = JsonSerializer.Serialize(decision.HardStops),
            RequiredApprovalsJson = JsonSerializer.Serialize(decision.RequiredApprovals),
            PolicyVersion = decision.PolicyVersion,
            RulesVersion = decision.RulesVersion,
            EvaluatedAt = _clock.UtcNow,
            EvaluatedBy = _user.UserName
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private static CompatibilityPathway SelectPathway(EvaluateRequest request)
    {
        if (request.ComponentClass is ComponentClass.Plasma or ComponentClass.Cryoprecipitate
            or ComponentClass.Platelets or ComponentClass.Other)
        {
            // Many plasma/platelet pathways are no-crossmatch when ABO-compatible — policy-driven.
            // INSTITUTIONAL_POLICY_REVIEW.
            return CompatibilityPathway.NoCrossmatch;
        }

        if (request.ElectronicCrossmatchEligible
            && request.CurrentAntibodyScreenNegative
            && !request.HasHistoricalAntibodies)
        {
            return CompatibilityPathway.ElectronicCrossmatch;
        }

        return CompatibilityPathway.SerologicAhg;
    }
}
