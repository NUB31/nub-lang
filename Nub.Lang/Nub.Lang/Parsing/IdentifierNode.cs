namespace Nub.Lang.Parsing;

public class IdentifierNode(string identifier) : ExpressionNode
{
    public string Identifier { get; } = identifier;
}