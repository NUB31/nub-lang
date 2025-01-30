using Nub.Core;

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
            "int64" => PrimitiveTypeKind.Int64,
            "int32" => PrimitiveTypeKind.Int32,
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
    Int64,
    Int32,
}

public record StringType : Type
{
    public override string ToString() => "String";
}

public record ArrayType : Type
{
    public ArrayType(Type innerType)
    {
        InnerType = innerType;
    }

    public Type InnerType { get; }

    public override string ToString() => $"{InnerType}";
}

public record DelegateType : Type
{
    public DelegateType(List<Type> parameters, Optional<Type> returnType)
    {
        Parameters = parameters;
        ReturnType = returnType;
    }
    
    public List<Type> Parameters { get; }
    public Optional<Type> ReturnType { get; }

    public override string ToString() => $"({string.Join(", ", Parameters)}): {(ReturnType.HasValue ? ReturnType.Value.ToString() : "")}";
}