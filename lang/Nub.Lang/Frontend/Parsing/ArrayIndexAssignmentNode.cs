namespace Nub.Lang.Frontend.Parsing;

public class ArrayIndexAssignmentNode(IdentifierNode identifier, ExpressionNode index, ExpressionNode value) : StatementNode
{
    public IdentifierNode Identifier { get; } = identifier;
    public ExpressionNode Index { get; } = index;
    public ExpressionNode Value { get; } = value;
}