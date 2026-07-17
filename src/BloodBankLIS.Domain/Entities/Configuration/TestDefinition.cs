using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Versioned definition of a blood bank test. Drives result entry (allowed values,
/// verification), patient-history contribution (ABO/Rh, antibodies), compatibility logic,
/// and billing. Clinically significant: edits are versioned and audited; activation is
/// validation-gated. <see cref="Code"/> is unique among active definitions.
/// </summary>
public class TestDefinition : VersionedConfigEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TestCategory Category { get; set; } = TestCategory.Other;

    public ResultValueType ResultValueType { get; set; } = ResultValueType.Coded;

    /// <summary>Allowed coded result values, newline- or comma-separated (when applicable).</summary>
    public string? AllowedResultValues { get; set; }

    /// <summary>
    /// JSON array of <see cref="ValueObjects.PanelSubtestAssignment"/> (catalog references)
    /// or legacy inline <see cref="ValueObjects.PanelSubtestDefinition"/> rows when
    /// <see cref="ResultValueType"/> is AboRh or Subtest.
    /// </summary>
    public string? PanelSubtestsJson { get; set; }

    /// <summary>
    /// JSON array of <see cref="ValueObjects.InterpretationLogicRow"/> for discrepancy
    /// detection between interpreted result and subtest reaction patterns.
    /// </summary>
    public string? InterpretationLogicJson { get; set; }

    public string? RequiredSpecimenType { get; set; }

    public string? TestingMethod { get; set; }

    public string? PerformingDepartment { get; set; }

    public int SortOrder { get; set; }

    public bool Billable { get; set; }

    /// <summary>Charge-code mapping placeholder (links to the charge master in a later phase).</summary>
    public string? ChargeCodeMapping { get; set; }

    public bool VerificationRequired { get; set; } = true;

    public bool ContributesToAboRhHistory { get; set; }

    public bool ContributesToAntibodyHistory { get; set; }

    public bool ContributesToCompatibility { get; set; }

    /// <summary>
    /// JSON array of blood attribute catalog codes this test may report when
    /// <see cref="ResultValueType"/> is BloodAttribute.
    /// </summary>
    public string? BloodAttributeScopeJson { get; set; }

    /// <summary>
    /// Whether scoped catalog codes are reported as antigens or antibodies when
    /// <see cref="ResultValueType"/> is BloodAttribute.
    /// </summary>
    public BloodAttributeKind? BloodAttributeScopeKind { get; set; }

    /// <summary>When true, verified results update unit blood attribute records.</summary>
    public bool ContributesToUnitBloodAttributes { get; set; }
}
