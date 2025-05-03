namespace Nub.Lang;

public class FuncParameter(string name, Type type)
{
    public string Name { get; } = name;
    public Type Type { get; } = type;

    public override string ToString() => $"{Name}: {Type}";
}