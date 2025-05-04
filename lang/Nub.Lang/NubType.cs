namespace Nub.Lang;

public sealed class NubType
{
    public NubType(string name, NubType[] generics)
    {
        Name = name;
        Generics = generics;
    }

    public string Name { get; }
    public NubType[] Generics { get; }

    public static NubType Int64 => new("int64", []);
    public static NubType Int32 => new("int32", []);
    public static NubType Int16 => new("int16", []);
    public static NubType Int8 => new("int8", []);
    public static NubType Bool => new("bool", []);
    public static NubType String => new("string", []);
    public static NubType Array(NubType innerType) => new("array", [innerType]);

    public override bool Equals(object? obj)
    {
        if (obj is not NubType item)
        {
            return false;
        }

        return Name.Equals(item.Name) && Generics.SequenceEqual(item.Generics);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Generics);
    }

    public override string ToString()
    {
        return $"{Name}<{string.Join(", ", Generics.Select(x => x.ToString()))}>";
    }
}