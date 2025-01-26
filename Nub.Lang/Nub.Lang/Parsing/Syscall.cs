namespace Nub.Lang.Parsing;

public class Syscall(IEnumerable<ExpressionNode> parameters)
{
    public IEnumerable<ExpressionNode> Parameters { get; } = parameters;
}