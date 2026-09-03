using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.HL7.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.HL7;

/// <summary>
/// Registers the HL7 interface services. The parser/builder are pure static helpers;
/// the inbound processor and outbound service are scoped because they use the
/// request-scoped unit of work and repositories.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddHl7Interfaces(this IServiceCollection services)
    {
        services.AddScoped<Hl7InboundProcessor>();
        services.AddScoped<Hl7OutboundService>();
        services.AddScoped<Hl7OutboundSender>();
        services.AddScoped<IBillingInterfacePublisher>(sp => sp.GetRequiredService<Hl7OutboundService>());
        return services;
    }
}
