using Nub.Lib;

namespace Nub.Lang.Parsing;

public class FuncDefinitionNode(string name, IReadOnlyCollection<FuncParameter> parameters, BlockNode body, Optional<Type> returnType) : DefinitionNode
{
    public string Name { get; } = name;
    public IReadOnlyCollection<FuncParameter> Parameters { get; } = parameters;
    public BlockNode Body { get; } = body;
    public Optional<Type> ReturnType { get; } = returnType;

    public override string ToString() => $"{Name}({string.Join(", ", Parameters.Select(p => p.ToString()))}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
}