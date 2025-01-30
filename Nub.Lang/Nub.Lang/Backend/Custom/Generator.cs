using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend.Custom;

public class Generator
{
    private const string Entrypoint = "main";
    
    private readonly List<DefinitionNode> _definitions;
    private readonly SymbolTable _symbolTable;
    private readonly StringBuilder _builder;
    private readonly LabelFactory _labelFactory;
    
    public Generator(List<DefinitionNode> definitions)
    {
        _definitions = [];
        _builder = new StringBuilder();
        _labelFactory = new LabelFactory();
        _symbolTable = new SymbolTable(_labelFactory);

        foreach (var globalVariableDefinition in definitions.OfType<GlobalVariableDefinitionNode>())
        {
            _symbolTable.DefineGlobalVariable(globalVariableDefinition);
            _definitions.Add(globalVariableDefinition);
        }
        
        foreach (var funcDefinitionNode in definitions.OfType<ExternFuncDefinitionNode>())
        {
            _symbolTable.DefineFunc(funcDefinitionNode);
            _definitions.Add(funcDefinitionNode);
        }
    
        foreach (var funcDefinitionNode in definitions.OfType<LocalFuncDefinitionNode>())
        {
            _symbolTable.DefineFunc(funcDefinitionNode);
            _definitions.Add(funcDefinitionNode);
        }
    }

    public string Generate()
    {
        _builder.AppendLine("global _start");
        
        foreach (var externFuncDefinition in _definitions.OfType<ExternFuncDefinitionNode>())
        {
            _builder.AppendLine($"extern {externFuncDefinition.Name}");
        }

        _builder.AppendLine();
        _builder.AppendLine("section .text");
        
        // TODO: Only add start label if main is present
        var main = _symbolTable.ResolveLocalFunc(Entrypoint, []);
        
        _builder.AppendLine("_start:");
        _builder.AppendLine($"    call {main.StartLabel}");

        _builder.AppendLine(main.ReturnType.HasValue
            ? "    mov rdi, rax"
            : "    mov rdi, 0");
        _builder.AppendLine("    mov rax, 60");
        _builder.AppendLine("    syscall");

        foreach (var funcDefinition in _definitions.OfType<LocalFuncDefinitionNode>())
        {
            _builder.AppendLine();
            GenerateFuncDefinition(funcDefinition);
        }

        _builder.AppendLine("""
                            
                            strcmp:
                                xor rdx, rdx
                            .loop:
                                mov al, [rsi + rdx]
                                mov bl, [rdi + rdx]
                                inc rdx
                                cmp al, bl
                                jne .not_equal
                                cmp al, 0
                                je .equal
                                jmp .loop
                            .not_equal:
                                mov rax, 0
                                ret
                            .equal:
                                mov rax, 1
                                ret
                            """);
        
        _builder.AppendLine();
        _builder.AppendLine("section .data");
        
        foreach (var str in _symbolTable.Strings)
        {
            _builder.AppendLine($"{str.Key}: db `{str.Value}`, 0");
        }


        Dictionary<string, string> completed = [];
        foreach (var globalVariableDefinition in _definitions.OfType<GlobalVariableDefinitionNode>())
        {
            var variable = _symbolTable.ResolveGlobalVariable(globalVariableDefinition.Name);
            var evaluated = EvaluateExpression(globalVariableDefinition.Value, completed);
            _builder.AppendLine($"{variable.Identifier}: dq {evaluated}");
            completed[variable.Name] = evaluated;
        }
        
        return _builder.ToString();
    }

