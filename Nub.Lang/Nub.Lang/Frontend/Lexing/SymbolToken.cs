namespace Nub.Lang.Frontend.Lexing;

public class SymbolToken(Symbol symbol) : Token
{
    public Symbol Symbol { get; } = symbol;
}

public enum Symbol
{
    Whitespace,
    Extern,
    Func,
    Return,
    Let,
    If,
    Else,
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
    Bang,
    Equal,
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