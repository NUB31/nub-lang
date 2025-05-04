using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend;

public class Generator
{
    private readonly List<DefinitionNode> _definitions;
    private readonly StringBuilder _builder = new();
    private readonly SymbolTable _symbolTable;
    
    public Generator(List<DefinitionNode> definitions)
    {
        _definitions = definitions;
        _symbolTable = SymbolTable.Create(definitions);
    }

    public string Generate()
    {
        foreach (var funcDefinition in _definitions.OfType<LocalFuncDefinitionNode>())
        {
            GenerateFuncDefinition(funcDefinition);
        }

        return _builder.ToString();
    }

    private string QbeTypeName(NubType type)
    {
        if (type.Equals(NubType.Int64))
        {
            return "l";
        }
        
        if (type.Equals(NubType.Int32))
        {
            return "w";
        }
        
        if (type.Equals(NubType.String))
        {
            return "l";
        }

        throw new Exception($"Invalid qbe type {type}");
    }

    private void GenerateFuncDefinition(LocalFuncDefinitionNode node)
    {
        var parameters = node.Parameters.Select(p => $"{QbeTypeName(p.Type)} %{p.Name}");
        _builder.Append("function ");
        if (node.ReturnType.HasValue)
        {
            _builder.Append($"{QbeTypeName(node.ReturnType.Value)} ");
        }

        _builder.Append(node.Name);
        _builder.AppendLine($"({string.Join(", ", parameters)}) {{");
        _builder.AppendLine("@start");
        GenerateBlock(node.Body, _symbolTable.ResolveLocalFunc(node.Name, node.Parameters.Select(x => x.Type).ToList()));
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
                GenerateFuncCall(funcCallStatement.FuncCall, func);
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

    private void GenerateIf(IfNode ifStatement, LocalFuncDef func)
    {
    }

    private void GenerateReturn(ReturnNode @return, LocalFuncDef func)
    {
    }

    private void GenerateVariableAssignment(VariableAssignmentNode variableAssignment, LocalFuncDef func)
    {
    }

    private void GenerateVariableReassignment(VariableReassignmentNode variableReassignment, LocalFuncDef func)
    {
    }

    private void GenerateWhile(WhileNode whileStatement, LocalFuncDef func)
    {
    }

    private void GenerateExpression(ExpressionNode expression, LocalFuncDef func)
    {
        switch (expression)
        {
            case BinaryExpressionNode binaryExpression:
                GenerateBinaryExpression(binaryExpression, func);
                break;
            case FuncCallExpressionNode funcCallExpression:
                GenerateFuncCall(funcCallExpression.FuncCall, func);
                break;
            case IdentifierNode identifier:
                GenerateIdentifier(identifier, func);
                break;
            case LiteralNode literal:
                GenerateLiteral(literal, func);
                break;
            case StructInitializerNode structInitializer:
                GenerateStructInitializer(structInitializer, func);
                break;
            case StructMemberAccessorNode structMemberAccessor:
                GenerateStructMemberAccessor(structMemberAccessor, func);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private void GenerateStructMemberAccessor(StructMemberAccessorNode structMemberAccessor, LocalFuncDef func)
    {
    }

    private void GenerateBinaryExpression(BinaryExpressionNode binaryExpression, LocalFuncDef func)
    {
    }

    private void GenerateIdentifier(IdentifierNode identifier, LocalFuncDef func)
    {
    }

    private void GenerateLiteral(LiteralNode literal, LocalFuncDef func)
    {
    }

    private void GenerateStructInitializer(StructInitializerNode structInitializer, LocalFuncDef func)
    {
    }

    private void GenerateFuncCall(FuncCall funcCall, LocalFuncDef func)
    {
    }
}