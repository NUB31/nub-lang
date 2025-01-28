namespace Nub.Lang.Frontend.Parsing;

public class SyscallExpressionNode(Syscall syscall) : ExpressionNode
{
    public Syscall Syscall { get; } = syscall;
}