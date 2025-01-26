namespace Nub.Lang.Lexing;

public class SymbolToken(Symbol symbol) : Token
{
    public Symbol Symbol { get; } = symbol;
}

public enum Symbol
{
    Whitespace,
    Func,
    Return,
    Let,
    Semicolon,
    Colon,
    OpenParen,
    CloseParen,
    OpenBrace,
    CloseBrace,
    OpenBracket,
    CloseBracket,
    Comma,
    Period,
    Assign,
    Equal,
    Bang,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Plus,
    Minus,
    Star,
    ForwardSlash,
}