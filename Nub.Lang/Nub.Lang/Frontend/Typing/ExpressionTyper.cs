using Nub.Core;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Frontend.Typing;

public class Func(string name, List<FuncParameter> parameters, Optional<BlockNode> body, Optional<Type> returnType)
{
    public string Name { get; } = name;
    public List<FuncParameter> Parameters { get; } = parameters;
    public Optional<BlockNode> Body { get; } = body;
    public Optional<Type> ReturnType { get; } = returnType;
}

public class ExpressionTyper
{
    private readonly List<Func> _functions;
    private readonly List<GlobalVariableDefinitionNode> _variableDefinitions;
    private readonly List<StructDefinitionNode> _classes;
    private readonly Stack<Variable> _variables;

    public ExpressionTyper(List<DefinitionNode> definitions)
    {
        _variables = new Stack<Variable>();
        _functions = [];
        _variableDefinitions = [];
        
        _classes = definitions.OfType<StructDefinitionNode>().ToList();
        
        var functions = definitions
            .OfType<LocalFuncDefinitionNode>()
            .Select(f => new Func(f.Name, f.Parameters, f.Body, f.ReturnType))
            .ToList();
        
        var externFunctions = definitions
            .OfType<ExternFuncDefinitionNode>()
            .Select(f => new Func(f.Name, f.Parameters, Optional<BlockNode>.Empty(), f.ReturnType))
            .ToList();
        
        _functions.AddRange(functions);
        _functions.AddRange(externFunctions);
        _variableDefinitions.AddRange(definitions.OfType<GlobalVariableDefinitionNode>());
    }

    public void Populate()
    {
        _variables.Clear();
        
        foreach (var @class in _classes)
        {
            foreach (var variable in @class.Members)
            {
                if (variable.Value.HasValue)
                {
                    PopulateExpression(variable.Value.Value);
                }
            }
        }
        
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
            case ArrayIndexAssignmentNode arrayIndexAssignment:
                PopulateArrayIndexAssignment(arrayIndexAssignment);
                break;
            case BreakNode:
            case ContinueNode:
                break;
            case FuncCallStatementNode funcCall:
                PopulateFuncCallStatement(funcCall);
                break;
            case IfNode ifStatement:
                PopulateIf(ifStatement);
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
            case WhileNode whileStatement:
                PopulateWhileStatement(whileStatement);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void PopulateArrayIndexAssignment(ArrayIndexAssignmentNode arrayIndexAssignment)
    {
        PopulateIdentifier(arrayIndexAssignment.Identifier);
        PopulateExpression(arrayIndexAssignment.Index);
        PopulateExpression(arrayIndexAssignment.Value);
    }

    private void PopulateFuncCallStatement(FuncCallStatementNode funcCall)
    {
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            PopulateExpression(parameter);
        }
    }

    private void PopulateIf(IfNode ifStatement)
    {
        PopulateExpression(ifStatement.Condition);
        PopulateBlock(ifStatement.Body);
        if (ifStatement.Else.HasValue)
        {
            ifStatement.Else.Value.Match
            (
                PopulateIf,
                PopulateBlock
            );
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

    private void PopulateWhileStatement(WhileNode whileStatement)
    {
        PopulateExpression(whileStatement.Condition);
        PopulateBlock(whileStatement.Body);
    }

    private void PopulateExpression(ExpressionNode expression)
    {
        switch (expression)
        {
            case ArrayIndexAccessNode arrayIndexAccess:
                PopulateArrayIndexAccess(arrayIndexAccess);
                break;
            case ArrayInitializerNode arrayInitializer:
                PopulateArrayInitializer(arrayInitializer);
                break;
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
            case StructInitializerNode structInitializer:
                PopulateStructInitializer(structInitializer);
                break;
            case SyscallExpressionNode syscall:
                PopulateSyscallExpression(syscall);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private void PopulateArrayIndexAccess(ArrayIndexAccessNode arrayIndexAccess)
    {
        PopulateExpression(arrayIndexAccess.Index);
        PopulateIdentifier(arrayIndexAccess.Identifier);
        
        var variable = _variables.FirstOrDefault(v => v.Name == arrayIndexAccess.Identifier.Identifier);
        if (variable == null)
        {
            throw new Exception($"Variable {arrayIndexAccess.Identifier} is not defined");
        }

        if (variable.Type is not ArrayType arrayType)
        {
            throw new Exception($"Variable {arrayIndexAccess.Identifier} is not an array type");
        }

        arrayIndexAccess.Type = arrayType.InnerType;
    }

    private void PopulateArrayInitializer(ArrayInitializerNode arrayInitializer)
    {
        arrayInitializer.Type = arrayInitializer.InnerType;
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

    private void PopulateStructInitializer(StructInitializerNode structInitializer)
    {
        foreach (var initializer in structInitializer.Initializers)
        {
            PopulateExpression(initializer.Value);
        }

        structInitializer.Type = structInitializer.StructType;
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