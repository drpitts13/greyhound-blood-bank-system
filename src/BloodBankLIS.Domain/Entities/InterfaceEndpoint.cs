using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Configuration for an HL7 interface. Facility-specific values (host/port, message
/// types, mapping profile) live here, not in code, so deployments differ only by data
/// (see docs/hl7-design.md section 6).
/// </summary>
public class InterfaceEndpoint : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public Hl7Direction Direction { get; set; } = Hl7Direction.Inbound;

    public InterfaceTransport Transport { get; set; } = InterfaceTransport.Mllp;

    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? Path { get; set; }

    /// <summary>Comma-separated supported message types (e.g. "ADT,ORM,ORU").</summary>
    public string MessageTypes { get; set; } = string.Empty;

    public string? MappingProfile { get; set; }

    /// <summary>Clinical purpose of this interface (ADT, billing, orders, results, BPAM).</summary>
    public InterfaceType InterfaceType { get; set; } = InterfaceType.Adt;

    /// <summary>Vendor preset code (Epic, Cerner, Custom, …). Mirrored onto <see cref="MappingProfile"/>.</summary>
    public string? VendorCode { get; set; }

    public InterfaceMappingMode MappingMode { get; set; } = InterfaceMappingMode.Custom;

    public ICollection<InterfaceFieldMapping> FieldMappings { get; set; } = new List<InterfaceFieldMapping>();

    public bool IsEnabled { get; set; } = true;

    // --- Extended interface configuration (admin) ---

    /// <summary>Target environment label (e.g. Development/Test/Production).</summary>
    public string? Environment { get; set; }

    public string? SendingApplication { get; set; }

    public string? SendingFacility { get; set; }

    public string? ReceivingApplication { get; set; }

    public string? ReceivingFacility { get; set; }

    /// <summary>ACK wait timeout in seconds (outbound).</summary>
    public int? AckTimeoutSeconds { get; set; }

    public int? MaxRetryCount { get; set; }

    public int? RetryDelaySeconds { get; set; }

    /// <summary>Logging verbosity label (e.g. None/Errors/All).</summary>
    public string? MessageLoggingLevel { get; set; }

    /// <summary>Whether replay of stored messages is permitted for this endpoint.</summary>
    public bool ReplayAllowed { get; set; }

    /// <summary>Monotonic config version; bumped on significant admin edits (snapshot history).</summary>
    public int Version { get; set; } = 1;
}
