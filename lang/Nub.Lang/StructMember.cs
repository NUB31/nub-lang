using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang;

public class StructMember(string name, Type type, Optional<ExpressionNode> value)
{
    public string Name { get; } = name;
    public Type Type { get; } = type;
    public Optional<ExpressionNode> Value { get; } = value;
}