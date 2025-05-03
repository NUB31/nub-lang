namespace Nub.Lang.Frontend.Parsing;

public class ArrayIndexAccessNode(IdentifierNode identifier, ExpressionNode index) : ExpressionNode
{
    public IdentifierNode Identifier { get; } = identifier;
    public ExpressionNode Index { get; } = index;

    public override string ToString() => $"{Identifier}[{Index}]";
}