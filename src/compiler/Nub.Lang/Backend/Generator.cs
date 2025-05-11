using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend;

public class Generator
{
    private readonly List<DefinitionNode> _definitions;
    private readonly StringBuilder _builder = new();
    private readonly Dictionary<string, int> _prefixIndexes = new();
    private readonly Dictionary<string, Variable> _variables = new();
    private readonly List<string> _strings = [];
    private readonly Stack<string> _breakLabels = new();
    private readonly Stack<string> _continueLabels = new();
    private bool _codeIsReachable = true;

    public Generator(List<DefinitionNode> definitions)
    {
        _definitions = definitions;
    }

    public string Generate()
    {
        foreach (var structDefinition in _definitions.OfType<StructDefinitionNode>())
        {
            GenerateStructDefinition(structDefinition);
            _builder.AppendLine();
        }

        foreach (var funcDefinition in _definitions.OfType<LocalFuncDefinitionNode>())
        {
            GenerateFuncDefinition(funcDefinition);
            _builder.AppendLine();
        }

        for (var i = 0; i < _strings.Count; i++)
        {
            var str = _strings[i];
            _builder.AppendLine($"data $str{i + 1} = {{ b \"{str}\", b 0 }}");
        }

        return _builder.ToString();
    }

    private string QbeTypeName(NubType type)
    {
        if (type.Equals(NubType.Int32) || type.Equals(NubType.Bool))
        {
            return "w";
        }

        return "l";
    }

    private int QbeTypeSize(NubType type)
    {
        if (type.Equals(NubType.Int32) || type.Equals(NubType.Bool))
        {
            return 4;
        }

        return 8;
    }

    private void GenerateFuncDefinition(LocalFuncDefinitionNode node)
    {
        _variables.Clear();

        foreach (var parameter in node.Parameters)
        {
            _variables.Add(parameter.Name, new Variable
            {
                Identifier = $"%{parameter.Name}",
                Type = parameter.Type
            });
        }

        if (node.Global)
        {
            _builder.Append("export ");
        }

        _builder.Append("function ");
        if (node.ReturnType.HasValue)
        {
            _builder.Append($"{QbeTypeName(node.ReturnType.Value)} ");
        }

        _builder.Append('$');
        _builder.Append(node.Name);

        _builder.AppendLine($"({string.Join(", ", node.Parameters.Select(p => $"{QbeTypeName(p.Type)} %{p.Name}"))}) {{");
        _builder.AppendLine("@start");
        GenerateBlock(node.Body);
        if (!node.ReturnType.HasValue)
        {
            _builder.AppendLine("    ret");
        }

        _builder.AppendLine("}");
    }

    private void GenerateStructDefinition(StructDefinitionNode structDefinition)
    {
        var fields = structDefinition.Fields.Select(f => QbeTypeName(f.Type));
        _builder.AppendLine($"type :{structDefinition.Name} = {{ {string.Join(", ", fields)} }}");
    }

    private void GenerateBlock(BlockNode block)
    {
        foreach (var statement in block.Statements.Where(_ => _codeIsReachable))
        {
            GenerateStatement(statement);
        }

        _codeIsReachable = true;
    }

