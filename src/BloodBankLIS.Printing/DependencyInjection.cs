using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Printing.Delivery;
using BloodBankLIS.Printing.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Printing;

/// <summary>
/// Registers the printing services: the label renderers (ZPL + preview) and the
/// scoped <see cref="PrintService"/> that records audited print jobs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPrinting(this IServiceCollection services)
    {
        services.AddSingleton<ILabelRenderer, ZplLabelRenderer>();
        services.AddSingleton<ILabelRenderer, PreviewLabelRenderer>();
        services.AddSingleton<ILabelRenderer, PdfLabelRenderer>();
        services.AddSingleton<IPdfDocumentPrinter, UnavailablePdfDocumentPrinter>();
        services.AddSingleton<IZebraLabelSender>(sp => new ZebraLabelSender(sp.GetService<IRawSpooler>()));
        services.AddSingleton<IReportDocumentRenderer, TabularReportRenderer>();
        services.AddScoped<PrintService>();
        services.AddScoped<PrintDeliveryService>();
        return services;
    }
}
