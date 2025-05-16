namespace Nub.Lang.Frontend.Parsing;

public class FuncCall(string name, List<ExpressionNode> parameters)
{
    public string Name { get; } = name;
    public List<ExpressionNode> Parameters { get; } = parameters;

    public override string ToString() => $"{Name}()";
}