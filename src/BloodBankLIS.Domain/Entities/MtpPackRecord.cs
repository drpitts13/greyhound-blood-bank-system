using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One physical pack sent during a massive transfusion protocol: the container, the temperature when packed, who
/// packed it and when, and (through <see cref="Issue.MtpPackRecordId"/>) the issued units inside. Rows are never
/// deleted. Its printed report travels with the container.
/// </summary>
public class MtpPackRecord : BaseEntity
{
    public long MtpActivationId { get; set; }

    /// <summary>1, 2, 3... within the activation.</summary>
    public int PackNumber { get; set; }

    public long TransportContainerId { get; set; }

    /// <summary>The specific container used, for example "Cooler 3"; optional.</summary>
    public string? ContainerLabel { get; set; }

    public decimal TemperatureC { get; set; }

    /// <summary>True when the container has a facility range and the reading was outside it.</summary>
    public bool TemperatureOutsideRange { get; set; }

    public DateTime PackedUtc { get; set; }

    public string PackedBy { get; set; } = string.Empty;

    public string? Comment { get; set; }
}
