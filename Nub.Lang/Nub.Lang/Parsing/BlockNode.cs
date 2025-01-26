namespace Nub.Lang.Parsing;

public class BlockNode(IEnumerable<StatementNode> statements) : Node
{
    public IEnumerable<StatementNode> Statements { get; } = statements;
}