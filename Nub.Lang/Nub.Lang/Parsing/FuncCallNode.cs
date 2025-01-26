namespace Nub.Lang.Parsing;

public class FuncCallNode(string name, IEnumerable<ExpressionNode> parameters) : StatementNode
{
    public string Name { get; } = name;
    public IEnumerable<ExpressionNode> Parameters { get; } = parameters;
}