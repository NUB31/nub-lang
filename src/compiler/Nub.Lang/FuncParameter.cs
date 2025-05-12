namespace Nub.Lang;

public class FuncParameter(string name, NubType type, bool variadic)
{
    public string Name { get; } = name;
    public NubType Type { get; } = type;
    public bool Variadic { get; } = variadic;

    public override string ToString() => $"{Name}: {Type}";
}