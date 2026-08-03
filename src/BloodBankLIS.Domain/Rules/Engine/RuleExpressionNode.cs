namespace BloodBankLIS.Domain.Rules.Engine;

public enum RuleBinaryOperator
{
    And = 0,
    Or = 1,
    Equal = 2,
    NotEqual = 3,
    LessThan = 4,
    LessThanOrEqual = 5,
    GreaterThan = 6,
    GreaterThanOrEqual = 7,
    In = 8,
    NotIn = 9,
    Contains = 10
}

public enum RuleUnaryOperator
{
    Not = 0,
    Negate = 1,
    IsNull = 2,
    IsNotNull = 3
}

/// <summary>Parsed rule condition. Immutable and safe to cache across evaluations.</summary>
public abstract class RuleExpressionNode
{
    public abstract T Accept<T>(IRuleExpressionVisitor<T> visitor);
}

public interface IRuleExpressionVisitor<out T>
{
    T VisitLiteral(RuleLiteralNode node);

    T VisitAttribute(RuleAttributeNode node);

    T VisitFunction(RuleFunctionNode node);

    T VisitList(RuleListNode node);

    T VisitUnary(RuleUnaryNode node);

    T VisitBinary(RuleBinaryNode node);
}

public sealed class RuleLiteralNode : RuleExpressionNode
{
    public RuleLiteralNode(RuleValue value) => Value = value;

    public RuleValue Value { get; }

    public override T Accept<T>(IRuleExpressionVisitor<T> visitor) => visitor.VisitLiteral(this);
}

public sealed class RuleAttributeNode : RuleExpressionNode
{
    public RuleAttributeNode(string path, int position)
    {
        Path = path;
        Position = position;
    }

    public string Path { get; }

    public int Position { get; }

    public override T Accept<T>(IRuleExpressionVisitor<T> visitor) => visitor.VisitAttribute(this);
}

public sealed class RuleFunctionNode : RuleExpressionNode
{
    public RuleFunctionNode(string name, IReadOnlyList<RuleExpressionNode> arguments, int position)
    {
        Name = name;
        Arguments = arguments;
        Position = position;
    }

    public string Name { get; }

    public IReadOnlyList<RuleExpressionNode> Arguments { get; }

    public int Position { get; }

    public override T Accept<T>(IRuleExpressionVisitor<T> visitor) => visitor.VisitFunction(this);
}

public sealed class RuleListNode : RuleExpressionNode
{
    public RuleListNode(IReadOnlyList<RuleExpressionNode> items) => Items = items;

    public IReadOnlyList<RuleExpressionNode> Items { get; }

    public override T Accept<T>(IRuleExpressionVisitor<T> visitor) => visitor.VisitList(this);
}

public sealed class RuleUnaryNode : RuleExpressionNode
{
    public RuleUnaryNode(RuleUnaryOperator op, RuleExpressionNode operand)
    {
        Operator = op;
        Operand = operand;
    }

    public RuleUnaryOperator Operator { get; }

    public RuleExpressionNode Operand { get; }

    public override T Accept<T>(IRuleExpressionVisitor<T> visitor) => visitor.VisitUnary(this);
}

public sealed class RuleBinaryNode : RuleExpressionNode
{
    public RuleBinaryNode(RuleBinaryOperator op, RuleExpressionNode left, RuleExpressionNode right)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public RuleBinaryOperator Operator { get; }

    public RuleExpressionNode Left { get; }

    public RuleExpressionNode Right { get; }

    public override T Accept<T>(IRuleExpressionVisitor<T> visitor) => visitor.VisitBinary(this);
}
