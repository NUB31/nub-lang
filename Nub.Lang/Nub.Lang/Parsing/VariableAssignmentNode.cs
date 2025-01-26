namespace Nub.Lang.Parsing;

public class VariableAssignmentNode(string name, ExpressionNode value) : StatementNode
{
    public string Name { get; } = name;
    public ExpressionNode Value { get; } = value;
}