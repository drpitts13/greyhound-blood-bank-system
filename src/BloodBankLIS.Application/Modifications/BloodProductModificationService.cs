using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Modifications;

/// <summary>
/// Performs blood-product modifications (Divide, Pool, Irradiate, Thaw, Volume
/// Reduction, Leukoreduction) under an admin-configured <see cref="ModificationRule"/>.
/// Every modification retires its source unit(s) into the terminal <see cref="UnitStatus.Modified"/>
/// status and produces new result unit(s) in <see cref="UnitStatus.Quarantine"/>, linked
/// via a <see cref="UnitModification"/> header + <see cref="UnitModificationUnit"/> rows.
/// A dangerous action: requires a reason and records a named <see cref="AuditEventType.Modify"/>
/// audit event in addition to the automatic Create/Update audit (see docs/safety-rules.md).
/// </summary>
public sealed class BloodProductModificationService
{
    private readonly IInventoryRepository _inventory;
    private readonly IRepository<ModificationRule> _rules;
    private readonly IRepository<ProductType> _products;
    private readonly IRepository<ExpirationModificationCode> _expirationCodes;
    private readonly IRepository<UnitModification> _modifications;
    private readonly IRepository<UnitModificationUnit> _modificationUnits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    public BloodProductModificationService(
        IInventoryRepository inventory,
        IRepository<ModificationRule> rules,
        IRepository<ProductType> products,
        IRepository<ExpirationModificationCode> expirationCodes,
        IRepository<UnitModification> modifications,
        IRepository<UnitModificationUnit> modificationUnits,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _inventory = inventory;
        _rules = rules;
        _products = products;
        _expirationCodes = expirationCodes;
        _modifications = modifications;
        _modificationUnits = modificationUnits;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    /// <summary>Active modification rules whose source product matches the unit's current product type.</summary>
    public async Task<IReadOnlyList<EligibleModificationDto>> GetEligibleModificationsAsync(long unitId, CancellationToken ct = default)
    {
        var unit = await _inventory.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return Array.Empty<EligibleModificationDto>();
        }

        var rules = await _rules.ListAsync(r => r.IsActive && r.SourceProductTypeId == unit.ProductTypeId, ct);
        if (rules.Count == 0)
        {
            return Array.Empty<EligibleModificationDto>();
        }

        var products = await _products.ListAsync(ct);
        var codes = await _expirationCodes.ListAsync(ct);
        var now = _clock.UtcNow;
        var collection = ModificationExpirationRule.ResolveCollectionUtc(unit.CollectedUtc, unit.CollectionDateTime);
        return rules
            .OrderBy(r => r.ModificationType)
            .Select(r =>
            {
                var code = codes.FirstOrDefault(c => c.Id == r.ExpirationModificationCodeId);
                var requiresCollection = code?.RelativeTo == ExpirationRelativeTo.CollectionDateTime;
                DateTime? preview = null;
                var available = false;
                if (code is not null
                    && ModificationExpirationRule.TryResolveAnchorUtc(
                        code.RelativeTo, now, new[] { collection }, out var anchor, out _))
                {
                    available = true;
                    preview = ModificationExpirationRule.ComputeNewExpiresUtc(anchor, code.ToOffset(), unit.ExpiresUtc);
                }
                return new EligibleModificationDto(
                    r.Id, r.ModificationCode, r.ModificationType, r.TargetProductTypeId,
                    DisplayProductCode(products.FirstOrDefault(p => p.Id == r.TargetProductTypeId)),
                    code?.Code ?? string.Empty,
                    code?.RelativeTo ?? ExpirationRelativeTo.ModificationDateTime,
                    r.Description, preview, requiresCollection, available);
            })
            .ToList();
    }

