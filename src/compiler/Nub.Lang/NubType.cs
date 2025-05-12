namespace Nub.Lang;

public sealed class NubType
{
    public NubType(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public static NubType Int64 => new("i64");
    public static NubType Int32 => new("i32");
    public static NubType Bool => new("bool");
    public static NubType String => new("string");
    public static NubType Any => new("any");

    public override bool Equals(object? obj)
    {
        return obj is NubType item && Name.Equals(item.Name);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name);
    }

    public override string ToString()
    {
        return $"{Name}";
    }
}