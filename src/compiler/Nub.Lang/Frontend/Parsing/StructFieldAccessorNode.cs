namespace Nub.Lang.Frontend.Parsing;

public class StructFieldAccessorNode(List<string> fields) : ExpressionNode
{
    public List<string> Fields { get; } = fields;
}