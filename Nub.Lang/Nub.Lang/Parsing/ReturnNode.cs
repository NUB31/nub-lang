using Nub.Lib;

namespace Nub.Lang.Parsing;

public class ReturnNode(Optional<ExpressionNode> value) : StatementNode
{
    public Optional<ExpressionNode> Value { get; } = value;
}