using Nub.Core;

namespace Nub.Lang.Frontend.Parsing;

public class ExternFuncDefinitionNode(string name, IReadOnlyCollection<FuncParameter> parameters, Optional<Type> returnType) : DefinitionNode
{
    public string Name { get; } = name;
    public IReadOnlyCollection<FuncParameter> Parameters { get; } = parameters;
    public Optional<Type> ReturnType { get; } = returnType;

    public override string ToString() => $"{Name}({string.Join(", ", Parameters.Select(p => p.ToString()))}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
}