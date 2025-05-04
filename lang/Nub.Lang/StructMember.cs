using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang;

public class StructMember(string name, NubType type, Optional<ExpressionNode> value)
{
    public string Name { get; } = name;
    public NubType Type { get; } = type;
    public Optional<ExpressionNode> Value { get; } = value;
}