using Nub.Lang.Lexing;
using Nub.Lib;

namespace Nub.Lang.Parsing;

public class Parser
{
    private readonly Token[] _tokens;
    private int _index;

    public Parser(IEnumerable<Token> tokens)
    {
        _tokens = tokens.ToArray();
    }

    public IEnumerable<DefinitionNode> Parse()
    {
        List<DefinitionNode> definitions = [];
        while (Peek().HasValue)
        {
            definitions.Add(ParseDefinition());
        }
        return definitions;
    }

    private DefinitionNode ParseDefinition()
    {
        throw new NotImplementedException();
    }

    private Token ExpectToken()
    {
        var token = Peek();
        if (!token.HasValue)
        {
            throw new Exception("Reached end of tokens");
        }

        return token.Value;
    }

    private SymbolToken ExpectSymbol()
    {
        var token = ExpectToken();
        if (token is not SymbolToken symbol)
        {
            throw new Exception($"Expected {nameof(SymbolToken)} but got {token.GetType().Name}");
        }
        return symbol;
    }
    
    private void ExpectSymbol(Symbol symbol)
    {
        var token = ExpectSymbol();
        if (token.Symbol != symbol)
        {
            throw new Exception($"Expected symbol {symbol} but got {token.Symbol}");
        }
    }

    private IdentifierToken ExpectIdentifier()
    {
        var token = ExpectToken();
        if (token is not IdentifierToken identifier)
        {
            throw new Exception($"Expected {nameof(IdentifierToken)} but got {token.GetType().Name}");
        }
        return identifier;
    }
    
    private LiteralToken ExpectLiteral()
    {
        var token = ExpectToken();
        if (token is not LiteralToken literal)
        {
            throw new Exception($"Expected {nameof(LiteralToken)} but got {token.GetType().Name}");
        }
        return literal;
    }
    
    private Optional<Token> Peek()
    {
        if (_index < _tokens.Length)
        {
            return _tokens[_index];
        }
        
        return Optional<Token>.Empty();
    }
    
    private void Next()
    {
        _index++;
    }
}