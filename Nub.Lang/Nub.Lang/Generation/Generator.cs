using System.Text;
using Nub.Lang.Parsing;

namespace Nub.Lang.Generation;

public class Generator
{
    private const string Entrypoint = "main";
    
    private readonly IReadOnlyCollection<DefinitionNode> _definitions;
    private readonly SymbolTable _symbolTable;
    private readonly StringBuilder _builder;
    private Dictionary<string, string> _strings;
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
        _builder.AppendLine($"    call {main.Label}");

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
        _builder.AppendLine();
        _builder.AppendLine("section .data");
        foreach (var str in _strings)
        {
            _builder.AppendLine($"{str.Key}: db `{str.Value}`, 0");
        }
        
        return _builder.ToString();
    }

    private void GenerateFuncDefinition(FuncDefinitionNode funcDefinition)
    {
        var symbol = _symbolTable.ResolveFunc(funcDefinition.Name, funcDefinition.Parameters.Select(p => p.Type).ToList());
        _builder.AppendLine($"; {funcDefinition.ToString()}");
        _builder.AppendLine($"{symbol.Label}:");
        _builder.AppendLine("    push rbp");
        _builder.AppendLine("    mov rbp, rsp");
        _builder.AppendLine($"    sub rsp, {symbol.StackAllocation}");
        
        string[] registers = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"];
        
        for (var i = 0; i < symbol.Parameters.Count; i++)
        {
            var parameter = symbol.Parameters.ElementAt(i);

            if (i < registers.Length)
            {
                var variable = symbol.ResolveLocalVariable(parameter.Name);
                _builder.AppendLine($"    mov [rbp - {variable.Offset}], {registers[i]}");
            }
            else
            {
                // TODO: Implement parameters passed on the stack
                throw new NotImplementedException();
            }
        }
        
        GenerateBlock(funcDefinition.Body, symbol);
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
            case SyscallStatementNode syscallStatement:
                GenerateSyscall(syscallStatement.Syscall, func);
                break;
            case VariableAssignmentNode variableAssignment:
                throw new NotImplementedException();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void GenerateExpression(ExpressionNode expression, Func func)
    {
        switch (expression)
        {
            case FuncCallExpressionNode funcCallExpression:
                throw new NotImplementedException();
                break;
            case IdentifierNode identifier:
                GenerateIdentifier(identifier, func);
                break;
            case LiteralNode literal:
                GenerateLiteral(literal, func);
                break;
            case SyscallExpressionNode syscallExpression:
                throw new NotImplementedException();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
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
                throw new NotImplementedException();
                break;
            case StringType:
                var ident = $"string{++_stringIndex}";
                _strings.Add(ident, literal.Literal);
                _builder.AppendLine($"    mov rax, {ident}");
                break;
            case PrimitiveType primitive:
                switch (primitive.Kind)
                {
                    case PrimitiveTypeKind.Bool:
                    {
                        var value = literal.Literal == "true" ? 1 : 0;
                        _builder.AppendLine($"    mov al, {value}");
                        break;
                    }
                    case PrimitiveTypeKind.Char:
                        throw new NotImplementedException();
                        break;
                    case PrimitiveTypeKind.Int8:
                    case PrimitiveTypeKind.UInt8:
                        _builder.AppendLine($"    mov al, {literal.Literal}");
                        break;
                    case PrimitiveTypeKind.Int16:
                    case PrimitiveTypeKind.UInt16:
                        _builder.AppendLine($"    mov ax, {literal.Literal}");
                        break;
                    case PrimitiveTypeKind.Int32:
                    case PrimitiveTypeKind.UInt32:
                        _builder.AppendLine($"    mov eax, {literal.Literal}");
                        break;
                    case PrimitiveTypeKind.Int64:
                    case PrimitiveTypeKind.UInt64:
                        _builder.AppendLine($"    mov rax, {literal.Literal}");
                        break;
                    case PrimitiveTypeKind.Float:
                        throw new NotImplementedException();
                        break;
                    case PrimitiveTypeKind.Double:
                        throw new NotImplementedException();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // TODO: Use stack for more than 6 parameters
    private void GenerateFuncCall(FuncCall funcCall, Func func)
    {
        var symbol = _symbolTable.ResolveFunc(funcCall.Name, funcCall.Parameters.Select(p => p.Type).ToList());
        string[] registers = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"];

        foreach (var parameter in funcCall.Parameters)
        {
            GenerateExpression(parameter, func);
            _builder.AppendLine("    push rax");
        }
        
        for (var i = funcCall.Parameters.Count - 1; i >= 0; i--)
        {
            _builder.AppendLine($"    pop {registers[i]}");
        }
        
        _builder.AppendLine($"    call {symbol.Label}");
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