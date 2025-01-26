using Nub.Lib;

namespace Nub.Lang.Parsing;

public class FuncDefinitionNode(string name, IEnumerable<FuncParameter> parameters, BlockNode body, Optional<Type> returnType) : DefinitionNode
{
    public string Name { get; } = name;
    public IEnumerable<FuncParameter> Parameters { get; } = parameters;
    public BlockNode Body { get; } = body;
    public Optional<Type> ReturnType { get; } = returnType;

    public override string ToString() => $"{Name}({Parameters.Select(p => p.ToString())}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
}