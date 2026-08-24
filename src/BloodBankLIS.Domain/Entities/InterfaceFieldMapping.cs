using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One data-item-to-HL7-path mapping on an <see cref="InterfaceEndpoint"/>.
/// </summary>
public class InterfaceFieldMapping : BaseEntity
{
    public long EndpointId { get; set; }

    public InterfaceEndpoint? Endpoint { get; set; }

    /// <summary>Catalog key, e.g. <c>Patient.MedicalRecordNumber</c>.</summary>
    public string DataItemKey { get; set; } = string.Empty;

    /// <summary>HL7 location path, e.g. <c>PID-3-1</c>.</summary>
    public string Hl7Path { get; set; } = string.Empty;

    public bool IsRequired { get; set; }
}
