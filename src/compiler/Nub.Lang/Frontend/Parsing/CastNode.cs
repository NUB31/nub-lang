namespace Nub.Lang.Frontend.Parsing;

public class CastNode(NubType targetType, ExpressionNode expression) : ExpressionNode
{
    public NubType TargetType { get; } = targetType;
    public ExpressionNode Expression { get; } = expression;
}