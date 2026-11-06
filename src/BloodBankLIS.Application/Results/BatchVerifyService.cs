using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Results;

public sealed record BatchVerifyCandidateDto(
    long ResultId,
    long PatientId,
    string PatientName,
    string Mrn,
    string TestCode,
    string? Value,
    string? EnteredBy,
    string? AccessionNumber);

public sealed record BatchVerifyCandidatesDto(bool Enabled, IReadOnlyList<BatchVerifyCandidateDto> Candidates);

public sealed record BatchVerifyRequest(IReadOnlyList<long> ResultIds);

public sealed record BatchVerifySkippedDto(long ResultId, string Reason);

public sealed record BatchVerifyOutcomeDto(Guid BatchId, IReadOnlyList<long> Verified, IReadOnlyList<BatchVerifySkippedDto> Skipped);

/// <summary>
/// Batch verification (OCD-061): lists the results the signed-in person may verify together, and verifies a chosen set.
/// Eligibility (<see cref="BatchVerifyRule"/>) is decided here, on the server, both for the list and again at verify,
/// and each result then goes through the normal single verification with all its checks. A result that fails either
/// is left unverified with the reason. Each verified result gets an audit row naming the batch.
/// </summary>
public sealed class BatchVerifyService
{
    private readonly ResultService _results;
    private readonly TestWorklistService _worklist;
    private readonly IRepository<TestResult> _testResults;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<SpecialTransfusionRequirement> _requirements;
    private readonly IRepository<SpecialRequirementDefinition> _requirementDefinitions;
    private readonly FacilityPolicyService _policy;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public BatchVerifyService(
        ResultService results,
        TestWorklistService worklist,
        IRepository<TestResult> testResults,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IRepository<SpecialTransfusionRequirement> requirements,
        IRepository<SpecialRequirementDefinition> requirementDefinitions,
        FacilityPolicyService policy,
        ICurrentUser currentUser,
        IClock clock,
        IAuditWriter audit,
        IUnitOfWork unitOfWork)
    {
        _results = results;
        _worklist = worklist;
        _testResults = testResults;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _requirements = requirements;
        _requirementDefinitions = requirementDefinitions;
        _policy = policy;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<BatchVerifyCandidatesDto> ListCandidatesAsync(CancellationToken ct = default)
    {
        if (!await _policy.GetAllowBatchVerifyAsync(ct))
        {
            return new BatchVerifyCandidatesDto(false, []);
        }

        var rows = (await _worklist.ListPendingGlobalAsync(ct))
            .Where(r => r.CurrentResultId is > 0 && r.CanEnterResults && BatchVerifyRule.Tests.Contains(r.TestCode))
            .ToList();
        if (rows.Count == 0)
        {
            return new BatchVerifyCandidatesDto(true, []);
        }

        var resultIds = rows.Select(r => r.CurrentResultId!.Value).Distinct().ToList();
        var results = (await _testResults.ListAsync(t => resultIds.Contains(t.Id), ct)).ToDictionary(t => t.Id);
        var facts = await PatientFactsAsync(results.Values.Select(r => r.PatientId), ct);
        var candidates = new List<BatchVerifyCandidateDto>();
        foreach (var row in rows)
        {
            if (!results.TryGetValue(row.CurrentResultId!.Value, out var result)
                || BatchVerifyRule.Evaluate(FactsFor(result, facts)).Severity != RuleSeverity.Pass)
            {
                continue;
            }

            var shown = result.TestCode.Equals("ABORH", StringComparison.OrdinalIgnoreCase)
                ? AboRhResultValue.FormatDisplay(result.Value)
                : result.Value;
            candidates.Add(new BatchVerifyCandidateDto(
                result.Id, row.PatientId, row.PatientName, row.Mrn, result.TestCode, shown, result.EnteredBy, row.AccessionNumber));
        }

        return new BatchVerifyCandidatesDto(true, candidates.DistinctBy(c => c.ResultId).ToList());
    }

    public async Task<OperationResult<BatchVerifyOutcomeDto>> VerifyAsync(BatchVerifyRequest request, CancellationToken ct = default)
    {
        if (!await _policy.GetAllowBatchVerifyAsync(ct))
        {
            return OperationResult<BatchVerifyOutcomeDto>.Fail("Batch verification is turned off in Facility Policies.");
        }

        var ids = (request.ResultIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return OperationResult<BatchVerifyOutcomeDto>.Fail("Choose at least one result to verify.");
        }

        var batchId = Guid.NewGuid();
        var verified = new List<long>();
        var skipped = new List<BatchVerifySkippedDto>();
        var results = (await _testResults.ListAsync(t => ids.Contains(t.Id), ct)).ToDictionary(t => t.Id);
        var facts = await PatientFactsAsync(results.Values.Select(r => r.PatientId), ct);
        foreach (var id in ids)
        {
            if (!results.TryGetValue(id, out var result))
            {
                skipped.Add(new BatchVerifySkippedDto(id, "Result not found."));
                continue;
            }

            // Decided again here: the list may be stale, and a caller may send any id.
            var eligibility = BatchVerifyRule.Evaluate(FactsFor(result, facts));
            if (eligibility.Severity != RuleSeverity.Pass)
            {
                skipped.Add(new BatchVerifySkippedDto(id, eligibility.Message));
                continue;
            }

            var outcome = await _results.VerifyResultAsync(id, ct: ct);
            if (!outcome.Succeeded)
            {
                var stops = string.Join(" ", outcome.Evaluation?.HardStops.Select(r => r.Message) ?? []);
                var reason = !string.IsNullOrWhiteSpace(outcome.Error) ? outcome.Error!
                    : !string.IsNullOrWhiteSpace(stops) ? stops
                    : "Not verified.";
                skipped.Add(new BatchVerifySkippedDto(id, reason));
                continue;
            }

            verified.Add(id);
            _audit.Record(
                AuditEventType.Verify,
                nameof(TestResult),
                id,
                newValue: new { BatchId = batchId, result.TestCode, result.PatientId, BatchSize = ids.Count },
                reason: $"Verified in batch {batchId}.");
        }

        if (verified.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<BatchVerifyOutcomeDto>.Ok(new BatchVerifyOutcomeDto(batchId, verified, skipped));
    }

    private sealed record PatientFacts(
        Dictionary<long, AboRh> CurrentTypes,
        HashSet<long> WithAntibodies,
        HashSet<long> WithRequirements);

    private async Task<PatientFacts> PatientFactsAsync(IEnumerable<long> patientIdsIn, CancellationToken ct)
    {
        var patientIds = patientIdsIn.Distinct().ToList();
        var types = (await _bloodTypes.ListAsync(h => patientIds.Contains(h.PatientId) && h.IsCurrent && h.InvalidatedUtc == null, ct))
            .GroupBy(h => h.PatientId)
            .Select(g => g.OrderByDescending(h => h.Id).First())
            .Where(h => h.Abo != AboGroup.Unknown && h.RhD != RhType.Unknown)
            .ToDictionary(h => h.PatientId, h => new AboRh(h.Abo, h.RhD));
        var antibodies = (await _antibodies.ListAsync(a => patientIds.Contains(a.PatientId), ct)).Select(a => a.PatientId).ToHashSet();
        var informational = PatientBloodBankFlags.InformationalIds(await _requirementDefinitions.ListAsync(ct));
        var now = _clock.UtcNow;
        var requirements = (await _requirements.ListAsync(r => patientIds.Contains(r.PatientId), ct))
            .GroupBy(r => r.PatientId)
            .Where(g => PatientBloodBankFlags.RequirementsInEffect(g, now, informational) > 0)
            .Select(g => g.Key)
            .ToHashSet();
        return new PatientFacts(types, antibodies, requirements);
    }

    private BatchVerifyRule.Facts FactsFor(TestResult result, PatientFacts facts) =>
        new(result.TestCode, result.Value, result.Status, result.EnteredBy, _currentUser.UserName,
            facts.CurrentTypes.TryGetValue(result.PatientId, out var type) ? type : null,
            facts.WithAntibodies.Contains(result.PatientId),
            facts.WithRequirements.Contains(result.PatientId));
}
