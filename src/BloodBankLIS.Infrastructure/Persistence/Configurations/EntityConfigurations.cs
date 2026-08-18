using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodBankLIS.Infrastructure.Persistence.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.ToTable("Patients");
        b.HasKey(p => p.Id);
        b.Property(p => p.MedicalRecordNumber).HasMaxLength(50).IsRequired();
        b.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        b.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        b.Property(p => p.MiddleName).HasMaxLength(100);
        b.Property(p => p.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(p => p.ModifiedBy).HasMaxLength(100);

        b.HasIndex(p => p.MedicalRecordNumber).IsUnique();
        b.HasIndex(p => new { p.LastName, p.FirstName, p.DateOfBirth });

        b.HasMany(p => p.Specimens).WithOne(s => s.Patient!).HasForeignKey(s => s.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(p => p.Orders).WithOne(o => o.Patient!).HasForeignKey(o => o.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(p => p.Encounters).WithOne(e => e.Patient!).HasForeignKey(e => e.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> b)
    {
        b.ToTable("Encounters");
        b.HasKey(e => e.Id);
        b.Property(e => e.VisitNumber).HasMaxLength(50).IsRequired();
        b.Property(e => e.AccountNumber).HasMaxLength(50);
        b.Property(e => e.AttendingProvider).HasMaxLength(150);
        b.Property(e => e.AdmissionLocation).HasMaxLength(100);
        b.Property(e => e.CurrentLocation).HasMaxLength(100);
        b.Property(e => e.DischargeDisposition).HasMaxLength(100);
        b.Property(e => e.FinancialClass).HasMaxLength(50);
        b.Property(e => e.SourceSystem).HasMaxLength(50);
        b.Property(e => e.ExternalVisitId).HasMaxLength(50);
        b.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(e => e.ModifiedBy).HasMaxLength(100);

        b.HasOne(e => e.AttendingProviderRef).WithMany().HasForeignKey(e => e.AttendingProviderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.AttendingProviderId);

        b.HasIndex(e => e.VisitNumber).IsUnique();
        b.HasIndex(e => e.PatientId);
        b.HasIndex(e => e.Status);
    }
}

public sealed class OrderingProviderConfiguration : IEntityTypeConfiguration<OrderingProvider>
{
    public void Configure(EntityTypeBuilder<OrderingProvider> b)
    {
        b.ToTable("OrderingProviders");
        b.HasKey(p => p.Id);
        b.Property(p => p.ProviderId).HasMaxLength(50).IsRequired();
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Specialty).HasMaxLength(100);
        b.Property(p => p.Location).HasMaxLength(100);
        b.Property(p => p.SourceSystem).HasMaxLength(50);
        b.Property(p => p.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(p => p.ModifiedBy).HasMaxLength(100);

        b.HasIndex(p => p.ProviderId).IsUnique();
        b.HasIndex(p => p.IsActive);
    }
}

public sealed class OrderingLocationConfiguration : IEntityTypeConfiguration<OrderingLocation>
{
    public void Configure(EntityTypeBuilder<OrderingLocation> b)
    {
        b.ToTable("OrderingLocations");
        b.HasKey(l => l.Id);
        b.Property(l => l.Code).HasMaxLength(50).IsRequired();
        b.Property(l => l.Name).HasMaxLength(150).IsRequired();
        b.Property(l => l.Department).HasMaxLength(100);
        b.Property(l => l.Hl7MappingCode).HasMaxLength(50);
        b.Property(l => l.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(l => l.ModifiedBy).HasMaxLength(100);

        b.HasIndex(l => l.Code).IsUnique();
    }
}

public sealed class SpecimenConfiguration : IEntityTypeConfiguration<Specimen>
{
    public void Configure(EntityTypeBuilder<Specimen> b)
    {
        b.ToTable("Specimens");
        b.HasKey(s => s.Id);
        b.Property(s => s.AccessionNumber).HasMaxLength(50).IsRequired();
        b.Property(s => s.SpecimenType).HasMaxLength(100).IsRequired();
        b.Property(s => s.Barcode).HasMaxLength(100);
        b.Property(s => s.DrawLocation).HasMaxLength(100);
        b.Property(s => s.Collector).HasMaxLength(100);
        b.Property(s => s.RejectionReason).HasMaxLength(500);
        b.Property(s => s.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(s => s.ModifiedBy).HasMaxLength(100);

        b.HasIndex(s => s.AccessionNumber).IsUnique();
        b.HasIndex(s => s.Barcode);
        b.HasIndex(s => s.PatientId);
        b.HasIndex(s => s.EncounterId);
        b.HasIndex(s => s.ExpiresUtc);
        b.HasIndex(s => s.Status);

        b.Property(s => s.Identifier1Value).HasMaxLength(100);
        b.Property(s => s.Identifier2Value).HasMaxLength(100);

        b.HasOne(s => s.Encounter).WithMany().HasForeignKey(s => s.EncounterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");
        b.HasKey(o => o.Id);
        b.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
        b.Property(o => o.OrderName).HasMaxLength(200).IsRequired();
        b.Property(o => o.TestCode).HasMaxLength(50);
        b.Property(o => o.OrderingProvider).HasMaxLength(150);
        b.Property(o => o.FillerOrderNumber).HasMaxLength(50);
        b.Property(o => o.SourceSystem).HasMaxLength(50);
        b.Property(o => o.OrderedByUser).HasMaxLength(100);
        b.Property(o => o.CancellationReason).HasMaxLength(500);
        b.Property(o => o.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(o => o.ModifiedBy).HasMaxLength(100);

        b.HasOne(o => o.Encounter).WithMany(e => e.Orders).HasForeignKey(o => o.EncounterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(o => o.OrderingLocation).WithMany().HasForeignKey(o => o.OrderingLocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(o => o.OrderingProviderRef).WithMany().HasForeignKey(o => o.OrderingProviderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(o => o.ProductType).WithMany().HasForeignKey(o => o.ProductTypeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(o => o.OrderNumber).IsUnique();
        b.HasIndex(o => o.OrderingProviderId);
        b.HasIndex(o => o.PatientId);
        b.HasIndex(o => o.EncounterId);
        b.HasIndex(o => o.OrderingLocationId);
        b.HasIndex(o => o.OrderedUtc);
        b.HasIndex(o => o.Status);
    }
}

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> b)
    {
        b.ToTable("OrderLines");
        b.HasKey(l => l.Id);
        b.Property(l => l.LineName).HasMaxLength(200).IsRequired();
        b.Property(l => l.TestCode).HasMaxLength(50);
        b.Property(l => l.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(l => l.ModifiedBy).HasMaxLength(100);

        b.HasOne(l => l.Order).WithMany(o => o.Lines).HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(l => l.ProductType).WithMany().HasForeignKey(l => l.ProductTypeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(l => l.OrderId);
        b.HasIndex(l => new { l.OrderId, l.LineNumber });
    }
}

public sealed class OrderSpecimenConfiguration : IEntityTypeConfiguration<OrderSpecimen>
{
    public void Configure(EntityTypeBuilder<OrderSpecimen> b)
    {
        b.ToTable("OrderSpecimens");
        b.HasKey(os => os.Id);
        b.Property(os => os.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(os => os.ModifiedBy).HasMaxLength(100);

        b.HasOne(os => os.Order).WithMany(o => o.OrderSpecimens).HasForeignKey(os => os.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(os => os.Specimen).WithMany().HasForeignKey(os => os.SpecimenId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(os => new { os.OrderId, os.SpecimenId }).IsUnique();
    }
}

public sealed class TestResultConfiguration : IEntityTypeConfiguration<TestResult>
{
    public void Configure(EntityTypeBuilder<TestResult> b)
    {
        b.ToTable("TestResults");
        b.HasKey(r => r.Id);
        b.Property(r => r.TestCode).HasMaxLength(50).IsRequired();
        b.Property(r => r.Value).HasMaxLength(500);
        b.Property(r => r.Units).HasMaxLength(50);
        b.Property(r => r.Interpretation).HasMaxLength(500);
        b.Property(r => r.EnteredBy).HasMaxLength(100);
        b.Property(r => r.VerifiedBy).HasMaxLength(100);
        b.Property(r => r.CorrectionReason).HasMaxLength(500);
        b.Property(r => r.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(r => r.ModifiedBy).HasMaxLength(100);

        b.HasOne(r => r.Specimen).WithMany().HasForeignKey(r => r.SpecimenId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.SupersededByResult).WithMany().HasForeignKey(r => r.SupersededByResultId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.SpecimenId, r.TestCode });
        b.HasIndex(r => r.PatientId);
        b.HasIndex(r => r.Status);
    }
}

public sealed class PatientBloodTypeHistoryConfiguration : IEntityTypeConfiguration<PatientBloodTypeHistory>
{
    public void Configure(EntityTypeBuilder<PatientBloodTypeHistory> b)
    {
        b.ToTable("PatientBloodTypeHistory");
        b.HasKey(h => h.Id);
        b.Property(h => h.Reason).HasMaxLength(500);
        b.Property(h => h.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(h => h.ModifiedBy).HasMaxLength(100);
        b.Ignore(h => h.BloodType);

        b.HasIndex(h => new { h.PatientId, h.IsCurrent });
    }
}

public sealed class AntibodyHistoryConfiguration : IEntityTypeConfiguration<AntibodyHistory>
{
    public void Configure(EntityTypeBuilder<AntibodyHistory> b)
    {
        b.ToTable("AntibodyHistory");
        b.HasKey(a => a.Id);
        b.Property(a => a.AntibodySpecificity).HasMaxLength(100).IsRequired();
        b.Property(a => a.Comment).HasMaxLength(1000);
        b.Property(a => a.DeactivationReason).HasMaxLength(500);
        b.Property(a => a.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(a => a.ModifiedBy).HasMaxLength(100);

        b.HasIndex(a => new { a.PatientId, a.IsActive });
    }
}

public sealed class AntigenProfileConfiguration : IEntityTypeConfiguration<AntigenProfile>
{
    public void Configure(EntityTypeBuilder<AntigenProfile> b)
    {
        b.ToTable("AntigenProfiles");
        b.HasKey(a => a.Id);
        b.Property(a => a.Method).HasMaxLength(100);
        b.Property(a => a.TestedBy).HasMaxLength(100);
        b.Property(a => a.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(a => a.ModifiedBy).HasMaxLength(100);

        b.HasIndex(a => new { a.PatientId, a.BloodAttributeDefinitionId });
    }
}

public sealed class UnitBloodAttributeConfiguration : IEntityTypeConfiguration<UnitBloodAttribute>
{
    public void Configure(EntityTypeBuilder<UnitBloodAttribute> b)
    {
        b.ToTable("UnitBloodAttributes");
        b.HasKey(a => a.Id);
        b.Property(a => a.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(a => a.ModifiedBy).HasMaxLength(100);

        b.HasIndex(a => new { a.BloodProductId, a.BloodAttributeDefinitionId, a.AttributeKind });
    }
}

public sealed class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
{
    public void Configure(EntityTypeBuilder<ProductType> b)
    {
        b.ToTable("ProductTypes");
        b.HasKey(t => t.Id);
        b.Property(t => t.ProductCode).HasMaxLength(50).IsRequired();
        b.Property(t => t.Name).HasMaxLength(150).IsRequired();
        b.Property(t => t.Category).HasMaxLength(100);
        b.Property(t => t.Isbt128ProductCode).HasMaxLength(20);
        b.Property(t => t.DefaultChargeCode).HasMaxLength(50);
        b.Property(t => t.StorageRequirements).HasMaxLength(200);
        b.Property(t => t.IssueRules).HasMaxLength(1000);
        b.Property(t => t.ReturnRules).HasMaxLength(1000);
        b.Property(t => t.ModificationRules).HasMaxLength(1000);
        b.Property(t => t.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(t => t.ModifiedBy).HasMaxLength(100);

        b.HasIndex(t => t.ProductCode).IsUnique();
    }
}

public sealed class InventoryLocationConfiguration : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> b)
    {
        b.ToTable("InventoryLocations");
        b.HasKey(l => l.Id);
        b.Property(l => l.Code).HasMaxLength(50).IsRequired();
        b.Property(l => l.Name).HasMaxLength(150).IsRequired();
        b.Property(l => l.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(l => l.ModifiedBy).HasMaxLength(100);

        b.HasIndex(l => l.Code).IsUnique();
    }
}

public sealed class BloodUnitConfiguration : IEntityTypeConfiguration<BloodUnit>
{
    public void Configure(EntityTypeBuilder<BloodUnit> b)
    {
        b.ToTable("BloodProducts");
        b.HasKey(u => u.Id);
        b.Property(u => u.UnitNumber).HasMaxLength(80).IsRequired();
        b.Property(u => u.ComponentIdentity).HasMaxLength(80);
        b.Property(u => u.ComponentIdentityKey).HasMaxLength(80);
        b.Property(u => u.Din).HasMaxLength(13);
        b.Property(u => u.Fin).HasMaxLength(5);
        b.Property(u => u.NominalYear).HasMaxLength(2);
        b.Property(u => u.DonationSequence).HasMaxLength(6);
        b.Property(u => u.DinFlags).HasMaxLength(2);
        b.Property(u => u.DinKeyboardCheck).HasMaxLength(1);
        b.Property(u => u.AboRhdCode).HasMaxLength(10);
        b.Property(u => u.DonationCollectionCategory).HasMaxLength(100);
        b.Property(u => u.EncodedPhenotype).HasMaxLength(100);
        b.Property(u => u.AboSpecialMessage).HasMaxLength(200);
        b.Property(u => u.ProductCodeData).HasMaxLength(8);
        b.Property(u => u.ProductDescriptionCode).HasMaxLength(5);
        b.Property(u => u.CollectionTypeCode).HasMaxLength(1);
        b.Property(u => u.DivisionCode).HasMaxLength(2);
        b.Property(u => u.ExtendedDivisionCode).HasMaxLength(20);
        b.Property(u => u.ExpirationEncoded).HasMaxLength(11);
        b.Property(u => u.ExpirationTimezone).HasMaxLength(100);
        b.Property(u => u.ProcessingFacilityCode).HasMaxLength(20);
        b.Property(u => u.StandardVersion).HasMaxLength(50);
        b.Property(u => u.Isbt128ProductCode).HasMaxLength(20);
        b.Property(u => u.Isbt128DonationId).HasMaxLength(30);
        b.Property(u => u.CollectionFacility).HasMaxLength(150);
        b.Property(u => u.Supplier).HasMaxLength(150);
        b.Property(u => u.ShipmentId).HasMaxLength(100);
        b.Property(u => u.QuarantineReason).HasMaxLength(500);
        b.Property(u => u.DiscardReason).HasMaxLength(500);
        b.Property(u => u.RecallReason).HasMaxLength(500);
        b.Property(u => u.Volume).HasPrecision(18, 3);
        b.Property(u => u.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(u => u.ModifiedBy).HasMaxLength(100);

        // Computed convenience projection only; not persisted.
        b.Ignore(u => u.BloodType);

        b.HasOne(u => u.ProductType).WithMany(t => t.Units).HasForeignKey(u => u.ProductTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(u => u.CurrentLocation).WithMany().HasForeignKey(u => u.CurrentLocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(u => u.RawScans).WithOne(s => s.Unit!).HasForeignKey(s => s.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(u => u.SpecialTests).WithOne(s => s.Unit!).HasForeignKey(s => s.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(u => u.DerivedFromModification).WithMany().HasForeignKey(u => u.DerivedFromModificationId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(u => u.UnitNumber).IsUnique();
        b.HasIndex(u => u.ComponentIdentityKey).IsUnique().HasFilter("[ComponentIdentityKey] IS NOT NULL");
        b.HasIndex(u => u.Din);
        b.HasIndex(u => u.Status);
        b.HasIndex(u => u.ExpiresUtc);
        b.HasIndex(u => u.ProductTypeId);
        b.HasIndex(u => new { u.Abo, u.RhD });
        b.HasIndex(u => u.CurrentLocationId);
        b.HasIndex(u => u.DerivedFromModificationId);
    }
}

public sealed class ModificationRuleConfiguration : IEntityTypeConfiguration<ModificationRule>
{
    public void Configure(EntityTypeBuilder<ModificationRule> b)
    {
        b.ToTable("ModificationRules");
        b.HasKey(r => r.Id);
        b.Property(r => r.ExpirationOffsetCode).HasMaxLength(10).IsRequired();
        b.Property(r => r.Description).HasMaxLength(500);
        b.Property(r => r.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(r => r.ModifiedBy).HasMaxLength(100);

        b.HasOne(r => r.SourceProductType).WithMany().HasForeignKey(r => r.SourceProductTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.TargetProductType).WithMany().HasForeignKey(r => r.TargetProductTypeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(r => new { r.SourceProductTypeId, r.ModificationType, r.TargetProductTypeId });
        b.HasIndex(r => r.IsActive);
    }
}

public sealed class UnitModificationConfiguration : IEntityTypeConfiguration<UnitModification>
{
    public void Configure(EntityTypeBuilder<UnitModification> b)
    {
        b.ToTable("UnitModifications");
        b.HasKey(m => m.Id);
        b.Property(m => m.ExpirationOffsetCodeApplied).HasMaxLength(10).IsRequired();
        b.Property(m => m.Reason).HasMaxLength(500).IsRequired();
        b.Property(m => m.PerformedBy).HasMaxLength(100).IsRequired();
        b.Property(m => m.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(m => m.ModifiedBy).HasMaxLength(100);

        b.HasOne(m => m.ModificationRule).WithMany().HasForeignKey(m => m.ModificationRuleId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(m => m.Units).WithOne(u => u.UnitModification!).HasForeignKey(u => u.UnitModificationId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(m => m.ModificationRuleId);
        b.HasIndex(m => m.PerformedUtc);
    }
}

public sealed class UnitModificationUnitConfiguration : IEntityTypeConfiguration<UnitModificationUnit>
{
    public void Configure(EntityTypeBuilder<UnitModificationUnit> b)
    {
        b.ToTable("UnitModificationUnits");
        b.HasKey(u => u.Id);
        b.Property(u => u.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(u => u.ModifiedBy).HasMaxLength(100);

        b.HasOne(u => u.Unit).WithMany().HasForeignKey(u => u.BloodProductId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(u => u.UnitModificationId);
        b.HasIndex(u => u.BloodProductId);
    }
}

public sealed class InventoryStatusHistoryConfiguration : IEntityTypeConfiguration<InventoryStatusHistory>
{
    public void Configure(EntityTypeBuilder<InventoryStatusHistory> b)
    {
        b.ToTable("InventoryStatusHistory");
        b.HasKey(h => h.Id);
        b.Property(h => h.Reason).HasMaxLength(500);
        b.Property(h => h.ChangedBy).HasMaxLength(100).IsRequired();
        b.Property(h => h.RelatedEntityType).HasMaxLength(100);

        b.HasOne(h => h.Unit).WithMany().HasForeignKey(h => h.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.BloodProductId, h.ChangedUtc });
    }
}

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("AuditEvents");
        b.HasKey(a => a.Id);
        b.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        b.Property(a => a.UserName).HasMaxLength(100).IsRequired();
        b.Property(a => a.Workstation).HasMaxLength(100);
        b.Property(a => a.Reason).HasMaxLength(1000);
        b.Property(a => a.Environment).HasMaxLength(50);

        b.HasIndex(a => new { a.EntityType, a.EntityId });
        b.HasIndex(a => a.UserName);
        b.HasIndex(a => a.OccurredUtc);
        b.HasIndex(a => a.EventType);
    }
}

public sealed class CrossmatchConfiguration : IEntityTypeConfiguration<Crossmatch>
{
    public void Configure(EntityTypeBuilder<Crossmatch> b)
    {
        b.ToTable("Crossmatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.PerformedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);

        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.BloodProductId);
        b.HasIndex(x => new { x.PatientId, x.SpecimenId });
    }
}

public sealed class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public void Configure(EntityTypeBuilder<Allocation> b)
    {
        b.ToTable("Allocations");
        b.HasKey(a => a.Id);
        b.Property(a => a.AllocatedBy).HasMaxLength(100).IsRequired();
        b.Property(a => a.ReleaseReason).HasMaxLength(500);
        b.Property(a => a.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(a => a.ModifiedBy).HasMaxLength(100);

        b.HasOne(a => a.Unit).WithMany().HasForeignKey(a => a.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.BloodProductId, a.Status });
        b.HasIndex(a => a.PatientId);
        b.HasIndex(a => a.EncounterId);
        b.HasIndex(a => a.OrderId);
    }
}

public sealed class OverrideConfiguration : IEntityTypeConfiguration<Override>
{
    public void Configure(EntityTypeBuilder<Override> b)
    {
        b.ToTable("Overrides");
        b.HasKey(o => o.Id);
        b.Property(o => o.ContextType).HasMaxLength(100).IsRequired();
        b.Property(o => o.RuleCode).HasMaxLength(200).IsRequired();
        b.Property(o => o.Reason).HasMaxLength(1000).IsRequired();
        b.Property(o => o.AuthorizedBy).HasMaxLength(100).IsRequired();
        b.Property(o => o.Resolution).HasMaxLength(50);
        b.Property(o => o.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(o => o.ModifiedBy).HasMaxLength(100);

        b.HasIndex(o => new { o.ContextType, o.ContextId });
    }
}

public sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> b)
    {
        b.ToTable("Issues");
        b.HasKey(i => i.Id);
        b.Property(i => i.IssuedTo).HasMaxLength(150);
        b.Property(i => i.IssuedToLocation).HasMaxLength(150);
        b.Property(i => i.IssuedBy).HasMaxLength(100).IsRequired();
        b.Property(i => i.Comment).HasMaxLength(1000);
        b.Property(i => i.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(i => i.ModifiedBy).HasMaxLength(100);

        b.HasOne(i => i.Unit).WithMany().HasForeignKey(i => i.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Override).WithMany().HasForeignKey(i => i.OverrideId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(i => i.BloodProductId);
        b.HasIndex(i => i.PatientId);
        b.HasIndex(i => i.EncounterId);
        b.HasIndex(i => i.OrderId);
        b.Property(i => i.SecondVerifier).HasMaxLength(100);
        b.Property(i => i.PatientIdentifier1).HasMaxLength(100);
        b.Property(i => i.PatientIdentifier2).HasMaxLength(100);
    }
}

public sealed class ReturnConfiguration : IEntityTypeConfiguration<Return>
{
    public void Configure(EntityTypeBuilder<Return> b)
    {
        b.ToTable("Returns");
        b.HasKey(r => r.Id);
        b.Property(r => r.ReturnedBy).HasMaxLength(100).IsRequired();
        b.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        b.Property(r => r.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(r => r.ModifiedBy).HasMaxLength(100);

        b.HasOne(r => r.Issue).WithMany().HasForeignKey(r => r.IssueId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => r.IssueId);
    }
}

public sealed class TransfusionEventConfiguration : IEntityTypeConfiguration<TransfusionEvent>
{
    public void Configure(EntityTypeBuilder<TransfusionEvent> b)
    {
        b.ToTable("TransfusionEvents");
        b.HasKey(t => t.Id);
        b.Property(t => t.Transfusionist).HasMaxLength(150);
        b.Property(t => t.DocumentedBy).HasMaxLength(100).IsRequired();
        b.Property(t => t.VolumeTransfused).HasPrecision(18, 3);
        b.Property(t => t.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(t => t.ModifiedBy).HasMaxLength(100);

        b.HasOne(t => t.Issue).WithMany().HasForeignKey(t => t.IssueId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => t.PatientId);
        b.HasIndex(t => t.IssueId);
    }
}

public sealed class InterfaceEndpointConfiguration : IEntityTypeConfiguration<InterfaceEndpoint>
{
    public void Configure(EntityTypeBuilder<InterfaceEndpoint> b)
    {
        b.ToTable("InterfaceEndpoints");
        b.HasKey(e => e.Id);
        b.Property(e => e.Name).HasMaxLength(100).IsRequired();
        b.Property(e => e.Host).HasMaxLength(255);
        b.Property(e => e.Path).HasMaxLength(500);
        b.Property(e => e.MessageTypes).HasMaxLength(200).IsRequired();
        b.Property(e => e.MappingProfile).HasMaxLength(100);
        b.Property(e => e.Environment).HasMaxLength(50);
        b.Property(e => e.SendingApplication).HasMaxLength(100);
        b.Property(e => e.SendingFacility).HasMaxLength(100);
        b.Property(e => e.ReceivingApplication).HasMaxLength(100);
        b.Property(e => e.ReceivingFacility).HasMaxLength(100);
        b.Property(e => e.MessageLoggingLevel).HasMaxLength(50);
        b.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(e => e.ModifiedBy).HasMaxLength(100);

        b.HasIndex(e => e.Name).IsUnique();
    }
}

public sealed class Hl7MessageLogConfiguration : IEntityTypeConfiguration<Hl7MessageLog>
{
    public void Configure(EntityTypeBuilder<Hl7MessageLog> b)
    {
        b.ToTable("HL7Messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.MessageType).HasMaxLength(20).IsRequired();
        b.Property(m => m.TriggerEvent).HasMaxLength(20);
        b.Property(m => m.MessageControlId).HasMaxLength(199);
        b.Property(m => m.AckCode).HasMaxLength(2);
        b.Property(m => m.ErrorDetail).HasMaxLength(2000);
        b.Property(m => m.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(m => m.ModifiedBy).HasMaxLength(100);

        b.HasIndex(m => m.MessageControlId);
        b.HasIndex(m => m.Status);
        b.HasIndex(m => m.ReceivedUtc);
        b.HasIndex(m => m.MessageType);
    }
}

public sealed class InterfaceErrorQueueItemConfiguration : IEntityTypeConfiguration<InterfaceErrorQueueItem>
{
    public void Configure(EntityTypeBuilder<InterfaceErrorQueueItem> b)
    {
        b.ToTable("InterfaceErrorQueue");
        b.HasKey(e => e.Id);
        b.Property(e => e.ErrorType).HasMaxLength(100).IsRequired();
        b.Property(e => e.ErrorDetail).HasMaxLength(2000).IsRequired();
        b.Property(e => e.ResolvedBy).HasMaxLength(100);
        b.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(e => e.ModifiedBy).HasMaxLength(100);

        b.HasOne(e => e.Hl7Message).WithMany().HasForeignKey(e => e.Hl7MessageId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.Resolved);
        b.HasIndex(e => e.NextRetryUtc);
    }
}

public sealed class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> b)
    {
        b.ToTable("PrintJobs");
        b.HasKey(p => p.Id);
        b.Property(p => p.TemplateCode).HasMaxLength(50).IsRequired();
        b.Property(p => p.TargetPrinter).HasMaxLength(100);
        b.Property(p => p.ContextType).HasMaxLength(100);
        b.Property(p => p.ReprintReason).HasMaxLength(1000);
        b.Property(p => p.PrintedBy).HasMaxLength(100).IsRequired();
        b.Property(p => p.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(p => p.ModifiedBy).HasMaxLength(100);

        b.HasIndex(p => new { p.ContextType, p.ContextId });
        b.HasIndex(p => p.Status);
    }
}

public sealed class ChargeCodeConfiguration : IEntityTypeConfiguration<ChargeCode>
{
    public void Configure(EntityTypeBuilder<ChargeCode> b)
    {
        b.ToTable("ChargeCodes");
        b.HasKey(c => c.Id);
        b.Property(c => c.Code).HasMaxLength(50).IsRequired();
        b.Property(c => c.Description).HasMaxLength(300).IsRequired();
        b.Property(c => c.DefaultAmount).HasPrecision(18, 2);
        b.Property(c => c.CptCode).HasMaxLength(20);
        b.Property(c => c.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(c => c.ModifiedBy).HasMaxLength(100);

        b.HasIndex(c => c.Code).IsUnique();
    }
}

public sealed class ChargeRuleConfiguration : IEntityTypeConfiguration<ChargeRule>
{
    public void Configure(EntityTypeBuilder<ChargeRule> b)
    {
        b.ToTable("ChargeRules");
        b.HasKey(r => r.Id);
        b.Property(r => r.TriggerKey).HasMaxLength(100);
        b.Property(r => r.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(r => r.ModifiedBy).HasMaxLength(100);

        b.HasOne(r => r.ChargeCode).WithMany().HasForeignKey(r => r.ChargeCodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.TriggerType, r.TriggerKey });
    }
}

public sealed class BillingEventConfiguration : IEntityTypeConfiguration<BillingEvent>
{
    public void Configure(EntityTypeBuilder<BillingEvent> b)
    {
        b.ToTable("BillingEvents");
        b.HasKey(e => e.Id);
        b.Property(e => e.TriggerEntityType).HasMaxLength(100).IsRequired();
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.DedupeKey).HasMaxLength(200).IsRequired();
        b.Property(e => e.ReviewedBy).HasMaxLength(100);
        b.Property(e => e.CancellationReason).HasMaxLength(1000);
        b.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(e => e.ModifiedBy).HasMaxLength(100);

        b.HasOne(e => e.ChargeCode).WithMany().HasForeignKey(e => e.ChargeCodeId).OnDelete(DeleteBehavior.Restrict);
        // Duplicate-charge prevention (docs B.3).
        b.HasIndex(e => e.DedupeKey).IsUnique();
        b.HasIndex(e => e.Status);
        b.HasIndex(e => e.PatientId);
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(u => u.Id);
        b.Property(u => u.UserName).HasMaxLength(100).IsRequired();
        b.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256);
        b.Property(u => u.PasswordHash).HasMaxLength(500);
        b.Property(u => u.PinHash).HasMaxLength(500);
        b.Property(u => u.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(u => u.ModifiedBy).HasMaxLength(100);

        b.HasIndex(u => u.UserName).IsUnique();
        b.HasMany(u => u.UserRoles).WithOne(ur => ur.User!).HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.HasKey(r => r.Id);
        b.Property(r => r.Name).HasMaxLength(100).IsRequired();
        b.Property(r => r.Description).HasMaxLength(500);
        b.Property(r => r.SecurityLevel).HasDefaultValue(0);
        b.Property(r => r.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(r => r.ModifiedBy).HasMaxLength(100);

        b.HasIndex(r => r.Name).IsUnique();
        b.HasMany(r => r.RolePermissions).WithOne(rp => rp.Role!).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(r => r.UserRoles).WithOne(ur => ur.Role!).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ExceptionDefinitionConfiguration : IEntityTypeConfiguration<ExceptionDefinition>
{
    public void Configure(EntityTypeBuilder<ExceptionDefinition> b)
    {
        b.ToTable("ExceptionDefinitions");
        b.HasKey(e => e.Id);
        b.Property(e => e.RuleCode).HasMaxLength(100).IsRequired();
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Description).HasMaxLength(1000);
        b.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(e => e.ModifiedBy).HasMaxLength(100);

        b.HasIndex(e => e.RuleCode).IsUnique();
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permissions");
        b.HasKey(p => p.Id);
        b.Property(p => p.Code).HasMaxLength(100).IsRequired();
        b.Property(p => p.Description).HasMaxLength(500);
        b.Property(p => p.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(p => p.ModifiedBy).HasMaxLength(100);

        b.HasIndex(p => p.Code).IsUnique();
        b.HasMany(p => p.RolePermissions).WithOne(rp => rp.Permission!).HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions");
        b.HasKey(rp => rp.Id);
        b.Property(rp => rp.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(rp => rp.ModifiedBy).HasMaxLength(100);

        b.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRoles");
        b.HasKey(ur => ur.Id);
        b.Property(ur => ur.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(ur => ur.ModifiedBy).HasMaxLength(100);

        b.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
    }
}

public sealed class ElectronicSignatureConfiguration : IEntityTypeConfiguration<ElectronicSignature>
{
    public void Configure(EntityTypeBuilder<ElectronicSignature> b)
    {
        b.ToTable("ElectronicSignatures");
        b.HasKey(s => s.Id);
        b.Property(s => s.Action).HasMaxLength(100).IsRequired();
        b.Property(s => s.ContextType).HasMaxLength(100);
        b.Property(s => s.MeaningOfSignature).HasMaxLength(500).IsRequired();
        b.Property(s => s.Workstation).HasMaxLength(100);
        b.Property(s => s.SignatureHash).HasMaxLength(64);
        b.Property(s => s.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(s => s.ModifiedBy).HasMaxLength(100);

        b.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => s.UserId);
        b.HasIndex(s => new { s.ContextType, s.ContextId });
    }
}

public sealed class ConfigurationChangeHistoryConfiguration : IEntityTypeConfiguration<ConfigurationChangeHistory>
{
    public void Configure(EntityTypeBuilder<ConfigurationChangeHistory> b)
    {
        b.ToTable("ConfigurationChangeHistory");
        b.HasKey(h => h.Id);
        b.Property(h => h.EntityType).HasMaxLength(100).IsRequired();
        b.Property(h => h.ChangeReason).HasMaxLength(1000);
        b.Property(h => h.ChangedBy).HasMaxLength(100).IsRequired();
        b.Property(h => h.Workstation).HasMaxLength(100);
        b.Property(h => h.Environment).HasMaxLength(50);

        b.HasIndex(h => new { h.EntityType, h.EntityId });
        b.HasIndex(h => h.ChangedUtc);
    }
}

public sealed class TestDefinitionConfiguration : IEntityTypeConfiguration<TestDefinition>
{
    public void Configure(EntityTypeBuilder<TestDefinition> b)
    {
        b.ToTable("TestDefinitions");
        b.HasKey(t => t.Id);
        b.Property(t => t.Code).HasMaxLength(50).IsRequired();
        b.Property(t => t.Name).HasMaxLength(200).IsRequired();
        b.Property(t => t.AllowedResultValues).HasMaxLength(2000);
        b.Property(t => t.PanelSubtestsJson).HasMaxLength(4000);
        b.Property(t => t.InterpretationLogicJson).HasMaxLength(8000);
        b.Property(t => t.BloodAttributeScopeJson).HasMaxLength(2000);
        b.Property(t => t.RequiredSpecimenType).HasMaxLength(100);
        b.Property(t => t.TestingMethod).HasMaxLength(150);
        b.Property(t => t.PerformingDepartment).HasMaxLength(150);
        b.Property(t => t.ChargeCodeMapping).HasMaxLength(50);
        b.Property(t => t.ChangeReason).HasMaxLength(1000);
        b.Property(t => t.ApprovedBy).HasMaxLength(100);
        b.Property(t => t.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(t => t.ModifiedBy).HasMaxLength(100);

        // Unique only among active definitions (filtered index on SQL Server).
        b.HasIndex(t => t.Code);
        b.HasIndex(t => t.Category);
    }
}

public sealed class BloodAttributeDefinitionConfiguration : IEntityTypeConfiguration<BloodAttributeDefinition>
{
    public void Configure(EntityTypeBuilder<BloodAttributeDefinition> b)
    {
        b.ToTable("BloodAttributeDefinitions");
        b.HasKey(d => d.Id);
        b.Property(d => d.Code).HasMaxLength(50).IsRequired();
        b.Property(d => d.Name).HasMaxLength(200).IsRequired();
        b.Property(d => d.AntibodyName).HasMaxLength(100).IsRequired();
        b.Property(d => d.ChangeReason).HasMaxLength(1000);
        b.Property(d => d.ApprovedBy).HasMaxLength(100);
        b.Property(d => d.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(d => d.ModifiedBy).HasMaxLength(100);

        b.HasIndex(d => d.Code);
    }
}

public sealed class SpecimenTypeDefinitionConfiguration : IEntityTypeConfiguration<SpecimenTypeDefinition>
{
    public void Configure(EntityTypeBuilder<SpecimenTypeDefinition> b)
    {
        b.ToTable("SpecimenTypeDefinitions");
        b.HasKey(d => d.Id);
        b.Property(d => d.Code).HasMaxLength(50).IsRequired();
        b.Property(d => d.Description).HasMaxLength(200).IsRequired();
        b.Property(d => d.ExcludedTestCodesJson).HasMaxLength(4000);
        b.Property(d => d.ChangeReason).HasMaxLength(1000);
        b.Property(d => d.ApprovedBy).HasMaxLength(100);
        b.Property(d => d.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(d => d.ModifiedBy).HasMaxLength(100);

        b.HasIndex(d => d.Code);
    }
}

public sealed class SubtestDefinitionConfiguration : IEntityTypeConfiguration<SubtestDefinition>
{
    public void Configure(EntityTypeBuilder<SubtestDefinition> b)
    {
        b.ToTable("SubtestDefinitions");
        b.HasKey(s => s.Id);
        b.Property(s => s.Code).HasMaxLength(50).IsRequired();
        b.Property(s => s.Name).HasMaxLength(200).IsRequired();
        b.Property(s => s.ChoicesJson).HasMaxLength(4000);
        b.Property(s => s.ChangeReason).HasMaxLength(1000);
        b.Property(s => s.ApprovedBy).HasMaxLength(100);
        b.Property(s => s.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(s => s.ModifiedBy).HasMaxLength(100);

        b.HasIndex(s => s.Code);
    }
}

public sealed class TestGrouperConfiguration : IEntityTypeConfiguration<TestGrouper>
{
    public void Configure(EntityTypeBuilder<TestGrouper> b)
    {
        b.ToTable("TestGroupers");
        b.HasKey(g => g.Id);
        b.Property(g => g.Code).HasMaxLength(50).IsRequired();
        b.Property(g => g.Name).HasMaxLength(200).IsRequired();
        b.Property(g => g.MemberTestsJson).HasMaxLength(2000);
        b.Property(g => g.ChangeReason).HasMaxLength(1000);
        b.Property(g => g.ApprovedBy).HasMaxLength(100);
        b.Property(g => g.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(g => g.ModifiedBy).HasMaxLength(100);

        b.HasIndex(g => g.Code);
    }
}

public sealed class ReflexRuleConfiguration : IEntityTypeConfiguration<ReflexRule>
{
    public void Configure(EntityTypeBuilder<ReflexRule> b)
    {
        b.ToTable("ReflexRules");
        b.HasKey(r => r.Id);
        b.Property(r => r.Code).HasMaxLength(50).IsRequired();
        b.Property(r => r.Name).HasMaxLength(200).IsRequired();
        b.Property(r => r.TriggerTestCode).HasMaxLength(50).IsRequired();
        b.Property(r => r.TriggerResultValue).HasMaxLength(200).IsRequired();
        b.Property(r => r.ReflexTestCode).HasMaxLength(50).IsRequired();
        b.Property(r => r.ChangeReason).HasMaxLength(1000);
        b.Property(r => r.ApprovedBy).HasMaxLength(100);
        b.Property(r => r.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(r => r.ModifiedBy).HasMaxLength(100);

        b.HasIndex(r => r.Code);
        b.HasIndex(r => r.TriggerTestCode);
    }
}

public sealed class RuleDefinitionConfiguration : IEntityTypeConfiguration<RuleDefinition>
{
    public void Configure(EntityTypeBuilder<RuleDefinition> b)
    {
        b.ToTable("RuleDefinitions");
        b.HasKey(r => r.Id);
        b.Property(r => r.Code).HasMaxLength(50).IsRequired();
        b.Property(r => r.Name).HasMaxLength(200).IsRequired();
        b.Property(r => r.Description).HasMaxLength(1000);
        b.Property(r => r.Level).HasConversion<int>();
        b.Property(r => r.ConditionExpression).HasMaxLength(2000).IsRequired();
        b.Property(r => r.ActionExpression).HasMaxLength(2000).IsRequired();
        b.Property(r => r.ChangeReason).HasMaxLength(1000);
        b.Property(r => r.ApprovedBy).HasMaxLength(100);
        b.Property(r => r.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(r => r.ModifiedBy).HasMaxLength(100);

        b.HasIndex(r => r.Code);
        b.HasIndex(r => new { r.Level, r.IsActive });
    }
}

public sealed class RuleExecutionLogConfiguration : IEntityTypeConfiguration<RuleExecutionLog>
{
    public void Configure(EntityTypeBuilder<RuleExecutionLog> b)
    {
        b.ToTable("RuleExecutionLogs");
        b.HasKey(l => l.Id);
        b.Property(l => l.RuleCode).HasMaxLength(50).IsRequired();
        b.Property(l => l.Level).HasConversion<int>();
        b.Property(l => l.ActionsJson).HasMaxLength(2000);
        b.Property(l => l.Notes).HasMaxLength(2000);
        b.Property(l => l.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(l => l.ModifiedBy).HasMaxLength(100);

        b.HasIndex(l => l.OrderId);
        b.HasIndex(l => l.TestResultId);
        b.HasIndex(l => l.RuleId);
    }
}

public sealed class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> b)
    {
        b.ToTable("ProductAttributes");
        b.HasKey(a => a.Id);
        b.Property(a => a.Code).HasMaxLength(50).IsRequired();
        b.Property(a => a.Name).HasMaxLength(150).IsRequired();
        b.Property(a => a.Description).HasMaxLength(500);
        b.Property(a => a.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(a => a.ModifiedBy).HasMaxLength(100);

        b.HasIndex(a => a.Code).IsUnique();
    }
}

public sealed class ProductAttributeAssignmentConfiguration : IEntityTypeConfiguration<ProductAttributeAssignment>
{
    public void Configure(EntityTypeBuilder<ProductAttributeAssignment> b)
    {
        b.ToTable("ProductAttributeAssignments");
        b.HasKey(a => a.Id);
        b.Property(a => a.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(a => a.ModifiedBy).HasMaxLength(100);

        b.HasOne(a => a.ProductType).WithMany(t => t.AttributeAssignments).HasForeignKey(a => a.ProductTypeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.ProductAttribute).WithMany(p => p.Assignments).HasForeignKey(a => a.ProductAttributeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.ProductTypeId, a.ProductAttributeId }).IsUnique();
    }
}

public sealed class BloodComponentRawScanConfiguration : IEntityTypeConfiguration<BloodComponentRawScan>
{
    public void Configure(EntityTypeBuilder<BloodComponentRawScan> b)
    {
        b.ToTable("BloodComponentRawScans");
        b.HasKey(x => x.Id);
        b.Property(x => x.OriginalValue).HasMaxLength(500).IsRequired();
        b.Property(x => x.SanitizedValue).HasMaxLength(500).IsRequired();
        b.Property(x => x.NormalizedValue).HasMaxLength(200);
        b.Property(x => x.EnteredBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.BloodProductId);
    }
}

public sealed class BloodComponentSpecialTestConfiguration : IEntityTypeConfiguration<BloodComponentSpecialTest>
{
    public void Configure(EntityTypeBuilder<BloodComponentSpecialTest> b)
    {
        b.ToTable("BloodComponentSpecialTests");
        b.HasKey(x => x.Id);
        b.Property(x => x.TestCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.Result).HasMaxLength(200);
        b.Property(x => x.StandardVersion).HasMaxLength(50);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.BloodProductId);
    }
}

public sealed class BloodComponentScanSessionConfiguration : IEntityTypeConfiguration<BloodComponentScanSession>
{
    public void Configure(EntityTypeBuilder<BloodComponentScanSession> b)
    {
        b.ToTable("BloodComponentScanSessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExpectedStructuresJson).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ReceivedStructuresJson).HasMaxLength(4000).IsRequired();
        b.Property(x => x.DraftJson).IsRequired();
        b.Property(x => x.StartedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.CompletedComponentIdentity).HasMaxLength(80);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.SessionKey).IsUnique();
        b.HasMany(x => x.Lines).WithOne(l => l.Session!).HasForeignKey(l => l.ScanSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BloodComponentScanSessionLineConfiguration : IEntityTypeConfiguration<BloodComponentScanSessionLine>
{
    public void Configure(EntityTypeBuilder<BloodComponentScanSessionLine> b)
    {
        b.ToTable("BloodComponentScanSessionLines");
        b.HasKey(x => x.Id);
        b.Property(x => x.OriginalValue).HasMaxLength(500).IsRequired();
        b.Property(x => x.SanitizedValue).HasMaxLength(500).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.ScanSessionId);
    }
}

public sealed class IsbtAboRhdCodeConfiguration : IEntityTypeConfiguration<IsbtAboRhdCode>
{
    public void Configure(EntityTypeBuilder<IsbtAboRhdCode> b)
    {
        b.ToTable("IsbtAboRhdCodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.Property(x => x.CollectionType).HasMaxLength(100);
        b.Property(x => x.SpecialMessage).HasMaxLength(200);
        b.Property(x => x.AdditionalPhenotype).HasMaxLength(100);
        b.Property(x => x.StandardVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.Code, x.StandardVersion }).IsUnique();
    }
}

public sealed class IsbtProductCodeConfiguration : IEntityTypeConfiguration<IsbtProductCode>
{
    public void Configure(EntityTypeBuilder<IsbtProductCode> b)
    {
        b.ToTable("IsbtProductCodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductDescriptionCode).HasMaxLength(5).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.ComponentClass).HasMaxLength(50).IsRequired();
        b.Property(x => x.Modifier).HasMaxLength(50);
        b.Property(x => x.AttributesJson).HasMaxLength(2000).IsRequired();
        b.Property(x => x.StorageRequirements).HasMaxLength(200);
        b.Property(x => x.StandardVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.ProductDescriptionCode, x.StandardVersion }).IsUnique();
    }
}

public sealed class IsbtCollectionTypeConfiguration : IEntityTypeConfiguration<IsbtCollectionType>
{
    public void Configure(EntityTypeBuilder<IsbtCollectionType> b)
    {
        b.ToTable("IsbtCollectionTypes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(5).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.StandardVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class IsbtDataStructureConfiguration : IEntityTypeConfiguration<IsbtDataStructure>
{
    public void Configure(EntityTypeBuilder<IsbtDataStructure> b)
    {
        b.ToTable("IsbtDataStructures");
        b.HasKey(x => x.Id);
        b.Property(x => x.DataIdentifier).HasMaxLength(5).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.StandardVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.DataIdentifier).IsUnique();
    }
}

public sealed class BloodComponentCompatibilityDecisionConfiguration : IEntityTypeConfiguration<BloodComponentCompatibilityDecision>
{
    public void Configure(EntityTypeBuilder<BloodComponentCompatibilityDecision> b)
    {
        b.ToTable("BloodComponentCompatibilityDecisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.PolicyVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.RulesVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.EvaluatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BloodProductId, x.PatientId, x.EvaluatedAt });
    }
}

public sealed class BloodComponentIdentityCorrectionConfiguration : IEntityTypeConfiguration<BloodComponentIdentityCorrection>
{
    public void Configure(EntityTypeBuilder<BloodComponentIdentityCorrection> b)
    {
        b.ToTable("BloodComponentIdentityCorrections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Field).HasMaxLength(50).IsRequired();
        b.Property(x => x.OriginalValue).HasMaxLength(200).IsRequired();
        b.Property(x => x.CorrectedValue).HasMaxLength(200).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.CorrectedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ApproverId).HasMaxLength(100);
        b.Property(x => x.SupportingEvidence).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.BloodProductId);
    }
}

public sealed class BloodComponentExceptionConfiguration : IEntityTypeConfiguration<BloodComponentException>
{
    public void Configure(EntityTypeBuilder<BloodComponentException> b)
    {
        b.ToTable("BloodComponentExceptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExceptionCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Severity).HasMaxLength(20).IsRequired();
        b.Property(x => x.OverrideCode).HasMaxLength(80);
        b.Property(x => x.OverrideReason).HasMaxLength(1000);
        b.Property(x => x.ApproverId).HasMaxLength(100);
        b.Property(x => x.RecordedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.BloodProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.BloodProductId);
    }
}

public sealed class CompatibilityRuleVersionConfiguration : IEntityTypeConfiguration<CompatibilityRuleVersion>
{
    public void Configure(EntityTypeBuilder<CompatibilityRuleVersion> b)
    {
        b.ToTable("CompatibilityRuleVersions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).HasMaxLength(50).IsRequired();
        b.Property(x => x.PolicyVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.Version).IsUnique();
        b.HasMany(x => x.Rules).WithOne(r => r.Version!).HasForeignKey(r => r.CompatibilityRuleVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CompatibilityRuleConfiguration : IEntityTypeConfiguration<CompatibilityRule>
{
    public void Configure(EntityTypeBuilder<CompatibilityRule> b)
    {
        b.ToTable("CompatibilityRules");
        b.HasKey(x => x.Id);
        b.Property(x => x.RuleCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.RuleFamily).HasMaxLength(50).IsRequired();
        b.Property(x => x.ExpressionJson).IsRequired();
        b.Property(x => x.Severity).HasMaxLength(20).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.CompatibilityRuleVersionId, x.RuleCode }).IsUnique();
    }
}

public sealed class SpecialTransfusionRequirementConfiguration : IEntityTypeConfiguration<SpecialTransfusionRequirement>
{
    public void Configure(EntityTypeBuilder<SpecialTransfusionRequirement> b)
    {
        b.ToTable("SpecialTransfusionRequirements");
        b.HasKey(x => x.Id);
        b.Property(x => x.AntigenCode).HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.EnteredBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.DeactivationReason).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.PatientId, x.IsActive });
    }
}

public sealed class PatientIdentifierConfiguration : IEntityTypeConfiguration<PatientIdentifier>
{
    public void Configure(EntityTypeBuilder<PatientIdentifier> b)
    {
        b.ToTable("PatientIdentifiers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasMaxLength(100).IsRequired();
        b.Property(x => x.AssigningAuthority).HasMaxLength(100);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.IdentifierType, x.Value, x.AssigningAuthority }).IsUnique();
        b.HasIndex(x => x.PatientId);
    }
}

public sealed class ReactionInvestigationConfiguration : IEntityTypeConfiguration<ReactionInvestigation>
{
    public void Configure(EntityTypeBuilder<ReactionInvestigation> b)
    {
        b.ToTable("ReactionInvestigations");
        b.HasKey(x => x.Id);
        b.Property(x => x.ReportedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ReactionType).HasMaxLength(100);
        b.Property(x => x.Findings).HasMaxLength(4000);
        b.Property(x => x.Conclusions).HasMaxLength(4000);
        b.Property(x => x.FollowUp).HasMaxLength(2000);
        b.Property(x => x.Disposition).HasMaxLength(500);
        b.Property(x => x.ClosedBy).HasMaxLength(100);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasOne(x => x.TransfusionEvent).WithMany().HasForeignKey(x => x.TransfusionEventId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.TransfusionEventId);
        b.HasIndex(x => x.PatientId);
    }
}

public sealed class LookbackNotificationConfiguration : IEntityTypeConfiguration<LookbackNotification>
{
    public void Configure(EntityTypeBuilder<LookbackNotification> b)
    {
        b.ToTable("LookbackNotifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Din).HasMaxLength(13).IsRequired();
        b.Property(x => x.PhysicianOfRecord).HasMaxLength(200);
        b.Property(x => x.AttemptedBy).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.Din);
        b.HasIndex(x => x.PatientId);
    }
}

public sealed class DeviationConfiguration : IEntityTypeConfiguration<Deviation>
{
    public void Configure(EntityTypeBuilder<Deviation> b)
    {
        b.ToTable("Deviations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        b.Property(x => x.ContextType).HasMaxLength(100);
        b.Property(x => x.CorrectiveAction).HasMaxLength(4000);
        b.Property(x => x.ReportedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ClosedBy).HasMaxLength(100);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.Status);
    }
}

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.ToTable("SystemSettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(150).IsRequired();
        b.Property(x => x.Value).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Category).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.Key).IsUnique();
    }
}
