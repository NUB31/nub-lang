namespace Nub.Lang.Frontend.Parsing;

public class SyscallStatementNode(Syscall syscall) : StatementNode
{
    public Syscall Syscall { get; } = syscall;
}