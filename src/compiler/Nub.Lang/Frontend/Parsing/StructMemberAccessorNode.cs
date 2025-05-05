namespace Nub.Lang.Frontend.Parsing;

public class StructMemberAccessorNode(List<string> fields) : ExpressionNode
{
    public List<string> Fields { get; } = fields;
}