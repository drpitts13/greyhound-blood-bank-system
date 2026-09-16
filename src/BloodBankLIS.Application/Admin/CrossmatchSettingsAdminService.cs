using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

public sealed record CrossmatchSettingsDto(
    long Id,
    string NegativeAntibodyHistoryTestCode,
    string PositiveAntibodyHistoryTestCode,
    string ElectronicCrossmatchTestCode,
    int ElectronicXmMinimumVisits,
    int ElectronicXmMinimumSpecimens,
    int ElectronicXmMinimumTests,
    int Version,
    bool AllowElectronicCrossmatch,
    IReadOnlyList<CrossmatchTestOptionDto> CrossmatchTests)
{
    public static CrossmatchSettingsDto From(
        CrossmatchSettings settings,
        bool allowElectronic,
        IReadOnlyList<CrossmatchTestOptionDto> tests) =>
        new(
            settings.Id,
            settings.NegativeAntibodyHistoryTestCode,
            settings.PositiveAntibodyHistoryTestCode,
            settings.ElectronicCrossmatchTestCode,
            settings.ElectronicXmMinimumVisits,
            settings.ElectronicXmMinimumSpecimens,
            settings.ElectronicXmMinimumTests,
            settings.Version,
            allowElectronic,
            tests);
}

public sealed record SaveCrossmatchSettingsRequest(
    string NegativeAntibodyHistoryTestCode,
    string PositiveAntibodyHistoryTestCode,
    string ElectronicCrossmatchTestCode,
    int ElectronicXmMinimumVisits,
    int ElectronicXmMinimumSpecimens,
    int ElectronicXmMinimumTests,
    string Reason);

public sealed class CrossmatchSettingsAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(CrossmatchSettings);

    private readonly IRepository<CrossmatchSettings> _settings;
    private readonly IRepository<TestDefinition> _tests;
    private readonly FacilityPolicyService _policy;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public CrossmatchSettingsAdminService(
        IRepository<CrossmatchSettings> settings,
        IRepository<TestDefinition> tests,
        FacilityPolicyService policy,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _settings = settings;
        _tests = tests;
        _policy = policy;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<CrossmatchSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var entity = await GetOrCreateAsync(ct);
        return await ToDtoAsync(entity, ct);
    }

    public async Task<EvaluationResult<CrossmatchSettingsDto>> UpdateAsync(
        SaveCrossmatchSettingsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await GetOrCreateAsync(ct);
        var old = await ToDtoAsync(entity, ct);
        var negative = await FindTestAsync(request.NegativeAntibodyHistoryTestCode, ct);
        var positive = await FindTestAsync(request.PositiveAntibodyHistoryTestCode, ct);
        var electronic = await FindTestAsync(request.ElectronicCrossmatchTestCode, ct);

        entity.NegativeAntibodyHistoryTestCode = NormalizeCode(request.NegativeAntibodyHistoryTestCode);
        entity.PositiveAntibodyHistoryTestCode = NormalizeCode(request.PositiveAntibodyHistoryTestCode);
        entity.ElectronicCrossmatchTestCode = NormalizeCode(request.ElectronicCrossmatchTestCode);
        entity.ElectronicXmMinimumVisits = request.ElectronicXmMinimumVisits;
        entity.ElectronicXmMinimumSpecimens = request.ElectronicXmMinimumSpecimens;
        entity.ElectronicXmMinimumTests = request.ElectronicXmMinimumTests;

        var validation = CrossmatchSettingsValidator.Validate(entity, request.Reason, negative, positive, electronic);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<CrossmatchSettingsDto>.Blocked(validation);
        }
        entity.Version += 1;
        entity.ChangeReason = request.Reason.Trim();
        entity.IsActive = true;
        entity.IsDraft = false;
        entity.EffectiveUtc ??= Clock.UtcNow;
        _settings.Update(entity);

        var dto = await ToDtoAsync(entity, ct);
        RecordChange(
            EntityType,
            entity.Id,
            entity.Version,
            ConfigChangeAction.Update,
            AuditEventType.Configure,
            old,
            dto,
            request.Reason.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CrossmatchSettingsDto>.Ok(dto, validation);
    }

    private async Task<CrossmatchSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var listed = (await _settings.ListAsync(ct))
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.Version)
            .FirstOrDefault();
        if (listed is not null)
        {
            return await _settings.GetByIdAsync(listed.Id, ct) ?? listed;
        }

        var created = CrossmatchSettings.CreateDefault();
        created.EffectiveUtc = Clock.UtcNow;
        await _settings.AddAsync(created, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        return created;
    }

    private async Task<CrossmatchSettingsDto> ToDtoAsync(CrossmatchSettings entity, CancellationToken ct)
    {
        var allow = await _policy.GetAllowElectronicCrossmatchAsync(ct);
        var tests = await ListCrossmatchTestsAsync(ct);
        return CrossmatchSettingsDto.From(entity, allow, tests);
    }

    private async Task<IReadOnlyList<CrossmatchTestOptionDto>> ListCrossmatchTestsAsync(CancellationToken ct)
    {
        var tests = await _tests.ListAsync(
            t => t.IsActive
                 && !t.IsDraft
                 && (t.ResultValueType == ResultValueType.Crossmatch
                     || t.ResultValueType == ResultValueType.ComplexCrossmatch),
            ct);
        return tests
            .OrderBy(t => t.ResultValueType)
            .ThenBy(t => t.Code)
            .Select(t => new CrossmatchTestOptionDto(t.Code, t.Name, t.ResultValueType))
            .ToList();
    }

    private Task<TestDefinition?> FindTestAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult<TestDefinition?>(null);
        }

        var normalized = NormalizeCode(code);
        return _tests.FirstOrDefaultAsync(t => t.Code == normalized, ct);
    }

    private static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<EvaluationResult<CrossmatchSettingsDto>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            CurrentUser.UserName, PermissionCodes.AdminConfigEdit, ct);
        var auth = CrossmatchSettingsAuthorizationRule.EvaluateUpdate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<CrossmatchSettingsDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
