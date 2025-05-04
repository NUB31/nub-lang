using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend;

public class SymbolTable
{
    public static SymbolTable Create(IEnumerable<DefinitionNode> program)
    {
        var externFuncDefs = new List<ExternFuncDef>();
        var localFuncDefs = new List<LocalFuncDef>();

        var strings = new List<string>();

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

                    Search(localFuncDefinitionNode.Body);

                    localFuncDefs.Add(new LocalFuncDef
                    {
                        Name = localFuncDefinitionNode.Name,
                        Parameters = parameters,
                        LocalVariables = localVariables,
                        ReturnType = localFuncDefinitionNode.ReturnType
                    });
                    break;

                    void Search(BlockNode blockNode)
                    {
                        foreach (var statement in blockNode.Statements)
                        {
                            switch (statement)
                            {
                                case FuncCallStatementNode funcCallStatementNode:
                                {
                                    foreach (var parameter in funcCallStatementNode.FuncCall.Parameters)
                                    {
                                        FindStrings(parameter);
                                    }
                                    break;
                                }
                                case IfNode ifNode:
                                {
                                    SearchIf(ifNode);
                                    break;
                                }
                                case ReturnNode returnNode:
                                {
                                    if (returnNode.Value.HasValue)
                                    {
                                        FindStrings(returnNode.Value.Value);
                                    }
                                    break;
                                }
                                case WhileNode whileNode:
                                {
                                    FindStrings(whileNode.Condition);
                                    Search(whileNode.Body);
                                    break;
                                }
                                case VariableAssignmentNode variableAssignmentNode:
                                {
                                    FindStrings(variableAssignmentNode.Value);
                                    localVariables.Add(new Variable(variableAssignmentNode.Name, variableAssignmentNode.Value.Type));
                                    break;
                                }
                                case VariableReassignmentNode variableReassignmentNode:
                                {
                                    FindStrings(variableReassignmentNode.Value);
                                    break;
                                }
                            }
                        }
                    }

                    void SearchIf(IfNode ifNode)
                    {
                        FindStrings(ifNode.Condition);
                        Search(ifNode.Body);
                        if (ifNode.Else.HasValue)
                        {
                            ifNode.Else.Value.Match(SearchIf, Search);
                        }
                    }

                    void FindStrings(ExpressionNode expressionNode)
                    {
                        switch (expressionNode)
                        {
                            case BinaryExpressionNode binaryExpressionNode:
                            {
                                FindStrings(binaryExpressionNode.Left);
                                FindStrings(binaryExpressionNode.Right);
                                break;
                            }
                            case FuncCallExpressionNode funcCallExpressionNode:
                            {
                                foreach (var parameter in funcCallExpressionNode.FuncCall.Parameters)
                                {
                                    FindStrings(parameter);
                                }
                                break;
                            }
                            case LiteralNode literalNode:
                            {
                                if (literalNode.LiteralType.Equals(NubType.String))
                                {
                                    strings.Add(literalNode.Literal);
                                }
                                break;
                            }
                            case StructInitializerNode structInitializerNode:
                            {
                                foreach (var initializer in structInitializerNode.Initializers)
                                {
                                    FindStrings(initializer.Value);
                                }
                                break;
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

        return new SymbolTable(strings, externFuncDefs, localFuncDefs);
    }

    private readonly List<string> _strings;
    private readonly List<ExternFuncDef> _externFuncDefs;
    private readonly List<LocalFuncDef> _localFuncDefs;

    private SymbolTable(List<string> strings, List<ExternFuncDef> externFuncDefs, List<LocalFuncDef> localFuncDefs)
    {
        _strings = strings;
        _externFuncDefs = externFuncDefs;
        _localFuncDefs = localFuncDefs;
    }

    public int ResolveString(string value)
    {
        var index = _strings.IndexOf(value);
        if (index == -1)
        {
            throw new Exception("String not found: " + value);
        }

        return index;
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