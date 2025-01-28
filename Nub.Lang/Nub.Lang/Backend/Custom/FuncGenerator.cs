using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend.Custom;

public class FuncGenerator
{
    private readonly SymbolTable _symbolTable;
    private readonly ExpressionGenerator _expressionGenerator;
    private readonly StringBuilder _builder;

    public FuncGenerator(SymbolTable symbolTable, StringBuilder builder, ExpressionGenerator expressionGenerator)
    {
        _symbolTable = symbolTable;
        _builder = builder;
        _expressionGenerator = expressionGenerator;
    }

    public string GenerateFuncDefinition(LocalFuncDefinitionNode node)
    {
        var func = _symbolTable.ResolveLocalFunc(node.Name, node.Parameters.Select(p => p.Type).ToList());
        
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
        _builder.AppendLine("    ; Clean up stack frame");
        _builder.AppendLine("    mov rsp, rbp");
        _builder.AppendLine("    pop rbp");
        _builder.AppendLine("    ret");

        var result = _builder.ToString();
        _builder.Clear();
        return result;
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
                _expressionGenerator.GenerateFuncCall(funcCallStatement.FuncCall, func);
                break;
            case IfNode ifStatement:
                GenerateIf(ifStatement, func);
                break;
            case ReturnNode @return:
                GenerateReturn(@return, func);
                break;
            case SyscallStatementNode syscallStatement:
                _expressionGenerator.GenerateSyscall(syscallStatement.Syscall, func);
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
        var endLabel = _symbolTable.LabelFactory.Create();
        GenerateIf(ifStatement, endLabel, func);
        _builder.AppendLine($"{endLabel}:");
    }
    
    private void GenerateIf(IfNode ifStatement, string endLabel, LocalFunc func)
    {
        var nextLabel = _symbolTable.LabelFactory.Create();
        _expressionGenerator.GenerateExpression(ifStatement.Condition, func);
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
            _expressionGenerator.GenerateExpression(@return.Value.Value, func);
        }

        _builder.AppendLine($"    jmp {func.EndLabel}");
    }

    private void GenerateVariableAssignment(VariableAssignmentNode variableAssignment, LocalFunc func)
    {
        var variable = func.ResolveLocalVariable(variableAssignment.Name);
        _expressionGenerator.GenerateExpression(variableAssignment.Value, func);
        _builder.AppendLine($"    mov [rbp - {variable.Offset}], rax");
    }

    private void GenerateVariableReassignment(VariableReassignmentNode variableReassignment, LocalFunc func)
    {
        var variable = func.ResolveLocalVariable(variableReassignment.Name);
        _expressionGenerator.GenerateExpression(variableReassignment.Value, func);
        _builder.AppendLine($"    mov [rbp - {variable.Offset}], rax");
    }
}