using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class ProductAboRhAdminTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ProductAboRhAdminTests(SqliteContextFactory factory) => _factory = factory;

    private ProductAdminService Products(BloodBankDbContext c) =>
        new(
            new EfRepository<ProductType>(c),
            new EfRepository<ProductAttribute>(c),
            new EfRepository<ProductAttributeAssignment>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", isDevMode: false)),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", isDevMode: false)),
            aboCompatibilities: new EfRepository<ProductAboCompatibility>(c),
            rhCompatibilities: new EfRepository<ProductRhCompatibility>(c));

    private static SaveProductDefinitionRequest Request(
        string code,
        ComponentClass componentClass,
        IReadOnlyList<ProductAboCompatibilityDto>? abo = null,
        IReadOnlyList<ProductRhCompatibilityDto>? rh = null) =>
        new(
            code,
            "Temp product",
            componentClass,
            null,
            24,
            false,
            true,
            true,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            "Catalog.",
            AboCompatibilities: abo,
            RhCompatibilities: rh);

    [Fact]
    public async Task Create_WithoutGrid_StoresTheStandardMatrix()
    {
        await using var c = _factory.Create();
        var code = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var created = await Products(c).CreateAsync(Request(code, ComponentClass.Plasma));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(StandardProductAboRhPolicy.AboCellCount, created.Value!.AboCompatibilities!.Count);
        Assert.Equal(StandardProductAboRhPolicy.RhCellCount, created.Value.RhCompatibilities!.Count);
        Assert.Contains(created.Value.AboCompatibilities, cell =>
            cell.PatientAbo == AboGroup.O && cell.UnitAbo == AboGroup.AB && cell.IsAllowed);

        var loaded = await Products(c).GetAsync(created.Value.Id);
        Assert.Equal(StandardProductAboRhPolicy.AboCellCount, loaded!.AboCompatibilities!.Count);
    }

    [Fact]
    public async Task Update_PersistsAMaleOnlyRhAllowance()
    {
        await using var c = _factory.Create();
        var code = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var created = await Products(c).CreateAsync(Request(code, ComponentClass.RedBloodCells));
        Assert.True(created.Succeeded, created.Error);

        var policy = StandardProductAboRhPolicy.For(ComponentClass.RedBloodCells)
            .WithRh(RhCompatibilitySex.Male, RhType.Negative, RhType.Positive, true);
        var abo = policy.AboCells
            .Select(cell => new ProductAboCompatibilityDto(cell.PatientAbo, cell.UnitAbo, cell.IsAllowed))
            .ToList();
        var rh = policy.RhCells
            .Select(cell => new ProductRhCompatibilityDto(cell.SexGroup, cell.PatientRh, cell.UnitRh, cell.IsAllowed))
            .ToList();

        var updated = await Products(c).UpdateAsync(
            created.Value!.Id,
            Request(code, ComponentClass.RedBloodCells, abo, rh));
        Assert.True(updated.Succeeded, updated.Error);

        var male = updated.Value!.RhCompatibilities!.Single(cell =>
            cell.SexGroup == RhCompatibilitySex.Male
            && cell.PatientRh == RhType.Negative
            && cell.UnitRh == RhType.Positive);
        var female = updated.Value.RhCompatibilities!.Single(cell =>
            cell.SexGroup == RhCompatibilitySex.Female
            && cell.PatientRh == RhType.Negative
            && cell.UnitRh == RhType.Positive);
        Assert.True(male.IsAllowed);
        Assert.False(female.IsAllowed);

        var reloaded = await Products(c).GetAsync(created.Value.Id);
        Assert.True(reloaded!.RhCompatibilities!.Single(cell =>
            cell.SexGroup == RhCompatibilitySex.Male
            && cell.PatientRh == RhType.Negative
            && cell.UnitRh == RhType.Positive).IsAllowed);
    }

    [Fact]
    public async Task Update_RejectsAPartialGrid()
    {
        await using var c = _factory.Create();
        var code = $"P{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var created = await Products(c).CreateAsync(Request(code, ComponentClass.RedBloodCells));
        Assert.True(created.Succeeded, created.Error);

        var partial = await Products(c).UpdateAsync(
            created.Value!.Id,
            Request(
                code,
                ComponentClass.RedBloodCells,
                [new ProductAboCompatibilityDto(AboGroup.O, AboGroup.O, true)],
                []));
        Assert.False(partial.Succeeded);
        Assert.Contains(partial.Evaluation!.HardStops, r => r.Code == "PRODUCT.ABORH.INCOMPLETE");
    }
}
