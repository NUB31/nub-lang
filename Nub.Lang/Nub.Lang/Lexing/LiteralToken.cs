﻿namespace Nub.Lang.Lexing;

public class LiteralToken(Type type, string value) : Token
{
    public Type Type { get; } = type;
    public string Value { get; } = value;
}