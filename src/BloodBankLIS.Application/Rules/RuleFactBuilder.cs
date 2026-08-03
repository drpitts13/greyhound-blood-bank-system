using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Engine;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Rules;

/// <summary>
/// Everything the engine needs to evaluate one order-level rule, gathered by the caller
/// so evaluation stays synchronous and free of database access.
/// </summary>
public sealed record OrderRuleContext(
    Patient? Patient,
    PatientBloodTypeHistory? CurrentBloodType,
    Order Order,
    IReadOnlyList<OrderLine> Lines,
    string? SpecimenTypeCode,
    IReadOnlyDictionary<long, string> ProductCodesByTypeId,
    IReadOnlyDictionary<string, IReadOnlyList<string>> GrouperMembers);

/// <summary>Order context plus the verified result that triggered a test-level rule.</summary>
public sealed record TestRuleContext(OrderRuleContext Order, TestResult Result);

/// <summary>
/// Maps domain entities onto the attribute paths declared by <see cref="RuleAttributeCatalog"/>.
/// Every path in the catalog must be produced here, otherwise a valid rule would silently
/// evaluate against null.
/// </summary>
public static class RuleFactBuilder
{
    public static RuleFactBag ForOrder(OrderRuleContext context, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bag = new RuleFactBag();
        AddPatientFacts(bag, context, nowUtc);
        AddOrderFacts(bag, context);
        return bag;
    }

    public static RuleFactBag ForTest(TestRuleContext context, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bag = ForOrder(context.Order, nowUtc);
        AddTestFacts(bag, context.Result);
        return bag;
    }

    private static void AddPatientFacts(RuleFactBag bag, OrderRuleContext context, DateTime nowUtc)
    {
        var age = context.Patient is null
            ? PatientAge.Zero
            : PatientAge.FromDateOfBirth(context.Patient.DateOfBirth, nowUtc);

        bag.Set("patient.ageDays", age.Days)
            .Set("patient.ageMonths", age.Months)
            .Set("patient.ageYears", age.Years)
            .Set("patient.sex", context.Patient?.Sex.ToString());

        var bloodType = context.CurrentBloodType;
        if (bloodType is null)
        {
            bag.Set("patient.abo", RuleValue.Null)
                .Set("patient.rh", RuleValue.Null)
                .Set("patient.bloodType", RuleValue.Null);
            return;
        }

        bag.Set("patient.abo", bloodType.Abo.ToString())
            .Set("patient.rh", bloodType.RhD.ToString())
            .Set("patient.bloodType", ResultInterpretation.Format(bloodType.BloodType));
    }

    private static void AddOrderFacts(RuleFactBag bag, OrderRuleContext context)
    {
        var activeLines = context.Lines.Where(l => l.IsActive).ToList();

        var testCodes = activeLines
            .Where(l => l.LineCategory == OrderCategory.Test && !string.IsNullOrWhiteSpace(l.TestCode))
            .Select(l => l.TestCode!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Groupers are expanded into member lines before an order is saved, so a rule that
        // names the grouper (e.g. 'TNS' for type and screen) would otherwise never match.
        // A grouper counts as ordered when every one of its members is on the order.
        testCodes.AddRange(ImpliedGrouperCodes(context.GrouperMembers, testCodes));

        var productCodes = activeLines
            .Where(l => l.LineCategory == OrderCategory.Product && l.ProductTypeId is > 0)
            .Select(l => context.ProductCodesByTypeId.GetValueOrDefault(l.ProductTypeId!.Value))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        bag.Set("order.date", context.Order.OrderedUtc)
            .Set("order.priority", context.Order.Priority.ToString())
            .Set("order.category", context.Order.OrderCategory.ToString())
            .Set("order.number", string.IsNullOrWhiteSpace(context.Order.OrderNumber) ? null : context.Order.OrderNumber)
            .Set("order.specimenType", string.IsNullOrWhiteSpace(context.SpecimenTypeCode) ? null : context.SpecimenTypeCode)
            .SetList("order.tests", testCodes)
            .SetList("order.productTypes", productCodes);

        bag.SetFunction(
            FunctionNames("order.hasTest"),
            args => RuleValue.FromBoolean(ContainsCode(testCodes, args)));
        bag.SetFunction(
            FunctionNames("order.hasProduct"),
            args => RuleValue.FromBoolean(ContainsCode(productCodes, args)));
    }

    private static void AddTestFacts(RuleFactBag bag, TestResult result)
    {
        var subtests = ResultInterpretation.ResolveSubtests(result.Value);

        bag.Set("test.code", result.TestCode)
            .Set("test.result", result.Value)
            .Set("test.interpretation", ResultInterpretation.Resolve(result.Interpretation, result.Value))
            .Set("test.status", result.Status.ToString())
            .SetList("test.subtests", subtests.Keys);

        if (AboRhResultValue.TryParse(result.Value, out var aboRh) && aboRh.IsKnown)
        {
            bag.Set("test.abo", aboRh.Abo.ToString()).Set("test.rh", aboRh.Rh.ToString());
        }
        else
        {
            bag.Set("test.abo", RuleValue.Null).Set("test.rh", RuleValue.Null);
        }

        bag.SetFunction(FunctionNames("test.subtest"), args =>
        {
            var code = args.Count > 0 ? args[0].AsText()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(code))
            {
                return RuleValue.Null;
            }

            var match = subtests.FirstOrDefault(kv =>
                string.Equals(kv.Key, code, StringComparison.OrdinalIgnoreCase));
            return RuleValue.FromText(match.Key is null ? null : match.Value);
        });
    }

    public static IReadOnlyList<string> ImpliedGrouperCodes(
        IReadOnlyDictionary<string, IReadOnlyList<string>> grouperMembers,
        IReadOnlyCollection<string> testCodes)
    {
        if (grouperMembers.Count == 0 || testCodes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var present = testCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return grouperMembers
            .Where(g => g.Value.Count > 0 && g.Value.All(present.Contains) && !present.Contains(g.Key))
            .Select(g => g.Key.Trim().ToUpperInvariant())
            .ToList();
    }

    private static bool ContainsCode(IReadOnlyCollection<string> codes, IReadOnlyList<RuleValue> arguments)
    {
        var wanted = arguments.Count > 0 ? arguments[0].AsText()?.Trim() : null;
        return !string.IsNullOrWhiteSpace(wanted)
               && codes.Contains(wanted, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Canonical function name plus every alias the catalog accepts for it.</summary>
    private static IEnumerable<string> FunctionNames(string canonical)
    {
        yield return canonical;

        var descriptor = RuleAttributeCatalog.Functions(RuleLevel.Test)
            .FirstOrDefault(f => string.Equals(f.Name, canonical, StringComparison.OrdinalIgnoreCase));
        foreach (var alias in descriptor?.Aliases ?? Array.Empty<string>())
        {
            yield return alias;
        }
    }
}
