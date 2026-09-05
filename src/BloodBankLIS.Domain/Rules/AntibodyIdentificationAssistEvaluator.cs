using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Advisory antibody-identification engine. Applies rule-out, rule-in, dosage,
/// phenotype/genotype comparison, historical antibodies, autocontrol, and DAT
/// context. It never classifies a specificity as Identified.
/// </summary>
public static class AntibodyIdentificationAssistEvaluator
{
    public const string AutocontrolPositiveCode = "ABID-AC-POS";
    public const string DatIndicatedCode = "ABID-DAT-INDICATED";
    public const string DatPositiveCode = "ABID-DAT-POS";
    public const string PhenotypeConflictCode = "ABID-PHENO-CONFLICT";
    public const string GenotypeConflictCode = "ABID-GENO-CONFLICT";
    public const string HistoricalUndetectedCode = "ABID-HIST-UNDETECTED";
    public const string IncompleteReactionsCode = "ABID-INCOMPLETE-RXN";
    public const string SelectedCellNeededCode = "ABID-SEL-CELL";

    public static AntibodyIdentificationAssistResult Evaluate(AntibodyIdentificationAssistInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var warnings = new List<RuleResult>
        {
            AntibodyIdentificationInterpretationRule.AssistIsAdvisory()
        };

        var cells = input.Cells ?? [];
        var phases = input.InterpretivePhases.Count > 0
            ? input.InterpretivePhases
            : ["IS", "37C", "AHG"];

        if (HasIncompleteInterpretiveReactions(cells, phases))
        {
            warnings.Add(RuleResult.Warning(
                IncompleteReactionsCode,
                "One or more panel or selected cells are missing interpretive-phase reactions. Assistance used the reactions that are present."));
        }

        AddAutocontrolAndDatWarnings(cells, phases, input.Dat, warnings);

        var findings = new List<AntibodyIdentificationAssistFinding>();
        foreach (var antigen in input.Antigens)
        {
            findings.Add(EvaluateAntigen(antigen, cells, phases, input.Policy, input.PatientAntigens, warnings));
        }

        AddHistoricalFindings(input.HistoricalAntibodies, findings, warnings);
        AddSelectedCellRecommendations(findings, input.Policy, warnings);

        return new AntibodyIdentificationAssistResult(findings, new RuleEvaluation(warnings));
    }

    private static AntibodyIdentificationAssistFinding EvaluateAntigen(
        AntibodyIdAntigenInfo antigen,
        IReadOnlyList<AntibodyIdentificationCellSnapshot> cells,
        IReadOnlyList<string> phases,
        AntibodyIdentificationPolicy policy,
        IReadOnlyList<PatientAntigenSnapshot> patientAntigens,
        List<RuleResult> warnings)
    {
        var dosageSensitive = policy.IsDosageSensitive(antigen.Code);
        var homozygous = 0;
        var heterozygous = 0;
        var concordantPositives = 0;
        var discordantPositives = 0;
        var antigenPositiveNegatives = 0;

        foreach (var cell in cells.Where(c => c.Role != PanelCellRole.Autocontrol))
        {
            if (!cell.Antigens.TryGetValue(antigen.Code, out var expression)
                || expression == AntigenExpression.NotTested)
            {
                continue;
            }

            var reaction = Summarize(cell, phases);
            if (reaction == CellReaction.Incomplete)
            {
                continue;
            }

            if (AntigenExpressionInfo.IsPresent(expression))
            {
                if (reaction == CellReaction.Negative)
                {
                    antigenPositiveNegatives++;
                    if (CountsAsHomozygousExclusion(expression, dosageSensitive))
                    {
                        homozygous++;
                    }
                    else
                    {
                        heterozygous++;
                    }
                }
                else if (reaction == CellReaction.Positive)
                {
                    concordantPositives++;
                }
            }
            else if (AntigenExpressionInfo.IsAbsent(expression) && reaction == CellReaction.Positive)
            {
                discordantPositives++;
            }
        }

        var excluded = IsExcluded(policy, dosageSensitive, homozygous, heterozygous);
        var cannotExcludeForDosage = !excluded
            && dosageSensitive
            && heterozygous > 0
            && homozygous < policy.MinHomozygousExclusions;

        AntibodyIdClassification classification;
        string rationale;

        if (excluded)
        {
            classification = AntibodyIdClassification.Excluded;
            rationale = BuildExclusionRationale(antigen.AntibodyName, policy, dosageSensitive, homozygous, heterozygous);
        }
        else if (discordantPositives > 0)
        {
            classification = cannotExcludeForDosage
                ? AntibodyIdClassification.CannotExclude
                : AntibodyIdClassification.Inconclusive;
            rationale = cannotExcludeForDosage
                ? $"{antigen.AntibodyName} is not excluded: only heterozygous (single-dose) cells were negative and dosage-aware evaluation is on. Positive reactions on antigen-negative cells also mean this specificity alone does not explain the panel."
                : $"{antigen.AntibodyName} does not explain the panel: {discordantPositives} antigen-negative cell(s) reacted.";
        }
        else if (concordantPositives > 0 && antigenPositiveNegatives == 0)
        {
            classification = AntibodyIdClassification.Possible;
            rationale = $"{antigen.AntibodyName} is pattern-consistent on the cells tested ({concordantPositives} antigen-positive cell(s) reactive; no antigen-negative cells reactive). This is assistance only — not an identification.";
        }
        else if (cannotExcludeForDosage)
        {
            classification = AntibodyIdClassification.CannotExclude;
            rationale = $"{antigen.AntibodyName} cannot be ruled out: {heterozygous} heterozygous negative cell(s) and {homozygous} homozygous negative cell(s). Dosage-aware evaluation requires {policy.MinHomozygousExclusions} homozygous exclusion(s).";
        }
        else
        {
            classification = AntibodyIdClassification.Inconclusive;
            rationale = $"{antigen.AntibodyName} is neither excluded nor pattern-consistent on the reactions entered.";
        }

        classification = ApplyPhenotypeComparison(
            antigen, classification, ref rationale, patientAntigens, warnings);

        return new AntibodyIdentificationAssistFinding(
            antigen.AntibodyName,
            antigen.Code,
            classification,
            rationale,
            homozygous,
            heterozygous,
            concordantPositives,
            discordantPositives);
    }

