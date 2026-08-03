using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Engine;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Rules;

/// <summary>
/// Evaluates configurable <see cref="RuleDefinition"/> records and applies their actions.
/// Order-level rules run while an order's lines are still pending; test-level rules run
/// after a result is verified. A rule fires at most once per context (enforced through
/// <see cref="RuleExecutionLog"/>), and order-level cascading is bounded so rules that
/// trigger one another cannot loop.
/// </summary>
public sealed class RuleEngineService
{
    /// <summary>Upper bound on order-level re-evaluation after a rule adds or cancels a test.</summary>
    private const int MaxCascadePasses = 3;

    private static readonly ConcurrentDictionary<string, RuleExpressionNode> ParsedConditions = new(StringComparer.Ordinal);

    /// <summary>
    /// Action text is quoted, so the default encoder would store it as \u0027 and make the
    /// audit trail unreadable. The value is only ever rendered through encoding UI.
    /// </summary>
    private static readonly JsonSerializerOptions ActionJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IRepository<RuleDefinition> _rules;
    private readonly IRepository<RuleExecutionLog> _logs;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderLine> _orderLines;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<TestDefinition> _testDefinitions;
    private readonly IRepository<TestGrouper> _testGroupers;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;

    public RuleEngineService(
        IRepository<RuleDefinition> rules,
        IRepository<RuleExecutionLog> logs,
        IRepository<Patient> patients,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<Order> orders,
        IRepository<OrderLine> orderLines,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<Specimen> specimens,
        IRepository<TestDefinition> testDefinitions,
        IRepository<TestGrouper> testGroupers,
        IRepository<ProductType> productTypes,
        IClock clock,
        IAuditWriter audit)
    {
        _testGroupers = testGroupers;
        _rules = rules;
        _logs = logs;
        _patients = patients;
        _bloodTypes = bloodTypes;
        _orders = orders;
        _orderLines = orderLines;
        _orderSpecimens = orderSpecimens;
        _specimens = specimens;
        _testDefinitions = testDefinitions;
        _productTypes = productTypes;
        _clock = clock;
        _audit = audit;
    }

    /// <summary>
    /// Runs the order-level rules against a pending line set. The order need not be saved yet;
    /// the returned logs are staged and persisted by <see cref="PersistOrderLogsAsync"/> once
    /// the order has an identity.
    /// </summary>
    public async Task<OrderRuleOutcome> ApplyOrderRulesAsync(
        long patientId,
        Order order,
        IReadOnlyList<OrderLine> pendingLines,
        string? specimenTypeCode,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(pendingLines);

        var rules = await LoadRulesAsync(RuleLevel.Order, ct);
        var lines = pendingLines.ToList();
        if (rules.Count == 0)
        {
            return new OrderRuleOutcome(lines, Array.Empty<RuleResult>(), null, Array.Empty<RuleExecutionLog>());
        }

        var patient = await _patients.FirstOrDefaultAsync(p => p.Id == patientId, ct);
        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == patientId && h.IsCurrent, ct);
        var productCodes = await LoadProductCodesAsync(ct);
        var testCatalog = await LoadTestCatalogAsync(ct);
        var grouperMembers = await LoadGrouperMembersAsync(ct);
        var alreadyFired = await LoadFiredRuleIdsAsync(order.Id, testResultId: null, ct);

        var warnings = new List<RuleResult>();
        var logs = new List<RuleExecutionLog>();
        string? blockMessage = null;
        var now = _clock.UtcNow;

        for (var pass = 0; pass < MaxCascadePasses && blockMessage is null; pass++)
        {
            var context = new OrderRuleContext(patient, bloodType, order, lines, specimenTypeCode, productCodes, grouperMembers);
            var facts = RuleFactBuilder.ForOrder(context, now);
            var firedThisPass = false;

            foreach (var rule in rules.Where(r => !alreadyFired.Contains(r.Id)))
            {
                if (!Matches(rule, facts))
                {
                    continue;
                }

                var applied = ApplyOrderActions(rule, lines, testCatalog, grouperMembers, out var ruleWarnings, out var ruleBlock, out var notes);
                alreadyFired.Add(rule.Id);
                firedThisPass = true;
                warnings.AddRange(ruleWarnings);

                logs.Add(BuildLog(rule, patientId, order.Id == 0 ? null : order.Id, testResultId: null, applied, notes, now));

                if (ruleBlock is not null)
                {
                    blockMessage = ruleBlock;
                    break;
                }

                if (rule.StopOnMatch)
                {
                    return Finish();
                }
            }

            if (!firedThisPass)
            {
                break;
            }
        }

        return Finish();

