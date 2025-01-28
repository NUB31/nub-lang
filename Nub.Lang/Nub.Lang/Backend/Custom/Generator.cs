using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend.Custom;

public class Generator
{
    private const string Entrypoint = "main";
    
    private readonly List<DefinitionNode> _definitions;
    private readonly SymbolTable _symbolTable;
    private readonly StringBuilder _builder;
    private readonly HashSet<string> _externFuncDefinitions;
    private readonly ExpressionGenerator _expressionGenerator;
    private readonly FuncGenerator _funcGenerator;
    
    public Generator(FileNode file, Dictionary<string, FileNode> deps)
    {
        _definitions = [];
        _builder = new StringBuilder();
        _externFuncDefinitions = ["strcmp"];
        _symbolTable = new SymbolTable();
        _expressionGenerator = new ExpressionGenerator(_symbolTable, _builder);
        _funcGenerator = new FuncGenerator(_symbolTable, _builder, _expressionGenerator);
        
        ResolveGlobalVariables(file, deps);
        ResolveDefinitions(file, deps);
    }

    private void ResolveGlobalVariables(FileNode file, Dictionary<string, FileNode> deps)
    {
        
        foreach (var globalVariableDefinition in file.Definitions.OfType<GlobalVariableDefinitionNode>())
        {
            _symbolTable.DefineGlobalVariable(globalVariableDefinition);
            _definitions.Add(globalVariableDefinition);
        }
        
        foreach (var include in file.Includes)
        {
            ResolveGlobalVariables(deps[include], deps);
        }
    }
    
    private void ResolveDefinitions(FileNode file, Dictionary<string, FileNode> deps)
    {
        foreach (var funcDefinitionNode in file.Definitions.OfType<ExternFuncDefinitionNode>())
        {
            _symbolTable.DefineFunc(funcDefinitionNode);
            _definitions.Add(funcDefinitionNode);
            _externFuncDefinitions.Add(_symbolTable.ResolveExternFunc(funcDefinitionNode.Name, funcDefinitionNode.Parameters.Select(p => p.Type).ToList()).StartLabel);
        }
        
        foreach (var funcDefinitionNode in file.Definitions.OfType<LocalFuncDefinitionNode>())
        {
            _symbolTable.DefineFunc(funcDefinitionNode);
            _definitions.Add(funcDefinitionNode);
        }
        
        foreach (var include in file.Includes)
        {
            ResolveDefinitions(deps[include], deps);
        }
    }

    public string Generate()
    {
        _builder.AppendLine("global _start");
        
        foreach (var externFuncDefinition in _externFuncDefinitions)
        {
            _builder.AppendLine($"extern {externFuncDefinition}");
        }
        
        _builder.AppendLine();
        _builder.AppendLine("section .bss");
        foreach (var globalVariable in _definitions.OfType<GlobalVariableDefinitionNode>())
        {
            var symbol = _symbolTable.ResolveGlobalVariable(globalVariable.Name);
            _builder.AppendLine($"    {symbol.Identifier}: resq 1 ; {globalVariable.Name}");
        }

        _builder.AppendLine();
        _builder.AppendLine("section .text");
        _builder.AppendLine("_start:");
        
        var main = _symbolTable.ResolveLocalFunc(Entrypoint, []);
        
        _builder.AppendLine("    ; Initialize global variables");
        foreach (var globalVariable in _definitions.OfType<GlobalVariableDefinitionNode>())
        {
            var symbol = _symbolTable.ResolveGlobalVariable(globalVariable.Name);
            _expressionGenerator.GenerateExpression(globalVariable.Value, main);
            _builder.AppendLine($"    mov [{symbol.Identifier}], rax");
        }

        _builder.AppendLine();
        _builder.AppendLine($"    ; Call entrypoint {Entrypoint}");
        _builder.AppendLine($"    call {main.StartLabel}");

        _builder.AppendLine();
        _builder.AppendLine(main.ReturnType.HasValue
            ? "    mov rdi, rax ; Exit with return value of entrypoint"
            : "    mov rdi, 0 ; Exit with default status code 0");
        _builder.AppendLine("    mov rax, 60");
        _builder.AppendLine("    syscall");

        foreach (var funcDefinition in _definitions.OfType<LocalFuncDefinitionNode>())
        {
            _builder.AppendLine();
            _builder.AppendLine(_funcGenerator.GenerateFuncDefinition(funcDefinition));
        }
        
        _builder.AppendLine();
        _builder.AppendLine("section .data");
        foreach (var str in _symbolTable.Strings)
        {
            _builder.AppendLine($"{str.Key}: db `{str.Value}`, 0");
        }
        
        return _builder.ToString();
    }
}