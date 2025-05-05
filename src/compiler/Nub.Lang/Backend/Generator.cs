using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend;

public class Generator
{
    private readonly List<DefinitionNode> _definitions;
    private readonly StringBuilder _builder = new();
    private readonly Dictionary<string, int> _prefixIndexes = new();
    private readonly Dictionary<string, string> _variables = new();
    private readonly List<string> _strings = [];
    private readonly Stack<string> _breakLabels = new();
    private readonly Stack<string> _continueLabels = new();
    private bool _codeIsReachable = true;

    public Generator(List<DefinitionNode> definitions)
    {
        _definitions = definitions;
    }

    public string Generate()
    {
        foreach (var funcDefinition in _definitions.OfType<LocalFuncDefinitionNode>())
        {
            GenerateFuncDefinition(funcDefinition);
        }

        for (var i = 0; i < _strings.Count; i++)
        {
            var str = _strings[i];
            _builder.AppendLine($"data $str{i + 1} = {{ b \"{str}\", b 0 }}");
        }

        return _builder.ToString();
    }

    private string QbeTypeName(NubType type)
    {
        if (type.Equals(NubType.Int64) || type.Equals(NubType.String))
        {
            return "l";
        }

        if (type.Equals(NubType.Int32) || type.Equals(NubType.Bool))
        {
            return "w";
        }

        throw new Exception($"Invalid qbe type {type}");
    }

    private void GenerateFuncDefinition(LocalFuncDefinitionNode node)
    {
        _variables.Clear();
        var parameters = node.Parameters.Select(p => $"{QbeTypeName(p.Type)} %{p.Name}");
        if (node.Global)
        {
            _builder.Append("export ");
        }

        _builder.Append("function ");
        if (node.ReturnType.HasValue)
        {
            _builder.Append($"{QbeTypeName(node.ReturnType.Value)} ");
        }

        _builder.Append('$');
        _builder.Append(node.Name);

        _builder.AppendLine($"({string.Join(", ", parameters)}) {{");
        _builder.AppendLine("@start");
        GenerateBlock(node.Body);
        if (!node.ReturnType.HasValue)
        {
            _builder.AppendLine("    ret");
        }

        _builder.AppendLine("}");
    }

    private void GenerateBlock(BlockNode block)
    {
        foreach (var statement in block.Statements.Where(_ => _codeIsReachable))
        {
            GenerateStatement(statement);
        }

        _codeIsReachable = true;
    }

