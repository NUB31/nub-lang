using Nub.Lang.Lexing;
using Nub.Lib;

namespace Nub.Lang.Parsing;

public class Parser
{
    private readonly Token[] _tokens;
    private int _index;

    public Parser(IReadOnlyCollection<Token> tokens)
    {
        _tokens = tokens.ToArray();
    }

    public IReadOnlyCollection<DefinitionNode> Parse()
    {
        _index = 0;
        List<DefinitionNode> definitions = [];
        while (Peek().HasValue)
        {
            definitions.Add(ParseDefinition());
        }

        return definitions;
    }

    private DefinitionNode ParseDefinition()
    {
        var keyword = ExpectSymbol();
        return keyword.Symbol switch
        {
            Symbol.Let => ParseGlobalVariableDefinition(),
            Symbol.Func => ParseFuncDefinition(),
            _ => throw new Exception("Unexpected symbol: " + keyword.Symbol)
        };
    }

    private GlobalVariableDefinitionNode ParseGlobalVariableDefinition()
    {
        var name = ExpectIdentifier();
        ExpectSymbol(Symbol.Assign);
        var value = ParseExpression();
        ExpectSymbol(Symbol.Semicolon);

        return new GlobalVariableDefinitionNode(name.Value, value);
    }

    private FuncDefinitionNode ParseFuncDefinition()
    {
        var name = ExpectIdentifier();
        List<FuncParameter> parameters = [];
        ExpectSymbol(Symbol.OpenParen);
        if (!TryExpectSymbol(Symbol.CloseParen))
        {
            while (!TryExpectSymbol(Symbol.CloseParen))
            {
                parameters.Add(ParseFuncParameter());
                TryExpectSymbol(Symbol.Comma);
            }
        }

        var returnType = Optional<Type>.Empty();
        if (TryExpectSymbol(Symbol.Colon))
        {
            returnType = ParseType();
        }

        var body = ParseBlock();

        return new FuncDefinitionNode(name.Value, parameters, body, returnType);
    }

    private FuncParameter ParseFuncParameter()
    {
        var name = ExpectIdentifier();
        ExpectSymbol(Symbol.Colon);
        var type = ParseType();

        return new FuncParameter(name.Value, type);
    }

    private StatementNode ParseStatement()
    {
        var token = ExpectToken();
        switch (token)
        {
            case IdentifierToken identifier:
            {
                var symbol = ExpectSymbol();
                switch (symbol.Symbol)
                {
                    case Symbol.OpenParen:
                    {
                        var parameters = new List<ExpressionNode>();
                        while (!TryExpectSymbol(Symbol.CloseParen))
                        {
                            parameters.Add(ParseExpression());
                            TryExpectSymbol(Symbol.Comma);
                        }

                        ExpectSymbol(Symbol.Semicolon);

                        if (identifier.Value == "syscall")
                        {
                            return new SyscallStatementNode(new Syscall(parameters));
                        }

                        return new FuncCallStatementNode(new FuncCall(identifier.Value, parameters));
                    }
                    case Symbol.Assign:
                    {
                        var value = ParseExpression();
                        ExpectSymbol(Symbol.Semicolon);
                        return new VariableReassignmentNode(identifier.Value, value);
                    }
                    default:
                    {
                        throw new Exception($"Unexpected symbol {symbol.Symbol}");
                    }
                }
            }
            case SymbolToken symbol:
            {
                switch (symbol.Symbol)
                {
                    case Symbol.Return:
                    {
                        var value = Optional<ExpressionNode>.Empty();
                        if (!TryExpectSymbol(Symbol.Semicolon))
                        {
                            value = ParseExpression();
                            ExpectSymbol(Symbol.Semicolon);
                        }

                        return new ReturnNode(value);
                    }
                    case Symbol.Let:
                    {
                        var name = ExpectIdentifier().Value;
                        ExpectSymbol(Symbol.Assign);
                        var value = ParseExpression();
                        ExpectSymbol(Symbol.Semicolon);
                        return new VariableAssignmentNode(name, value);
                    }
                    default:
                    {
                        throw new Exception($"Unexpected symbol {symbol.Symbol}");
                    }
                }   
            }
            default:
            {
                throw new Exception($"Unexpected token type {token.GetType().Name}");
            }
        }
    }

    private ExpressionNode ParseExpression()
    {
        var token = ExpectToken();
        return token switch
        {
            LiteralToken literal => new LiteralNode(literal.Value, literal.Type),
            IdentifierToken identifier => ParseExpressionIdentifier(identifier),
            _ => throw new Exception($"Unexpected token type {token.GetType().Name}")
        };
    }

    private ExpressionNode ParseExpressionIdentifier(IdentifierToken identifier)
    {
        if (TryExpectSymbol(Symbol.OpenParen))
        {
            List<ExpressionNode> parameters = [];
            while (!TryExpectSymbol(Symbol.CloseParen))
            {
                parameters.Add(ParseExpression());
                TryExpectSymbol(Symbol.Comma);
            }
            
            if (identifier.Value == "syscall")
            {
                return new SyscallExpressionNode(new Syscall(parameters));
            }

            if (identifier.Value == "strlen" && parameters.Count == 1)
            {
                return new StrlenNode(parameters[0]);
            }

            return new FuncCallExpressionNode(new FuncCall(identifier.Value, parameters));
        }
        
        return new IdentifierNode(identifier.Value);
    }

    private BlockNode ParseBlock()
    {
        ExpectSymbol(Symbol.OpenBrace);
        List<StatementNode> statements = [];
        while (!TryExpectSymbol(Symbol.CloseBrace))
        {
            statements.Add(ParseStatement());
        }

        return new BlockNode(statements);
    }

    private Type ParseType()
    {
        var name = ExpectIdentifier().Value;
        if (name == "Func")
        {
            List<Type> typeArguments = [];
            if (TryExpectSymbol(Symbol.LessThan))
            {
                while (!TryExpectSymbol(Symbol.GreaterThan))
                {
                    var type = ParseType();
                    typeArguments.Add(type);
                    TryExpectSymbol(Symbol.Comma);
                }
            }

            var returnType = Optional<Type>.OfNullable(typeArguments.LastOrDefault());

            return new DelegateType(typeArguments.Take(typeArguments.Count - 1).ToList(), returnType);
        }

        if (name == "String")
        {
            return new StringType();
        }

        return PrimitiveType.Parse(name);
    }

    private Token ExpectToken()
    {
        var token = Peek();
        if (!token.HasValue)
        {
            throw new Exception("Reached end of tokens");
        }

        Next();
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

    private bool TryExpectSymbol(Symbol symbol)
    {
        var result = Peek() is { HasValue: true, Value: SymbolToken symbolToken } && symbolToken.Symbol == symbol;
        if (result) Next();
        return result;
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
        while (_index < _tokens.Length && _tokens[_index] is SymbolToken { Symbol: Symbol.Whitespace })
        {
            Next();
        }

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