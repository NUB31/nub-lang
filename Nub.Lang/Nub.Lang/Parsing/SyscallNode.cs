namespace Nub.Lang.Parsing;

public class SyscallNode(IEnumerable<ExpressionNode> parameters) : StatementNode
{
    public IEnumerable<ExpressionNode> Parameters { get; } = parameters;
}