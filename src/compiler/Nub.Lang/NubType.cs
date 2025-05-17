using System.Diagnostics.CodeAnalysis;

namespace Nub.Lang;

public abstract class NubType
{
    protected NubType(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public static NubType Parse(string s)
    {
        if (NubPrimitiveType.TryParse(s, out var kind))
        {
            return new NubPrimitiveType(kind.Value);
        }

        return new NubStructType(s);
    }

    public override bool Equals(object? obj) => obj is NubType item && Name.Equals(item.Name);
    public override int GetHashCode() => HashCode.Combine(Name);
    public override string ToString() => Name;
}

public class NubStructType(string name) : NubType(name);

public class NubPointerType(NubType baseType) : NubType("^" + baseType)
{
    public NubType BaseType { get; } = baseType;
    
    public override bool Equals(object? obj)
    {
        if (obj is NubPointerType other)
        {
            return BaseType.Equals(other.BaseType);
        }
        return false;
    }

    public override int GetHashCode() => BaseType.GetHashCode() * 31;
}

public class NubPrimitiveType(PrimitiveTypeKind kind) : NubType(KindToString(kind))
{
    public PrimitiveTypeKind Kind { get; } = kind;

    public static NubPrimitiveType I64 => new(PrimitiveTypeKind.I64);
    public static NubPrimitiveType I32 => new(PrimitiveTypeKind.I32);
    public static NubPrimitiveType I16 => new(PrimitiveTypeKind.I16);
    public static NubPrimitiveType I8 => new(PrimitiveTypeKind.I8);
    
    public static NubPrimitiveType U64 => new(PrimitiveTypeKind.U64);
    public static NubPrimitiveType U32 => new(PrimitiveTypeKind.U32);
    public static NubPrimitiveType U16 => new(PrimitiveTypeKind.U16);
    public static NubPrimitiveType U8 => new(PrimitiveTypeKind.U8);
    
    public static NubPrimitiveType F64 => new(PrimitiveTypeKind.F64);
    public static NubPrimitiveType F32 => new(PrimitiveTypeKind.F32);
    
    public static NubPrimitiveType Bool => new(PrimitiveTypeKind.Bool);
    public static NubPrimitiveType String => new(PrimitiveTypeKind.String);
    public static NubPrimitiveType Any => new(PrimitiveTypeKind.Any);

    public static bool TryParse(string s, [NotNullWhen(true)] out PrimitiveTypeKind? kind)
    {
        kind = s switch
        {
            "i64" => PrimitiveTypeKind.I64,
            "i32" => PrimitiveTypeKind.I32,
            "i16" => PrimitiveTypeKind.I16,
            "i8" => PrimitiveTypeKind.I8,
            "u64" => PrimitiveTypeKind.U64,
            "u32" => PrimitiveTypeKind.U32,
            "u16" => PrimitiveTypeKind.U16,
            "u8" => PrimitiveTypeKind.U8,
            "f64" => PrimitiveTypeKind.F64,
            "f32" => PrimitiveTypeKind.F32,
            "bool" => PrimitiveTypeKind.Bool,
            "string" => PrimitiveTypeKind.String,
            "any" => PrimitiveTypeKind.Any,
            _ => null
        };

        return kind != null;
    }

    public static string KindToString(PrimitiveTypeKind kind)
    {
        return kind switch
        {
            PrimitiveTypeKind.I8 => "i8",
            PrimitiveTypeKind.I16 => "i16",
            PrimitiveTypeKind.I32 => "i32",
            PrimitiveTypeKind.I64 => "i64",
            
            PrimitiveTypeKind.U8 => "u8",
            PrimitiveTypeKind.U16 => "u16",
            PrimitiveTypeKind.U32 => "u32",
            PrimitiveTypeKind.U64 => "u64",
            
            PrimitiveTypeKind.F32 => "f32",
            PrimitiveTypeKind.F64 => "f64",
            
            PrimitiveTypeKind.Bool => "bool",
            PrimitiveTypeKind.String => "string",
            PrimitiveTypeKind.Any => "any",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}

public enum PrimitiveTypeKind
{
    I64,
    I32,
    I16,
    I8,
    U64,
    U32,
    U16,
    U8,
    F64,
    F32,
    Bool,
    String,
    Any
}