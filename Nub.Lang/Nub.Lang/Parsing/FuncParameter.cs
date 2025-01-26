namespace Nub.Lang.Parsing;

public class FuncParameter(string name, Type type)
{
    public string Name { get; } = name;
    public Type Type { get; } = type;
}