using BloodBankLIS.Astm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Astm;

public static class DependencyInjection
{
    public static IServiceCollection AddAstmInterfaces(this IServiceCollection services)
    {
        services.AddScoped<AstmInboundProcessor>();
        return services;
    }
}
