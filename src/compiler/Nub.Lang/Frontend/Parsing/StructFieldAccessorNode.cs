namespace Nub.Lang.Frontend.Parsing;

public class StructFieldAccessorNode(ExpressionNode @struct, string field) : ExpressionNode
{
    public ExpressionNode Struct { get; } = @struct;
    public string Field { get; } = field;
}