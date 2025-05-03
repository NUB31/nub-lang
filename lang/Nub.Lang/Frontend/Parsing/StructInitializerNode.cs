namespace Nub.Lang.Frontend.Parsing;

public class StructInitializerNode(StructType structType, Dictionary<string, ExpressionNode> initializers) : ExpressionNode
{
    public StructType StructType { get; } = structType;
    public Dictionary<string, ExpressionNode> Initializers { get; } = initializers;
}