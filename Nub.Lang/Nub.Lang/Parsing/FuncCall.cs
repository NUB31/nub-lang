namespace Nub.Lang.Parsing;

public class FuncCall(string name, IReadOnlyCollection<ExpressionNode> parameters)
{
    public string Name { get; } = name;
    public IReadOnlyCollection<ExpressionNode> Parameters { get; } = parameters;

    public override string ToString() => $"{Name}()";
}