    private static AntibodyIdClassification ApplyPhenotypeComparison(
        AntibodyIdAntigenInfo antigen,
        AntibodyIdClassification classification,
        ref string rationale,
        IReadOnlyList<PatientAntigenSnapshot> patientAntigens,
        List<RuleResult> warnings)
    {
        var match = patientAntigens.FirstOrDefault(p =>
            string.Equals(p.AttributeCode, antigen.Code, StringComparison.Ordinal)
            && p.Result == AntigenResult.Positive);
        if (match is null || classification is not AntibodyIdClassification.Possible)
        {
            return classification;
        }

        var code = match.FromGenotype ? GenotypeConflictCode : PhenotypeConflictCode;
        var source = match.FromGenotype ? "predicted genotype" : "phenotype";
        warnings.Add(RuleResult.Warning(
            code,
            $"{antigen.AntibodyName} is pattern-consistent, but the patient {source} is {antigen.Code}-positive. Alloantibody of this specificity is unexpected. Technologist judgment is required."));
        rationale += $" Patient {source} is {antigen.Code}-positive; assistance will not propose this as a possible alloantibody.";
        return AntibodyIdClassification.Inconclusive;
    }

    private static void AddSelectedCellRecommendations(
        IReadOnlyList<AntibodyIdentificationAssistFinding> findings,
        AntibodyIdentificationPolicy policy,
        List<RuleResult> warnings)
    {
        var needed = findings
            .Where(f => f.Classification == AntibodyIdClassification.CannotExclude
                        && f.HomozygousExclusions < policy.MinHomozygousExclusions)
            .Select(f => f.Specificity)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (needed.Count == 0)
        {
            return;
        }

        warnings.Add(RuleResult.Warning(
            SelectedCellNeededCode,
            "Assistance cannot exclude "
            + string.Join(", ", needed)
            + " without a homozygous (double-dose) selected cell. Attach an in-date selected-cell lot and record those reactions. This is not an identification."));
    }

    private static void AddHistoricalFindings(
        IReadOnlyList<HistoricalAntibodySnapshot> history,
        List<AntibodyIdentificationAssistFinding> findings,
        List<RuleResult> warnings)
    {
        foreach (var historical in history)
        {
            findings.Add(new AntibodyIdentificationAssistFinding(
                historical.Specificity,
                historical.AttributeCode,
                AntibodyIdClassification.Historical,
                historical.IsActive
                    ? $"{historical.Specificity} is on the patient's antibody history and must be considered regardless of the current panel."
                    : $"{historical.Specificity} is historical / currently undetectable and remains on the record.",
                0, 0, 0, 0));

            var excludedNow = findings.Any(f =>
                f.Classification == AntibodyIdClassification.Excluded
                && (CodesMatch(f.AttributeCode, historical.AttributeCode)
                    || string.Equals(f.Specificity, historical.Specificity, StringComparison.OrdinalIgnoreCase)));
            if (excludedNow)
            {
                warnings.Add(RuleResult.Warning(
                    HistoricalUndetectedCode,
                    $"{historical.Specificity} is on antibody history but the current panel reactions would exclude it. History is not removed. Technologist judgment is required."));
            }
        }
    }

