using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend;

public class Generator
{
    private readonly List<DefinitionNode> _definitions;
    private readonly StringBuilder _builder = new();
    private readonly Dictionary<string, string> _variables = new();
    private readonly List<string> _strings = [];

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
        foreach (var statement in block.Statements)
        {
            GenerateStatement(statement);
        }
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
        throw new NotImplementedException();
    }

    private void GenerateContinue()
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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

        var output = GenName();
        _builder.AppendLine($"    %{output} ={QbeTypeName(funcCall.Type)} call ${funcCall.FuncCall.Name}({string.Join(", ", parameters)})");

        return $"%{output}";
    }

    private int _nameIndex;
    private string GenName() => "v" + ++_nameIndex;
}