    private void GenerateStatement(StatementNode statement)
    {
        switch (statement)
        {
            case BreakNode:
                GenerateBreak();
                break;
            case ContinueNode:
                GenerateContinue();
                break;
            case FuncCallStatementNode funcCallStatement:
                GenerateStatementFuncCall(funcCallStatement);
                break;
            case IfNode ifStatement:
                GenerateIf(ifStatement);
                break;
            case ReturnNode @return:
                GenerateReturn(@return);
                break;
            case VariableAssignmentNode variableAssignment:
                GenerateVariableAssignment(variableAssignment);
                break;
            case VariableReassignmentNode variableReassignment:
                GenerateVariableReassignment(variableReassignment);
                break;
            case WhileNode whileStatement:
                GenerateWhile(whileStatement);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private void GenerateBreak()
    {
        _builder.AppendLine($"    jmp @{_breakLabels.Peek()}");
        _codeIsReachable = false;
    }

    private void GenerateContinue()
    {
        _builder.AppendLine($"    jmp @{_continueLabels.Peek()}");
        _codeIsReachable = false;
    }

    private void GenerateStatementFuncCall(FuncCallStatementNode funcCall)
    {
        var results = new List<(string, NubType)>();
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            results.Add((GenerateExpression(parameter), parameter.Type));
        }

        var parameters = results.Select(p => $"{QbeTypeName(p.Item2)} {p.Item1}");

        _builder.AppendLine($"    call ${funcCall.FuncCall.Name}({string.Join(", ", parameters)})");
    }

    private void GenerateIf(IfNode ifStatement)
    {
        var trueLabel = GenName("if_true");
        var endLabel = GenName("if_end");
        var falseLabel = GenName("if_false");

        var result = GenerateExpression(ifStatement.Condition);
        _builder.AppendLine($"    jnz {result}, @{trueLabel}, @{falseLabel}");
        _builder.AppendLine($"@{trueLabel}");
        GenerateBlock(ifStatement.Body);
        _builder.AppendLine($"    jmp @{endLabel}");
        _builder.AppendLine($"@{falseLabel}");
        if (ifStatement.Else.HasValue)
        {
            ifStatement.Else.Value.Match
            (
                GenerateIf,
                GenerateBlock
            );
        }

        _builder.AppendLine($"@{endLabel}");
    }

    private void GenerateReturn(ReturnNode @return)
    {
        if (@return.Value.HasValue)
        {
            var result = GenerateExpression(@return.Value.Value);
            _builder.AppendLine($"    ret {result}");
        }
        else
        {
            _builder.AppendLine("    ret");
        }
    }

    private void GenerateVariableAssignment(VariableAssignmentNode variableAssignment)
    {
        var result = GenerateExpression(variableAssignment.Value);
        _variables[variableAssignment.Name] = new Variable
        {
            Identifier = result,
            Type = variableAssignment.Value.Type
        };
    }

    private void GenerateVariableReassignment(VariableReassignmentNode variableReassignment)
    {
        var result = GenerateExpression(variableReassignment.Value);
        _variables[variableReassignment.Name] = new Variable
        {
            Identifier = result,
            Type = variableReassignment.Value.Type
        };
    }

    private void GenerateWhile(WhileNode whileStatement)
    {
        var conditionLabel = GenName("loop_condition");
        var iterationLabel = GenName("loop_iteration");
        var endLabel = GenName("loop_end");

        _breakLabels.Push(endLabel);
        _continueLabels.Push(conditionLabel);

        _builder.AppendLine($"    jmp @{conditionLabel}");
        _builder.AppendLine($"@{iterationLabel}");
        GenerateBlock(whileStatement.Body);
        _builder.AppendLine($"@{conditionLabel}");
        var result = GenerateExpression(whileStatement.Condition);
        _builder.AppendLine($"    jnz {result}, @{iterationLabel}, @{endLabel}");
        _builder.AppendLine($"@{endLabel}");

        _continueLabels.Pop();
        _breakLabels.Pop();
    }

    private string GenerateExpression(ExpressionNode expression)
    {
        switch (expression)
        {
            case BinaryExpressionNode binaryExpression:
                return GenerateBinaryExpression(binaryExpression);
            case FuncCallExpressionNode funcCallExpression:
                return GenerateExpressionFuncCall(funcCallExpression);
            case IdentifierNode identifier:
                return GenerateIdentifier(identifier);
            case LiteralNode literal:
                return GenerateLiteral(literal);
            case StructInitializerNode structInitializer:
                return GenerateStructInitializer(structInitializer);
            case StructFieldAccessorNode structMemberAccessor:
                return GenerateStructFieldAccessor(structMemberAccessor);
            default:
                throw new ArgumentOutOfRangeException(nameof(expression));
        }
    }

    private string GenerateStructFieldAccessor(StructFieldAccessorNode structFieldAccessor)
    {
        var structType = structFieldAccessor.Struct.Type;
        var structDefinition = _definitions
            .OfType<StructDefinitionNode>()
            .FirstOrDefault(s => s.Name == structType.Name);

        if (structDefinition == null)
        {
            throw new Exception($"Struct {structType.Name} is not defined");
        }

        var @struct = GenerateExpression(structFieldAccessor.Struct);

        var fieldIndex = -1;
        for (var i = 0; i < structDefinition.Fields.Count; i++)
        {
            if (structDefinition.Fields[i].Name == structFieldAccessor.Field)
            {
                fieldIndex = i;
                break;
            }
        }

        if (fieldIndex == -1)
        {
            throw new Exception($"Field {structFieldAccessor.Field} is not defined in struct {structType.Name}");
        }

        var offsetLabel = GenName("offset");
        _builder.AppendLine($"    %{offsetLabel} ={QbeTypeName(structFieldAccessor.Type)} add {@struct}, {fieldIndex * QbeTypeSize(structFieldAccessor.Type)}");

        var outputLabel = GenName("field");
        _builder.AppendLine($"    %{outputLabel} ={QbeTypeName(structFieldAccessor.Type)} load{QbeTypeName(structFieldAccessor.Type)} %{offsetLabel}");

        return $"%{outputLabel}";
    }

    private string GenerateBinaryExpression(BinaryExpressionNode binaryExpression)
    {
        var left = GenerateExpression(binaryExpression.Left);
        var right = GenerateExpression(binaryExpression.Right);
        var outputLabel = GenName();
        
        switch (binaryExpression.Operator)
        {
            case BinaryExpressionOperator.Equal:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w ceql {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w ceqw {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubType.String) && binaryExpression.Right.Type.Equals(NubType.String))
                {
                    _builder.AppendLine($"    %{outputLabel} =w call $nub_strcmp(l {left}, l {right})");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Bool) && binaryExpression.Right.Type.Equals(NubType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w ceqw {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.NotEqual:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cnel {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cnew {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubType.String) && binaryExpression.Right.Type.Equals(NubType.String))
                {
                    _builder.AppendLine($"    %{outputLabel} =w call $nub_strcmp(l {left}, l {right})");
                    _builder.AppendLine($"    %{outputLabel} =w xor %{outputLabel}, 1");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Bool) && binaryExpression.Right.Type.Equals(NubType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cnew {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.GreaterThan:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgtl {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgtw {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Bool) && binaryExpression.Right.Type.Equals(NubType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgtw {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.GreaterThanOrEqual:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgel {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgew {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Bool) && binaryExpression.Right.Type.Equals(NubType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgew {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.LessThan:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csltl {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csltw {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Bool) && binaryExpression.Right.Type.Equals(NubType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csltw {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.LessThanOrEqual:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cslel {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cslew {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Bool) && binaryExpression.Right.Type.Equals(NubType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cslew {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.Plus:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =l add {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w add {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.Minus:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =l sub {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w sub {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.Multiply:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =l mul {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w mul {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            case BinaryExpressionOperator.Divide:
            {
                if (binaryExpression.Left.Type.Equals(NubType.Int64) && binaryExpression.Right.Type.Equals(NubType.Int64))
                {
                    _builder.AppendLine($"    %{outputLabel} =l div {left}, {right}");
                    return $"%{outputLabel}";
                }
                
                if (binaryExpression.Left.Type.Equals(NubType.Int32) && binaryExpression.Right.Type.Equals(NubType.Int32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w div {left}, {right}");
                    return $"%{outputLabel}";
                }
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        throw new NotSupportedException($"Binary operator {binaryExpression.Operator} for types left: {binaryExpression.Left.Type}, right: {binaryExpression.Right.Type} not supported");
    }

    private string GenerateIdentifier(IdentifierNode identifier)
    {
        return _variables[identifier.Identifier].Identifier;
    }

    private string GenerateLiteral(LiteralNode literal)
    {
        if (literal.LiteralType.Equals(NubType.String))
        {
            _strings.Add(literal.Literal);
            return $"$str{_strings.Count}";
        }

        if (literal.LiteralType.Equals(NubType.Int64) || literal.LiteralType.Equals(NubType.Int32))
        {
            return literal.Literal;
        }

        if (literal.LiteralType.Equals(NubType.Bool))
        {
            return bool.Parse(literal.Literal) ? "1" : "0";
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private string GenerateStructInitializer(StructInitializerNode structInitializer)
    {
        var structDefinition = _definitions.OfType<StructDefinitionNode>()
            .FirstOrDefault(s => s.Name == structInitializer.StructType.Name);

        if (structDefinition == null)
        {
            throw new Exception($"Struct {structInitializer.StructType.Name} is not defined");
        }

        var structVar = GenName("struct");

        var size = structDefinition.Fields.Sum(x => QbeTypeSize(x.Type));
        _builder.AppendLine($"    %{structVar} =l alloc8 {size}");

        for (var i = 0; i < structDefinition.Fields.Count; i++)
        {
            var field = structDefinition.Fields[i];

            if (structInitializer.Initializers.TryGetValue(field.Name, out var fieldValue))
            {
                var var = GenerateExpression(fieldValue);
                var offsetLabel = GenName("offset");
                _builder.AppendLine($"    %{offsetLabel} =l add %{structVar}, {i * QbeTypeSize(field.Type)}");
                _builder.AppendLine($"    store{QbeTypeName(field.Type)} {var}, %{offsetLabel}");
            }
            else if (field.Value.HasValue)
            {
                var var = GenerateExpression(field.Value.Value);
                var offsetLabel = GenName("offset");
                _builder.AppendLine($"    %{offsetLabel} =l add %{structVar}, {i * QbeTypeSize(field.Type)}");
                _builder.AppendLine($"    store{QbeTypeName(field.Type)} {var}, %{offsetLabel}");
            }
            else
            {
                throw new Exception($"Field {field.Name} on struct {structInitializer.StructType.Name} is not initialized");
            }
        }

        return $"%{structVar}";
    }

    private string GenerateExpressionFuncCall(FuncCallExpressionNode funcCall)
    {
        var results = new List<(string, NubType)>();
        foreach (var parameter in funcCall.FuncCall.Parameters)
        {
            results.Add((GenerateExpression(parameter), parameter.Type));
        }

        var parameters = results.Select(p => $"{QbeTypeName(p.Item2)} {p.Item1}");

        var output = GenName();
        _builder.AppendLine($"    %{output} ={QbeTypeName(funcCall.Type)} call ${funcCall.FuncCall.Name}({string.Join(", ", parameters)})");

        return $"%{output}";
    }

    private string GenName(string prefix = "var")
    {
        var index = _prefixIndexes.GetValueOrDefault(prefix, 0);
        _prefixIndexes[prefix] = index + 1;
        return $"{prefix}_{index}";
    }

    private class Variable
    {
        public required string Identifier { get; init; }
        public required NubType Type { get; init; }
    }
}