    private static void AddAutocontrolAndDatWarnings(
        IReadOnlyList<AntibodyIdentificationCellSnapshot> cells,
        IReadOnlyList<string> phases,
        AntibodyIdDatResult dat,
        List<RuleResult> warnings)
    {
        var autocontrolPositive = cells
            .Where(c => c.Role == PanelCellRole.Autocontrol)
            .Any(c => Summarize(c, phases) == CellReaction.Positive);

        if (autocontrolPositive)
        {
            warnings.Add(RuleResult.Warning(
                AutocontrolPositiveCode,
                "Autocontrol is reactive. Consider autoantibody, recently transfused cells, or drug-related reactivity. Autocontrol is not used to rule out alloantibodies."));
        }

        if (dat is AntibodyIdDatResult.PositiveIgG or AntibodyIdDatResult.PositiveC3 or AntibodyIdDatResult.PositiveBoth)
        {
            warnings.Add(RuleResult.Warning(
                DatPositiveCode,
                "DAT is positive. Elution and clinical correlation may be indicated. DAT does not identify an alloantibody."));
        }
        else if (autocontrolPositive && dat == AntibodyIdDatResult.NotPerformed)
        {
            warnings.Add(RuleResult.Warning(
                DatIndicatedCode,
                "Autocontrol is reactive and DAT has not been recorded. Perform DAT when applicable before final interpretation."));
        }
    }

    public static bool HasIncompleteInterpretiveReactions(
        IReadOnlyList<AntibodyIdentificationCellSnapshot> cells,
        IReadOnlyList<string> phases) =>
        cells
            .Where(c => c.Role != PanelCellRole.Autocontrol)
            .Any(c => Summarize(c, phases) == CellReaction.Incomplete);

    private static bool IsExcluded(
        AntibodyIdentificationPolicy policy,
        bool dosageSensitive,
        int homozygous,
        int heterozygous)
    {
        if (homozygous >= policy.MinHomozygousExclusions)
        {
            return true;
        }

        if (dosageSensitive)
        {
            return false;
        }

        return heterozygous >= policy.MinHeterozygousExclusions;
    }

    private static bool CountsAsHomozygousExclusion(AntigenExpression expression, bool dosageSensitive)
    {
        if (expression == AntigenExpression.Homozygous)
        {
            return true;
        }

        // Unspecified "present" is treated as homozygous-quality only when dosage is not in play.
        return expression == AntigenExpression.Present && !dosageSensitive;
    }

    private static string BuildExclusionRationale(
        string antibodyName,
        AntibodyIdentificationPolicy policy,
        bool dosageSensitive,
        int homozygous,
        int heterozygous)
    {
        if (dosageSensitive)
        {
            return $"{antibodyName} is ruled out by {homozygous} homozygous (double-dose) negative cell(s) (minimum {policy.MinHomozygousExclusions}). This is assistance only.";
        }

        return $"{antibodyName} is ruled out by {homozygous} antigen-positive negative cell(s) and {heterozygous} additional negative cell(s). This is assistance only.";
    }

    private static bool CodesMatch(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(left, right, StringComparison.Ordinal);

    private static CellReaction Summarize(AntibodyIdentificationCellSnapshot cell, IReadOnlyList<string> phases)
    {
        var anyPositive = false;
        var anyNegative = false;
        var anyComplete = false;

        foreach (var phase in phases)
        {
            if (!cell.Reactions.TryGetValue(phase, out var grade) || !ReactionGradeInfo.IsComplete(grade))
            {
                continue;
            }

            anyComplete = true;
            if (ReactionGradeInfo.IsPositive(grade))
            {
                anyPositive = true;
            }
            else if (ReactionGradeInfo.IsNegative(grade))
            {
                anyNegative = true;
            }
        }

        if (anyPositive)
        {
            return CellReaction.Positive;
        }

        if (anyComplete && anyNegative)
        {
            return CellReaction.Negative;
        }

        return CellReaction.Incomplete;
    }

    private enum CellReaction
    {
        Incomplete,
        Negative,
        Positive
    }
}
