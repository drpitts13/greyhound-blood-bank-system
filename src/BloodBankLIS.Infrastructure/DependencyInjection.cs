using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Modifications;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Rules;
using BloodBankLIS.Application.Services;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Identity;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BloodBankLIS.Infrastructure;

/// <summary>
/// Registers the infrastructure services (DbContext, repositories, unit of work,
/// audit writer, clock) for the composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string provider = DatabaseOptions.SqlServer)
    {
        services.AddDbContext<BloodBankDbContext>(options =>
        {
            if (string.Equals(provider, DatabaseOptions.Sqlite, StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        });

        // The unit of work is the same scoped DbContext instance, so entity changes
        // and their audit events commit together.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BloodBankDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(EntityCrudService<>));
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IConfigurationHistoryWriter, ConfigurationHistoryWriter>();
        services.AddScoped<IIdentityAdminStore, IdentityAdminStore>();

        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<InventoryService>();
        services.AddScoped<BloodProductModificationService>();
        services.AddSingleton<IDinCheckCharacterValidator, Iso7064Mod37_2DinCheckCharacterValidator>();
        services.AddScoped<FacilityPolicyService>();
        services.AddScoped<SpecialRequirementService>();
        services.AddScoped<LookbackService>();
        services.AddScoped<ReactionInvestigationService>();
        services.AddScoped<DeviationService>();
        services.AddScoped<IsbtLookupCatalog>();
        services.AddScoped<IsbtParsingService>();
        services.AddScoped<ScanSessionService>();
        services.AddScoped<ManualComponentEntryService>();
        services.AddScoped<ComponentIdentityCorrectionService>();
        services.AddScoped<CompatibilityRulesEngine>();
        services.AddScoped<SpecimenService>();
        services.AddScoped<OrderingProviderService>();
        services.AddScoped<OrderingLocationService>();
        services.AddScoped<EncounterService>();
        services.AddScoped<RuleEngineService>();
        services.AddScoped<OrderService>();
        services.AddScoped<PatientProductHistoryService>();
        services.AddScoped<PatientAllocationService>();
        services.AddScoped<ResultService>();
        services.AddScoped<TestWorklistService>();
        services.AddScoped<ImmunohematologyService>();
        services.AddScoped<BloodAttributeCompatLoader>();
        services.AddScoped<AntibodyScreenCompatLoader>();
        services.AddScoped<CompatibilityService>();
        services.AddScoped<IssuingService>();
        services.AddScoped<BillingService>();

        // Admin configuration services.
        services.AddScoped<TestDefinitionAdminService>();
        services.AddScoped<BloodAttributeAdminService>();
        services.AddScoped<SpecimenTypeAdminService>();
        services.AddScoped<SubtestDefinitionAdminService>();
        services.AddScoped<PhaseDefinitionAdminService>();
        services.AddScoped<TestGrouperAdminService>();
        services.AddScoped<ReflexRuleAdminService>();
        services.AddScoped<RuleDefinitionAdminService>();
        services.AddScoped<ProductAdminService>();
        services.AddScoped<ModificationRuleAdminService>();
        services.AddScoped<ExpirationModificationCodeAdminService>();
        services.AddScoped<IsbtProductCodeAdminService>();
        services.AddScoped<OrderingProviderAdminService>();
        services.AddScoped<OrderingLocationAdminService>();
        services.AddScoped<ExceptionDefinitionAdminService>();
        services.AddScoped<Hl7ConfigAdminService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<IConfigurationHistoryReader, ConfigurationHistoryReader>();

        services.AddSingleton<IClock, SystemClock>();
        // Default environment descriptor; the host (API/Web) overrides with the real one.
        services.TryAddSingleton<IEnvironmentInfo>(new StaticEnvironmentInfo());
        services.TryAddCurrentUser();

        return services;
    }

    private static void TryAddCurrentUser(this IServiceCollection services)
    {
        // Phase 1 default identity; replaced by a request-scoped resolver in the Security phase.
        services.AddScoped<ICurrentUser>(_ => new StaticCurrentUser("api-user", Environment.MachineName));
    }
}
