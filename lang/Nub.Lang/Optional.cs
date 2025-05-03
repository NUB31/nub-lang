using System.Diagnostics.CodeAnalysis;

namespace Nub.Lang;

public readonly struct Optional
{
    public static Optional<TValue> Empty<TValue>() => new();

    /// <summary>
    /// Alias for creating an Optional&lt;TValue&gt; which allows for implicit types
    /// </summary>
    /// <param name="value"></param>
    /// <typeparam name="TValue"></typeparam>
    /// <returns></returns>
    public static Optional<TValue> OfNullable<TValue>(TValue? value)
    {
        return value ?? Optional<TValue>.Empty();
    }
}

public readonly struct Optional<TValue>
{
    public static Optional<TValue> Empty() => new();

    public static Optional<TValue> OfNullable(TValue? value)
    {
        return value ?? Empty();
    }

    public Optional()
    {
        Value = default;
        HasValue = false;
    }

    public Optional(TValue value)
    {
        Value = value;
        HasValue = true;
    }

    public TValue? Value { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue { get; }

    public static implicit operator Optional<TValue>(TValue value) => new(value);
}