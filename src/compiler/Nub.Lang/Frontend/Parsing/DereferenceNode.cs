namespace Nub.Lang.Frontend.Parsing;

public class DereferenceNode(ExpressionNode expression) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expression;
}