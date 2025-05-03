namespace Nub.Lang.Frontend.Parsing;

public class Syscall(List<ExpressionNode> parameters)
{
    public List<ExpressionNode> Parameters { get; } = parameters;
}