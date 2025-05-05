namespace Nub.Lang.Frontend.Parsing;

public class StructInitializerNode(NubType structType, Dictionary<string, ExpressionNode> initializers) : ExpressionNode
{
    public NubType StructType { get; } = structType;
    public Dictionary<string, ExpressionNode> Initializers { get; } = initializers;
}