    private string EvaluateExpression(ExpressionNode expression, Dictionary<string, string> completed)
    {
        switch (expression)
        {
            case BinaryExpressionNode binaryExpression:
            {
                var left = EvaluateExpression(binaryExpression.Left, completed);
                var right = EvaluateExpression(binaryExpression.Right, completed);
                return binaryExpression.Operator switch
                {
                    BinaryExpressionOperator.Equal => bool.Parse(left) == bool.Parse(right) ? "1" : "0",
                    BinaryExpressionOperator.NotEqual => bool.Parse(left) != bool.Parse(right) ? "1" : "0",
                    BinaryExpressionOperator.GreaterThan => long.Parse(left) > long.Parse(right) ? "1" : "0",
                    BinaryExpressionOperator.GreaterThanOrEqual => long.Parse(left) >= long.Parse(right) ? "1" : "0",
                    BinaryExpressionOperator.LessThan => long.Parse(left) < long.Parse(right) ? "1" : "0",
                    BinaryExpressionOperator.LessThanOrEqual => long.Parse(left) <= long.Parse(right) ? "1" : "0",
                    BinaryExpressionOperator.Plus => (long.Parse(left) + long.Parse(right)).ToString(),
                    BinaryExpressionOperator.Minus => (long.Parse(left) - long.Parse(right)).ToString(),
                    BinaryExpressionOperator.Multiply => (long.Parse(left) * long.Parse(right)).ToString(),
                    BinaryExpressionOperator.Divide => (long.Parse(left) / long.Parse(right)).ToString(),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            case IdentifierNode identifier:
            {
                return completed[identifier.Identifier];
            }
            case LiteralNode literal:
            {
                if (literal.Type is not PrimitiveType primitiveType)
                {
                    throw new NotSupportedException("Global variable literals must be of a primitive type");
                }

                return primitiveType.Kind switch
                {
                    PrimitiveTypeKind.Bool => bool.Parse(literal.Literal) ? "1" : "0",
                    PrimitiveTypeKind.Int64 or PrimitiveTypeKind.Int32 => $"{literal.Literal}",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            default:
            {
                throw new InvalidOperationException("Global variables must have the ability yo be evaluated at compile time");
            }
        }
    }
    
    private void GenerateFuncDefinition(LocalFuncDefinitionNode node)
    {
        var func = _symbolTable.ResolveLocalFunc(node.Name, node.Parameters.Select(p => p.Type).ToList());
        
        _builder.AppendLine($"{func.StartLabel}:");
        _builder.AppendLine("    push rbp");
        _builder.AppendLine("    mov rbp, rsp");
        _builder.AppendLine($"    sub rsp, {func.StackAllocation}");
        
        string[] registers = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"];
        
        for (var i = 0; i < func.Parameters.Count; i++)
        {
            var parameter = func.ResolveLocalVariable(func.Parameters.ElementAt(i).Name);
            if (i < registers.Length)
            {
                _builder.AppendLine($"    mov [rbp - {parameter.Offset}], {registers[i]}");
            }
            else
            {
                var stackOffset = 16 + (i - registers.Length) * 8;
                _builder.AppendLine($"    mov rax, [rbp + {stackOffset}]");
                _builder.AppendLine($"    mov [rbp - {parameter.Offset}], rax");
            }
        }
        
        GenerateBlock(node.Body, func);

        _builder.AppendLine($"{func.EndLabel}:");
        _builder.AppendLine("    mov rsp, rbp");
        _builder.AppendLine("    pop rbp");
        _builder.AppendLine("    ret");
    }
    
    private void GenerateBlock(BlockNode block, LocalFunc func)
    {
        foreach (var statement in block.Statements)
        {
            GenerateStatement(statement, func);
        }
    }

    private void GenerateStatement(StatementNode statement, LocalFunc func)
    {
        switch (statement)
        {
            case FuncCallStatementNode funcCallStatement:
                GenerateFuncCall(funcCallStatement.FuncCall, func);
                break;
            case IfNode ifStatement:
                GenerateIf(ifStatement, func);
                break;
            case ReturnNode @return:
                GenerateReturn(@return, func);
                break;
            case SyscallStatementNode syscallStatement:
                GenerateSyscall(syscallStatement.Syscall, func);
                break;
            case VariableAssignmentNode variableAssignment:
                GenerateVariableAssignment(variableAssignment, func);
                break;
            case VariableReassignmentNode variableReassignment:
                GenerateVariableReassignment(variableReassignment, func);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void GenerateIf(IfNode ifStatement, LocalFunc func)
    {
        var endLabel = _labelFactory.Create();
        GenerateIf(ifStatement, endLabel, func);
        _builder.AppendLine($"{endLabel}:");
    }
    
    private void GenerateIf(IfNode ifStatement, string endLabel, LocalFunc func)
    {
        var nextLabel = _labelFactory.Create();
        GenerateExpression(ifStatement.Condition, func);
        _builder.AppendLine("    cmp rax, 0");
        _builder.AppendLine($"    je {nextLabel}");
        GenerateBlock(ifStatement.Body, func);
        _builder.AppendLine($"    jmp {endLabel}");
        _builder.AppendLine($"{nextLabel}:");
        
        if (ifStatement.Else.HasValue)
        {
            ifStatement.Else.Value.Match
            (
                elseIfStatement => GenerateIf(elseIfStatement, endLabel, func),
                elseStatement => GenerateBlock(elseStatement, func)
            );
        }
    }

    private void GenerateReturn(ReturnNode @return, LocalFunc func)
    {
        if (@return.Value.HasValue)
        {
            GenerateExpression(@return.Value.Value, func);
        }

        _builder.AppendLine($"    jmp {func.EndLabel}");
    }

    private void GenerateVariableAssignment(VariableAssignmentNode variableAssignment, LocalFunc func)
    {
        var variable = func.ResolveLocalVariable(variableAssignment.Name);
        GenerateExpression(variableAssignment.Value, func);
        _builder.AppendLine($"    mov [rbp - {variable.Offset}], rax");
    }

    private void GenerateVariableReassignment(VariableReassignmentNode variableReassignment, LocalFunc func)
    {
        var variable = func.ResolveLocalVariable(variableReassignment.Name);
        GenerateExpression(variableReassignment.Value, func);
        _builder.AppendLine($"    mov [rbp - {variable.Offset}], rax");
    }

    private void GenerateExpression(ExpressionNode expression, LocalFunc func)
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
                throw new ArgumentOutOfRangeException(nameof(binaryExpression.Operator));
        }
    }

    private void GenerateComparison(Type type)
    {
        switch (type)
        {
            case DelegateType:
                throw new NotSupportedException($"Comparison on type {type.GetType().Name} is not supported");
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
            }
            case StringType:
            {
                var label = _symbolTable.DefineString(literal.Literal);
                _builder.AppendLine($"    mov rax, {label}");
                break;
            }
            case PrimitiveType primitive:
            {
                switch (primitive.Kind)
                {
                    case PrimitiveTypeKind.Bool:
                        _builder.AppendLine($"    mov rax, {(bool.Parse(literal.Literal) ? "1" : "0")}");
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

    private void GenerateFuncCall(FuncCall funcCall, LocalFunc func)
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

    private void GenerateSyscall(Syscall syscall, LocalFunc func)
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