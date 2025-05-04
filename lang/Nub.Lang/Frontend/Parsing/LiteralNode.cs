namespace Nub.Lang.Frontend.Parsing;

public class LiteralNode(string literal, NubType type) : ExpressionNode
{
    public string Literal { get; } = literal;
    public NubType LiteralType { get; } = type;
}