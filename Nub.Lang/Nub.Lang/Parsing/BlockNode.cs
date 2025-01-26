namespace Nub.Lang.Parsing;

public class BlockNode(IEnumerable<StatementNode> statements) : StatementNode
{
    public IEnumerable<StatementNode> Statements { get; } = statements;
}