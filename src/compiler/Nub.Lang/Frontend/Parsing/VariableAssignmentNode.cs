namespace Nub.Lang.Frontend.Parsing;

public class VariableAssignmentNode(string name, Optional<NubType> explicitType, ExpressionNode value) : StatementNode
{
    public string Name { get; } = name;
    public Optional<NubType> ExplicitType { get; } = explicitType;
    public ExpressionNode Value { get; } = value;
}