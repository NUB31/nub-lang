namespace Nub.Lang.Frontend.Parsing;

public class Syscall(IReadOnlyCollection<ExpressionNode> parameters)
{
    public IReadOnlyCollection<ExpressionNode> Parameters { get; } = parameters;
}