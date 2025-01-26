namespace Nub.Lang.Parsing;

public class FuncCall(string name, IEnumerable<ExpressionNode> parameters)
{
    public string Name { get; } = name;
    public IEnumerable<ExpressionNode> Parameters { get; } = parameters;

    public override string ToString() => $"{Name}()";
}