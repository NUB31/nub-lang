namespace Nub.Lang.Parsing;

public class SyscallStatementNode(Syscall syscall) : StatementNode
{
    public Syscall Syscall { get; } = syscall;
}