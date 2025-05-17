namespace Nub.Lang.Frontend.Parsing;

public class UnaryExpressionNode(UnaryExpressionOperator @operator, ExpressionNode operand) : ExpressionNode
{
    public UnaryExpressionOperator Operator { get; } = @operator;
    public ExpressionNode Operand { get; } = operand;
}

public enum UnaryExpressionOperator
{
    AddressOf,
    Dereference,
    Negate,
    Invert
}