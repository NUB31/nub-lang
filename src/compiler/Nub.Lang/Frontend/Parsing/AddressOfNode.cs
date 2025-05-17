namespace Nub.Lang.Frontend.Parsing;

public class AddressOfNode(ExpressionNode expression) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expression;
}