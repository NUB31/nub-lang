namespace Nub.Lang.Parsing;

public class ESyscallNode(IEnumerable<FuncParameter> parameters) : ExpressionNode
{
    public IEnumerable<FuncParameter> Parameters { get; } = parameters;
}

public class SSyscallNode(IEnumerable<FuncParameter> parameters) : StatementNode
{
    public IEnumerable<FuncParameter> Parameters { get; } = parameters;
}