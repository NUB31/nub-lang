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
            "char" => PrimitiveTypeKind.Char,
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
    Char,
    Int64,
    Int32,
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