using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Security.Authorization;
using BloodBankLIS.Security.Signatures;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Security;

public static class DependencyInjection
{
    /// <summary>
    /// Registers permission evaluation and electronic-signature services. Both depend
    /// only on the repository/unit-of-work abstractions, so the persistence provider is
    /// supplied by the Infrastructure registration.
    /// </summary>
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<ISignatureService, SignatureService>();
        return services;
    }
}
