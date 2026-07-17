using BloodBankLIS.Application.Services;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class EntityCrudServiceTests
{
    private static Patient NewPatient() => new()
    {
        MedicalRecordNumber = "MRN-TEST",
        LastName = "Test",
        FirstName = "Pat",
        DateOfBirth = new DateOnly(1990, 1, 1),
        Sex = Sex.Female
    };

    [Fact]
    public async Task CreateAsync_AddsEntity_AndCommitsOnce()
    {
        var repo = new FakeRepository<Patient>();
        var uow = new FakeUnitOfWork();
        var service = new EntityCrudService<Patient>(repo, uow);

        var created = await service.CreateAsync(NewPatient());

        Assert.True(created.Id > 0);
        Assert.Single(repo.Store);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_CommitsChange()
    {
        var repo = new FakeRepository<Patient>();
        var uow = new FakeUnitOfWork();
        var service = new EntityCrudService<Patient>(repo, uow);
        var created = await service.CreateAsync(NewPatient());

        created.LastName = "Renamed";
        await service.UpdateAsync(created);

        Assert.Equal("Renamed", repo.Store.Single().LastName);
        Assert.Equal(2, uow.SaveCount);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenMissing()
    {
        var service = new EntityCrudService<Patient>(new FakeRepository<Patient>(), new FakeUnitOfWork());
        Assert.Null(await service.GetAsync(999));
    }

    [Fact]
    public async Task CreateAsync_NullEntity_Throws()
    {
        var service = new EntityCrudService<Patient>(new FakeRepository<Patient>(), new FakeUnitOfWork());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync(null!));
    }
}
