namespace Nub.Lang.Parsing;

public class StrlenNode(ExpressionNode @string) : ExpressionNode
{
    public ExpressionNode String { get; } = @string;
}