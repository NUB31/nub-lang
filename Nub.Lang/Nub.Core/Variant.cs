namespace Nub.Core;

public readonly struct Variant<T1, T2> where T1 : notnull where T2 : notnull
{
    public Variant()
    {
        throw new InvalidOperationException("Variant must be initialized with a value");
    }
    
    public Variant(T1 value)
    {
        _value = value;
    }
    
    public Variant(T2 value)
    {
        _value = value;
    }

    private readonly object _value;

    public void Match(Action<T1> on1, Action<T2> on2)
    {
        switch (_value)
        {
            case T1 v1:
                on1(v1);
                break;
            case T2 v2:
                on2(v2);
                break;
            default:
                throw new InvalidCastException();
        }
    }
    
    public T Match<T>(Func<T1, T> on1, Func<T2, T> on2)
    {
        return _value switch
        {
            T1 v1 => on1(v1),
            T2 v2 => on2(v2),
            _ => throw new InvalidCastException()
        };
    }
    
    public static implicit operator Variant<T1, T2>(T1 value) => new(value);
    public static implicit operator Variant<T1, T2>(T2 value) => new(value);
}