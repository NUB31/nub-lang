using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend;

public class SymbolTable
{
    public static SymbolTable Create(IEnumerable<DefinitionNode> program)
    {
        var externFuncDefs = new List<ExternFuncDef>();
        var localFuncDefs = new List<LocalFuncDef>();

        foreach (var node in program)
        {
            switch (node)
            {
                case ExternFuncDefinitionNode externFuncDefinitionNode:
                {
                    var parameters = externFuncDefinitionNode.Parameters.Select(parameter => new Variable(parameter.Name, parameter.Type)).ToList();
                    externFuncDefs.Add(new ExternFuncDef
                    {
                        Name = externFuncDefinitionNode.Name,
                        Parameters = parameters,
                        ReturnType = externFuncDefinitionNode.ReturnType
                    });
                    break;
                }
                case LocalFuncDefinitionNode localFuncDefinitionNode:
                {
                    var parameters = localFuncDefinitionNode.Parameters.Select(parameter => new Variable(parameter.Name, parameter.Type)).ToList();
                    var localVariables = new List<Variable>();

                    FindVariables(localFuncDefinitionNode.Body);

                    localFuncDefs.Add(new LocalFuncDef
                    {
                        Name = localFuncDefinitionNode.Name,
                        Parameters = parameters,
                        LocalVariables = localVariables,
                        ReturnType = localFuncDefinitionNode.ReturnType
                    });
                    break;

                    void FindVariables(BlockNode blockNode)
                    {
                        foreach (var statement in blockNode.Statements)
                        {
                            switch (statement)
                            {
                                case IfNode ifNode:
                                {
                                    FindVariables(ifNode.Body);
                                    break;
                                }
                                case WhileNode whileNode:
                                {
                                    FindVariables(whileNode.Body);
                                    break;
                                }
                                case VariableAssignmentNode variableAssignmentNode:
                                {
                                    localVariables.Add(new Variable(variableAssignmentNode.Name, variableAssignmentNode.Value.Type));
                                    break;
                                }
                            }
                        }
                    }
                }
                case StructDefinitionNode structDefinitionNode:
                {
                    throw new NotImplementedException();
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(node));
                }
            }
        }

        return new SymbolTable(externFuncDefs, localFuncDefs);
    }

    private readonly List<ExternFuncDef> _externFuncDefs;
    private readonly List<LocalFuncDef> _localFuncDefs;

    private SymbolTable(List<ExternFuncDef> externFuncDefs, List<LocalFuncDef> localFuncDefs)
    {
        _externFuncDefs = externFuncDefs;
        _localFuncDefs = localFuncDefs;
    }

    public FuncDef ResolveFunc(string name, List<NubType> parameters)
    {
        var matching = _externFuncDefs.Concat<FuncDef>(_localFuncDefs).Where(funcDef => funcDef.SignatureMatches(name, parameters)).ToArray();
        return matching.Length switch
        {
            0 => throw new Exception($"Could not resolve a func with signature {name}({string.Join(", ", parameters)})"),
            > 1 => throw new Exception($"Multiple functions matches the signature {name}({string.Join(", ", parameters)})"),
            _ => matching[0]
        };
    }

    public LocalFuncDef ResolveLocalFunc(string name, List<NubType> parameters)
    {
        var funcDef = ResolveFunc(name, parameters);
        if (funcDef is LocalFuncDef localFuncDef)
        {
            return localFuncDef;
        }

        throw new Exception($"Could not resolve a local func with signature {name}({string.Join(", ", parameters)})");
    }

    public ExternFuncDef ResolveExternFunc(string name, List<NubType> parameters)
    {
        var funcDef = ResolveFunc(name, parameters);
        if (funcDef is ExternFuncDef externFuncDef)
        {
            return externFuncDef;
        }

        throw new Exception($"Could not resolve a extern func with signature {name}({string.Join(", ", parameters)})");
    }
}

public abstract class FuncDef
{
    public required string Name { get; init; }
    public required List<Variable> Parameters { get; init; }
    public required Optional<NubType> ReturnType { get; init; }

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
}

public sealed class LocalFuncDef : FuncDef
{
    public required List<Variable> LocalVariables { get; set; }

    public override string ToString()
    {
        return $"func {Name}({string.Join(", ", Parameters.Select(p => p.ToString()))}){(ReturnType.HasValue ? ": " + ReturnType.Value : "")}";
    }
}

public sealed class ExternFuncDef : FuncDef;

public sealed class Variable(string name, NubType type)
{
    public string Name { get; } = name;
    public NubType Type { get; } = type;

    public override string ToString() => $"{Name}: {Type}";
}