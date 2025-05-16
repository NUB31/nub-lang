namespace Nub.Lang.Frontend.Parsing;

public class FuncCallExpressionNode(FuncCall funcCall) : ExpressionNode
{
    public FuncCall FuncCall { get; } = funcCall;

    public override string ToString() => FuncCall.ToString();
}