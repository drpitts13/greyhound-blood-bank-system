using BloodBankLIS.Domain.Common;

using BloodBankLIS.Domain.Enums;



namespace BloodBankLIS.Domain.Entities;



/// <summary>

/// Extended antigen or antibody typing on a blood unit, linked to the blood attribute catalog.

/// </summary>

public class UnitBloodAttribute : BaseEntity

{

    public long BloodProductId { get; set; }



    public long BloodAttributeDefinitionId { get; set; }



    public BloodAttributeKind AttributeKind { get; set; } = BloodAttributeKind.Antigen;



    public AntigenResult Result { get; set; } = AntigenResult.NotTested;



    public long? SourceResultId { get; set; }

}

