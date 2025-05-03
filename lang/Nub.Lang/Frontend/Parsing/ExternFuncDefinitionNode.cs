namespace Nub.Lang.Frontend.Parsing;

public class ExternFuncDefinitionNode(string name, List<FuncParameter> parameters, Optional<Type> returnType) : DefinitionNode
{
    public string Name { get; } = name;
    public List<FuncParameter> Parameters { get; } = parameters;
    public Optional<Type> ReturnType { get; } = returnType;

    public override string ToString() => $"{Name}({string.Join(", ", Parameters.Select(p => p.ToString()))}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
}