using System.Diagnostics.CodeAnalysis;
using Nub.Lang.Frontend.Lexing;

namespace Nub.Lang.Frontend.Parsing;

public class Parser
{
    private List<Token> _tokens = [];
    private int _index;

    public ModuleNode ParseModule(List<Token> tokens, string path)
    {
        _index = 0;
        _tokens = tokens;

        List<DefinitionNode> definitions = [];
        List<string> imports = [];

        while (Peek().HasValue)
        {
            if (TryExpectSymbol(Symbol.Import))
            {
                var name = ExpectIdentifier();
                imports.Add(name.Value);
            }
            else
            {
                definitions.Add(ParseDefinition());
            }
        }

        return new ModuleNode(path, imports, definitions);
    }

    private DefinitionNode ParseDefinition()
    {
        List<Modifier> modifiers = [];
        
        while (TryExpectModifier(out var modifier))
        {
            modifiers.Add(modifier);
        }
        
        var keyword = ExpectSymbol();
        return keyword.Symbol switch
        {
            Symbol.Func => ParseFuncDefinition(modifiers),
            Symbol.Struct => ParseStruct(modifiers),
            _ => throw new Exception("Unexpected symbol: " + keyword.Symbol)
        };
    }

    private DefinitionNode ParseFuncDefinition(List<Modifier> modifiers)
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

        var returnType = Optional<NubType>.Empty();
        if (TryExpectSymbol(Symbol.Colon))
        {
            returnType = ParseType();
        }

        if (modifiers.Remove(Modifier.Extern))
        {
            if (modifiers.Count != 0)
            {
                throw new Exception($"Modifiers: {string.Join(", ", modifiers)} is not valid for an extern function");
            }
            return new ExternFuncDefinitionNode(name.Value, parameters, returnType);
        }
        
        var body = ParseBlock();
        var global = modifiers.Remove(Modifier.Global);
        
        if (modifiers.Count != 0)
        {
            throw new Exception($"Modifiers: {string.Join(", ", modifiers)} is not valid for a local function");
        }
        return new LocalFuncDefinitionNode(name.Value, parameters, body, returnType, global);
    }

    private StructDefinitionNode ParseStruct(List<Modifier> modifiers)
    {
        var name = ExpectIdentifier().Value;

        ExpectSymbol(Symbol.OpenBrace);

        List<StructField> variables = [];

        while (!TryExpectSymbol(Symbol.CloseBrace))
        {
            var variableName = ExpectIdentifier().Value;
            ExpectSymbol(Symbol.Colon);
            var variableType = ParseType();

            var variableValue = Optional<ExpressionNode>.Empty();

            if (TryExpectSymbol(Symbol.Assign))
            {
                variableValue = ParseExpression();
            }

            variables.Add(new StructField(variableName, variableType, variableValue));
        }

        return new StructDefinitionNode(name, variables);
    }

    private FuncParameter ParseFuncParameter()
    {
        var variadic = false;
        if (TryExpectSymbol(Symbol.Period))
        {
            ExpectSymbol(Symbol.Period);
            ExpectSymbol(Symbol.Period);
            variadic = true;
        }

        var name = ExpectIdentifier();
        ExpectSymbol(Symbol.Colon);
        var type = ParseType();

        return new FuncParameter(name.Value, type, variadic);
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

                        return new FuncCallStatementNode(new FuncCall(identifier.Value, parameters));
                    }
                    case Symbol.Assign:
                    {
                        var value = ParseExpression();
                        return new VariableAssignmentNode(identifier.Value, value);
                    }
                    default:
                    {
                        throw new Exception($"Unexpected symbol {symbol.Symbol}");
                    }
                }
            }
            case SymbolToken symbol:
            {
                return symbol.Symbol switch
                {
                    Symbol.Return => ParseReturn(),
                    Symbol.If => ParseIf(),
                    Symbol.While => ParseWhile(),
                    Symbol.Break => new BreakNode(),
                    Symbol.Continue => new ContinueNode(),
                    _ => throw new Exception($"Unexpected symbol {symbol.Symbol}")
                };
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
        }

        return new ReturnNode(value);
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

    private WhileNode ParseWhile()
    {
        var condition = ParseExpression();
        var body = ParseBlock();
        return new WhileNode(condition, body);
    }

    private ExpressionNode ParseExpression(int precedence = 0)
    {
        var left = ParsePrimaryExpression();

        while (true)
        {
            var token = Peek();
            if (!token.HasValue || token.Value is not SymbolToken symbolToken || !TryGetBinaryOperator(symbolToken.Symbol, out var op) ||
                GetBinaryOperatorPrecedence(op.Value) < precedence)
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
            {
                return new LiteralNode(literal.Value, literal.Type);
            }
            case IdentifierToken identifier:
            {
                return ParseExpressionIdentifier(identifier);
            }
            case SymbolToken symbolToken:
            {
                switch (symbolToken.Symbol)
                {
                    case Symbol.OpenParen:
                    {
                        var expression = ParseExpression();
                        ExpectSymbol(Symbol.CloseParen);
                        return expression;
                    }
                    case Symbol.New:
                    {
                        var type = ParseType();
                        Dictionary<string, ExpressionNode> initializers = [];
                        ExpectSymbol(Symbol.OpenBrace);
                        while (!TryExpectSymbol(Symbol.CloseBrace))
                        {
                            var name = ExpectIdentifier().Value;
                            ExpectSymbol(Symbol.Assign);
                            var value = ParseExpression();
                            initializers.Add(name, value);
                        }

                        return new StructInitializerNode(type, initializers);
                    }
                    default:
                    {
                        throw new Exception($"Unknown symbol: {symbolToken.Symbol}");
                    }
                }
            }
            default:
                throw new Exception($"Unexpected token type {token.GetType().Name}");
        }
    }

    private ExpressionNode ParseExpressionIdentifier(IdentifierToken identifier)
    {
        var token = Peek();
        if (!token.HasValue)
        {
            return new IdentifierNode(identifier.Value);
        }

        switch (token.Value)
        {
            case SymbolToken symbolToken:
            {
                switch (symbolToken.Symbol)
                {
                    case Symbol.Period:
                    {
                        Next();
                        ExpressionNode result = new IdentifierNode(identifier.Value);

                        do
                        {
                            var field = ExpectIdentifier();
                            result = new StructFieldAccessorNode(result, field.Value);
                        } while (TryExpectSymbol(Symbol.Period));

                        return result;
                    }
                    case Symbol.OpenParen:
                    {
                        Next();
                        List<ExpressionNode> parameters = [];
                        while (!TryExpectSymbol(Symbol.CloseParen))
                        {
                            parameters.Add(ParseExpression());
                            TryExpectSymbol(Symbol.Comma);
                        }

                        return new FuncCallExpressionNode(new FuncCall(identifier.Value, parameters));
                    }
                }

                break;
            }
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

    private NubType ParseType()
    {
        var name = ExpectIdentifier().Value;
        return NubType.Parse(name);
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

    private bool TryExpectModifier(out Modifier modifier)
    {
        if (Peek() is { HasValue: true, Value: ModifierToken modifierToken })
        {
            modifier = modifierToken.Modifier;
            Next();
            return true;
        }
        
        modifier = default;
        return false;
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
        while (_index < _tokens.Count && _tokens.ElementAt(_index) is SymbolToken { Symbol: Symbol.Whitespace })
        {
            Next();
        }

        if (_index < _tokens.Count)
        {
            return _tokens.ElementAt(_index);
        }

        return Optional<Token>.Empty();
    }

    private void Next()
    {
        _index++;
    }
}