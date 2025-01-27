using Nub.Core;
using Nub.Lang.Parsing;

namespace Nub.Lang.Typing;

public class Func(string name, IReadOnlyCollection<FuncParameter> parameters, Optional<BlockNode> body, Optional<Type> returnType)
{
    public string Name { get; } = name;
    public IReadOnlyCollection<FuncParameter> Parameters { get; } = parameters;
    public Optional<BlockNode> Body { get; } = body;
    public Optional<Type> ReturnType { get; } = returnType;
}

public class ExpressionTyper
{
    private readonly IReadOnlyCollection<Func> _functions;
    private readonly IReadOnlyCollection<GlobalVariableDefinitionNode> _variableDefinitions;
    private readonly Stack<Variable> _variables;

    public ExpressionTyper(IReadOnlyCollection<DefinitionNode> definitions)
    {
        var functions = definitions
            .OfType<LocalFuncDefinitionNode>()
            .Select(f => new Func(f.Name, f.Parameters, f.Body, f.ReturnType))
            .ToList();
        
        var externFunctions = definitions
            .OfType<ExternFuncDefinitionNode>()
            .Select(f => new Func(f.Name, f.Parameters, Optional<BlockNode>.Empty(), f.ReturnType))
            .ToList();
        
        _functions = functions.Concat(externFunctions).ToList();
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

            if (function.Body.HasValue)
            {
                PopulateBlock(function.Body.Value);
            }
            for (var i = 0; i < function.Parameters.Count; i++)
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
            case VariableReassignmentNode variableReassignment:
                PopulateVariableReassignment(variableReassignment);
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
        _variables.Push(new Variable(variableAssignment.Name, variableAssignment.Value.Type));
    }

    private void PopulateVariableReassignment(VariableReassignmentNode variableReassignment)
    {
        PopulateExpression(variableReassignment.Value);
    }

    private void PopulateExpression(ExpressionNode expression)
    {
        switch (expression)
        {
            case BinaryExpressionNode binaryExpression:
                PopulateBinaryExpression(binaryExpression);
                break;
            case FuncCallExpressionNode funcCall:
                PopulateFuncCallExpression(funcCall);
                break;
            case IdentifierNode identifier:
                PopulateIdentifier(identifier);
                break;
            case LiteralNode literal:
                PopulateLiteral(literal);
                break;
            case SyscallExpressionNode syscall:
                PopulateSyscallExpression(syscall);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private void PopulateBinaryExpression(BinaryExpressionNode binaryExpression)
    {
        PopulateExpression(binaryExpression.Left);
        PopulateExpression(binaryExpression.Right);
        switch (binaryExpression.Operator)
        {
            case BinaryExpressionOperator.Equal:
            case BinaryExpressionOperator.NotEqual:
            case BinaryExpressionOperator.GreaterThan:
            case BinaryExpressionOperator.GreaterThanOrEqual:
            case BinaryExpressionOperator.LessThan:
            case BinaryExpressionOperator.LessThanOrEqual:
            {
                binaryExpression.Type = new PrimitiveType(PrimitiveTypeKind.Bool);
                break;
            }
            case BinaryExpressionOperator.Plus:
            case BinaryExpressionOperator.Minus:
            case BinaryExpressionOperator.Multiply:
            case BinaryExpressionOperator.Divide:
            {
                binaryExpression.Type = binaryExpression.Left.Type;
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(binaryExpression.Operator));
            }
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
            throw new Exception($"Variable {identifier} is not defined");
        }
        identifier.Type = type;
    }

    private static void PopulateLiteral(LiteralNode literal)
    {
        literal.Type = literal.LiteralType;
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