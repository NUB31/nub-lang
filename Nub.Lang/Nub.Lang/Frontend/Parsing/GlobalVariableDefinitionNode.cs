namespace Nub.Lang.Frontend.Parsing;

public class GlobalVariableDefinitionNode(string name, ExpressionNode value) : DefinitionNode
{
    public string Name { get; } = name;
    public ExpressionNode Value { get; } = value;
}