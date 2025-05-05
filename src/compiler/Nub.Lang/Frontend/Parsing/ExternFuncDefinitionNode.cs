namespace Nub.Lang.Frontend.Parsing;

public class ExternFuncDefinitionNode(string name, List<FuncParameter> parameters, Optional<NubType> returnType) : DefinitionNode
{
    public string Name { get; } = name;
    public List<FuncParameter> Parameters { get; } = parameters;
    public Optional<NubType> ReturnType { get; } = returnType;

    public override string ToString() => $"{Name}({string.Join(", ", Parameters.Select(p => p.ToString()))}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
}