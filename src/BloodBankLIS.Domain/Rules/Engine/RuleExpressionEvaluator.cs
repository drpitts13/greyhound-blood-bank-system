namespace BloodBankLIS.Domain.Rules.Engine;

/// <summary>
/// Evaluates a parsed rule condition against a fact source. Pure and synchronous.
/// A reference the fact source cannot supply evaluates to null rather than throwing,
/// so a stale rule fails closed (does not match) instead of breaking the workflow.
/// </summary>
public static class RuleExpressionEvaluator
{
    public static RuleValue Evaluate(RuleExpressionNode node, IRuleFactSource facts)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(facts);

        return node.Accept(new Evaluator(facts));
    }

    public static bool IsSatisfied(RuleExpressionNode? node, IRuleFactSource facts)
    {
        if (node is null)
        {
            return false;
        }

        return Evaluate(node, facts).AsBoolean();
    }

    private sealed class Evaluator : IRuleExpressionVisitor<RuleValue>
    {
        private readonly IRuleFactSource _facts;

        public Evaluator(IRuleFactSource facts) => _facts = facts;

        public RuleValue VisitLiteral(RuleLiteralNode node) => node.Value;

        public RuleValue VisitAttribute(RuleAttributeNode node) =>
            _facts.TryGetAttribute(node.Path, out var value) ? value ?? RuleValue.Null : RuleValue.Null;

        public RuleValue VisitFunction(RuleFunctionNode node)
        {
            var arguments = node.Arguments.Select(a => a.Accept(this)).ToList();
            return _facts.TryInvoke(node.Name, arguments, out var value) ? value ?? RuleValue.Null : RuleValue.Null;
        }

        public RuleValue VisitList(RuleListNode node) =>
            RuleValue.FromList(node.Items.Select(i => i.Accept(this)));

        public RuleValue VisitUnary(RuleUnaryNode node)
        {
            var operand = node.Operand.Accept(this);
            return node.Operator switch
            {
                RuleUnaryOperator.Not => RuleValue.FromBoolean(!operand.AsBoolean()),
                RuleUnaryOperator.IsNull => RuleValue.FromBoolean(operand.IsNull),
                RuleUnaryOperator.IsNotNull => RuleValue.FromBoolean(!operand.IsNull),
                RuleUnaryOperator.Negate => operand.TryAsNumber(out var number)
                    ? RuleValue.FromNumber(-number)
                    : RuleValue.Null,
                _ => RuleValue.Null
            };
        }

        public RuleValue VisitBinary(RuleBinaryNode node)
        {
            // Short-circuit so a null-valued branch cannot influence the other side.
            if (node.Operator == RuleBinaryOperator.And)
            {
                return RuleValue.FromBoolean(
                    node.Left.Accept(this).AsBoolean() && node.Right.Accept(this).AsBoolean());
            }

            if (node.Operator == RuleBinaryOperator.Or)
            {
                return RuleValue.FromBoolean(
                    node.Left.Accept(this).AsBoolean() || node.Right.Accept(this).AsBoolean());
            }

            var left = node.Left.Accept(this);
            var right = node.Right.Accept(this);

            return node.Operator switch
            {
                RuleBinaryOperator.Equal => RuleValue.FromBoolean(RuleValue.AreEqual(left, right)),
                RuleBinaryOperator.NotEqual => EitherIsNull(left, right)
                    ? RuleValue.False
                    : RuleValue.FromBoolean(!RuleValue.AreEqual(left, right)),
                RuleBinaryOperator.LessThan => Compare(left, right, c => c < 0),
                RuleBinaryOperator.LessThanOrEqual => Compare(left, right, c => c <= 0),
                RuleBinaryOperator.GreaterThan => Compare(left, right, c => c > 0),
                RuleBinaryOperator.GreaterThanOrEqual => Compare(left, right, c => c >= 0),
                RuleBinaryOperator.In => RuleValue.FromBoolean(RuleValue.Contains(right, left)),
                RuleBinaryOperator.NotIn => EitherIsNull(left, right)
                    ? RuleValue.False
                    : RuleValue.FromBoolean(!RuleValue.Contains(right, left)),
                RuleBinaryOperator.Contains => RuleValue.FromBoolean(RuleValue.Contains(left, right)),
                _ => RuleValue.Null
            };
        }

        /// <summary>
        /// Negated operators fail closed on missing data: a rule must not fire just because
        /// an attribute is absent. Authors use IS NULL / IS NOT NULL to test for absence.
        /// </summary>
        private static bool EitherIsNull(RuleValue left, RuleValue right) => left.IsNull || right.IsNull;

        private static RuleValue Compare(RuleValue left, RuleValue right, Func<int, bool> predicate) =>
            RuleValue.TryCompare(left, right, out var comparison)
                ? RuleValue.FromBoolean(predicate(comparison))
                : RuleValue.False;
    }
}
