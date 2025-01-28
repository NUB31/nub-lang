namespace Nub.Lang.Frontend.Parsing;

public class BlockNode(IReadOnlyCollection<StatementNode> statements) : Node
{
    public IReadOnlyCollection<StatementNode> Statements { get; } = statements;
}