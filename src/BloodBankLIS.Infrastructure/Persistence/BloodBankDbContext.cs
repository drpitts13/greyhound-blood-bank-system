using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the Blood Bank LIS. Stamps audit metadata and
/// writes append-only <see cref="AuditEvent"/> rows for every entity insert/update/
/// delete within the same transaction as the change (see docs/architecture.md 4.1).
/// </summary>
public class BloodBankDbContext : DbContext, IUnitOfWork
{
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public BloodBankDbContext(DbContextOptions<BloodBankDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options)
    {
        _clock = clock;
        _currentUser = currentUser;
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<OrderingLocation> OrderingLocations => Set<OrderingLocation>();
    public DbSet<OrderingProvider> OrderingProviders => Set<OrderingProvider>();
    public DbSet<Specimen> Specimens => Set<Specimen>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderSpecimen> OrderSpecimens => Set<OrderSpecimen>();
    public DbSet<TestResult> TestResults => Set<TestResult>();
    public DbSet<PatientBloodTypeHistory> PatientBloodTypeHistory => Set<PatientBloodTypeHistory>();
    public DbSet<AntibodyHistory> AntibodyHistory => Set<AntibodyHistory>();
    public DbSet<AntigenProfile> AntigenProfiles => Set<AntigenProfile>();
    public DbSet<AntibodyPanelManufacturer> AntibodyPanelManufacturers => Set<AntibodyPanelManufacturer>();
    public DbSet<AntibodyPanelLot> AntibodyPanelLots => Set<AntibodyPanelLot>();
    public DbSet<AntibodyPanelCell> AntibodyPanelCells => Set<AntibodyPanelCell>();
    public DbSet<AntibodyPanelCellAntigen> AntibodyPanelCellAntigens => Set<AntibodyPanelCellAntigen>();
    public DbSet<AntibodyIdentificationWorkup> AntibodyIdentificationWorkups => Set<AntibodyIdentificationWorkup>();
    public DbSet<AntibodyIdentificationWorkupLot> AntibodyIdentificationWorkupLots => Set<AntibodyIdentificationWorkupLot>();
    public DbSet<AntibodyIdentificationReaction> AntibodyIdentificationReactions => Set<AntibodyIdentificationReaction>();
    public DbSet<AntibodyIdentificationFinding> AntibodyIdentificationFindings => Set<AntibodyIdentificationFinding>();
    public DbSet<UnitBloodAttribute> UnitBloodAttributes => Set<UnitBloodAttribute>();
    public DbSet<BloodUnit> BloodUnits => Set<BloodUnit>();
    public DbSet<ProductRetypeResult> ProductRetypeResults => Set<ProductRetypeResult>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    public DbSet<InventoryStatusHistory> InventoryStatusHistory => Set<InventoryStatusHistory>();
    public DbSet<Crossmatch> Crossmatches => Set<Crossmatch>();
    public DbSet<Allocation> Allocations => Set<Allocation>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<TransfusionEvent> TransfusionEvents => Set<TransfusionEvent>();
    public DbSet<ReactionInvestigation> ReactionInvestigations => Set<ReactionInvestigation>();
    public DbSet<SpecialTransfusionRequirement> SpecialTransfusionRequirements => Set<SpecialTransfusionRequirement>();
    public DbSet<PatientIdentifier> PatientIdentifiers => Set<PatientIdentifier>();
    public DbSet<LookbackNotification> LookbackNotifications => Set<LookbackNotification>();
    public DbSet<Deviation> Deviations => Set<Deviation>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Override> Overrides => Set<Override>();
    public DbSet<InterfaceEndpoint> InterfaceEndpoints => Set<InterfaceEndpoint>();
    public DbSet<InterfaceFieldMapping> InterfaceFieldMappings => Set<InterfaceFieldMapping>();
    public DbSet<InterfaceValueTranslation> InterfaceValueTranslations => Set<InterfaceValueTranslation>();
    public DbSet<Hl7MessageLog> Hl7Messages => Set<Hl7MessageLog>();
    public DbSet<InterfaceErrorQueueItem> InterfaceErrorQueue => Set<InterfaceErrorQueueItem>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<ChargeCode> ChargeCodes => Set<ChargeCode>();
    public DbSet<ChargeRule> ChargeRules => Set<ChargeRule>();
    public DbSet<BillingEvent> BillingEvents => Set<BillingEvent>();
    public DbSet<TestServiceBilling> TestServiceBillings => Set<TestServiceBilling>();
    public DbSet<ProductBilling> ProductBillings => Set<ProductBilling>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ElectronicSignature> ElectronicSignatures => Set<ElectronicSignature>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    // Admin configuration
    public DbSet<ConfigurationChangeHistory> ConfigurationChangeHistory => Set<ConfigurationChangeHistory>();
    public DbSet<BloodAttributeDefinition> BloodAttributeDefinitions => Set<BloodAttributeDefinition>();
    public DbSet<SpecimenTypeDefinition> SpecimenTypeDefinitions => Set<SpecimenTypeDefinition>();
    public DbSet<TestDefinition> TestDefinitions => Set<TestDefinition>();
    public DbSet<SubtestDefinition> SubtestDefinitions => Set<SubtestDefinition>();
    public DbSet<PhaseDefinition> PhaseDefinitions => Set<PhaseDefinition>();
    public DbSet<TestGrouper> TestGroupers => Set<TestGrouper>();
    public DbSet<ReflexRule> ReflexRules => Set<ReflexRule>();
    public DbSet<RuleDefinition> RuleDefinitions => Set<RuleDefinition>();
    public DbSet<RuleExecutionLog> RuleExecutionLogs => Set<RuleExecutionLog>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductAttributeAssignment> ProductAttributeAssignments => Set<ProductAttributeAssignment>();
    public DbSet<ExceptionDefinition> ExceptionDefinitions => Set<ExceptionDefinition>();
    public DbSet<ExpirationModificationCode> ExpirationModificationCodes => Set<ExpirationModificationCode>();
    public DbSet<ModificationRule> ModificationRules => Set<ModificationRule>();
    public DbSet<UnitModification> UnitModifications => Set<UnitModification>();
    public DbSet<UnitModificationUnit> UnitModificationUnits => Set<UnitModificationUnit>();

    // ISBT 128 component identity / lookups / workflow
    public DbSet<BloodComponentRawScan> BloodComponentRawScans => Set<BloodComponentRawScan>();
    public DbSet<BloodComponentSpecialTest> BloodComponentSpecialTests => Set<BloodComponentSpecialTest>();
    public DbSet<BloodComponentScanSession> BloodComponentScanSessions => Set<BloodComponentScanSession>();
    public DbSet<BloodComponentScanSessionLine> BloodComponentScanSessionLines => Set<BloodComponentScanSessionLine>();
    public DbSet<IsbtAboRhdCode> IsbtAboRhdCodes => Set<IsbtAboRhdCode>();
    public DbSet<IsbtProductCode> IsbtProductCodes => Set<IsbtProductCode>();
    public DbSet<IsbtCollectionType> IsbtCollectionTypes => Set<IsbtCollectionType>();
    public DbSet<IsbtDataStructure> IsbtDataStructures => Set<IsbtDataStructure>();
    public DbSet<BloodComponentCompatibilityDecision> BloodComponentCompatibilityDecisions => Set<BloodComponentCompatibilityDecision>();
    public DbSet<BloodComponentIdentityCorrection> BloodComponentIdentityCorrections => Set<BloodComponentIdentityCorrection>();
    public DbSet<BloodComponentException> BloodComponentExceptions => Set<BloodComponentException>();
    public DbSet<CompatibilityRuleVersion> CompatibilityRuleVersions => Set<CompatibilityRuleVersion>();
    public DbSet<CompatibilityRule> CompatibilityRules => Set<CompatibilityRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BloodBankDbContext).Assembly);

        // RowVersion concurrency: native rowversion on SQL Server, plain column elsewhere
        // (e.g. SQLite used by integration tests) so the model is provider-portable.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var rowVersion = modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.RowVersion));
            if (Database.IsSqlServer())
            {
                rowVersion.IsRowVersion();
            }
            else
            {
                rowVersion.IsConcurrencyToken(false);
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectAuditEventMutations();
        ApplyAuditMetadata();
        var captures = CaptureAuditEntries();

        if (captures.Count == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // If a transaction is already in progress (e.g. a multi-step workflow), reuse it.
        if (Database.CurrentTransaction is not null)
        {
            var inner = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            AppendAuditEvents(captures);
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            return inner;
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            AppendAuditEvents(captures);
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        // Route synchronous saves through the async pipeline to keep one audit path.
        return SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();
    }

    private void RejectAuditEventMutations()
    {
        foreach (var entry in ChangeTracker.Entries<AuditEvent>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("AuditEvent records are append-only and cannot be updated or deleted.");
            }
        }
    }

    private void ApplyAuditMetadata()
    {
        var now = _clock.UtcNow;
        var user = _currentUser.UserName;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted
                && IsStrictAppendOnly(entry.Entity.GetType()))
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} records are append-only and cannot be {entry.State}.");
            }

            if (entry.State == EntityState.Deleted
                && IsDeleteProtected(entry.Entity.GetType()))
            {
                throw new InvalidOperationException(
                    $"{entry.Entity.GetType().Name} records cannot be deleted.");
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedUtc = now;
                    entry.Entity.CreatedBy = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedUtc = now;
                    entry.Entity.ModifiedBy = user;
                    break;
            }
        }
    }

    private List<AuditCapture> CaptureAuditEntries()
    {
        var captures = new List<AuditCapture>();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            var eventType = entry.State switch
            {
                EntityState.Added => (AuditEventType?)AuditEventType.Create,
                EntityState.Modified => AuditEventType.Update,
                EntityState.Deleted => AuditEventType.Delete,
                _ => null
            };

            if (eventType is null)
            {
                continue;
            }

            var oldValues = entry.State is EntityState.Modified or EntityState.Deleted
                ? SerializeValues(entry.OriginalValues)
                : null;

            captures.Add(new AuditCapture(entry, eventType.Value, entry.Entity.GetType().Name, oldValues));
        }

        return captures;
    }

    private void AppendAuditEvents(IEnumerable<AuditCapture> captures)
    {
        var now = _clock.UtcNow;

        foreach (var capture in captures)
        {
            var newValues = capture.EventType == AuditEventType.Delete
                ? null
                : SerializeValues(capture.Entry.CurrentValues);

            AuditEvents.Add(new AuditEvent
            {
                EventType = capture.EventType,
                EntityType = capture.EntityType,
                EntityId = ((BaseEntity)capture.Entry.Entity).Id,
                UserName = _currentUser.UserName,
                Workstation = _currentUser.Workstation,
                OccurredUtc = now,
                OldValueJson = capture.OldValues,
                NewValueJson = newValues
            });
        }
    }

    private static string SerializeValues(PropertyValues values)
    {
        var snapshot = new Dictionary<string, object?>();
        foreach (var property in values.Properties)
        {
            if (property.Name == nameof(BaseEntity.RowVersion))
            {
                continue;
            }

            snapshot[property.Name] = values[property.Name];
        }

        return JsonSerializer.Serialize(snapshot, AuditJsonOptions);
    }

    private static bool IsStrictAppendOnly(Type type) =>
        type == typeof(InventoryStatusHistory)
        || type == typeof(ConfigurationChangeHistory);

    private static bool IsDeleteProtected(Type type) =>
        IsStrictAppendOnly(type)
        || type == typeof(ElectronicSignature)
        || type == typeof(PatientBloodTypeHistory)
        || type == typeof(AntibodyHistory)
        || type == typeof(LookbackNotification)
        || type == typeof(ReactionInvestigation)
        || type == typeof(SpecialTransfusionRequirement);

    private sealed record AuditCapture(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry,
        AuditEventType EventType,
        string EntityType,
        string? OldValues);
}