        OrderRuleOutcome Finish()
        {
            Renumber(lines);
            return new OrderRuleOutcome(lines, warnings, blockMessage, logs);
        }
    }

    /// <summary>Stamps the order identity onto staged logs and queues them for the caller's save.</summary>
    public async Task PersistOrderLogsAsync(OrderRuleOutcome outcome, long orderId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        foreach (var log in outcome.Logs)
        {
            log.OrderId = orderId;
            await _logs.AddAsync(log, ct);
        }
    }

    /// <summary>
    /// Runs the test-level rules for a verified result, adding or cancelling order lines.
    /// Changes are staged on the caller's unit of work.
    /// </summary>
    public async Task<TestRuleOutcome> ApplyTestRulesAsync(TestResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.OrderId is not > 0)
        {
            return TestRuleOutcome.Empty;
        }

        var rules = await LoadRulesAsync(RuleLevel.Test, ct);
        if (rules.Count == 0)
        {
            return TestRuleOutcome.Empty;
        }

        var orderId = result.OrderId.Value;
        var order = await _orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
        {
            return TestRuleOutcome.Empty;
        }

        var patient = await _patients.FirstOrDefaultAsync(p => p.Id == result.PatientId, ct);
        var bloodType = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == result.PatientId && h.IsCurrent, ct);
        var lines = (await _orderLines.ListAsync(l => l.OrderId == orderId && l.IsActive, ct)).ToList();
        var specimenType = await ResolveSpecimenTypeAsync(orderId, ct);
        var productCodes = await LoadProductCodesAsync(ct);
        var testCatalog = await LoadTestCatalogAsync(ct);
        var grouperMembers = await LoadGrouperMembersAsync(ct);
        var alreadyFired = await LoadFiredRuleIdsAsync(orderId, result.Id, ct);

        var orderContext = new OrderRuleContext(patient, bloodType, order, lines, specimenType, productCodes, grouperMembers);
        var facts = RuleFactBuilder.ForTest(new TestRuleContext(orderContext, result), _clock.UtcNow);

        var warnings = new List<RuleResult>();
        var addedCodes = new List<string>();
        var now = _clock.UtcNow;
        var nextLineNumber = lines.Count == 0 ? 1 : lines.Max(l => l.LineNumber) + 1;

        foreach (var rule in rules.Where(r => !alreadyFired.Contains(r.Id)))
        {
            if (!Matches(rule, facts))
            {
                continue;
            }

            var applied = new List<RuleActionInstruction>();
            var notes = new List<string>();

            foreach (var action in ParseActions(rule))
            {
                switch (action.Kind)
                {
                    case RuleActionKind.AddTest:
                    {
                        var added = false;
                        foreach (var code in Expand(action.TestCode, grouperMembers))
                        {
                            if (lines.Any(l => l.LineCategory == OrderCategory.Test
                                               && l.IsActive
                                               && string.Equals(l.TestCode, code, StringComparison.OrdinalIgnoreCase)))
                            {
                                notes.Add($"Test '{code}' is already on the order.");
                                continue;
                            }

                            if (!testCatalog.TryGetValue(code, out var name))
                            {
                                notes.Add($"Test '{code}' is not in the active catalog.");
                                continue;
                            }

                            var line = new OrderLine
                            {
                                OrderId = orderId,
                                LineNumber = nextLineNumber++,
                                LineCategory = OrderCategory.Test,
                                LineName = name,
                                TestCode = code,
                                OrderType = OrderLineBuilder.MapTestOrderType(code),
                                ResultStatus = ResultStatus.Pending,
                                IsActive = true
                            };

                            await _orderLines.AddAsync(line, ct);
                            lines.Add(line);
                            addedCodes.Add(code);
                            added = true;
                        }

                        if (added)
                        {
                            applied.Add(action);
                        }

                        break;
                    }

                    case RuleActionKind.CancelTest:
                    {
                        var cancelled = false;
                        foreach (var code in Expand(action.TestCode, grouperMembers))
                        {
                            var target = lines.FirstOrDefault(l => l.LineCategory == OrderCategory.Test
                                                                   && l.IsActive
                                                                   && string.Equals(l.TestCode, code, StringComparison.OrdinalIgnoreCase));
                            if (target is null)
                            {
                                notes.Add($"Test '{code}' is not on the order.");
                                continue;
                            }

                            if (target.ResultStatus is ResultStatus.Verified or ResultStatus.Corrected)
                            {
                                notes.Add($"Test '{code}' is already resulted and was not cancelled.");
                                continue;
                            }

                            target.IsActive = false;
                            target.FulfillmentStatus = FulfillmentStatus.Cancelled;
                            _orderLines.Update(target);
                            cancelled = true;
                        }

                        if (cancelled)
                        {
                            applied.Add(action);
                        }

                        break;
                    }

                    case RuleActionKind.Warn:
                        warnings.Add(RuleResult.Warning($"RULE.{rule.Code}", action.Argument));
                        applied.Add(action);
                        break;

                    case RuleActionKind.Block:
                        // Rejected at authoring time; ignored defensively for legacy rows.
                        notes.Add("block() is not supported for test-level rules and was ignored.");
                        break;
                }
            }

            var log = BuildLog(rule, result.PatientId, orderId, result.Id, applied, notes, now);
            await _logs.AddAsync(log, ct);
            RecordAudit(rule, orderId, applied, notes);

            if (rule.StopOnMatch)
            {
                break;
            }
        }

        return new TestRuleOutcome(warnings, addedCodes);
    }

    /// <summary>Specimen type code of the order's primary specimen, or null when unlinked.</summary>
    public async Task<string?> ResolveSpecimenTypeAsync(long orderId, CancellationToken ct = default)
    {
        if (orderId <= 0)
        {
            return null;
        }

        var links = await _orderSpecimens.ListAsync(os => os.OrderId == orderId, ct);
        var primary = links.OrderByDescending(l => l.IsPrimary).FirstOrDefault();
        if (primary is null)
        {
            return null;
        }

        var specimen = await _specimens.FirstOrDefaultAsync(s => s.Id == primary.SpecimenId, ct);
        return string.IsNullOrWhiteSpace(specimen?.SpecimenType) ? null : specimen!.SpecimenType;
    }

    private List<RuleActionInstruction> ApplyOrderActions(
        RuleDefinition rule,
        List<OrderLine> lines,
        IReadOnlyDictionary<string, string> testCatalog,
        IReadOnlyDictionary<string, IReadOnlyList<string>> grouperMembers,
        out List<RuleResult> warnings,
        out string? blockMessage,
        out List<string> notes)
    {
        warnings = new List<RuleResult>();
        notes = new List<string>();
        blockMessage = null;
        var applied = new List<RuleActionInstruction>();
        var localNotes = notes;

        foreach (var action in ParseActions(rule))
        {
            switch (action.Kind)
            {
                case RuleActionKind.AddTest:
                {
                    var added = false;
                    foreach (var code in Expand(action.TestCode, grouperMembers))
                    {
                        if (lines.Any(l => l.IsActive
                                           && l.LineCategory == OrderCategory.Test
                                           && string.Equals(l.TestCode, code, StringComparison.OrdinalIgnoreCase)))
                        {
                            localNotes.Add($"Test '{code}' is already on the order.");
                            continue;
                        }

                        if (!testCatalog.TryGetValue(code, out var name))
                        {
                            localNotes.Add($"Test '{code}' is not in the active catalog.");
                            continue;
                        }

                        lines.Add(new OrderLine
                        {
                            LineNumber = lines.Count == 0 ? 1 : lines.Max(l => l.LineNumber) + 1,
                            LineCategory = OrderCategory.Test,
                            LineName = name,
                            TestCode = code,
                            OrderType = OrderLineBuilder.MapTestOrderType(code),
                            ResultStatus = ResultStatus.Pending,
                            IsActive = true
                        });
                        added = true;
                    }

                    if (added)
                    {
                        applied.Add(action);
                    }

                    break;
                }

                case RuleActionKind.CancelTest:
                {
                    var cancelled = false;
                    foreach (var code in Expand(action.TestCode, grouperMembers))
                    {
                        var target = lines.FirstOrDefault(l => l.IsActive
                                                               && l.LineCategory == OrderCategory.Test
                                                               && string.Equals(l.TestCode, code, StringComparison.OrdinalIgnoreCase));
                        if (target is null)
                        {
                            localNotes.Add($"Test '{code}' is not on the order.");
                            continue;
                        }

                        if (target.ResultStatus is ResultStatus.Verified or ResultStatus.Corrected)
                        {
                            localNotes.Add($"Test '{code}' is already resulted and was not cancelled.");
                            continue;
                        }

                        // Lines that were never persisted simply disappear from the pending set.
                        if (target.Id == 0)
                        {
                            lines.Remove(target);
                        }
                        else
                        {
                            target.IsActive = false;
                            target.FulfillmentStatus = FulfillmentStatus.Cancelled;
                        }

                        cancelled = true;
                    }

                    if (cancelled)
                    {
                        applied.Add(action);
                    }

                    break;
                }

                case RuleActionKind.Warn:
                    warnings.Add(RuleResult.Warning($"RULE.{rule.Code}", action.Argument));
                    applied.Add(action);
                    break;

                case RuleActionKind.Block:
                    blockMessage = action.Argument;
                    applied.Add(action);
                    return applied;
            }
        }

        return applied;
    }

    /// <summary>
    /// A grouper code resolves to its member tests, since orders carry expanded member
    /// lines rather than the grouper itself. Any other code stands for itself.
    /// </summary>
    private static IReadOnlyList<string> Expand(
        string code,
        IReadOnlyDictionary<string, IReadOnlyList<string>> grouperMembers) =>
        grouperMembers.TryGetValue(code, out var members) && members.Count > 0
            ? members
            : new[] { code };

    private static bool Matches(RuleDefinition rule, IRuleFactSource facts)
    {
        var condition = ParsedConditions.GetOrAdd(rule.ConditionExpression, expression =>
            RuleExpressionParser.TryParse(expression, out var node, out _)
                ? node!
                : new RuleLiteralNode(RuleValue.False));

        return RuleExpressionEvaluator.IsSatisfied(condition, facts);
    }

    private static IReadOnlyList<RuleActionInstruction> ParseActions(RuleDefinition rule) =>
        RuleActionParser.TryParse(rule.ActionExpression, rule.Level, out var actions, out _)
            ? actions
            : Array.Empty<RuleActionInstruction>();

    private RuleExecutionLog BuildLog(
        RuleDefinition rule,
        long patientId,
        long? orderId,
        long? testResultId,
        IReadOnlyList<RuleActionInstruction> applied,
        IReadOnlyList<string> notes,
        DateTime now) =>
        new()
        {
            RuleId = rule.Id,
            RuleCode = rule.Code,
            RuleVersion = rule.Version,
            Level = rule.Level,
            PatientId = patientId,
            OrderId = orderId,
            TestResultId = testResultId,
            ActionsJson = JsonSerializer.Serialize(applied.Select(a => a.ToString()).ToList(), ActionJsonOptions),
            Notes = notes.Count == 0 ? null : string.Join(" ", notes),
            EvaluatedUtc = now
        };

    private void RecordAudit(
        RuleDefinition rule,
        long orderId,
        IReadOnlyList<RuleActionInstruction> applied,
        IReadOnlyList<string> notes)
    {
        if (applied.Count == 0 && notes.Count == 0)
        {
            return;
        }

        _audit.Record(
            AuditEventType.Configure,
            nameof(Order),
            orderId,
            newValue: new
            {
                RuleCode = rule.Code,
                rule.Level,
                Actions = applied.Select(a => a.ToString()).ToList(),
                Notes = notes
            },
            reason: $"Rule '{rule.Code}' matched.");
    }

    private async Task<List<RuleDefinition>> LoadRulesAsync(RuleLevel level, CancellationToken ct)
    {
        var rules = await _rules.ListAsync(r => r.IsActive && !r.IsDraft && r.Level == level, ct);
        return rules.OrderBy(r => r.Priority).ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<HashSet<long>> LoadFiredRuleIdsAsync(long orderId, long? testResultId, CancellationToken ct)
    {
        if (orderId <= 0)
        {
            return new HashSet<long>();
        }

        var logs = testResultId is > 0
            ? await _logs.ListAsync(l => l.TestResultId == testResultId, ct)
            : await _logs.ListAsync(l => l.OrderId == orderId && l.TestResultId == null, ct);

        return logs.Select(l => l.RuleId).ToHashSet();
    }

    private async Task<Dictionary<long, string>> LoadProductCodesAsync(CancellationToken ct) =>
        (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id, p => p.ProductCode);

    /// <summary>Active test codes mapped to their display names, used to name added lines.</summary>
    private async Task<Dictionary<string, string>> LoadTestCatalogAsync(CancellationToken ct)
    {
        var definitions = await _testDefinitions.ListAsync(t => t.IsActive && !t.IsDraft, ct);
        var catalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            catalog[definition.Code] = definition.Name;
        }

        return catalog;
    }

    /// <summary>Active grouper codes mapped to their member test codes.</summary>
    private async Task<Dictionary<string, IReadOnlyList<string>>> LoadGrouperMembersAsync(CancellationToken ct)
    {
        var groupers = await _testGroupers.ListAsync(g => g.IsActive && !g.IsDraft, ct);
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var grouper in groupers)
        {
            map[grouper.Code.Trim().ToUpperInvariant()] = TestGrouperMembers.Parse(grouper.MemberTestsJson)
                .OrderBy(m => m.SortOrder)
                .Select(m => m.TestCode.Trim().ToUpperInvariant())
                .ToList();
        }

        return map;
    }

    private static void Renumber(List<OrderLine> lines)
    {
        var number = 1;
        foreach (var line in lines.Where(l => l.IsActive))
        {
            line.LineNumber = number++;
        }
    }
}
