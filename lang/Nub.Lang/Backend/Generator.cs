using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend;

public class Generator
{
    private readonly List<DefinitionNode> _definitions;
    private readonly StringBuilder _builder = new();
    private readonly SymbolTable _symbolTable;
    private readonly Dictionary<string, string> _variables = new();
    
    public Generator(List<DefinitionNode> definitions)
    {
        _definitions = definitions;
        _symbolTable = SymbolTable.Create(definitions);
    }

    public string Generate()
    {
        for (var i = 0; i < _symbolTable.Strings.Count; i++)
        {
            var str = _symbolTable.Strings[i];
            _builder.AppendLine($"data $str{i} = {{ b \"{str}\", b 0 }}");
        }

        foreach (var funcDefinition in _definitions.OfType<LocalFuncDefinitionNode>())
        {
            GenerateFuncDefinition(funcDefinition);
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
        _builder.Append("export function ");
        if (node.ReturnType.HasValue)
        {
            _builder.Append($"{QbeTypeName(node.ReturnType.Value)} ");
        }

        _builder.Append('$');
        _builder.Append(node.Name);
        
        _builder.AppendLine($"({string.Join(", ", parameters)}) {{");
        _builder.AppendLine("@start");
        GenerateBlock(node.Body, _symbolTable.ResolveLocalFunc(node.Name, node.Parameters.Select(x => x.Type).ToList()));
        if (!node.ReturnType.HasValue)
        {
            _builder.AppendLine("    ret");
        }
        _builder.AppendLine("}");
    }
    
    private void GenerateBlock(BlockNode block, LocalFuncDef func)
    {
        foreach (var statement in block.Statements)
        {
            GenerateStatement(statement, func);
        }
    }

    private void GenerateStatement(StatementNode statement, LocalFuncDef func)
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
                GenerateStatementFuncCall(funcCallStatement, func);
                break;
            case IfNode ifStatement:
                GenerateIf(ifStatement, func);
                break;
            case ReturnNode @return:
                GenerateReturn(@return, func);
                break;
            case VariableAssignmentNode variableAssignment:
                GenerateVariableAssignment(variableAssignment, func);
                break;
            case VariableReassignmentNode variableReassignment:
                GenerateVariableReassignment(variableReassignment, func);
                break;
            case WhileNode whileStatement:
                GenerateWhile(whileStatement, func);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void GenerateBreak()
    {
    }
    
    private void GenerateContinue()
    {
    }
    
    private void GenerateStatementFuncCall(FuncCallStatementNode funcCall, LocalFuncDef func)
    {
        var results = new List<(string, NubType)>();
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            results.Add((GenerateExpression(parameter, func), parameter.Type));
        }

        var parameters = results.Select(p => $"{QbeTypeName(p.Item2)} {p.Item1}");
        
        _builder.AppendLine($"    call ${funcCall.FuncCall.Name}({string.Join(", ", parameters)})");
    }

    private void GenerateIf(IfNode ifStatement, LocalFuncDef func)
    {
    }

    private void GenerateReturn(ReturnNode @return, LocalFuncDef func)
    {
        if (@return.Value.HasValue)
        {
            var result = GenerateExpression(@return.Value.Value, func);
            _builder.AppendLine($"    ret {result}");
        }
        else
        {
            _builder.AppendLine("    ret");
        }
    }

    private void GenerateVariableAssignment(VariableAssignmentNode variableAssignment, LocalFuncDef func)
    {
        _variables[variableAssignment.Name] = GenerateExpression(variableAssignment.Value, func);
    }

    private void GenerateVariableReassignment(VariableReassignmentNode variableReassignment, LocalFuncDef func)
    {
        _variables[variableReassignment.Name] = GenerateExpression(variableReassignment.Value, func);
    }

    private void GenerateWhile(WhileNode whileStatement, LocalFuncDef func)
    {
    }

    private string GenerateExpression(ExpressionNode expression, LocalFuncDef func)
    {
        switch (expression)
        {
            case BinaryExpressionNode binaryExpression:
                return GenerateBinaryExpression(binaryExpression, func);
            case FuncCallExpressionNode funcCallExpression:
                return GenerateExpressionFuncCall(funcCallExpression, func);
            case IdentifierNode identifier:
                return GenerateIdentifier(identifier, func);
            case LiteralNode literal:
                return GenerateLiteral(literal, func);
            case StructInitializerNode structInitializer:
                return GenerateStructInitializer(structInitializer, func);
            case StructMemberAccessorNode structMemberAccessor:
                return GenerateStructMemberAccessor(structMemberAccessor, func);
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private string GenerateStructMemberAccessor(StructMemberAccessorNode structMemberAccessor, LocalFuncDef func)
    {
        throw new NotImplementedException();
    }

    private string GenerateBinaryExpression(BinaryExpressionNode binaryExpression, LocalFuncDef func)
    {
        throw new NotImplementedException();
    }

    private string GenerateIdentifier(IdentifierNode identifier, LocalFuncDef func)
    {
        return _variables[identifier.Identifier];
    }

    private string GenerateLiteral(LiteralNode literal, LocalFuncDef func)
    {
        if (literal.LiteralType.Equals(NubType.String))
        {
            return $"$str{_symbolTable.ResolveString(literal.Literal)}";
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private string GenerateStructInitializer(StructInitializerNode structInitializer, LocalFuncDef func)
    {
        throw new NotImplementedException();
    }

    private string GenerateExpressionFuncCall(FuncCallExpressionNode funcCall, LocalFuncDef func)
    {
        var results = new List<(string, NubType)>();
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            results.Add((GenerateExpression(parameter, func), parameter.Type));
        }

        var parameters = results.Select(p => $"{QbeTypeName(p.Item2)} {p.Item1}");
        
        var output = GenName();
        _builder.AppendLine($"    %{output} ={QbeTypeName(funcCall.Type)} call ${funcCall.FuncCall.Name}({string.Join(", ", parameters)})");
        
        return $"%{output}";
    }

    private int _nameIndex;
    private string GenName() => "v" + ++_nameIndex;
}