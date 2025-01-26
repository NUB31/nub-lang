using Nub.Lib;

namespace Nub.Lang;

public abstract record Type;

public record PrimitiveType : Type
{
    public PrimitiveType(PrimitiveTypeKind kind)
    {
        Kind = kind;
    }
    
    public static PrimitiveType Parse(string value)
    {
        var kind = value switch
        {
            "bool" => PrimitiveTypeKind.Bool,
            "char" => PrimitiveTypeKind.Char,
            "int8" => PrimitiveTypeKind.Int8,
            "uint8" => PrimitiveTypeKind.UInt8,
            "int16" => PrimitiveTypeKind.Int16,
            "uint16" => PrimitiveTypeKind.UInt16,
            "int32" => PrimitiveTypeKind.Int32,
            "uint32" => PrimitiveTypeKind.UInt32,
            "int64" => PrimitiveTypeKind.Int64,
            "uint64" => PrimitiveTypeKind.UInt64,
            "float" => PrimitiveTypeKind.Float,
            "double" => PrimitiveTypeKind.Double,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
        
        return new PrimitiveType(kind);
    }
    
    public PrimitiveTypeKind Kind { get; }

    public override string ToString() => Kind.ToString();
}

public enum PrimitiveTypeKind
{
    Bool,
    Char,
    Int8,
    UInt8,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float,
    Double,
}

public record StringType : Type
{
    public override string ToString() => "String";
}

public record DelegateType : Type
{
    public DelegateType(IReadOnlyCollection<Type> parameters, Optional<Type> returnType)
    {
        Parameters = parameters;
        ReturnType = returnType;
    }
    
    public IReadOnlyCollection<Type> Parameters { get; }
    public Optional<Type> ReturnType { get; }

    public override string ToString() => $"({string.Join(", ", Parameters)}): {(ReturnType.HasValue ? ReturnType.Value.ToString() : "")}";
}