    private void GenerateStatement(StatementNode statement)
    {
        switch (statement)
        {
            case BreakNode:
                GenerateBreak();
                break;
            case ContinueNode:
                GenerateContinue();
                break;
            case FuncCallStatementNode funcCallStatement:
                GenerateStatementFuncCall(funcCallStatement);
                break;
            case IfNode ifStatement:
                GenerateIf(ifStatement);
                break;
            case ReturnNode @return:
                GenerateReturn(@return);
                break;
            case VariableAssignmentNode variableAssignment:
                GenerateVariableAssignment(variableAssignment);
                break;
            case VariableReassignmentNode variableReassignment:
                GenerateVariableReassignment(variableReassignment);
                break;
            case WhileNode whileStatement:
                GenerateWhile(whileStatement);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void GenerateBreak()
    {
        _builder.AppendLine($"    jmp @{_breakLabels.Peek()}");
        _codeIsReachable = false;
    }

    private void GenerateContinue()
    {
        _builder.AppendLine($"    jmp @{_continueLabels.Peek()}");
        _codeIsReachable = false;
    }

    private void GenerateStatementFuncCall(FuncCallStatementNode funcCall)
    {
        var results = new List<(string, NubType)>();
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            results.Add((GenerateExpression(parameter), parameter.Type));
        }

        var parameters = results.Select(p => $"{QbeTypeName(p.Item2)} {p.Item1}");

        _builder.AppendLine($"    call ${funcCall.FuncCall.Name}({string.Join(", ", parameters)})");
    }

    private void GenerateIf(IfNode ifStatement)
    {
        var trueLabel = GenName("if_true");
        var endLabel = GenName("if_end");
        var falseLabel = GenName("if_false");

        var result = GenerateExpression(ifStatement.Condition);
        _builder.AppendLine($"    jnz {result}, @{trueLabel}, @{falseLabel}");
        _builder.AppendLine($"@{trueLabel}");
        GenerateBlock(ifStatement.Body);
        _builder.AppendLine($"    jmp @{endLabel}");
        _builder.AppendLine($"@{falseLabel}");
        if (ifStatement.Else.HasValue)
        {
            ifStatement.Else.Value.Match
            (
                GenerateIf,
                GenerateBlock
            );
        }
        _builder.AppendLine($"@{endLabel}");
    }

    private void GenerateReturn(ReturnNode @return)
    {
        if (@return.Value.HasValue)
        {
            var result = GenerateExpression(@return.Value.Value);
            _builder.AppendLine($"    ret {result}");
        }
        else
        {
            _builder.AppendLine("    ret");
        }
    }

    private void GenerateVariableAssignment(VariableAssignmentNode variableAssignment)
    {
        _variables[variableAssignment.Name] = GenerateExpression(variableAssignment.Value);
    }

    private void GenerateVariableReassignment(VariableReassignmentNode variableReassignment)
    {
        _variables[variableReassignment.Name] = GenerateExpression(variableReassignment.Value);
    }

    private void GenerateWhile(WhileNode whileStatement)
    {
        var conditionLabel = GenName("loop_condition");
        var iterationLabel = GenName("loop_iteration");
        var endLabel = GenName("loop_end");
        
        _breakLabels.Push(endLabel);
        _continueLabels.Push(conditionLabel);

        _builder.AppendLine($"    jmp @{conditionLabel}");
        _builder.AppendLine($"@{iterationLabel}");
        GenerateBlock(whileStatement.Body);
        _builder.AppendLine($"@{conditionLabel}");
        var result = GenerateExpression(whileStatement.Condition);
        _builder.AppendLine($"    jnz {result}, @{iterationLabel}, @{endLabel}");
        _builder.AppendLine($"@{endLabel}");
        
        _continueLabels.Pop();
        _breakLabels.Pop();
    }

    private string GenerateExpression(ExpressionNode expression)
    {
        switch (expression)
        {
            case BinaryExpressionNode binaryExpression:
                return GenerateBinaryExpression(binaryExpression);
            case FuncCallExpressionNode funcCallExpression:
                return GenerateExpressionFuncCall(funcCallExpression);
            case IdentifierNode identifier:
                return GenerateIdentifier(identifier);
            case LiteralNode literal:
                return GenerateLiteral(literal);
            case StructInitializerNode structInitializer:
                return GenerateStructInitializer(structInitializer);
            case StructMemberAccessorNode structMemberAccessor:
                return GenerateStructMemberAccessor(structMemberAccessor);
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private string GenerateStructMemberAccessor(StructMemberAccessorNode structMemberAccessor)
    {
        throw new NotImplementedException();
    }

    private string GenerateBinaryExpression(BinaryExpressionNode binaryExpression)
    {
        throw new NotImplementedException();
    }

    private string GenerateIdentifier(IdentifierNode identifier)
    {
        return _variables[identifier.Identifier];
    }

    private string GenerateLiteral(LiteralNode literal)
    {
        if (literal.LiteralType.Equals(NubType.String))
        {
            _strings.Add(literal.Literal);
            return $"$str{_strings.Count}";
        }

        if (literal.LiteralType.Equals(NubType.Int64) || literal.LiteralType.Equals(NubType.Int32))
        {
            return literal.Literal;
        }

        if (literal.LiteralType.Equals(NubType.Bool))
        {
            return bool.Parse(literal.Literal) ? "1" : "0";
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private string GenerateStructInitializer(StructInitializerNode structInitializer)
    {
        throw new NotImplementedException();
    }

    private string GenerateExpressionFuncCall(FuncCallExpressionNode funcCall)
    {
        var results = new List<(string, NubType)>();
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            results.Add((GenerateExpression(parameter), parameter.Type));
        }

        var parameters = results.Select(p => $"{QbeTypeName(p.Item2)} {p.Item1}");

        var output = GenName("var");
        _builder.AppendLine($"    %{output} ={QbeTypeName(funcCall.Type)} call ${funcCall.FuncCall.Name}({string.Join(", ", parameters)})");

        return $"%{output}";
    }

    private string GenName(string prefix)
    {
        var index = _prefixIndexes.GetValueOrDefault(prefix, 0);
        _prefixIndexes[prefix] = index + 1;
        return $"{prefix}_{index}";
    }
}