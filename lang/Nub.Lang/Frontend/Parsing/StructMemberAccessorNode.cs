namespace Nub.Lang.Frontend.Parsing;

public class StructMemberAccessorNode(List<string> members) : ExpressionNode
{
    public List<string> Members { get; } = members;
}