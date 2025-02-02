using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend.Custom;

public class Generator
{
    private const string Entrypoint = "main";
    private const bool ZeroBasedIndexing = false;

    private readonly List<DefinitionNode> _definitions;
    private readonly SymbolTable _symbolTable;
    private readonly StringBuilder _builder;
    private readonly LabelFactory _labelFactory;
    private readonly Stack<(string StartLabel, string EndLabel)> _loops;
    
    public Generator(List<DefinitionNode> definitions)
    {
        _definitions = definitions;
        _builder = new StringBuilder();
        _labelFactory = new LabelFactory();
        _symbolTable = new SymbolTable(_labelFactory);
        _loops = [];

        foreach (var globalVariableDefinition in definitions.OfType<GlobalVariableDefinitionNode>())
        {
            _symbolTable.DefineGlobalVariable(globalVariableDefinition);
        }
        
        foreach (var funcDefinitionNode in definitions.OfType<ExternFuncDefinitionNode>())
        {
            _symbolTable.DefineFunc(funcDefinitionNode);
        }
    
        foreach (var funcDefinitionNode in definitions.OfType<LocalFuncDefinitionNode>())
        {
            _symbolTable.DefineFunc(funcDefinitionNode);
        }
    }

    public string Generate()
    {
        _builder.AppendLine("global _start");
        
        _builder.AppendLine("extern gc_init");
        _builder.AppendLine("extern gc_alloc");
        _builder.AppendLine("extern str_cmp");
        foreach (var externFuncDefinition in _definitions.OfType<ExternFuncDefinitionNode>())
        {
            _builder.AppendLine($"extern {externFuncDefinition.Name}");
        }

        _builder.AppendLine();
        _builder.AppendLine("section .text");

        // TODO: Only add start label if entrypoint is present, otherwise assume library
        var main = _symbolTable.ResolveLocalFunc(Entrypoint, []);
        
        _builder.AppendLine("_start:");
        _builder.AppendLine("    call gc_init");
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
                            
                            eb6e_oob_error:
                                mov rax, 60
                                mov rdi, 139
                                syscall
                            """);
        
        _builder.AppendLine();
        _builder.AppendLine("section .data");
        
        foreach (var str in _symbolTable.Strings)
        {
            _builder.AppendLine($"    {str.Key}: db `{str.Value}`, 0");
        }

        Dictionary<string, string> completed = [];
        foreach (var globalVariableDefinition in _definitions.OfType<GlobalVariableDefinitionNode>())
        {
            var variable = _symbolTable.ResolveGlobalVariable(globalVariableDefinition.Name);
            var evaluated = EvaluateExpression(globalVariableDefinition.Value, completed);
            _builder.AppendLine($"    {variable.Identifier}: dq {evaluated}");
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
            case ArrayIndexAssignmentNode arrayIndexAssignment:
                GenerateArrayIndexAssignment(arrayIndexAssignment, func);
                break;
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
            case SyscallStatementNode syscallStatement:
                GenerateSyscall(syscallStatement.Syscall, func);
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
        _builder.AppendLine($"    jmp {_loops.Peek().EndLabel}");
    }
    
    private void GenerateContinue()
    {
        _builder.AppendLine($"    jmp {_loops.Peek().StartLabel}");
    }

    private void GenerateArrayIndexAssignment(ArrayIndexAssignmentNode arrayIndexAssignment, LocalFunc func)
    {
        GenerateExpression(arrayIndexAssignment.Value, func);
        _builder.AppendLine("    push rax");
        GenerateArrayIndexPointerAccess(arrayIndexAssignment.Identifier, arrayIndexAssignment.Index, func);
        _builder.AppendLine("    pop rdx");
        _builder.AppendLine("    mov [rax], rdx");
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

    private void GenerateWhile(WhileNode whileStatement, LocalFunc func)
    {
        var startLabel = _labelFactory.Create();
        var endLabel = _labelFactory.Create();
        
        _builder.AppendLine($"{startLabel}:");
        GenerateExpression(whileStatement.Condition, func);
        _builder.AppendLine("    cmp rax, 0");
        _builder.AppendLine($"    je {endLabel}");
        _loops.Push((startLabel, endLabel));
        GenerateBlock(whileStatement.Body, func);
        _loops.Pop();
        _builder.AppendLine($"    jmp {startLabel}");
        _builder.AppendLine($"{endLabel}:");
    }

    private void GenerateExpression(ExpressionNode expression, LocalFunc func)
    {
        switch (expression)
        {
            case ArrayIndexAccessNode arrayIndexAccess:
                GenerateArrayIndexAccess(arrayIndexAccess, func);
                break;
            case ArrayInitializerNode arrayInitializer:
                GenerateArrayInitializer(arrayInitializer);
                break;
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
            case StructInitializerNode structInitializer:
                GenerateStructInitializer(structInitializer, func);
                break;
            case StructMemberAccessorNode structMemberAccessor:
                GenerateStructMemberAccessor(structMemberAccessor, func);
                break;
            case SyscallExpressionNode syscallExpression:
                GenerateSyscall(syscallExpression.Syscall, func);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private void GenerateStructMemberAccessor(StructMemberAccessorNode structMemberAccessor, LocalFunc func)
    {
        var variable = func.ResolveLocalVariable(structMemberAccessor.Members[0]);

        if (variable.Type is not StructType structType)
        {
            throw new Exception($"Cannot access struct member on {variable} since it is not a struct type");
        }

        _builder.AppendLine($"    mov rax, [rbp - {variable.Offset}]");

        Type prevMemberType = structType;
        for (var i = 1; i < structMemberAccessor.Members.Count; i++)
        {
            if (prevMemberType is not StructType prevMemberStructType)
            {
                throw new Exception($"Cannot access {structMemberAccessor.Members[i]} on type {prevMemberType} because it is not a struct type");
            }
            
            var structDefinition = _definitions.OfType<StructDefinitionNode>().FirstOrDefault(sd => sd.Name == prevMemberStructType.Name);
            if (structDefinition == null)
            {
                throw new Exception($"Struct {prevMemberStructType} is not defined");
            }

            var member = structDefinition.Members.FirstOrDefault(m => m.Name == structMemberAccessor.Members[i]);
            if (member == null)
            {
                throw new Exception($"Struct {prevMemberStructType} has no member with name {structMemberAccessor.Members[i]}");
            }

            var offset = structDefinition.Members.IndexOf(member);
            _builder.AppendLine($"    mov rax, [rax + {offset * 8}]");
            
            prevMemberType = member.Type;
        }
    }

    private void GenerateArrayIndexAccess(ArrayIndexAccessNode arrayIndexAccess, LocalFunc func)
    {
        GenerateArrayIndexPointerAccess(arrayIndexAccess.Identifier, arrayIndexAccess.Index, func);
        _builder.AppendLine("    mov rax, [rax]");
    }

    private void GenerateArrayInitializer(ArrayInitializerNode arrayInitializer)
    {
        _builder.AppendLine($"    mov rdi, {8 + arrayInitializer.Length * 8}");
        _builder.AppendLine("    call gc_alloc");
        _builder.AppendLine($"    mov qword [rax], {arrayInitializer.Length}");
    }

    private void GenerateBinaryExpression(BinaryExpressionNode binaryExpression, LocalFunc func)
    {
        GenerateExpression(binaryExpression.Left, func);
        _builder.AppendLine("    push rax");
        GenerateExpression(binaryExpression.Right, func);
        _builder.AppendLine("    mov rcx, rax");
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
            case AnyType:
                throw new InvalidOperationException($"Cannot compare type {type}");
            case ArrayType:
                // compare pointers
                _builder.AppendLine("    cmp rax, rcx");
                break;
            case PrimitiveType:
                _builder.AppendLine("    cmp rax, rcx");
                break;
            case StringType:
                _builder.AppendLine("    mov rdi, rax");
                _builder.AppendLine("    mov rsi, rcx");
                _builder.AppendLine("    call str_cmp");
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
                _builder.AppendLine("    add rax, rcx");
                break;
            case PrimitiveTypeKind.Int32:
                _builder.AppendLine("    add eax, ecx");
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
                _builder.AppendLine("    sub rax, rcx");
                break;
            case PrimitiveTypeKind.Int32:
                _builder.AppendLine("    sub eax, ecx");
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
                _builder.AppendLine("    imul rcx");
                break;
            case PrimitiveTypeKind.Int32:
                _builder.AppendLine("    imul ecx");
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
                _builder.AppendLine("    idiv rcx");
                break;
            case PrimitiveTypeKind.Int32:
                _builder.AppendLine("    cdq");
                _builder.AppendLine("    idiv ecx");
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

    private void GenerateStructInitializer(StructInitializerNode structInitializer, LocalFunc func)
    {
        var structDefinition = _definitions
            .OfType<StructDefinitionNode>()
            .FirstOrDefault(sd => sd.Name == structInitializer.StructType.Name);

        if (structDefinition == null)
        {
            throw new Exception($"Struct {structInitializer.StructType} is not defined");
        }

        _builder.AppendLine($"    mov rdi, {structDefinition.Members.Count * 8}");
        _builder.AppendLine("    call gc_alloc");
        _builder.AppendLine("    mov rcx, rax");
        
        foreach (var initializer in structInitializer.Initializers)
        {
            _builder.AppendLine("    push rcx");
            GenerateExpression(initializer.Value, func);
            var index = structDefinition.Members.FindIndex(sd => sd.Name == initializer.Key);
            if (index == -1)
            {
                throw new Exception($"Member {initializer.Key} is not defined on struct {structInitializer.StructType}");
            }

            _builder.AppendLine("    pop rcx");
            _builder.AppendLine($"    mov [rcx + {index * 8}], rax");
        }

        foreach (var uninitializedMember in structDefinition.Members.Where(m => !structInitializer.Initializers.ContainsKey(m.Name)))
        {
            if (!uninitializedMember.Value.HasValue)
            {
                throw new Exception($"Struct {structInitializer.StructType} must be initializer with member {uninitializedMember.Name}");
            }
            
            _builder.AppendLine("    push rcx");
            GenerateExpression(uninitializedMember.Value.Value, func);
            var index = structDefinition.Members.IndexOf(uninitializedMember);
            _builder.AppendLine("    pop rcx");
            _builder.AppendLine($"    mov [rcx + {index * 8}], rax");
        }
        
        _builder.AppendLine("    mov rax, rcx");
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

    private void GenerateArrayIndexPointerAccess(IdentifierNode identifier, ExpressionNode index, LocalFunc func)
    {
        GenerateExpression(index, func);
        _builder.AppendLine("    push rax");
        GenerateIdentifier(identifier, func);
        _builder.AppendLine("    pop rdx");
        
        // rcx now holds the length of the array which we can use to check bounds
        _builder.AppendLine("    mov rcx, [rax]");
        _builder.AppendLine("    cmp rdx, rcx");
        if (ZeroBasedIndexing)
        {
            _builder.AppendLine("    jge eb6e_oob_error");
            _builder.AppendLine("    cmp rdx, 0");
        }
        else
        {
            _builder.AppendLine("    jg eb6e_oob_error");
            _builder.AppendLine("    cmp rdx, 1");
        }
        _builder.AppendLine("    jl eb6e_oob_error");

        _builder.AppendLine("    inc rdx");
        _builder.AppendLine("    shl rdx, 3");
        _builder.AppendLine("    add rax, rdx");
    }
}