using System.Diagnostics.CodeAnalysis;

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
    
    public static bool operator == (Type? left, Type? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return ReferenceEquals(left, right) || left.Equals(right);
    }

    public static bool operator !=(Type? left, Type? right) => !(left == right);
}

public class AnyType : Type
{
    protected override bool Equals(Type other) => other is AnyType;
    public override int GetHashCode() => nameof(AnyType).GetHashCode();
    public override string ToString() => "Any";
}

public class PrimitiveType(PrimitiveTypeKind kind) : Type
{
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

    public static bool TryParse(string value, [NotNullWhen(true)] out PrimitiveType? result)
    {
        result = value switch
        {
            "bool" => new PrimitiveType(PrimitiveTypeKind.Bool),
            "int64" => new PrimitiveType(PrimitiveTypeKind.Int64),
            "int32" => new PrimitiveType(PrimitiveTypeKind.Int32),
            _ => null
        };
        
        return result != null;
    }

    public PrimitiveTypeKind Kind { get; } = kind;

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

public class ArrayType(Type innerType) : Type
{
    public Type InnerType { get; } = innerType;

    public override bool IsAssignableTo(Type otherType)
    {
        if (otherType is ArrayType arrayType && arrayType.InnerType.IsAssignableTo(InnerType)) return true;
        return base.IsAssignableTo(otherType);
    }

    protected override bool Equals(Type other) => other is ArrayType at && InnerType.Equals(at.InnerType);
    public override int GetHashCode() => HashCode.Combine(InnerType);
    public override string ToString() => $"Array<{InnerType}>";
}

public class StructType(string name) : Type
{
    public string Name { get; } = name;
    
    protected override bool Equals(Type other) => other is StructType classType && Name == classType.Name;
    public override int GetHashCode() => Name.GetHashCode();
    public override string ToString() => Name;
}