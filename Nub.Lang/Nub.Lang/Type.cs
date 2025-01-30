namespace Nub.Lang;

public abstract class Type
{
    public virtual bool IsAssignableTo(Type otherType)
    {
        return this == otherType || otherType is AnyType;
    }
    
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not Type otherType) return false;
        return Equals(otherType);
    }
    
    protected abstract bool Equals(Type other);
    public abstract override int GetHashCode();
    
    public static bool operator == (Type left, Type right)
    {
        return ReferenceEquals(left, right) || left.Equals(right);
    }

    public static bool operator !=(Type left, Type right) => !(left == right);
}

public class AnyType : Type
{
    protected override bool Equals(Type other) => other is AnyType;
    public override int GetHashCode() => nameof(AnyType).GetHashCode();
    public override string ToString() => "Any";
}

public class PrimitiveType : Type
{
    public PrimitiveType(PrimitiveTypeKind kind)
    {
        Kind = kind;
    }

    // TODO: This should be looked at more in the future
    public override bool IsAssignableTo(Type otherType)
    {
        if (base.IsAssignableTo(otherType)) return true;
        
        if (otherType is PrimitiveType otherPrimitive)
        {
            return (Kind, otherPrimitive.Kind) switch
            {
                (PrimitiveTypeKind.Int32, PrimitiveTypeKind.Int64) => true,
                _ => false
            };
        }

        return false;
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

    protected override bool Equals(Type other) => other is PrimitiveType primitiveType && Kind == primitiveType.Kind;
    public override int GetHashCode() => Kind.GetHashCode();
    public override string ToString() => Kind.ToString();
}

public enum PrimitiveTypeKind
{
    Bool,
    Int64,
    Int32,
}

public class StringType : Type
{
    protected override bool Equals(Type other) => other is StringType;
    public override int GetHashCode() => nameof(StringType).GetHashCode();
    public override string ToString() => "String";
}

public class ArrayType : Type
{
    public ArrayType(Type innerType)
    {
        InnerType = innerType;
    }

    public Type InnerType { get; }

    public override bool IsAssignableTo(Type otherType)
    {
        if (otherType is ArrayType arrayType && arrayType.InnerType.IsAssignableTo(InnerType)) return true;
        return base.IsAssignableTo(otherType);
    }

    protected override bool Equals(Type other) => other is ArrayType at && InnerType.Equals(at.InnerType);
    public override int GetHashCode() => HashCode.Combine(InnerType);
    public override string ToString() => $"Array<{InnerType}>";
}