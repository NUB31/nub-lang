namespace Nub.Lang.Frontend.Parsing;

public class LocalFuncDefinitionNode(string name, List<FuncParameter> parameters, BlockNode body, Optional<NubType> returnType, bool global) : DefinitionNode
{
    public string Name { get; } = name;
    public List<FuncParameter> Parameters { get; } = parameters;
    public BlockNode Body { get; } = body;
    public Optional<NubType> ReturnType { get; } = returnType;
    public bool Global { get; } = global;

    public override string ToString() => $"{Name}({string.Join(", ", Parameters.Select(p => p.ToString()))}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
}