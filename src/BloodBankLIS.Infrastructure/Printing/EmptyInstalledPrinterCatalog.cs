using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Infrastructure.Printing;

/// <summary>Used until the API host replaces it with the Windows printer list.</summary>
public sealed class EmptyInstalledPrinterCatalog : IInstalledPrinterCatalog
{
    public IReadOnlyList<string> List() => [];
}
