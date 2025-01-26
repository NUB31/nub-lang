using Nub.Lib;

namespace Nub.Lang;

public abstract class Type;

public class PrimitiveType(PrimitiveTypeKind kind) : Type
{
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
    
    public PrimitiveTypeKind Kind { get; } = kind;
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

public class PointerType : Type;

public class DelegateType(IEnumerable<Type> parameters, Optional<Type> returnType) : Type
{
    public IEnumerable<Type> Parameters { get; } = parameters;
    public Optional<Type> ReturnType { get; } = returnType;
}