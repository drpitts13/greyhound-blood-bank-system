using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodBankLIS.Infrastructure.Persistence.Configurations;

public sealed class AntibodyPanelManufacturerConfiguration : IEntityTypeConfiguration<AntibodyPanelManufacturer>
{
    public void Configure(EntityTypeBuilder<AntibodyPanelManufacturer> b)
    {
        b.ToTable("AntibodyPanelManufacturers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ChangeReason).HasMaxLength(1000);
        b.Property(x => x.ApprovedBy).HasMaxLength(100);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.Code);
    }
}

public sealed class AntibodyPanelLotConfiguration : IEntityTypeConfiguration<AntibodyPanelLot>
{
    public void Configure(EntityTypeBuilder<AntibodyPanelLot> b)
    {
        b.ToTable("AntibodyPanelLots");
        b.HasKey(x => x.Id);
        b.Property(x => x.LotNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.PanelName).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.ManufacturerId, x.LotNumber }).IsUnique();
        b.HasIndex(x => x.ExpiresOn);
        b.HasIndex(x => x.IsActive);
    }
}

public sealed class AntibodyPanelCellConfiguration : IEntityTypeConfiguration<AntibodyPanelCell>
{
    public void Configure(EntityTypeBuilder<AntibodyPanelCell> b)
    {
        b.ToTable("AntibodyPanelCells");
        b.HasKey(x => x.Id);
        b.Property(x => x.CellNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.LotId, x.CellNumber }).IsUnique();
        b.HasIndex(x => x.LotId);
    }
}

public sealed class AntibodyPanelCellAntigenConfiguration : IEntityTypeConfiguration<AntibodyPanelCellAntigen>
{
    public void Configure(EntityTypeBuilder<AntibodyPanelCellAntigen> b)
    {
        b.ToTable("AntibodyPanelCellAntigens");
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.CellId, x.BloodAttributeDefinitionId }).IsUnique();
        b.HasIndex(x => x.BloodAttributeDefinitionId);
    }
}

public sealed class AntibodyIdentificationWorkupConfiguration : IEntityTypeConfiguration<AntibodyIdentificationWorkup>
{
    public void Configure(EntityTypeBuilder<AntibodyIdentificationWorkup> b)
    {
        b.ToTable("AntibodyIdentificationWorkups");
        b.HasKey(x => x.Id);
        b.Property(x => x.DatMethod).HasMaxLength(100);
        b.Property(x => x.Comment).HasMaxLength(4000);
        b.Property(x => x.TechnologistInterpretation).HasMaxLength(4000);
        b.Property(x => x.TechnologistUser).HasMaxLength(100);
        b.Property(x => x.SupervisorUser).HasMaxLength(100);
        b.Property(x => x.SupervisorComment).HasMaxLength(2000);
        b.Property(x => x.CompletedBy).HasMaxLength(100);
        b.Property(x => x.VoidReason).HasMaxLength(1000);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.PatientId);
        b.HasIndex(x => x.SpecimenId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.PrimaryLotId);
    }
}

public sealed class AntibodyIdentificationWorkupLotConfiguration : IEntityTypeConfiguration<AntibodyIdentificationWorkupLot>
{
    public void Configure(EntityTypeBuilder<AntibodyIdentificationWorkupLot> b)
    {
        b.ToTable("AntibodyIdentificationWorkupLots");
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.WorkupId, x.LotId }).IsUnique();
    }
}

public sealed class AntibodyIdentificationReactionConfiguration : IEntityTypeConfiguration<AntibodyIdentificationReaction>
{
    public void Configure(EntityTypeBuilder<AntibodyIdentificationReaction> b)
    {
        b.ToTable("AntibodyIdentificationReactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.PhaseCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.WorkupId, x.CellId, x.PhaseCode }).IsUnique();
        b.HasIndex(x => x.CellId);
    }
}

public sealed class AntibodyIdentificationFindingConfiguration : IEntityTypeConfiguration<AntibodyIdentificationFinding>
{
    public void Configure(EntityTypeBuilder<AntibodyIdentificationFinding> b)
    {
        b.ToTable("AntibodyIdentificationFindings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Specificity).HasMaxLength(100).IsRequired();
        b.Property(x => x.Rationale).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.ModifiedBy).HasMaxLength(100);
        b.HasIndex(x => x.WorkupId);
        b.HasIndex(x => x.BloodAttributeDefinitionId);
    }
}
