#pragma warning disable CA1416
using System.Drawing.Printing;
using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Api.Printing;

/// <summary>Print queues installed on this computer, including network printers Windows already has a connection to.</summary>
public sealed class WindowsInstalledPrinterCatalog : IInstalledPrinterCatalog
{
    public IReadOnlyList<string> List()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return PrinterSettings.InstalledPrinters.Cast<string>()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
