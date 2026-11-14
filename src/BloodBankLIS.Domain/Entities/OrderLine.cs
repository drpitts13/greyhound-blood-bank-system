using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A single test or product line on a patient order. Orders may contain multiple lines.
/// </summary>
public class OrderLine : BaseEntity
{
    public long OrderId { get; set; }

    public Order? Order { get; set; }

    public int LineNumber { get; set; }

    public OrderCategory LineCategory { get; set; } = OrderCategory.Test;

    public string LineName { get; set; } = string.Empty;

    public string? TestCode { get; set; }

    /// <summary>
    /// The test grouper this line was ordered as part of (for example TNS for ABORH and ABSC), so a result can be
    /// reported to the EHR under the grouper's procedure code. Null when the test was ordered on its own.
    /// </summary>
    public string? GrouperCode { get; set; }

    public OrderType OrderType { get; set; } = OrderType.Other;

    public long? ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public FulfillmentStatus? FulfillmentStatus { get; set; }

    public ResultStatus? ResultStatus { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Free-text test-line comment. Coded comments from the Test catalog may be inserted.</summary>
    public string? Comment { get; set; }

    /// <summary>For a line added by a reflex rule: the test whose verified result added it. Null otherwise.</summary>
    public string? ReflexFromTestCode { get; set; }

    /// <summary>For a reflex line: the verified value (or interpretation) that matched the rule.</summary>
    public string? ReflexFromValue { get; set; }

    /// <summary>For a reflex line: the verified result that added it.</summary>
    public long? ReflexFromResultId { get; set; }

    /// <summary>Patient-blood-management check for a product line (OCD-038).</summary>
    public BloodBankLIS.Domain.Rules.PbmStatus PbmStatus { get; set; }

    /// <summary>Criterion that was met, automatically or selected by the orderer.</summary>
    public string? PbmIndicationCode { get; set; }

    /// <summary>Orderer's reason when the order is outside the facility criteria.</summary>
    public string? PbmJustification { get; set; }

    /// <summary>What the check found, for the review list.</summary>
    public string? PbmDetail { get; set; }
}
