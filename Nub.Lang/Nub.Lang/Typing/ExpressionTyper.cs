using Nub.Lang.Parsing;

namespace Nub.Lang.Typing;

public class ExpressionTyper
{
    private readonly IReadOnlyCollection<FuncDefinitionNode> _functions;
    private readonly IReadOnlyCollection<GlobalVariableDefinitionNode> _variableDefinitions;
    private readonly Stack<Variable> _variables;

    public ExpressionTyper(IReadOnlyCollection<DefinitionNode> definitions)
    {
        _functions = definitions.OfType<FuncDefinitionNode>().ToList();
        _variableDefinitions = definitions.OfType<GlobalVariableDefinitionNode>().ToList();
        _variables = new Stack<Variable>();
    }

    public void Populate()
    {
        _variables.Clear();
        
        foreach (var variable in _variableDefinitions)
        {
            PopulateExpression(variable.Value);
            _variables.Push(new Variable(variable.Name, variable.Value.Type));
        }
        
        foreach (var function in _functions)
        {
            foreach (var parameter in function.Parameters)
            {
                _variables.Push(new Variable(parameter.Name, parameter.Type));
            }
            PopulateBlock(function.Body);
            for (var i = 0; i < function.Parameters.Count(); i++)
            {
                _variables.Pop();
            }
        }
    }

    private void PopulateBlock(BlockNode block)
    {
        var variableCount = _variables.Count;
        foreach (var statement in block.Statements)
        {
            PopulateStatement(statement);
        }
        while (_variables.Count > variableCount)
        {
            _variables.Pop();
        }
    }

    private void PopulateStatement(StatementNode statement)
    {
        switch (statement)
        {
            case FuncCallStatementNode funcCall:
                PopulateFuncCallStatement(funcCall);
                break;
            case ReturnNode returnNode:
                PopulateReturn(returnNode);
                break;
            case SyscallStatementNode syscall:
                PopulateSyscallStatement(syscall);
                break;
            case VariableAssignmentNode variableAssignment:
                PopulateVariableAssignment(variableAssignment);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void PopulateFuncCallStatement(FuncCallStatementNode funcCall)
    {
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            PopulateExpression(parameter);
        }
    }

    private void PopulateSyscallStatement(SyscallStatementNode syscall)
    {
        foreach (var parameter in syscall.Syscall.Parameters)
        {
            PopulateExpression(parameter);
        }
    }

    private void PopulateReturn(ReturnNode returnNode)
    {
        if (returnNode.Value.HasValue)
        {
            PopulateExpression(returnNode.Value.Value);
        }
    }

    private void PopulateVariableAssignment(VariableAssignmentNode variableAssignment)
    {
        PopulateExpression(variableAssignment.Value);
    }

    private void PopulateExpression(ExpressionNode expression)
    {
        switch (expression)
        {
            case FuncCallExpressionNode funcCall:
                PopulateFuncCallExpression(funcCall);
                break;
            case IdentifierNode identifier:
                PopulateIdentifier(identifier);
                break;
            case LiteralNode literal:
                PopulateLiteral(literal);
                break;
            case StrlenNode strlen:
                PopulateStrlen(strlen);
                break;
            case SyscallExpressionNode syscall:
                PopulateSyscallExpression(syscall);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private void PopulateFuncCallExpression(FuncCallExpressionNode funcCall)
    {
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            PopulateExpression(parameter);
        }
                
        var function = _functions.FirstOrDefault(f => f.Name == funcCall.FuncCall.Name);
        if (function == null)
        {
            throw new Exception($"Func {funcCall} is not defined");
        }
        if (!function.ReturnType.HasValue)
        {
            throw new Exception($"Func {funcCall} must have a return type when used in an expression");
        }
        funcCall.Type = function.ReturnType.Value;
    }

    private void PopulateIdentifier(IdentifierNode identifier)
    {
        var type = _variables.FirstOrDefault(v => v.Name == identifier.Identifier)?.Type;
        if (type == null)
        {
            throw new Exception($"Identifier {identifier} is not defined");
        }
        identifier.Type = type;
    }

    private static void PopulateLiteral(LiteralNode literal)
    {
        literal.Type = literal.LiteralType;
    }

    private static void PopulateStrlen(StrlenNode strlen)
    {
        strlen.Type = new PrimitiveType(PrimitiveTypeKind.Int64);
    }

    private void PopulateSyscallExpression(SyscallExpressionNode syscall)
    {
        foreach (var parameter in syscall.Syscall.Parameters)
        {
            PopulateExpression(parameter);
        }

        syscall.Type = new PrimitiveType(PrimitiveTypeKind.Int64);
    }

    private class Variable(string name, Type type)
    {
        public string Name { get; } = name;
        public Type Type { get; } = type;
    }
}