    /// <summary>Modification history for a unit, whether it participated as a source or a result.</summary>
    public async Task<IReadOnlyList<UnitModificationDto>> GetHistoryAsync(long unitId, CancellationToken ct = default)
    {
        var myLinks = await _modificationUnits.ListAsync(l => l.BloodProductId == unitId, ct);
        if (myLinks.Count == 0)
        {
            return Array.Empty<UnitModificationDto>();
        }

        var modIds = myLinks.Select(l => l.UnitModificationId).Distinct().ToList();
        var mods = await _modifications.ListAsync(m => modIds.Contains(m.Id), ct);
        var allLinks = await _modificationUnits.ListAsync(l => modIds.Contains(l.UnitModificationId), ct);
        var unitIds = allLinks.Select(l => l.BloodProductId).Distinct().ToList();
        var units = await LoadUnitsAsync(unitIds, ct);
        var rules = await _rules.ListAsync(ct);
        var products = await _products.ListAsync(ct);

        var unitById = units.ToDictionary(u => u.Id);
        var ruleById = rules.ToDictionary(r => r.Id);

        return mods
            .OrderByDescending(m => m.PerformedUtc)
            .Select(m =>
            {
                var links = allLinks.Where(l => l.UnitModificationId == m.Id).OrderBy(l => l.SortOrder).ToList();
                ruleById.TryGetValue(m.ModificationRuleId, out var rule);
                var sourceCode = rule is not null
                    ? DisplayProductCode(products.FirstOrDefault(p => p.Id == rule.SourceProductTypeId))
                    : null;
                var targetCode = rule is not null
                    ? DisplayProductCode(products.FirstOrDefault(p => p.Id == rule.TargetProductTypeId))
                    : null;

                return new UnitModificationDto(
                    m.Id, m.ModificationType, sourceCode ?? string.Empty, targetCode ?? string.Empty,
                    m.ExpirationOffsetCodeApplied, m.ResultExpiresUtc, m.Reason, m.PerformedBy, m.PerformedUtc,
                    links.Select(l => new ModificationUnitSummaryDto(
                        l.BloodProductId, unitById.GetValueOrDefault(l.BloodProductId)?.UnitNumber ?? string.Empty, l.Role)).ToList());
            })
            .ToList();
    }

    /// <summary>Divides one source unit into two or more result units of the rule's target product.</summary>
    public async Task<ModificationActionResult> DivideAsync(long sourceUnitId, PerformDivideRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ModificationActionResult.Fail("A reason is required to perform a modification.");
        }

        var rule = await _rules.GetByIdAsync(request.RuleId, ct);
        if (rule is null || !rule.IsActive)
        {
            return ModificationActionResult.Fail("Modification rule not found or inactive.");
        }

        if (rule.ModificationType != ModificationType.Divide)
        {
            return ModificationActionResult.Fail("The selected rule is not a Divide rule.");
        }

        var source = await _inventory.GetUnitAsync(sourceUnitId, ct);
        if (source is null)
        {
            return ModificationActionResult.Fail("Source unit not found.");
        }

        var children = request.Children ?? Array.Empty<DivideChildSpec>();
        var divideEval = UnitModificationEligibilityRule.EvaluateDivide(
            children.Count, source.Volume, children.Select(c => c.Volume).ToList());
        if (divideEval.IsHardStopped)
        {
            return ModificationActionResult.Blocked(divideEval);
        }

        var sourceEval = UnitModificationEligibilityRule.EvaluateSource(ToSnapshot(source), rule.SourceProductTypeId, _clock.UtcNow);
        if (sourceEval.IsHardStopped)
        {
            return ModificationActionResult.Blocked(sourceEval);
        }

