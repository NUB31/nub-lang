namespace Nub.Lang.Parsing;

public class LiteralNode(string value) : ExpressionNode
{
    public string Value { get; } = value;
}