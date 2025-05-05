namespace Nub.Lang.Frontend.Parsing;

public class VariableReassignmentNode(string name, ExpressionNode value) : StatementNode
{
    public string Name { get; } = name;
    public ExpressionNode Value { get; } = value;
}