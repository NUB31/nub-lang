using System.Diagnostics.CodeAnalysis;
using Nub.Core;
using Nub.Lang.Frontend.Lexing;

namespace Nub.Lang.Frontend.Parsing;

public class Parser
{
    private readonly Token[] _tokens;
    private int _index;

    public Parser(IReadOnlyCollection<Token> tokens)
    {
        _tokens = tokens.ToArray();
    }

    public FileNode ParseFile(string relativePath)
    {
        _index = 0;
        List<DefinitionNode> definitions = [];
        List<string> includes = [];

        while (TryExpectSymbol(Symbol.Include))
        {
            var name = ExpectLiteral();
            if (name.Type is not StringType)
            {
                throw new Exception("Using statements must have a string literal value");
            }

            TryExpectSymbol(Symbol.Semicolon);
            includes.Add(name.Value);
        }
        
        while (Peek().HasValue)
        {
            definitions.Add(ParseDefinition());
        }

        return new FileNode(includes, definitions);
    }

    private DefinitionNode ParseDefinition()
    {
        var keyword = ExpectSymbol();
        return keyword.Symbol switch
        {
            Symbol.Let => ParseGlobalVariableDefinition(),
            Symbol.Func => ParseFuncDefinition(),
            Symbol.Extern => ParseExternFuncDefinition(),
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

    private LocalFuncDefinitionNode ParseFuncDefinition()
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

        return new LocalFuncDefinitionNode(name.Value, parameters, body, returnType);
    }

    private ExternFuncDefinitionNode ParseExternFuncDefinition()
    {
        ExpectSymbol(Symbol.Func);
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

        ExpectSymbol(Symbol.Semicolon);
        
        return new ExternFuncDefinitionNode(name.Value, parameters, returnType);
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
                        return ParseReturn();
                    }
                    case Symbol.Let:
                    {
                        return ParseVariableAssignment();
                    }
                    case Symbol.If:
                    {
                        return ParseIf();
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

    private ReturnNode ParseReturn()
    {
        var value = Optional<ExpressionNode>.Empty();
        if (!TryExpectSymbol(Symbol.Semicolon))
        {
            value = ParseExpression();
            ExpectSymbol(Symbol.Semicolon);
        }

        return new ReturnNode(value);
    }

    private VariableAssignmentNode ParseVariableAssignment()
    {
        var name = ExpectIdentifier().Value;
        ExpectSymbol(Symbol.Assign);
        var value = ParseExpression();
        ExpectSymbol(Symbol.Semicolon);
        
        return new VariableAssignmentNode(name, value);
    }

    private IfNode ParseIf()
    {
        var condition = ParseExpression();
        var body = ParseBlock();

        var elseStatement = Optional<Variant<IfNode, BlockNode>>.Empty();
        if (TryExpectSymbol(Symbol.Else))
        {
            elseStatement = TryExpectSymbol(Symbol.If)
                ? (Variant<IfNode, BlockNode>)ParseIf()
                : (Variant<IfNode, BlockNode>)ParseBlock();
        }
                        
        return new IfNode(condition, body, elseStatement);
    }

    private ExpressionNode ParseExpression(int precedence = 0)
    {
        var left = ParsePrimaryExpression();

        while (true)
        {
            var token = Peek();
            if (!token.HasValue || token.Value is not SymbolToken symbolToken || !TryGetBinaryOperator(symbolToken.Symbol, out var op) || GetBinaryOperatorPrecedence(op.Value) < precedence)
                break;
            
            Next();
            var right = ParseExpression(GetBinaryOperatorPrecedence(op.Value) + 1);

            left = new BinaryExpressionNode(left, op.Value, right);
        }
        
        return left;
    }

    private static int GetBinaryOperatorPrecedence(BinaryExpressionOperator binaryExpressionOperator)
    {
        return binaryExpressionOperator switch
        {
            BinaryExpressionOperator.Multiply => 3,
            BinaryExpressionOperator.Divide => 3,
            BinaryExpressionOperator.Plus => 2,
            BinaryExpressionOperator.Minus => 2,
            BinaryExpressionOperator.GreaterThan => 1,
            BinaryExpressionOperator.GreaterThanOrEqual => 1,
            BinaryExpressionOperator.LessThan => 1,
            BinaryExpressionOperator.LessThanOrEqual => 1,
            BinaryExpressionOperator.Equal => 0,
            BinaryExpressionOperator.NotEqual => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(binaryExpressionOperator), binaryExpressionOperator, null)
        };
    }

    private static bool TryGetBinaryOperator(Symbol symbol, [NotNullWhen(true)] out BinaryExpressionOperator? binaryExpressionOperator)
    {
        switch (symbol)
        {
            case Symbol.Equal:
                binaryExpressionOperator = BinaryExpressionOperator.Equal;
                return true;
            case Symbol.NotEqual:
                binaryExpressionOperator = BinaryExpressionOperator.NotEqual;
                return true;
            case Symbol.LessThan:
                binaryExpressionOperator = BinaryExpressionOperator.LessThan;
                return true;
            case Symbol.LessThanOrEqual:
                binaryExpressionOperator = BinaryExpressionOperator.LessThanOrEqual;
                return true;
            case Symbol.GreaterThan:
                binaryExpressionOperator = BinaryExpressionOperator.GreaterThan;
                return true;
            case Symbol.GreaterThanOrEqual:
                binaryExpressionOperator = BinaryExpressionOperator.GreaterThanOrEqual;
                return true;
            case Symbol.Plus:
                binaryExpressionOperator = BinaryExpressionOperator.Plus;
                return true;
            case Symbol.Minus:
                binaryExpressionOperator = BinaryExpressionOperator.Minus;
                return true;
            case Symbol.Star:
                binaryExpressionOperator = BinaryExpressionOperator.Multiply;
                return true;
            case Symbol.ForwardSlash:
                binaryExpressionOperator = BinaryExpressionOperator.Divide;
                return true;
            default:
                binaryExpressionOperator = null;
                return false;
        }
    }

    private ExpressionNode ParsePrimaryExpression()
    {
        var token = ExpectToken();
        switch (token)
        {
            case LiteralToken literal:
                return new LiteralNode(literal.Value, literal.Type);
            case IdentifierToken identifier:
                return ParseExpressionIdentifier(identifier);
            case SymbolToken { Symbol: Symbol.OpenParen }:
                var expression = ParseExpression();
                ExpectSymbol(Symbol.CloseParen);
                return expression;
            default: 
                throw new Exception($"Unexpected token type {token.GetType().Name}");
        }
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