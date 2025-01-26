namespace Nub.Lang.Parsing;

public class LiteralNode(string literal, Type type) : ExpressionNode
{
    public string Literal { get; } = literal;
    public Type LiteralType { get; } = type;
}