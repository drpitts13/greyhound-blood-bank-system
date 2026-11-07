using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which results a second person may verify in a batch (OCD-061, decided 2026-11-06): antibody screens and DATs that
/// are plainly Negative, and ABO/Rh results that match the patient's historical type, entered or posted and waiting
/// for verification, entered by someone else, on a patient with no antibody history and no special requirement in
/// effect. Everything else (a first-time type, a discrepancy, a correction, a positive) is verified one at a time.
/// Each result in a batch still passes the normal verification checks.
/// </summary>
public static class BatchVerifyRule
{
    public const string Code = "BATCH-VERIFY";

    public static readonly IReadOnlySet<string> Tests = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ABORH", "ABSC", "DAT" };

    public sealed record Facts(
        string TestCode,
        string? Value,
        ResultStatus Status,
        string? EnteredBy,
        string Verifier,
        AboRh? CurrentType,
        bool HasAntibodyHistory,
        bool HasSpecialRequirementInEffect);

    public static RuleResult Evaluate(Facts f)
    {
        if (!Tests.Contains(f.TestCode))
        {
            return Stop($"{f.TestCode} is not verified in batches.");
        }

        if (f.Status is not (ResultStatus.Entered or ResultStatus.PendingVerification))
        {
            return Stop("Only first results waiting for verification are batched; a correction is verified on its own.");
        }

        if (string.IsNullOrWhiteSpace(f.EnteredBy) || string.Equals(f.EnteredBy.Trim(), f.Verifier.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Stop("You entered this result; another person must verify it.");
        }

        if (f.HasAntibodyHistory)
        {
            return Stop("The patient has antibody history; verify on its own.");
        }

        if (f.HasSpecialRequirementInEffect)
        {
            return Stop("The patient has a special requirement in effect; verify on its own.");
        }

        if (string.Equals(f.TestCode, "ABORH", StringComparison.OrdinalIgnoreCase))
        {
            if (!AboRhResultValue.TryParse(f.Value, out var typed))
            {
                return Stop("The ABO/Rh value cannot be read; verify on its own.");
            }

            if (f.CurrentType is not AboRh current)
            {
                return Stop("No historical type to match (a first type); verify on its own.");
            }

            return current.Abo == typed.Abo && current.Rh == typed.Rh
                ? RuleResult.Pass(Code)
                : Stop("The type differs from the patient's history; verify on its own.");
        }

        return string.Equals(f.Value?.Trim(), "Negative", StringComparison.OrdinalIgnoreCase)
            ? RuleResult.Pass(Code)
            : Stop("Only a plainly Negative result is batched.");
    }

    private static RuleResult Stop(string message) => RuleResult.HardStop(Code, message);
}
