using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Issuing;

public sealed record OverdueDispositionWorkItemDto(
    long IssueId,
    long UnitId,
    string UnitNumber,
    string PatientLabel,
    DateTime IssuedUtc,
    DateTime DueUtc);

public sealed class FinalDispositionBoardService
{
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<Return> _returns;
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<Patient> _patients;
    private readonly FacilityPolicyService _policy;
    private readonly IClock _clock;

    public FinalDispositionBoardService(
        IRepository<Issue> issues,
        IRepository<TransfusionEvent> transfusions,
        IRepository<Return> returns,
        IInventoryRepository inventory,
        IRepository<Patient> patients,
        FacilityPolicyService policy,
        IClock clock)
    {
        _issues = issues;
        _transfusions = transfusions;
        _returns = returns;
        _inventory = inventory;
        _patients = patients;
        _policy = policy;
        _clock = clock;
    }

    public async Task<IReadOnlyList<OverdueDispositionWorkItemDto>> ListAsync(CancellationToken ct = default)
    {
        var hours = await _policy.GetFinalDispositionDueHoursAsync(ct);
        var now = _clock.UtcNow;
        var issued = await _issues.ListAsync(issue => issue.Status == IssueStatus.Issued, ct);
        if (issued.Count == 0)
        {
            return [];
        }

        var issueIds = issued.Select(issue => issue.Id).ToList();
        var transfusions = await _transfusions.ListAsync(row => issueIds.Contains(row.IssueId), ct);
        var returns = await _returns.ListAsync(row => issueIds.Contains(row.IssueId), ct);
        var transfusionIds = transfusions.Select(row => row.IssueId).ToHashSet();
        var returnIds = returns.Select(row => row.IssueId).ToHashSet();
        var items = new List<OverdueDispositionWorkItemDto>();
        foreach (var issue in issued.OrderBy(row => row.IssuedUtc))
        {
            var unit = await _inventory.GetUnitAsync(issue.BloodProductId, ct);
            var discarded = unit?.Status == UnitStatus.Discarded;
            if (!FinalDispositionDueRule.IsOverdue(
                    issue.IssuedUtc, now, hours, transfusionIds.Contains(issue.Id), returnIds.Contains(issue.Id), discarded))
            {
                continue;
            }

            var patient = await _patients.GetByIdAsync(issue.PatientId, ct);
            var patientLabel = patient is null
                ? issue.PatientId.ToString()
                : $"{patient.LastName}, {patient.FirstName} ({patient.MedicalRecordNumber})";
            items.Add(new OverdueDispositionWorkItemDto(
                issue.Id,
                issue.BloodProductId,
                unit?.UnitNumber ?? string.Empty,
                patientLabel,
                issue.IssuedUtc,
                issue.IssuedUtc.AddHours(hours > 0 ? hours : FinalDispositionDueRule.DefaultDueHours)));
        }

        return items;
    }
}
