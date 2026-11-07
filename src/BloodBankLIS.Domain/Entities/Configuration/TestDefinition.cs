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

    // Numeric results (ResultValueType.Numeric). Every limit is the facility's; none is assumed.

    /// <summary>Units reported with the value (for example %), defaulted onto each result.</summary>
    public string? ResultUnits { get; set; }

    /// <summary>Most decimal places a value may be entered with; null allows any.</summary>
    public int? DecimalPlaces { get; set; }

    /// <summary>Range the method can report; a value outside it is refused (dilute and repeat, or report as above/below).</summary>
    public decimal? ReportableLow { get; set; }

    public decimal? ReportableHigh { get; set; }

    /// <summary>Reference range: a value outside it is flagged L or H.</summary>
    public decimal? ReferenceLow { get; set; }

    public decimal? ReferenceHigh { get; set; }

    /// <summary>Critical limits: a value beyond them is flagged LL or HH and warns at entry.</summary>
    public decimal? CriticalLow { get; set; }

    public decimal? CriticalHigh { get; set; }

    // Titers (ResultValueType.Titer): a doubling dilution series read at one phase (OCD-065).

    /// <summary>First dilution tested, as the n of 1:n (for example 1).</summary>
    public int? TiterStartDilution { get; set; }

    /// <summary>Last dilution tested, the start times a power of two (for example 2048).</summary>
    public int? TiterEndDilution { get; set; }

    /// <summary>Phase the dilutions are read at (a phase catalog code, for example AHG).</summary>
    public string? TiterPhaseCode { get; set; }

    /// <summary>Weakest grade that still counts toward the endpoint (for example 1+).</summary>
    public string? TiterEndpointMinGrade { get; set; }

    /// <summary>When true, the titer score is calculated from the grades.</summary>
    public bool TiterScored { get; set; }
}
