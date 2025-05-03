namespace Nub.Lang.Frontend.Parsing;

public class BlockNode(List<StatementNode> statements) : Node
{
    public List<StatementNode> Statements { get; } = statements;
}