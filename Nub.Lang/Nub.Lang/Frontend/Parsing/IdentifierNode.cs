namespace Nub.Lang.Frontend.Parsing;

public class IdentifierNode(string identifier) : ExpressionNode
{
    public string Identifier { get; } = identifier;

    public override string ToString()
    {
        return Identifier;
    }
}