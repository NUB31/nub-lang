using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend.Custom;

public class ExpressionGenerator
{
    private readonly StringBuilder _builder;
    private readonly SymbolTable _symbolTable;

    public ExpressionGenerator(SymbolTable symbolTable, StringBuilder builder)
    {
        _symbolTable = symbolTable;
        _builder = builder;
    }
    
    public void GenerateExpression(ExpressionNode expression, LocalFunc func)
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
                GenerateLiteral(literal);
                break;
            case SyscallExpressionNode syscallExpression:
                GenerateSyscall(syscallExpression.Syscall, func);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private void GenerateBinaryExpression(BinaryExpressionNode binaryExpression, LocalFunc func)
    {
        GenerateExpression(binaryExpression.Left, func);
        _builder.AppendLine("    push rax");
        GenerateExpression(binaryExpression.Right, func);
        _builder.AppendLine("    mov rbx, rax");
        _builder.AppendLine("    pop rax");

        switch (binaryExpression.Operator)
        {
            case BinaryExpressionOperator.Equal:
                GenerateComparison(binaryExpression.Left.Type);
                _builder.AppendLine("    sete al");
                _builder.AppendLine("    movzx rax, al");
                break;
            case BinaryExpressionOperator.NotEqual:
                GenerateComparison(binaryExpression.Left.Type);
                _builder.AppendLine("    setne al");
                _builder.AppendLine("    movzx rax, al");
                break;
            case BinaryExpressionOperator.GreaterThan:
                GenerateComparison(binaryExpression.Left.Type);
                _builder.AppendLine("    setg al");
                _builder.AppendLine("    movzx rax, al");
                break;
            case BinaryExpressionOperator.GreaterThanOrEqual:
                GenerateComparison(binaryExpression.Left.Type);
                _builder.AppendLine("    setge al");
                _builder.AppendLine("    movzx rax, al");
                break;
            case BinaryExpressionOperator.LessThan:
                GenerateComparison(binaryExpression.Left.Type);
                _builder.AppendLine("    setl al");
                _builder.AppendLine("    movzx rax, al");
                break;
            case BinaryExpressionOperator.LessThanOrEqual:
                GenerateComparison(binaryExpression.Left.Type);
                _builder.AppendLine("    setle al");
                _builder.AppendLine("    movzx rax, al");
                break;
            case BinaryExpressionOperator.Plus:
                GenerateBinaryAddition(binaryExpression.Left.Type);
                break;
            case BinaryExpressionOperator.Minus:
                GenerateBinarySubtraction(binaryExpression.Left.Type);
                break;
            case BinaryExpressionOperator.Multiply:
                GenerateBinaryMultiplication(binaryExpression.Left.Type);
                break;
            case BinaryExpressionOperator.Divide:
                GenerateBinaryDivision(binaryExpression.Left.Type);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void GenerateComparison(Type type)
    {
        switch (type)
        {
            case DelegateType:
                throw new NotSupportedException($"Comparison on type {type.GetType().Name} is not supported");
                break;
            case PrimitiveType:
                _builder.AppendLine("    cmp rax, rax");
                break;
            case StringType:
                _builder.AppendLine("    mov rdi, rax");
                _builder.AppendLine("    mov rsi, rbx");
                _builder.AppendLine("    call strcmp");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private void GenerateBinaryAddition(Type type)
    {
        if (type is not PrimitiveType primitiveType)
        {
            throw new InvalidOperationException("Addition can only be done on primitive types");
        }

        switch (primitiveType.Kind)
        {
            case PrimitiveTypeKind.Int64:
                _builder.AppendLine("    add rax, rbx");
                break;
            case PrimitiveTypeKind.Int32:
                _builder.AppendLine("    add eax, ebx");
                break;
            default:
                throw new InvalidOperationException($"Invalid type {primitiveType.Kind}");
        }
    }
    
    private void GenerateBinarySubtraction(Type type)
    {
        if (type is not PrimitiveType primitiveType)
        {
            throw new InvalidOperationException("Subtraction can only be done on primitive types");
        }

        switch (primitiveType.Kind)
        {
            case PrimitiveTypeKind.Int64:
                _builder.AppendLine("    sub rax, rbx");
                break;
            case PrimitiveTypeKind.Int32:
                _builder.AppendLine("    sub eax, ebx");
                break;
            default:
                throw new InvalidOperationException($"Invalid type {primitiveType.Kind}");
        }
    }

    private void GenerateBinaryMultiplication(Type type)
    {
        if (type is not PrimitiveType primitiveType)
        {
            throw new InvalidOperationException("Multiplication can only be done on primitive types");
        }

        switch (primitiveType.Kind)
        {
            case PrimitiveTypeKind.Int64:
                _builder.AppendLine("    imul rbx");
                break;
            case PrimitiveTypeKind.Int32:
                _builder.AppendLine("    imul ebx");
                break;
            default:
                throw new InvalidOperationException($"Invalid type {primitiveType.Kind}");
        }
    }

    private void GenerateBinaryDivision(Type type)
    {
        if (type is not PrimitiveType primitiveType)
        {
            throw new InvalidOperationException("Division can only be done on primitive types");
        }

        switch (primitiveType.Kind)
        {
            case PrimitiveTypeKind.Int64:
                _builder.AppendLine("    cqo");
                _builder.AppendLine("    idiv rbx");
                break;
            case PrimitiveTypeKind.Int32:
                _builder.AppendLine("    cdq");
                _builder.AppendLine("    idiv ebx");
                break;
            default:
                throw new InvalidOperationException($"Invalid type {primitiveType.Kind}");
        }
    }

    private void GenerateIdentifier(IdentifierNode identifier, LocalFunc func)
    {
        var variable = func.ResolveVariable(identifier.Identifier);

        switch (variable)
        {
            case GlobalVariable globalVariable:
                _builder.AppendLine($"    mov rax, [{globalVariable.Identifier}]");
                break;
            case LocalVariable localVariable:
            {
                _builder.AppendLine($"    mov rax, [rbp - {localVariable.Offset}]");
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(variable));
            }
        }
    }

    private void GenerateLiteral(LiteralNode literal)
    {
        switch (literal.Type)
        {
            case DelegateType:
            {
                throw new NotImplementedException();
                break;
            }
            case StringType:
            {
                var ident = _symbolTable.LabelFactory.Create();
                _symbolTable.DefineString(ident, literal.Literal);
                _builder.AppendLine($"    mov rax, {ident}");
                break;
            }
            case PrimitiveType primitive:
            {
                switch (primitive.Kind)
                {
                    case PrimitiveTypeKind.Bool:
                        _builder.AppendLine($"    mov rax, {(bool.Parse(literal.Literal) ? "1" : "0")}");
                        break;
                    case PrimitiveTypeKind.Char:
                        _builder.AppendLine($"    mov rax, '{literal.Literal}'");
                        break;
                    case PrimitiveTypeKind.Int64:
                        _builder.AppendLine($"    mov rax, {literal.Literal}");
                        break;
                    case PrimitiveTypeKind.Int32:
                        _builder.AppendLine($"    mov rax, {literal.Literal}");
                        break;
                    default:
                        throw new Exception("Cannot convert literal to string");
                }
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void GenerateFuncCall(FuncCall funcCall, LocalFunc func)
    {
        var symbol = _symbolTable.ResolveFunc(funcCall.Name, funcCall.Parameters.Select(p => p.Type).ToList());
        string[] registers = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"];

        for (var i = funcCall.Parameters.Count - 1; i >= 0; i--)
        {
            GenerateExpression(funcCall.Parameters.ElementAt(i), func);
            _builder.AppendLine("    push rax");
        }
        
        var registerParameters = Math.Min(registers.Length, funcCall.Parameters.Count);
        var stackParameters = funcCall.Parameters.Count - registerParameters;
        
        for (var i = 0; i < registerParameters; i++)
        {
            _builder.AppendLine($"    pop {registers[i]}");
        }

        _builder.AppendLine($"    call {symbol.StartLabel}");
        if (stackParameters != 0)
        {
            _builder.AppendLine($"    add rsp, {stackParameters}");
        }
    }

    public void GenerateSyscall(Syscall syscall, LocalFunc func)
    {
        string[] registers = ["rax", "rdi", "rsi", "rdx", "r10", "r8", "r9"];

        foreach (var parameter in syscall.Parameters)
        {
            GenerateExpression(parameter, func);
            _builder.AppendLine("    push rax");
        }
                
        for (var i = syscall.Parameters.Count - 1; i >= 0; i--)
        {
            _builder.AppendLine($"    pop {registers[i]}");
        }
                
        _builder.AppendLine("    syscall");
    }
}