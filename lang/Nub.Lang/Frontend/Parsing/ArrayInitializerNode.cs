namespace Nub.Lang.Frontend.Parsing;

public class ArrayInitializerNode(long length, Type innerType) : ExpressionNode
{
    public long Length { get; } = length;
    public Type InnerType { get; } = innerType;
}