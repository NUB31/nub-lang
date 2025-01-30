namespace Nub.Lang.Frontend.Lexing;

public class SymbolToken(Symbol symbol) : Token
{
    public Symbol Symbol { get; } = symbol;
}

public enum Symbol
{
    Whitespace,
    Import,
    Extern,
    Func,
    Return,
    Let,
    If,
    Else,
    While,
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
    New,
}