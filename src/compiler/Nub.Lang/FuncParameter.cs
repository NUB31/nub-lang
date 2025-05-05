namespace Nub.Lang;

public class FuncParameter(string name, NubType type)
{
    public string Name { get; } = name;
    public NubType Type { get; } = type;

    public override string ToString() => $"{Name}: {Type}";
}