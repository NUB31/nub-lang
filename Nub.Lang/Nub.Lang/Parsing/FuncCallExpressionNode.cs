namespace Nub.Lang.Parsing;

public class FuncCallExpressionNode(FuncCall funcCall) : ExpressionNode
{
    public FuncCall FuncCall { get; } = funcCall;

    public override string ToString() => FuncCall.ToString();
}