using System.Globalization;
using System.Text;
using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Backend;

public class Generator
{
    private readonly List<DefinitionNode> _definitions;
    private readonly StringBuilder _builder = new();
    private readonly Dictionary<string, Variable> _variables = new();
    private readonly List<string> _strings = [];
    private readonly Stack<string> _breakLabels = new();
    private readonly Stack<string> _continueLabels = new();
    private int _variableIndex;
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

    private static string SQT(NubType type)
    {
        switch (type)
        {
            case NubPrimitiveType primitiveType:
            {
                switch (primitiveType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                    case PrimitiveTypeKind.String:
                    case PrimitiveTypeKind.Any:
                        return "l";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                    case PrimitiveTypeKind.Bool:
                        return "w";
                    case PrimitiveTypeKind.F64:
                        return "d";
                    case PrimitiveTypeKind.F32:
                        return "s";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            case NubStructType:
            case NubPointerType:
            {
                return "l";
            }
            default:
            {
                throw new NotImplementedException();
            }
        }
    }

    private static string EQT(NubType type)
    {
        switch (type)
        {
            case NubPrimitiveType primitiveType:
            {
                switch (primitiveType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                    case PrimitiveTypeKind.String:
                        return "l";
                    case PrimitiveTypeKind.Any:
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                        return "w";
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                        return "h";
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                        return "b";
                    case PrimitiveTypeKind.Bool:
                    case PrimitiveTypeKind.F64:
                        return "d";
                    case PrimitiveTypeKind.F32:
                        return "s";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            case NubStructType nubCustomType:
            {
                return ":" + nubCustomType.Name;
            }
            case NubPointerType:
            {
                return "l";
            }
            default:
            {
                throw new NotImplementedException();
            }
        }
    }

    private static string FQT(NubType type)
    {
        switch (type)
        {
            case NubPrimitiveType primitiveType:
            {
                switch (primitiveType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                    case PrimitiveTypeKind.String:
                    case PrimitiveTypeKind.Any:
                        return "l";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                        return "w";
                    case PrimitiveTypeKind.I16:
                        return "sh";
                    case PrimitiveTypeKind.U16:
                        return "uh";
                    case PrimitiveTypeKind.I8:
                        return "sb";
                    case PrimitiveTypeKind.U8:
                        return "ub";
                    case PrimitiveTypeKind.Bool:
                        return "b";
                    case PrimitiveTypeKind.F64:
                        return "d";
                    case PrimitiveTypeKind.F32:
                        return "s";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            case NubStructType nubCustomType:
            {
                return ":" + nubCustomType.Name;
            }
            case NubPointerType:
            {
                return "l";
            }
            default:
            {
                throw new NotImplementedException();
            }
        }
    }

    private int QbeTypeSize(NubType type)
    {
        switch (type)
        {
            case NubPrimitiveType primitiveType:
            {
                switch (primitiveType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                    case PrimitiveTypeKind.String:
                    case PrimitiveTypeKind.Any:
                        return 8;
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                        return 4;
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                        return 2;
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                    case PrimitiveTypeKind.Bool:
                        return 1;
                    case PrimitiveTypeKind.F64:
                        return 8;
                    case PrimitiveTypeKind.F32:
                        return 4;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            case NubStructType nubCustomType:
            {
                var definition = _definitions.OfType<StructDefinitionNode>().FirstOrDefault(s => s.Name == nubCustomType.Name);
                if (definition == null)
                {
                    throw new Exception($"Cannot determine size of non-existent type {nubCustomType}");
                }

                return definition.Fields.Sum(f => QbeTypeSize(f.Type));
            }
            case NubPointerType:
            {
                return 8;
            }
            default:
            {
                throw new NotImplementedException();
            }
        }
    }

    private void GenerateFuncDefinition(LocalFuncDefinitionNode node)
    {
        _variables.Clear();

        if (node.Global)
        {
            _builder.Append("export ");
        }

        _builder.Append("function ");
        if (node.ReturnType.HasValue)
        {
            _builder.Append($"{FQT(node.ReturnType.Value)} ");
        }
        else if (!node.ReturnType.HasValue && node.Name == "main")
        {
            _builder.Append("l ");
        }

        _builder.Append('$');
        _builder.Append(node.Name);

        var parameterStrings = node.Parameters.Select(parameter => parameter.Variadic ? "..." : $"{FQT(parameter.Type)} %{parameter.Name}");

        _builder.AppendLine($"({string.Join(", ", parameterStrings)}) {{");
        _builder.AppendLine("@start");

        _builder.AppendLine("    # Variable allocation");
 
        foreach (var parameter in node.Parameters)
        {
            var parameterName = parameter.Name;

            switch (FQT(parameter.Type))
            {
                case "sb":
                    parameterName = GenName();
                    _builder.AppendLine($"    %{parameterName} =w extsb %{parameter.Name}");
                    break;
                case "ub":
                    parameterName = GenName();
                    _builder.AppendLine($"    %{parameterName} =w extub %{parameter.Name}");
                    break;
                case "sh":
                    parameterName = GenName();
                    _builder.AppendLine($"    %{parameterName} =w extsh %{parameter.Name}");
                    break;
                case "uh":
                    parameterName = GenName();
                    _builder.AppendLine($"    %{parameterName} =w extuh %{parameter.Name}");
                    break;
            }

            var pointerLabel = GenName();
            _builder.AppendLine($"    %{pointerLabel} ={SQT(parameter.Type)} alloc8 {QbeTypeSize(parameter.Type)}");
            _builder.AppendLine($"    storel %{parameterName}, %{pointerLabel}");
            
            
            _variables[parameter.Name] = new Variable
            {
                Pointer = $"%{pointerLabel}",
                Type = parameter.Type
            };
        }

        _builder.AppendLine("    # End variable allocation");
        _builder.AppendLine();

        GenerateBlock(node.Body);

        if (node.Body.Statements.LastOrDefault() is not ReturnNode)
        {
            if (!node.ReturnType.HasValue && node.Name == "main")
            {
                _builder.AppendLine();
                _builder.AppendLine("    # Implicit return for main");
                _builder.AppendLine("    ret 0");
            }
            else if (!node.ReturnType.HasValue)
            {
                _builder.AppendLine();
                _builder.AppendLine("    # Implicit return");
                _builder.AppendLine("    ret");
            }
        }

        _builder.AppendLine("}");
    }

    private void GenerateStructDefinition(StructDefinitionNode structDefinition)
    {
        var fields = structDefinition.Fields.Select(f => EQT(f.Type));
        _builder.AppendLine($"type :{structDefinition.Name} = {{ {string.Join(", ", fields)} }}");
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
            case WhileNode whileStatement:
                GenerateWhile(whileStatement);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement));
        }
    }

    private string GenerateFuncCall(FuncCall funcCall)
    {
        var parameterDefinitions = _definitions
            .OfType<LocalFuncDefinitionNode>()
            .FirstOrDefault(d => d.Name == funcCall.Name)
            ?.Parameters;

        parameterDefinitions ??= _definitions
            .OfType<ExternFuncDefinitionNode>()
            .FirstOrDefault(d => d.Name == funcCall.Name)
            ?.Parameters;

        if (parameterDefinitions == null)
        {
            throw new Exception($"Unknown function {funcCall}");
        }

        var parameterStrings = new List<string>();

        for (var i = 0; i < funcCall.Parameters.Count; i++)
        {
            if (i < parameterDefinitions.Count && parameterDefinitions[i].Variadic)
            {
                parameterStrings.Add("...");
            }

            NubType expectedType;
            if (i < parameterDefinitions.Count)
            {
                expectedType = parameterDefinitions[i].Type;
            }
            else if (parameterDefinitions[^1].Variadic)
            {
                expectedType = parameterDefinitions[^1].Type;
            }
            else
            {
                throw new Exception($"Parameters for func {funcCall} does not not match");
            }

            var parameter = funcCall.Parameters[i];
            var result = GenerateExpression(parameter);

            var qbeParameterType = SQT(expectedType.Equals(NubPrimitiveType.Any) ? parameter.Type : expectedType);
            parameterStrings.Add($"{qbeParameterType} {result}");
        }

        return $"call ${funcCall.Name}({string.Join(", ", parameterStrings)})";
    }

    private void GenerateBlock(BlockNode block)
    {
        foreach (var statement in block.Statements.Where(_ => _codeIsReachable))
        {
            GenerateStatement(statement);
        }

        _codeIsReachable = true;
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
        var call = GenerateFuncCall(funcCall.FuncCall);
        _builder.AppendLine($"    {call}");
    }

    private void GenerateIf(IfNode ifStatement)
    {
        var trueLabel = GenName();
        var falseLabel = GenName();
        var endLabel = GenName();

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
        var pointerLabel = GenName();
        _builder.AppendLine($"    %{pointerLabel} ={SQT(variableAssignment.Value.Type)} alloc8 {QbeTypeSize(variableAssignment.Value.Type)}");
        _builder.AppendLine($"    storel {result}, %{pointerLabel}");

        _variables[variableAssignment.Name] = new Variable
        {
            Pointer = $"%{pointerLabel}",
            Type = variableAssignment.Value.Type
        };
    }

    private void GenerateWhile(WhileNode whileStatement)
    {
        var conditionLabel = GenName();
        var iterationLabel = GenName();
        var endLabel = GenName();

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
        return expression switch
        {
            AddressOfNode addressOf => GenerateAddressOf(addressOf),
            BinaryExpressionNode binaryExpression => GenerateBinaryExpression(binaryExpression),
            CastNode cast => GenerateCast(cast),
            DereferenceNode dereference => GenerateDereference(dereference),
            FuncCallExpressionNode funcCallExpression => GenerateExpressionFuncCall(funcCallExpression),
            IdentifierNode identifier => GenerateIdentifier(identifier),
            LiteralNode literal => GenerateLiteral(literal),
            StructInitializerNode structInitializer => GenerateStructInitializer(structInitializer),
            UnaryExpressionNode unaryExpression => GenerateUnaryExpression(unaryExpression),
            StructFieldAccessorNode structMemberAccessor => GenerateStructFieldAccessor(structMemberAccessor),
            _ => throw new ArgumentOutOfRangeException(nameof(expression))
        };
    }

    private string GenerateDereference(DereferenceNode dereference)
    {
        var result = GenerateExpression(dereference.Expression);
        var outputLabel = GenName();
        _builder.AppendLine($"    %{outputLabel} ={SQT(dereference.Type)} load{SQT(dereference.Type)} {result}");
        return $"%{outputLabel}";
    }

    private string GenerateAddressOf(AddressOfNode addressOf)
    {
        var result = GenerateExpression(addressOf.Expression);
        var outputLabel = GenName();
        switch (addressOf.Expression.Type)
        {
            case NubPointerType:
            case NubStructType:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.String }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.I64 }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.F64 }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.U64 }:
                _builder.AppendLine($"    %{outputLabel} =l alloc8 8");
                _builder.AppendLine($"    storel {result}, %{outputLabel}");
                return $"%{outputLabel}";
            case NubPrimitiveType { Kind: PrimitiveTypeKind.I32 }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.U32 }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.I16 }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.U16 }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.I8 }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.U8 }:
            case NubPrimitiveType { Kind: PrimitiveTypeKind.F32 }:
                _builder.AppendLine($"    %{outputLabel} =l alloc8 4");
                _builder.AppendLine($"    storew {result}, %{outputLabel}");
                return $"%{outputLabel}";
            default:
                throw new ArgumentOutOfRangeException();
        }
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
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w ceql {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w ceqw {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.String))
                {
                    _builder.AppendLine($"    %{outputLabel} =w call $nub_strcmp(l {left}, l {right})");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w ceqw {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.NotEqual:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cnel {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cnew {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.String))
                {
                    _builder.AppendLine($"    %{outputLabel} =w call $nub_strcmp(l {left}, l {right})");
                    _builder.AppendLine($"    %{outputLabel} =w xor %{outputLabel}, 1");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cnew {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.GreaterThan:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgtl {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgtw {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgtw {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.GreaterThanOrEqual:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgel {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgew {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csgew {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.LessThan:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csltl {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csltw {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w csltw {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.LessThanOrEqual:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cslel {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cslew {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.Bool))
                {
                    _builder.AppendLine($"    %{outputLabel} =w cslew {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.Plus:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =l add {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w add {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.Minus:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =l sub {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w sub {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.Multiply:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =l mul {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
                {
                    _builder.AppendLine($"    %{outputLabel} =w mul {left}, {right}");
                    return $"%{outputLabel}";
                }

                break;
            }
            case BinaryExpressionOperator.Divide:
            {
                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I64))
                {
                    _builder.AppendLine($"    %{outputLabel} =l div {left}, {right}");
                    return $"%{outputLabel}";
                }

                if (binaryExpression.Left.Type.Equals(NubPrimitiveType.I32))
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

        throw new NotSupportedException($"Binary operator {binaryExpression.Operator} for types {binaryExpression.Left.Type} and {binaryExpression.Right.Type} not supported");
    }

    private string GenerateCast(CastNode cast)
    {
        var input = GenerateExpression(cast.Expression);
        var outputType = cast.TargetType;
        var inputType = cast.Expression.Type;

        if (inputType.Equals(outputType))
        {
            return input;
        }

        if (outputType is not NubPrimitiveType primitiveOutputType || inputType is not NubPrimitiveType primitiveInputType)
        {
            throw new NotSupportedException("Casting is only supported for primitive types");
        }

        var outputLabel = GenName();

        switch (primitiveInputType.Kind)
        {
            case PrimitiveTypeKind.I64:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.Any:
                    case PrimitiveTypeKind.U8:
                        return input;
                    case PrimitiveTypeKind.F64:
                        _builder.AppendLine($"    %{outputLabel} =d sltof {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.F32:
                        _builder.AppendLine($"    %{outputLabel} =s sltof {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.String:
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_i64_to_string(l {input})");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.I32:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                        _builder.AppendLine($"    %{outputLabel} =l extsw {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                        return input;
                    case PrimitiveTypeKind.F64:
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extsw {input}");
                        _builder.AppendLine($"    %{outputLabel} =d sltof %{extLabel}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.F32:
                        _builder.AppendLine($"    %{outputLabel} =s swtof {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.String:
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_i32_to_string(w {input})");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.Any:
                        var extAnyLabel = GenName();
                        _builder.AppendLine($"    %{extAnyLabel} =l extsw {input}");
                        return $"%{extAnyLabel}";
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.I16:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                        _builder.AppendLine($"    %{outputLabel} =l extsh {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                        _builder.AppendLine($"    %{outputLabel} =w extsh {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                        return input;
                    case PrimitiveTypeKind.F64:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extsh {input}");
                        _builder.AppendLine($"    %{outputLabel} =d sltof %{extLabel}");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.F32:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =w extsh {input}");
                        _builder.AppendLine($"    %{outputLabel} =s swtof %{extLabel}");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.String:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =w extsh {input}");
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_i32_to_string(w %{extLabel})");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.Any:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extsh {input}");
                        return $"%{extLabel}";
                    }
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.I8:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                        _builder.AppendLine($"    %{outputLabel} =l extsb {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                        _builder.AppendLine($"    %{outputLabel} =w extsb {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                        return input;
                    case PrimitiveTypeKind.F64:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extsb {input}");
                        _builder.AppendLine($"    %{outputLabel} =d sltof %{extLabel}");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.F32:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =w extsb {input}");
                        _builder.AppendLine($"    %{outputLabel} =s swtof %{extLabel}");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.String:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =w extsb {input}");
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_i32_to_string(w %{extLabel})");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.Any:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extsb {input}");
                        return $"%{extLabel}";
                    }
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.U64:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                        return input;
                    case PrimitiveTypeKind.F64:
                        _builder.AppendLine($"    %{outputLabel} =d ultof {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.F32:
                        _builder.AppendLine($"    %{outputLabel} =s ultof {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.String:
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_u64_to_string(l {input})");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.Any:
                        return input;
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.U32:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                        _builder.AppendLine($"    %{outputLabel} =l extuw {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                        return input;
                    case PrimitiveTypeKind.F64:
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extuw {input}");
                        _builder.AppendLine($"    %{outputLabel} =d ultof %{extLabel}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.F32:
                        _builder.AppendLine($"    %{outputLabel} =s uwtof {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.String:
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_u32_to_string(w {input})");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.Any:
                        var extAnyLabel = GenName();
                        _builder.AppendLine($"    %{extAnyLabel} =l extuw {input}");
                        return $"%{extAnyLabel}";
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.U16:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                        _builder.AppendLine($"    %{outputLabel} =l extuh {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                        _builder.AppendLine($"    %{outputLabel} =w extuh {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                        return input;
                    case PrimitiveTypeKind.F64:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extuh {input}");
                        _builder.AppendLine($"    %{outputLabel} =d ultof %{extLabel}");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.F32:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =w extuh {input}");
                        _builder.AppendLine($"    %{outputLabel} =s uwtof %{extLabel}");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.String:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =w extuh {input}");
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_u32_to_string(w %{extLabel})");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.Any:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extuh {input}");
                        return $"%{extLabel}";
                    }
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.U8:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.U64:
                        _builder.AppendLine($"    %{outputLabel} =l extub {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.U16:
                        _builder.AppendLine($"    %{outputLabel} =w extub {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U8:
                        return input;
                    case PrimitiveTypeKind.F64:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extub {input}");
                        _builder.AppendLine($"    %{outputLabel} =d ultof %{extLabel}");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.F32:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =w extub {input}");
                        _builder.AppendLine($"    %{outputLabel} =s uwtof %{extLabel}");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.String:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =w extub {input}");
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_u32_to_string(w %{extLabel})");
                        return $"%{outputLabel}";
                    }
                    case PrimitiveTypeKind.Any:
                    {
                        var extLabel = GenName();
                        _builder.AppendLine($"    %{extLabel} =l extub {input}");
                        return $"%{extLabel}";
                    }
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.F64:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.F64:
                    case PrimitiveTypeKind.Any:
                        return input;
                    case PrimitiveTypeKind.F32:
                        _builder.AppendLine($"    %{outputLabel} =s dtos {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I64:
                        _builder.AppendLine($"    %{outputLabel} =l dtosi {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.I8:
                        _builder.AppendLine($"    %{outputLabel} =w dtosi {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.U64:
                        _builder.AppendLine($"    %{outputLabel} =l dtoui {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.U8:
                        _builder.AppendLine($"    %{outputLabel} =w dtoui {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.String:
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_f64_to_string(d {input})");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.F32:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.F64:
                        _builder.AppendLine($"    %{outputLabel} =d stod {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.F32:
                        return input;
                    case PrimitiveTypeKind.I64:
                        _builder.AppendLine($"    %{outputLabel} =l stosi {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.I8:
                        _builder.AppendLine($"    %{outputLabel} =w stosi {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.U64:
                        _builder.AppendLine($"    %{outputLabel} =l stoui {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.U8:
                        _builder.AppendLine($"    %{outputLabel} =w stoui {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.String:
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_f32_to_string(s {input})");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.Any:
                        throw new NotImplementedException();
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.Bool:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.Bool:
                        return input;
                    case PrimitiveTypeKind.Any:
                        _builder.AppendLine($"    %{outputLabel} =l extsw {input}");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.String:
                        _builder.AppendLine($"    %{outputLabel} =l call $nub_bool_to_string(s {input})");
                        return $"%{outputLabel}";
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U64:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.U8:
                    case PrimitiveTypeKind.F64:
                    case PrimitiveTypeKind.F32:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.String:
                switch (primitiveOutputType.Kind)
                {
                    case PrimitiveTypeKind.String:
                    case PrimitiveTypeKind.Any:
                        return input;
                    case PrimitiveTypeKind.I64:
                    case PrimitiveTypeKind.I32:
                    case PrimitiveTypeKind.I16:
                    case PrimitiveTypeKind.I8:
                    case PrimitiveTypeKind.U64:
                    case PrimitiveTypeKind.U32:
                    case PrimitiveTypeKind.U16:
                    case PrimitiveTypeKind.U8:
                    case PrimitiveTypeKind.F64:
                    case PrimitiveTypeKind.F32:
                    case PrimitiveTypeKind.Bool:
                    default:
                        throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
                }
            case PrimitiveTypeKind.Any:
                return input;
            default:
                throw new NotSupportedException($"Casting from {primitiveInputType.Kind} to {primitiveOutputType.Kind} is not supported");
        }
    }

    private string GenerateIdentifier(IdentifierNode identifier)
    {
        var variable = _variables[identifier.Identifier];
        var outputLabel = GenName();
        _builder.AppendLine($"    %{outputLabel} ={SQT(identifier.Type)} load{SQT(identifier.Type)} {variable.Pointer}");
        return $"%{outputLabel}";
    }

    private string GenerateLiteral(LiteralNode literal)
    {
        if (literal.LiteralType.Equals(NubPrimitiveType.String))
        {
            _strings.Add(literal.Literal);
            return $"$str{_strings.Count}";
        }

        if (literal.LiteralType.Equals(NubPrimitiveType.I64))
        {
            return literal.Literal;
        }

        if (literal.LiteralType.Equals(NubPrimitiveType.F64))
        {
            var value = double.Parse(literal.Literal, CultureInfo.InvariantCulture);
            var bits = BitConverter.DoubleToInt64Bits(value);
            return bits.ToString();
        }

        if (literal.LiteralType.Equals(NubPrimitiveType.Bool))
        {
            return bool.Parse(literal.Literal) ? "1" : "0";
        }

        throw new NotSupportedException($"Literal {literal.LiteralType} is not supported");
    }

    private string GenerateStructInitializer(StructInitializerNode structInitializer)
    {
        var structDefinition = _definitions.OfType<StructDefinitionNode>().FirstOrDefault(s => s.Name == structInitializer.StructType.Name);
        if (structDefinition == null)
        {
            throw new Exception($"Struct {structInitializer.StructType.Name} is not defined");
        }

        var structVar = GenName();

        var size = structDefinition.Fields.Sum(x => QbeTypeSize(x.Type));
        _builder.AppendLine($"    %{structVar} =l alloc8 {size}");

        for (var i = 0; i < structDefinition.Fields.Count; i++)
        {
            var field = structDefinition.Fields[i];

            if (structInitializer.Initializers.TryGetValue(field.Name, out var fieldValue))
            {
                var var = GenerateExpression(fieldValue);
                var offsetLabel = GenName();
                _builder.AppendLine($"    %{offsetLabel} =l add %{structVar}, {i * QbeTypeSize(field.Type)}");
                _builder.AppendLine($"    store{SQT(field.Type)} {var}, %{offsetLabel}");
            }
            else if (field.Value.HasValue)
            {
                var var = GenerateExpression(field.Value.Value);
                var offsetLabel = GenName();
                _builder.AppendLine($"    %{offsetLabel} =l add %{structVar}, {i * QbeTypeSize(field.Type)}");
                _builder.AppendLine($"    store{SQT(field.Type)} {var}, %{offsetLabel}");
            }
            else
            {
                throw new Exception($"Field {field.Name} on struct {structInitializer.StructType.Name} is not initialized");
            }
        }

        return $"%{structVar}";
    }

    private string GenerateUnaryExpression(UnaryExpressionNode unaryExpression)
    {
        var operand = GenerateExpression(unaryExpression.Operand);
        var outputLabel = GenName();

        switch (unaryExpression.Operator)
        {
            case UnaryExpressionOperator.Negate:
            {
                switch (unaryExpression.Operand.Type)
                {
                    case NubPrimitiveType { Kind: PrimitiveTypeKind.I64 }:
                        _builder.AppendLine($"    %{outputLabel} =l neg {operand}");
                        return $"%{outputLabel}";
                    case NubPrimitiveType { Kind: PrimitiveTypeKind.I32 or PrimitiveTypeKind.I16 or PrimitiveTypeKind.I8 }:
                        _builder.AppendLine($"    %{outputLabel} =w neg {operand}");
                        return $"%{outputLabel}";
                    case NubPrimitiveType { Kind: PrimitiveTypeKind.F64 }:
                        _builder.AppendLine($"    %{outputLabel} =d neg {operand}");
                        return $"%{outputLabel}";
                    case NubPrimitiveType { Kind: PrimitiveTypeKind.F32 }:
                        _builder.AppendLine($"    %{outputLabel} =s neg {operand}");
                        return $"%{outputLabel}";
                }
                
                break;
            }
            case UnaryExpressionOperator.Invert:
            {
                switch (unaryExpression.Operand.Type)
                {
                    case NubPrimitiveType { Kind: PrimitiveTypeKind.Bool }:
                        _builder.AppendLine($"    %{outputLabel} =w xor {operand}, 1");
                        return $"%{outputLabel}";
                }

                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        throw new NotSupportedException($"Unary operator {unaryExpression.Operator} for type {unaryExpression.Operand.Type} not supported");
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

        var offsetLabel = GenName();
        _builder.AppendLine($"    %{offsetLabel} =l add {@struct}, {fieldIndex * QbeTypeSize(structFieldAccessor.Type)}");

        var outputLabel = GenName();
        _builder.AppendLine($"    %{outputLabel} ={SQT(structFieldAccessor.Type)} load{SQT(structFieldAccessor.Type)} %{offsetLabel}");

        return $"%{outputLabel}";
    }

    private string GenerateExpressionFuncCall(FuncCallExpressionNode funcCall)
    {
        var outputLabel = GenName();
        var call = GenerateFuncCall(funcCall.FuncCall);
        _builder.AppendLine($"    %{outputLabel} ={SQT(funcCall.Type)} {call}");
        return $"%{outputLabel}";
    }

    private string GenName()
    {
        return $"v{++_variableIndex}";
    }

    private class Variable
    {
        public required string Pointer { get; init; }
        public required NubType Type { get; init; }
    }
}