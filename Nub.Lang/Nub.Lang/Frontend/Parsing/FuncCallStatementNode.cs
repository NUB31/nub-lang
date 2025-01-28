namespace Nub.Lang.Frontend.Parsing;

public class FuncCallStatementNode(FuncCall funcCall) : StatementNode
{
    public FuncCall FuncCall { get; } = funcCall;
    
    public override string ToString() => FuncCall.ToString();
}