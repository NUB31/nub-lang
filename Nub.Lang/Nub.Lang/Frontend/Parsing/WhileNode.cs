namespace Nub.Lang.Frontend.Parsing;

public class WhileNode(ExpressionNode condition, BlockNode body) : StatementNode
{
    public ExpressionNode Condition { get; } = condition;
    public BlockNode Body { get; } = body;
}