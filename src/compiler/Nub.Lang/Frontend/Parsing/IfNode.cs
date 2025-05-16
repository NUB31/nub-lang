namespace Nub.Lang.Frontend.Parsing;

public class IfNode(ExpressionNode condition, BlockNode body, Optional<Variant<IfNode, BlockNode>> @else) : StatementNode
{
    public ExpressionNode Condition { get; } = condition;
    public BlockNode Body { get; } = body;
    public Optional<Variant<IfNode, BlockNode>> Else { get; } = @else;
}