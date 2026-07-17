using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// Resolves ordering locations from the catalog and upserts locations received via HL7.
/// </summary>
public sealed class OrderingLocationService
{
    private readonly IRepository<OrderingLocation> _locations;
    private readonly IUnitOfWork _unitOfWork;

    public OrderingLocationService(IRepository<OrderingLocation> locations, IUnitOfWork unitOfWork)
    {
        _locations = locations;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderingLocation?> EnsureFromHl7Async(
        string? code,
        string? name,
        string? department,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();
        var existing = await _locations.FirstOrDefaultAsync(
            l => l.Code == normalized || l.Hl7MappingCode == normalized, ct);

        if (existing is not null)
        {
            var changed = false;
            if (!string.IsNullOrWhiteSpace(name) && existing.Name != name.Trim())
            {
                existing.Name = name.Trim();
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(department) && existing.Department != department.Trim())
            {
                existing.Department = department.Trim();
                changed = true;
            }

            if (existing.Hl7MappingCode != normalized)
            {
                existing.Hl7MappingCode = normalized;
                changed = true;
            }

            if (changed)
            {
                _locations.Update(existing);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return existing;
        }

        var location = new OrderingLocation
        {
            Code = normalized,
            Name = string.IsNullOrWhiteSpace(name) ? normalized : name.Trim(),
            Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim(),
            Hl7MappingCode = normalized,
            IsActive = true
        };

        var validation = OrderingLocationValidator.Validate(location, duplicateCode: false);
        if (validation.IsHardStopped)
        {
            return null;
        }

        await _locations.AddAsync(location, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return location;
    }
}
