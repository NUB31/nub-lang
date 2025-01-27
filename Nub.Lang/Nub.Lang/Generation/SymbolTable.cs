using Nub.Lang.Parsing;
using Nub.Core;

namespace Nub.Lang.Generation;

public class SymbolTable
{
    private readonly List<Func> _functions = [];
    private readonly List<GlobalVariable> _globalVariables = [];
    private int _labelIndex;

    public SymbolTable(IReadOnlyCollection<GlobalVariableDefinitionNode> globalVariableDefinitions)
    {
        var globalVariableIndex = 0;
        foreach (var globalVariable in globalVariableDefinitions)
        {
            var identifier = $"variable{++globalVariableIndex}";
            _globalVariables.Add(new GlobalVariable(globalVariable.Name, globalVariable.Value.Type, identifier));
        }
    }
    
    public void DefineFunc(FuncDefinitionNode funcDefinition)
    {
        var startLabel = $"func{++_labelIndex}";
        var endLabel = $"endfunc{_labelIndex}";
        var localVariables = ResolveFunctionVariables(funcDefinition);
        _functions.Add(new Func(startLabel, endLabel, funcDefinition.Name, funcDefinition.Parameters, funcDefinition.ReturnType, _globalVariables.Concat<Variable>(localVariables.Variables).ToList(), localVariables.StackSize));
    }

    private (int StackSize, List<LocalVariable> Variables) ResolveFunctionVariables(FuncDefinitionNode funcDefinition)
    {
        var offset = 0;
        List<LocalVariable> variables = [];

        foreach (var parameter in funcDefinition.Parameters)
        {
            offset += 8;
            variables.Add(new LocalVariable(parameter.Name, parameter.Type, offset));
        }
        
        foreach (var statement in funcDefinition.Body.Statements)
        {
            if (statement is VariableAssignmentNode variableAssignment)
            {
                offset += 8;
                variables.Add(new LocalVariable(variableAssignment.Name, variableAssignment.Value.Type, offset));
            }
        }

        return (offset, variables);
    }
    
    public Func ResolveFunc(string name, IReadOnlyCollection<Type> parameterTypes)
    {
        var func = _functions.FirstOrDefault(f => f.Name == name && f.Parameters.Count == parameterTypes.Count && f.Parameters.Where((p, i) => p.Type == parameterTypes.ElementAt(i)).Count() == parameterTypes.Count);
        if (func == null)
        {
            throw new Exception($"Func {name}({string.Join(", ", parameterTypes)}) is not defined");
        }

        return func;
    }
    
    public GlobalVariable ResolveGlobalVariable(string name)
    {
        var variable = _globalVariables.FirstOrDefault(v => v.Name == name);
        if (variable == null)
        {
            throw new Exception($"Global variable {name} is not defined");
        }

        return variable;
    }
}

public abstract class Variable(string name, Type type)
{
    public string Name { get; } = name;
    public Type Type { get; } = type;
}

public class LocalVariable(string name, Type type, int offset) : Variable(name, type)
{
    public int Offset { get; } = offset;
}

public class GlobalVariable(string name, Type type, string identifier) : Variable(name, type)
{
    public string Identifier { get; } = identifier;
}

public class Func(string startLabel, string endLabel, string name, IReadOnlyCollection<FuncParameter> parameters, Optional<Type> returnType, IReadOnlyCollection<Variable> variables, int stackAllocation)
{
    public string StartLabel { get; } = startLabel;
    public string EndLabel { get; } = endLabel;
    public string Name { get; } = name;
    public IReadOnlyCollection<FuncParameter> Parameters { get; } = parameters;
    public Optional<Type> ReturnType { get; } = returnType;
    public IReadOnlyCollection<Variable> Variables { get; } = variables;
    public int StackAllocation { get; } = stackAllocation;

    public Variable ResolveVariable(string name)
    {
        var variable = Variables.FirstOrDefault(v => v.Name == name);
        if (variable == null)
        {
            throw new Exception($"Variable {name} is not defined");
        }

        return variable;
    }
    
    public LocalVariable ResolveLocalVariable(string name)
    {
        var variable = Variables.FirstOrDefault(v => v.Name == name);
        if (variable == null)
        {
            throw new Exception($"Variable {name} is not defined");
        }

        if (variable is not LocalVariable localVariable)
        {
            throw new Exception($"Variable {name} is not a local variable");
        }

        return localVariable;
    }
    
    public override string ToString() => $"{Name}({string.Join(", ", Parameters.Select(p => p.ToString()))}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
}