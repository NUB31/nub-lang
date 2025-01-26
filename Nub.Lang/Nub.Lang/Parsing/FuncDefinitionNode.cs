namespace Nub.Lang.Parsing;

public class FuncDefinitionNode(string name, IEnumerable<FuncParameter> parameters) : DefinitionNode
{
    public string Name { get; } = name;
    public IEnumerable<FuncParameter> Parameters { get; } = parameters;
}