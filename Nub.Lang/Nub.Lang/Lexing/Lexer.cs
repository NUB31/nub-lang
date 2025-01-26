using Nub.Lib;

namespace Nub.Lang.Lexing;

public class Lexer
{
    private static readonly Dictionary<string, Symbol> Keywords = new()
    { 
        ["func"] = Symbol.Func,
        ["return"] = Symbol.Return,
        ["let"] = Symbol.Let,
    };

    private static readonly Dictionary<char, Symbol> Chars = new()
    {
        [';'] = Symbol.Semicolon,
        [':'] = Symbol.Colon,
        ['('] = Symbol.OpenParen,
        [')'] = Symbol.CloseParen,
        ['{'] = Symbol.OpenBrace,
        ['}'] = Symbol.CloseBrace,
        ['['] = Symbol.OpenBracket,
        [']'] = Symbol.CloseBracket,
        [','] = Symbol.Comma,
        ['.'] = Symbol.Period,
        ['='] = Symbol.Assign,
        ['<'] = Symbol.LessThan,
        ['>'] = Symbol.GreaterThan,
    };
    
    private readonly string _src;
    private int _index;

    public Lexer(string src)
    {
        _src = src;
    }
    
    public IReadOnlyCollection<Token> Lex()
    {
        _index = 0;
        List<Token> tokens = [];
        while (Peek().HasValue)
        {
            tokens.Add(ParseToken());
        }
        return tokens;
    }

    private Token ParseToken()
    {
        var current = Peek();
        
        if (char.IsLetter(current.Value) || current.Value == '_')
        {
            var buffer = string.Empty;
                
            while (current.HasValue && (char.IsLetterOrDigit(current.Value) || current.Value == '_'))
            {
                buffer += current.Value;
                Next();
                current = Peek();
            }

            if (Keywords.TryGetValue(buffer, out var keywordSymbol))
            {
                return new SymbolToken(keywordSymbol);
            }

            return new IdentifierToken(buffer);
        }

        if (char.IsDigit(current.Value))
        {
            var buffer = string.Empty;
                
            while (current.HasValue && char.IsDigit(current.Value))
            {
                buffer += current.Value;
                Next();
                current = Peek();
            }

            return new LiteralToken(new PrimitiveType(PrimitiveTypeKind.Int64), buffer);
        }

        if (Chars.TryGetValue(current.Value, out var charSymbol))
        {
            Next();
            return new SymbolToken(charSymbol);
        }

        if (current.Value == '"')
        {
            Next();
            var buffer = string.Empty;
            
            while (true)
            {
                current = Peek();
                Next();
                if (!current.HasValue) throw new Exception("Unclosed string literal");
                if (current.Value == '"') break;
                buffer += current.Value;
            }
            
            return new LiteralToken(new PointerType(), buffer);
        }

        if (char.IsWhiteSpace(current.Value))
        {
            Next();
            return new SymbolToken(Symbol.Whitespace);
        }

        throw new Exception($"Unknown character {current.Value}");
    }

    private Optional<char> Peek()
    {
        if (_index < _src.Length)
        {
            return _src[_index];
        }
        
        return Optional<char>.Empty();
    }

    private void Next()
    {
        _index++;
    }
}