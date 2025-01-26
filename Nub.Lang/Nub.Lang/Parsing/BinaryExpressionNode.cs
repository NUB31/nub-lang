namespace Nub.Lang.Parsing;

public class BinaryExpressionNode(ExpressionNode left, BinaryExpressionOperator @operator, ExpressionNode right) : ExpressionNode
{
    public ExpressionNode Left { get; } = left;
    public BinaryExpressionOperator Operator { get; } = @operator;
    public ExpressionNode Right { get; } = right;
}

public enum BinaryExpressionOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Plus,
    Minus,
    Multiply,
    Divide
}