        var specs = children.Select(c => (Suffix: c.UnitNumberSuffix, Volume: c.Volume)).ToList();
        return await ExecuteAsync(new[] { source }, rule, specs, request.Reason, ct);
    }

    /// <summary>Pools two or more source units of the same product/ABO/Rh into one result unit of the rule's target product.</summary>
    public async Task<ModificationActionResult> PoolAsync(PerformPoolRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ModificationActionResult.Fail("A reason is required to perform a modification.");
        }

        var rule = await _rules.GetByIdAsync(request.RuleId, ct);
        if (rule is null || !rule.IsActive)
        {
            return ModificationActionResult.Fail("Modification rule not found or inactive.");
        }

        if (rule.ModificationType != ModificationType.Pool)
        {
            return ModificationActionResult.Fail("The selected rule is not a Pool rule.");
        }

        var sourceIds = (request.SourceUnitIds ?? Array.Empty<long>()).Distinct().ToList();
        var sources = new List<BloodUnit>();
        foreach (var id in sourceIds)
        {
            var unit = await _inventory.GetUnitAsync(id, ct);
            if (unit is null)
            {
                return ModificationActionResult.Fail($"Source unit {id} not found.");
            }

            sources.Add(unit);
        }

        var now = _clock.UtcNow;
        var perUnitEval = new RuleEvaluation(sources
            .SelectMany(s => UnitModificationEligibilityRule.EvaluateSource(ToSnapshot(s), rule.SourceProductTypeId, now).Results));
        if (perUnitEval.IsHardStopped)
        {
            return ModificationActionResult.Blocked(perUnitEval);
        }

        var poolEval = UnitModificationEligibilityRule.EvaluatePool(sources.Select(ToSnapshot).ToList());
        if (poolEval.IsHardStopped)
        {
            return ModificationActionResult.Blocked(poolEval);
        }

        var totalVolume = sources.All(s => s.Volume.HasValue) ? sources.Sum(s => s.Volume!.Value) : (decimal?)null;
        var specs = new List<(string? Suffix, decimal? Volume)> { (null, totalVolume) };
        return await ExecuteAsync(sources, rule, specs, request.Reason, ct);
    }

    /// <summary>Performs a 1-source/1-result modification: Irradiate, Thaw, Volume Reduction, or Leukoreduction.</summary>
    public async Task<ModificationActionResult> ApplySingleAsync(long sourceUnitId, PerformSingleModificationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ModificationActionResult.Fail("A reason is required to perform a modification.");
        }

        var rule = await _rules.GetByIdAsync(request.RuleId, ct);
        if (rule is null || !rule.IsActive)
        {
            return ModificationActionResult.Fail("Modification rule not found or inactive.");
        }

        if (rule.ModificationType is ModificationType.Divide or ModificationType.Pool)
        {
            return ModificationActionResult.Fail("Use the divide or pool operation for this modification type.");
        }

        var source = await _inventory.GetUnitAsync(sourceUnitId, ct);
        if (source is null)
        {
            return ModificationActionResult.Fail("Source unit not found.");
        }

        var sourceEval = UnitModificationEligibilityRule.EvaluateSource(ToSnapshot(source), rule.SourceProductTypeId, _clock.UtcNow);
        if (sourceEval.IsHardStopped)
        {
            return ModificationActionResult.Blocked(sourceEval);
        }

        var specs = new List<(string? Suffix, decimal? Volume)> { (null, request.ResultVolume ?? source.Volume) };
        return await ExecuteAsync(new[] { source }, rule, specs, request.Reason, ct);
    }

    private async Task<ModificationActionResult> ExecuteAsync(
        IReadOnlyList<BloodUnit> sources,
        ModificationRule rule,
        IReadOnlyList<(string? Suffix, decimal? Volume)> resultSpecs,
        string reason,
        CancellationToken ct)
    {
        var expCode = await _expirationCodes.GetByIdAsync(rule.ExpirationModificationCodeId, ct);
        if (expCode is null)
        {
            return ModificationActionResult.Fail(
                $"Modification rule is missing expiration modification code '{rule.ExpirationModificationCodeId}'.");
        }

        foreach (var source in sources)
        {
            var transition = InventoryStatusTransition.Evaluate(source.Status, UnitStatus.Modified);
            if (transition.Severity == RuleSeverity.HardStop)
            {
                return ModificationActionResult.Blocked(new RuleEvaluation(new[] { transition }));
            }
        }

        var now = _clock.UtcNow;
        var collections = sources.Select(s =>
            ModificationExpirationRule.ResolveCollectionUtc(s.CollectedUtc, s.CollectionDateTime));
        if (!ModificationExpirationRule.TryResolveAnchorUtc(
                expCode.RelativeTo, now, collections, out var anchorUtc, out var collectionError))
        {
            return ModificationActionResult.Blocked(new RuleEvaluation(new[]
            {
                RuleResult.HardStop(
                    collectionError ?? ModificationExpirationRule.CollectionRequiredCode,
                    "This expiration code is relative to collection date/time, but a source unit has no collection date/time.")
            }));
        }

        var originalExpiresUtc = ModificationExpirationRule.EarliestExpiration(sources.Select(s => s.ExpiresUtc));
        var resultExpiresUtc = ModificationExpirationRule.ComputeNewExpiresUtc(anchorUtc, expCode.ToOffset(), originalExpiresUtc);

        var header = new UnitModification
        {
            ModificationRuleId = rule.Id,
            ModificationType = rule.ModificationType,
            ExpirationOffsetCodeApplied = expCode.Code,
            ResultExpiresUtc = resultExpiresUtc,
            Reason = reason,
            PerformedBy = _currentUser.UserName,
            PerformedUtc = now
        };
        await _modifications.AddAsync(header, ct);

        var primarySource = sources[0];
        var singleSource = sources.Count == 1;
        var sortOrder = 0;

        foreach (var source in sources)
        {
            var fromStatus = source.Status;
            source.Status = UnitStatus.Modified;

            _inventory.AddStatusHistory(new InventoryStatusHistory
            {
                BloodProductId = source.Id,
                FromStatus = fromStatus,
                ToStatus = UnitStatus.Modified,
                FromLocationId = source.CurrentLocationId,
                ToLocationId = source.CurrentLocationId,
                Reason = reason,
                ChangedBy = _currentUser.UserName,
                ChangedUtc = now,
                RelatedEntityType = nameof(UnitModification)
            });

            await _modificationUnits.AddAsync(new UnitModificationUnit
            {
                UnitModification = header,
                BloodProductId = source.Id,
                Role = ModificationUnitRole.Source,
                SortOrder = sortOrder++
            }, ct);
        }

        var resultUnits = new List<BloodUnit>();
        var usedUnitNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        sortOrder = 0;
        var targetProduct = await _products.GetByIdAsync(rule.TargetProductTypeId, ct);

        foreach (var spec in resultSpecs)
        {
            var unitNumber = await GenerateUnitNumberAsync(primarySource.UnitNumber, spec.Suffix, sortOrder + 1, usedUnitNumbers, ct);
            var result = new BloodUnit
            {
                UnitNumber = unitNumber,
                ProductTypeId = rule.TargetProductTypeId,
                Isbt128ProductCode = targetProduct?.Isbt128ProductCode,
                ProductDescriptionCode = targetProduct?.Isbt128ProductCode,
                Abo = primarySource.Abo,
                RhD = primarySource.RhD,
                ExpiresUtc = resultExpiresUtc,
                CurrentLocationId = primarySource.CurrentLocationId,
                Volume = spec.Volume,
                Status = UnitStatus.Quarantine,
                Source = ComponentEntrySource.Manual,
                CollectionFacility = primarySource.CollectionFacility,
                Supplier = primarySource.Supplier,
                CollectedUtc = primarySource.CollectedUtc,
                CollectionDateTime = primarySource.CollectionDateTime,
                DerivedFromModification = header,
                QuarantineReasonCode = UnitQuarantineReason.PendingRelease,
                QuarantineReason = $"Created by {rule.ModificationType} modification"
            };

            if (singleSource)
            {
                result.Din = primarySource.Din;
                result.Fin = primarySource.Fin;
                result.NominalYear = primarySource.NominalYear;
                result.DonationSequence = primarySource.DonationSequence;
                result.DinFlags = primarySource.DinFlags;
                result.DinKeyboardCheck = primarySource.DinKeyboardCheck;
            }

            await _inventory.AddUnitAsync(result, ct);

            _inventory.AddStatusHistory(new InventoryStatusHistory
            {
                Unit = result,
                FromStatus = null,
                ToStatus = UnitStatus.Quarantine,
                ToLocationId = result.CurrentLocationId,
                Reason = $"Created by {rule.ModificationType} modification",
                ChangedBy = _currentUser.UserName,
                ChangedUtc = now,
                RelatedEntityType = nameof(UnitModification)
            });

            await _modificationUnits.AddAsync(new UnitModificationUnit
            {
                UnitModification = header,
                Unit = result,
                Role = ModificationUnitRole.Result,
                SortOrder = sortOrder++
            }, ct);

            resultUnits.Add(result);
        }

        // First save assigns Ids to the header and every unit; the named audit event
        // below needs those Ids, so it is staged and committed in a second save.
        await _unitOfWork.SaveChangesAsync(ct);

        _audit.Record(
            AuditEventType.Modify,
            nameof(UnitModification),
            header.Id,
            oldValue: new
            {
                SourceUnitIds = sources.Select(s => s.Id).ToArray(),
                SourceUnitNumbers = sources.Select(s => s.UnitNumber).ToArray()
            },
            newValue: new
            {
                rule.ModificationType,
                ResultUnitIds = resultUnits.Select(u => u.Id).ToArray(),
                ResultUnitNumbers = resultUnits.Select(u => u.UnitNumber).ToArray(),
                ResultExpiresUtc = resultExpiresUtc
            },
            reason: reason);

        await _unitOfWork.SaveChangesAsync(ct);

        return ModificationActionResult.Ok(header, resultUnits);
    }

    private async Task<string> GenerateUnitNumberAsync(
        string baseUnitNumber, string? suffix, int sequence, HashSet<string> usedThisRun, CancellationToken ct)
    {
        var stem = string.IsNullOrWhiteSpace(suffix) ? $"{baseUnitNumber}-M{sequence:D2}" : $"{baseUnitNumber}-{suffix.Trim()}";
        var candidate = stem;
        var attempt = 1;
        while (usedThisRun.Contains(candidate) || await _inventory.UnitNumberExistsAsync(candidate, ct))
        {
            candidate = $"{stem}-{++attempt}";
        }

        usedThisRun.Add(candidate);
        return candidate;
    }

    private static string DisplayProductCode(ProductType? product) =>
        string.IsNullOrWhiteSpace(product?.Isbt128ProductCode)
            ? product?.ProductCode ?? string.Empty
            : product.Isbt128ProductCode;

    private static UnitModificationEligibilityRule.SourceUnitSnapshot ToSnapshot(BloodUnit unit) => new(
        unit.Id, unit.Status, unit.ProductTypeId, unit.Abo, unit.RhD, unit.ExpiresUtc, unit.Volume);

    private async Task<IReadOnlyList<BloodUnit>> LoadUnitsAsync(IReadOnlyList<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<BloodUnit>();
        }

        var result = new List<BloodUnit>();
        foreach (var id in ids)
        {
            var unit = await _inventory.GetUnitAsync(id, ct);
            if (unit is not null)
            {
                result.Add(unit);
            }
        }

        return result;
    }
}
