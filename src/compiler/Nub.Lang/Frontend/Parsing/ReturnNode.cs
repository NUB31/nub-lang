namespace Nub.Lang.Frontend.Parsing;

public class ReturnNode(Optional<ExpressionNode> value) : StatementNode
{
    public Optional<ExpressionNode> Value { get; } = value;
}