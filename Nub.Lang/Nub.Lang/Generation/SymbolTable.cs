using Nub.Lang.Parsing;
using Nub.Core;

namespace Nub.Lang.Generation;

public class SymbolTable
{
    private readonly List<Func> _funcDefinitions = [];
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
    
    public void DefineFunc(ExternFuncDefinitionNode externFuncDefinition)
    {
        _funcDefinitions.Add(new ExternFunc(externFuncDefinition.Name, externFuncDefinition.Name, externFuncDefinition.Parameters, externFuncDefinition.ReturnType));
    }
    
    public void DefineFunc(LocalFuncDefinitionNode localFuncDefinition)
    {
        var startLabel = $"func{++_labelIndex}";
        var endLabel = $"func_end{_labelIndex}";
        _funcDefinitions.Add(new LocalFunc(localFuncDefinition.Name, startLabel, endLabel, localFuncDefinition.Parameters, localFuncDefinition.ReturnType, _globalVariables.Concat<Variable>(ResolveFuncVariables(localFuncDefinition)).ToList()));
    }
    
    private static List<LocalVariable> ResolveFuncVariables(LocalFuncDefinitionNode localFuncDefinition)
    {
        var offset = 0;
        List<LocalVariable> variables = [];

        foreach (var parameter in localFuncDefinition.Parameters)
        {
            offset += 8;
            variables.Add(new LocalVariable(parameter.Name, parameter.Type, offset));
        }
        
        foreach (var statement in localFuncDefinition.Body.Statements)
        {
            if (statement is VariableAssignmentNode variableAssignment)
            {
                offset += 8;
                variables.Add(new LocalVariable(variableAssignment.Name, variableAssignment.Value.Type, offset));
            }
        }

        return variables;
    }
    
    public Func ResolveFunc(string name, IReadOnlyCollection<Type> parameterTypes)
    {
        var func = _funcDefinitions.FirstOrDefault(f => f.Name == name && f.Parameters.Count == parameterTypes.Count && f.Parameters.Where((p, i) => p.Type == parameterTypes.ElementAt(i)).Count() == parameterTypes.Count);
        if (func == null)
        {
            throw new Exception($"Func {name}({string.Join(", ", parameterTypes)}) is not defined");
        }

        return func;
    }
    
    public LocalFunc ResolveLocalFunc(string name, IReadOnlyCollection<Type> parameterTypes)
    {
        var func = ResolveFunc(name, parameterTypes);
        if (func is not LocalFunc localFunc)
        {
            throw new Exception($"Func {func} is not a local func");
        }
        return localFunc;
    }
    
    public ExternFunc ResolveExternFunc(string name, IReadOnlyCollection<Type> parameterTypes)
    {
        var func = ResolveFunc(name, parameterTypes);
        if (func is not ExternFunc externFunc)
        {
            throw new Exception($"Func {func} is not an extern func");
        }
        return externFunc;
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

public abstract class Func
{
    public Func(string name, string startLabel, IReadOnlyCollection<FuncParameter> parameters, Optional<Type> returnType)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
        StartLabel = startLabel;
    }

    public string Name { get; }
    public string StartLabel { get; }
    public IReadOnlyCollection<FuncParameter> Parameters { get; }
    public Optional<Type> ReturnType { get; }
}

public class ExternFunc : Func
{
    public ExternFunc(string name, string startLabel, IReadOnlyCollection<FuncParameter> parameters, Optional<Type> returnType) : base(name, startLabel, parameters, returnType)
    {
    }
}

public class LocalFunc : Func
{
    public LocalFunc(string name, string startLabel, string endLabel, IReadOnlyCollection<FuncParameter> parameters, Optional<Type> returnType, IReadOnlyCollection<Variable> variables) : base(name, startLabel, parameters, returnType)
    {
        EndLabel = endLabel;
        Variables = variables;
    }

    public string EndLabel { get; }
    public IReadOnlyCollection<Variable> Variables { get; }
    public int StackAllocation => Variables.OfType<LocalVariable>().Sum(variable => variable.Offset);

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