﻿using System.Text;
using Nub.Lang.Parsing;

namespace Nub.Lang.Generation;

public class Generator
{
    private const string Entrypoint = "main";
    
    private readonly IReadOnlyCollection<DefinitionNode> _definitions;
    private readonly SymbolTable _symbolTable;
    private readonly StringBuilder _builder;
    private readonly Dictionary<string, string> _strings;
    private int _stringIndex;
    
    public Generator(IReadOnlyCollection<DefinitionNode> definitions)
    {
        _strings = [];
        _definitions = definitions;
        _builder = new StringBuilder();
        _symbolTable = new SymbolTable(definitions.OfType<GlobalVariableDefinitionNode>().ToList());
        foreach (var funcDefinitionNode in definitions.OfType<FuncDefinitionNode>())
        {
            _symbolTable.DefineFunc(funcDefinitionNode);
        }
    }

    public string Generate()
    {
        _builder.AppendLine("global _start");
        
        _builder.AppendLine();
        _builder.AppendLine("section .bss");
        foreach (var globalVariable in _definitions.OfType<GlobalVariableDefinitionNode>())
        {
            var symbol = _symbolTable.ResolveGlobalVariable(globalVariable.Name);
            _builder.AppendLine($"    {symbol.Identifier}: resq 1");
        }

        _builder.AppendLine();
        _builder.AppendLine("section .text");
        _builder.AppendLine("_start:");
        
        var main = _symbolTable.ResolveFunc(Entrypoint, []);
        
        foreach (var globalVariable in _definitions.OfType<GlobalVariableDefinitionNode>())
        {
            var symbol = _symbolTable.ResolveGlobalVariable(globalVariable.Name);
            _builder.AppendLine($"    ; Initialize global variable {symbol.Name}");
            GenerateExpression(globalVariable.Value, main);
            _builder.AppendLine($"    mov [{symbol.Identifier}], rax");
        }

        _builder.AppendLine();
        _builder.AppendLine($"    ; Call entrypoint {Entrypoint}");
        _builder.AppendLine($"    call {main.StartLabel}");

        _builder.AppendLine();
        _builder.AppendLine("    ; Exit with status code 0");
        _builder.AppendLine("    mov rax, 60");
        _builder.AppendLine("    mov rdi, 0");
        _builder.AppendLine("    syscall");

        foreach (var funcDefinition in _definitions.OfType<FuncDefinitionNode>())
        {
            _builder.AppendLine();
            GenerateFuncDefinition(funcDefinition);
        }

        _builder.AppendLine("""
                            
                            ; https://tuttlem.github.io/2013/01/08/strlen-implementation-in-nasm.html
                            strlen:
                                push rcx            ; save and clear out counter
                                xor rcx, rcx
                            strlen_next:
                                cmp [rdi], byte 0   ; null byte yet?
                                jz strlen_null      ; yes, get out
                                inc rcx             ; char is ok, count it
                                inc rdi             ; move to next char
                                jmp strlen_next     ; process again
                            strlen_null:
                                mov rax, rcx        ; rcx = the length (put in rax)
                                pop rcx             ; restore rcx
                                ret                 ; get out
                            """);

        _builder.AppendLine("""
                            
                            strcmp:
                            xor rdx, rdx
                            strcmp_loop:
                            	mov al, [rsi + rdx]
                            	mov bl, [rdi + rdx]
                            	inc rdx
                            	cmp al, bl
                            	jne strcmp_not_equal
                            	cmp al, 0
                            	je strcmp_equal
                            	jmp strcmp_loop
                            strcmp_not_equal:
                            	mov rax, 0
                                ret
                            strcmp_equal:
                            	mov rax, 1
                                ret
                            """);
        
        
        _builder.AppendLine();
        _builder.AppendLine("section .data");
        foreach (var str in _strings)
        {
            _builder.AppendLine($"{str.Key}: db `{str.Value}`, 0");
        }
        
        return _builder.ToString();
    }

    private void GenerateFuncDefinition(FuncDefinitionNode node)
    {
        var func = _symbolTable.ResolveFunc(node.Name, node.Parameters.Select(p => p.Type).ToList());
        _builder.AppendLine($"; {node.ToString()}");
        _builder.AppendLine($"{func.StartLabel}:");
        _builder.AppendLine("    ; Set up stack frame");
        _builder.AppendLine("    push rbp");
        _builder.AppendLine("    mov rbp, rsp");
        _builder.AppendLine($"    sub rsp, {func.StackAllocation}");
        
        string[] registers = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"];
        
        _builder.AppendLine("    ; Body");
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
        _builder.AppendLine("; Clean up stack frame");
        _builder.AppendLine("    mov rsp, rbp");
        _builder.AppendLine("    pop rbp");
        _builder.AppendLine("    ret");
    }

    private void GenerateBlock(BlockNode block, Func func)
    {
        foreach (var statement in block.Statements)
        {
            GenerateStatement(statement, func);
        }
    }

    private void GenerateStatement(StatementNode statement, Func func)
    {
        switch (statement)
        {
            case FuncCallStatementNode funcCallStatement:
                GenerateFuncCall(funcCallStatement.FuncCall, func);
                break;
            case ReturnNode @return:
                GenerateReturn(func, @return);
                break;
            case SyscallStatementNode syscallStatement:
                GenerateSyscall(syscallStatement.Syscall, func);
                break;
            case VariableAssignmentNode variableAssignment:
                GenerateVariableAssignment(func, variableAssignment);
                break;
            case VariableReassignmentNode variableReassignment:
                GenerateVariableReassignment(variableReassignment, func);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void GenerateReturn(Func func, ReturnNode @return)
    {
        if (@return.Value.HasValue)
        {
            GenerateExpression(@return.Value.Value, func);
        }

        _builder.AppendLine($"    jmp {func.EndLabel}");
    }

    private void GenerateVariableAssignment(Func func, VariableAssignmentNode variableAssignment)
    {
        var variable = func.ResolveLocalVariable(variableAssignment.Name);
        GenerateExpression(variableAssignment.Value, func);
        _builder.AppendLine($"    mov [rbp - {variable.Offset}], rax");
    }

    private void GenerateVariableReassignment(VariableReassignmentNode variableReassignment, Func func)
    {
        var variable = func.ResolveLocalVariable(variableReassignment.Name);
        GenerateExpression(variableReassignment.Value, func);
        _builder.AppendLine($"    mov [rbp - {variable.Offset}], rax");
    }

    private void GenerateExpression(ExpressionNode expression, Func func)
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
            case StrlenNode strlen:
                GenerateStrlen(strlen, func);
                break;
            case SyscallExpressionNode syscallExpression:
                GenerateSyscall(syscallExpression.Syscall, func);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private void GenerateBinaryExpression(BinaryExpressionNode binaryExpression, Func func)
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

    private void GenerateIdentifier(IdentifierNode identifier, Func func)
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

    private void GenerateLiteral(LiteralNode literal, Func func)
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
                var ident = $"string{++_stringIndex}";
                _strings.Add(ident, literal.Literal);
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

    private void GenerateStrlen(StrlenNode strlen, Func func)
    {
        GenerateExpression(strlen.String, func);
        _builder.AppendLine("    mov rdi, rax");
        _builder.AppendLine("    call strlen");
    }

    private void GenerateFuncCall(FuncCall funcCall, Func func)
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
    
    private void GenerateSyscall(Syscall syscall, Func func)
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