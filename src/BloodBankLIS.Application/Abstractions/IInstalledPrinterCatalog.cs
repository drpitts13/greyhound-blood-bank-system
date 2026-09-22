namespace BloodBankLIS.Application.Abstractions;

/// <summary>Windows print queues installed on the computer that runs the API.</summary>
public interface IInstalledPrinterCatalog
{
    IReadOnlyList<string> List();
}
