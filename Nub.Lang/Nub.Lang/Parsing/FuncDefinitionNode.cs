namespace Nub.Lang.Parsing;

public class FuncDefinitionNode(string name, IEnumerable<FuncParameter> parameters, BlockNode body) : DefinitionNode
{
    public string Name { get; } = name;
    public IEnumerable<FuncParameter> Parameters { get; } = parameters;
    public BlockNode Body { get; } = body;
}