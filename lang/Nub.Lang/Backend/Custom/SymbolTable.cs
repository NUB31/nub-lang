using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend.Custom;

public class SymbolTable
{
    private readonly List<Func> _funcDefinitions = [];
    private readonly List<GlobalVariable> _globalVariables = [];
    private readonly LabelFactory _labelFactory;
    
    public readonly Dictionary<string, string> Strings = [];

    public SymbolTable(LabelFactory labelFactory)
    {
        _labelFactory = labelFactory;
    }
    
    public string DefineString(string value)
    {
        var label = _labelFactory.Create();
        Strings.Add(label, value);
        return label;
    }
    
    public void DefineGlobalVariable(GlobalVariableDefinitionNode globalVariableDefinition)
    {
        var identifier =  _labelFactory.Create();
        _globalVariables.Add(new GlobalVariable(globalVariableDefinition.Name, globalVariableDefinition.Value.Type, identifier));
    }
    
    public void DefineFunc(ExternFuncDefinitionNode externFuncDefinition)
    {
        var existing = _funcDefinitions
            .FirstOrDefault(f => f
                .SignatureMatches
                (
                    externFuncDefinition.Name, 
                    externFuncDefinition.Parameters.Select(p => p.Type).ToList()
                ));
        
        if (existing != null)
        {
            throw new Exception($"Func {existing} is already defined");
        }
        
        _funcDefinitions.Add(new ExternFunc(externFuncDefinition.Name, externFuncDefinition.Name, externFuncDefinition.Parameters, externFuncDefinition.ReturnType));
    }
    
    public void DefineFunc(LocalFuncDefinitionNode localFuncDefinition)
    {
        var existing = _funcDefinitions
            .FirstOrDefault(f => f
                .SignatureMatches
                (
                    localFuncDefinition.Name, 
                    localFuncDefinition.Parameters.Select(p => p.Type).ToList()
                ));
        
        if (existing != null)
        {
            throw new Exception($"Func {existing} is already defined");
        }
        
        var startLabel = _labelFactory.Create();
        var endLabel = _labelFactory.Create();
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
        
        ResolveBlockVariables(localFuncDefinition.Body, variables, offset);
        
        return variables;
    }

    private static int ResolveBlockVariables(BlockNode block, List<LocalVariable> variables, int offset)
    {
        foreach (var statement in block.Statements)
        {
            switch (statement)
            {
                case IfNode ifStatement:
                {
                    offset = ResolveBlockVariables(ifStatement.Body, variables, offset);
                    if (ifStatement.Else.HasValue)
                    {
                        ifStatement.Else.Value.Match
                        (
                            elseIfStatement => offset = ResolveBlockVariables(elseIfStatement.Body, variables, offset),
                            elseStatement => offset = ResolveBlockVariables(elseStatement, variables, offset)
                        );
                    }
                    break;
                }
                case WhileNode whileStatement:
                {
                    offset = ResolveBlockVariables(whileStatement.Body, variables, offset);
                    break;
                }
                case VariableAssignmentNode variableAssignment:
                {
                    offset += 8;
                    variables.Add(new LocalVariable(variableAssignment.Name, variableAssignment.Value.Type, offset));
                    break;
                }
            }
        }

        return offset;
    }    
    
    public Func ResolveFunc(string name, List<NubType> parameterTypes)
    {
        var func = _funcDefinitions.FirstOrDefault(f => f.SignatureMatches(name, parameterTypes));
        if (func == null)
        {
            throw new Exception($"Func {name}({string.Join(", ", parameterTypes)}) is not defined");
        }

        return func;
    }
    
    public LocalFunc ResolveLocalFunc(string name, List<NubType> parameterTypes)
    {
        var func = ResolveFunc(name, parameterTypes);
        if (func is not LocalFunc localFunc)
        {
            throw new Exception($"Func {func} is not a local func");
        }
        return localFunc;
    }
    
    public ExternFunc ResolveExternFunc(string name, List<NubType> parameterTypes)
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

public abstract class Variable(string name, NubType type)
{
    public string Name { get; } = name;
    public NubType Type { get; } = type;

    public override string ToString() => $"{Name}: {Type}";
}

public class LocalVariable(string name, NubType type, int offset) : Variable(name, type)
{
    public int Offset { get; } = offset;
}

public class GlobalVariable(string name, NubType type, string identifier) : Variable(name, type)
{
    public string Identifier { get; } = identifier;
}

public abstract class Func
{
    protected Func(string name, string startLabel, List<FuncParameter> parameters, Optional<NubType> returnType)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
        StartLabel = startLabel;
    }

    public string Name { get; }
    public string StartLabel { get; }
    public List<FuncParameter> Parameters { get; }
    public Optional<NubType> ReturnType { get; }

    public bool SignatureMatches(string name, List<NubType> parameterTypes)
    {
        if (Name != name) return false;
        if (Parameters.Count != parameterTypes.Count) return false;

        for (var i = 0; i < parameterTypes.Count; i++)
        {
            if (!Parameters[i].Type.Equals(parameterTypes[i])) return false;
        }

        return true;
    }
    
    public override string ToString() => $"{Name}({string.Join(", ", Parameters.Select(p => p.ToString()))}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
}

public class ExternFunc : Func
{
    public ExternFunc(string name, string startLabel, List<FuncParameter> parameters, Optional<NubType> returnType) : base(name, startLabel, parameters, returnType)
    {
    }
}

public class LocalFunc : Func
{
    public LocalFunc(string name, string startLabel, string endLabel, List<FuncParameter> parameters, Optional<NubType> returnType, List<Variable> variables) : base(name, startLabel, parameters, returnType)
    {
        EndLabel = endLabel;
        Variables = variables;
    }

    public string EndLabel { get; }
    public List<Variable> Variables { get; }
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
}