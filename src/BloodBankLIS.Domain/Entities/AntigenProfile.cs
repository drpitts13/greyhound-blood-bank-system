using BloodBankLIS.Domain.Common;

using BloodBankLIS.Domain.Enums;



namespace BloodBankLIS.Domain.Entities;



/// <summary>

/// Patient antigen phenotype for a catalog-defined blood attribute.

/// </summary>

public class AntigenProfile : BaseEntity

{

    public long PatientId { get; set; }



    public long BloodAttributeDefinitionId { get; set; }



    public AntigenResult Result { get; set; } = AntigenResult.NotTested;



    public string? Method { get; set; }



    public DateTime? TestedUtc { get; set; }



    public string? TestedBy { get; set; }



    public long? SourceResultId { get; set; }

}

