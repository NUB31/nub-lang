using Nub.Lang.Frontend.Parsing;

namespace Nub.Lang.Frontend.Typing;

public class TypeCheckingException : Exception
{
    public TypeCheckingException(string message) : base(message) { }
}

public class TypeChecker
{
    private readonly Dictionary<string, NubType> _variables = new();
    private readonly List<DefinitionNode> _definitions;
    private NubType? _currentFunctionReturnType;
    private bool _hasReturnStatement;

    public TypeChecker(List<DefinitionNode> definitions)
    {
        _definitions = definitions;
    }

    public void TypeCheck()
    {
        foreach (var structDef in _definitions.OfType<StructDefinitionNode>())
        {
            TypeCheckStructDef(structDef);
        }
   
        foreach (var funcDef in _definitions.OfType<LocalFuncDefinitionNode>())
        {
            TypeCheckFuncDef(funcDef);
        }
    }

    private void TypeCheckStructDef(StructDefinitionNode structDef)
    {
        var fields = new Dictionary<string, NubType>();
        foreach (var field in structDef.Fields)
        {
            if (fields.ContainsKey(field.Name))
            {
                throw new TypeCheckingException($"Duplicate field '{field.Name}' in struct '{structDef.Name}'");
            }

            if (field.Value.HasValue)
            {
                if (!TypeCheckExpression(field.Value.Value).Equals(field.Type))
                {
                    throw new TypeCheckingException("Default field initializer does not match the defined type");
                }
            }
            
            fields[field.Name] = field.Type;
        }
    }

    private void TypeCheckFuncDef(LocalFuncDefinitionNode funcDef)
    {
        _variables.Clear();
        _currentFunctionReturnType = funcDef.ReturnType.HasValue ? funcDef.ReturnType.Value : null;
        _hasReturnStatement = false;

        foreach (var param in funcDef.Parameters)
        {
            _variables[param.Name] = param.Type;
        }

        TypeCheckBlock(funcDef.Body);

        if (_currentFunctionReturnType != null && !_hasReturnStatement)
        {
            throw new TypeCheckingException($"Function '{funcDef.Name}' must return a value of type '{_currentFunctionReturnType}'");
        }
    }

    private void TypeCheckBlock(BlockNode block)
    {
        foreach (var statement in block.Statements)
        {
            TypeCheckStatement(statement);
        }
    }

    private void TypeCheckStatement(StatementNode statement)
    {
        switch (statement)
        {
            case VariableAssignmentNode varAssign:
                TypeCheckVariableAssignment(varAssign);
                break;
            case FuncCallStatementNode funcCall:
                TypeCheckFuncCall(funcCall.FuncCall);
                break;
            case IfNode ifNode:
                TypeCheckIf(ifNode);
                break;
            case WhileNode whileNode:
                TypeCheckWhile(whileNode);
                break;
            case ReturnNode returnNode:
                TypeCheckReturn(returnNode);
                break;
            case BreakNode:
            case ContinueNode:
                break;
            default:
                throw new TypeCheckingException($"Unsupported statement type: {statement.GetType().Name}");
        }
    }

    private void TypeCheckVariableAssignment(VariableAssignmentNode varAssign)
    {
        var valueType = TypeCheckExpression(varAssign.Value);
            
        if (varAssign.ExplicitType.HasValue)
        {
            var explicitType = varAssign.ExplicitType.Value;
            if (!AreTypesCompatible(valueType, explicitType))
            {
                throw new TypeCheckingException($"Cannot assign expression of type '{valueType}' to variable '{varAssign.Name}' of type '{explicitType}'");
            }
            _variables[varAssign.Name] = explicitType;
        }
        else
        {
            _variables[varAssign.Name] = valueType;
        }
    }

    private NubType TypeCheckFuncCall(FuncCall funcCall)
    {
        var localFuncDef = _definitions.OfType<LocalFuncDefinitionNode>().FirstOrDefault(f => f.Name == funcCall.Name);
        var externFuncDef = _definitions.OfType<ExternFuncDefinitionNode>().FirstOrDefault(f => f.Name == funcCall.Name);

        List<FuncParameter> parameters;
        Optional<NubType> returnType;
        if (localFuncDef != null)
        {
            parameters = localFuncDef.Parameters;
            returnType = localFuncDef.ReturnType;
        }
        else if (externFuncDef != null)
        {
            parameters = externFuncDef.Parameters;
            returnType = externFuncDef.ReturnType;
        }
        
        else
        {
            throw new TypeCheckingException($"Function '{funcCall.Name}' is not defined");
        }

        if (parameters.Take(parameters.Count - 1).Any(x => x.Variadic))
        {
            throw new TypeCheckingException($"Function '{funcCall.Name}' has multiple variadic parameters");
        }
        
        for (var i = 0; i < funcCall.Parameters.Count; i++)
        {
            var argType = TypeCheckExpression(funcCall.Parameters[i]);

            NubType paramType;
            if (i < parameters.Count)
            {
                paramType = parameters[i].Type;
            }
            else if (parameters.LastOrDefault()?.Variadic ?? false)
            {
                return parameters[^1].Type;
            }
            else
            {
                throw new TypeCheckingException($"Function '{funcCall.Name}' does not take {funcCall.Parameters.Count} parameters");
            }
                
            if (!AreTypesCompatible(argType, paramType))
            {
                throw new TypeCheckingException($"Parameter {i} of function '{funcCall.Name}' expects type '{paramType}', but got '{argType}'");
            }
        }

        return returnType.HasValue ? returnType.Value : NubPrimitiveType.Any;
    }

    private void TypeCheckIf(IfNode ifNode)
    {
        var conditionType = TypeCheckExpression(ifNode.Condition);
        if (!conditionType.Equals(NubPrimitiveType.Bool))
        {
            throw new TypeCheckingException($"If condition must be a boolean expression, got '{conditionType}'");
        }

        TypeCheckBlock(ifNode.Body);

        if (ifNode.Else.HasValue)
        {
            var elseValue = ifNode.Else.Value;
            elseValue.Match(TypeCheckIf, TypeCheckBlock);
        }
    }

    private void TypeCheckWhile(WhileNode whileNode)
    {
        var conditionType = TypeCheckExpression(whileNode.Condition);
        if (!conditionType.Equals(NubPrimitiveType.Bool))
        {
            throw new TypeCheckingException($"While condition must be a boolean expression, got '{conditionType}'");
        }

        TypeCheckBlock(whileNode.Body);
    }

    private void TypeCheckReturn(ReturnNode returnNode)
    {
        _hasReturnStatement = true;

        if (returnNode.Value.HasValue)
        {
            var returnType = TypeCheckExpression(returnNode.Value.Value);
                
            if (_currentFunctionReturnType == null)
            {
                throw new TypeCheckingException("Cannot return a value from a function with no return type");
            }
                
            if (!AreTypesCompatible(returnType, _currentFunctionReturnType))
            {
                throw new TypeCheckingException($"Return value of type '{returnType}' is not compatible with function return type '{_currentFunctionReturnType}'");
            }
        }
        else if (_currentFunctionReturnType != null)
        {
            throw new TypeCheckingException($"Function must return a value of type '{_currentFunctionReturnType}'");
        }
    }

    private NubType TypeCheckExpression(ExpressionNode expression)
    {
        NubType resultType;

        switch (expression)
        {
            case LiteralNode literal:
                resultType = literal.LiteralType;
                break;
            case IdentifierNode identifier:
                if (!_variables.TryGetValue(identifier.Identifier, out var varType))
                {
                    throw new TypeCheckingException($"Variable '{identifier.Identifier}' is not defined");
                }
                resultType = varType;
                break;
            case BinaryExpressionNode binaryExpr:
                resultType = TypeCheckBinaryExpression(binaryExpr);
                break;
            case CastNode cast:
                resultType = TypeCheckCast(cast);
                break;
            case FuncCallExpressionNode funcCallExpr:
                resultType = TypeCheckFuncCall(funcCallExpr.FuncCall);
                break;
            case StructInitializerNode structInit:
                resultType = TypeCheckStructInitializer(structInit);
                break;
            case StructFieldAccessorNode fieldAccess:
                resultType = TypeCheckStructFieldAccess(fieldAccess);
                break;
            default:
                throw new TypeCheckingException($"Unsupported expression type: {expression.GetType().Name}");
        }

        expression.Type = resultType;
        return resultType;
    }

    private NubType TypeCheckBinaryExpression(BinaryExpressionNode binaryExpr)
    {
        var leftType = TypeCheckExpression(binaryExpr.Left);
        var rightType = TypeCheckExpression(binaryExpr.Right);

        if (!leftType.Equals(rightType))
        {
            throw new TypeCheckingException($"Left '{leftType}' and right '{rightType}' side of the binary expression is not equal");
        }

        switch (binaryExpr.Operator)
        {
            case BinaryExpressionOperator.Equal:
            case BinaryExpressionOperator.NotEqual:
                return NubPrimitiveType.Bool;
            case BinaryExpressionOperator.GreaterThan:
            case BinaryExpressionOperator.GreaterThanOrEqual:
            case BinaryExpressionOperator.LessThan:
            case BinaryExpressionOperator.LessThanOrEqual:
                if (!IsNumeric(leftType))
                {
                    throw new TypeCheckingException($"Comparison operators require numeric operands, got '{leftType}' and '{rightType}'");
                }
                return NubPrimitiveType.Bool;
            case BinaryExpressionOperator.Plus:
            case BinaryExpressionOperator.Minus:
            case BinaryExpressionOperator.Multiply:
            case BinaryExpressionOperator.Divide:
                if (!IsNumeric(leftType))
                {
                    throw new TypeCheckingException($"Arithmetic operators require numeric operands, got '{leftType}' and '{rightType}'");
                }
                return leftType;
            default:
                throw new TypeCheckingException($"Unsupported binary operator: {binaryExpr.Operator}");
        }
    }

    private NubType TypeCheckCast(CastNode cast)
    {
        TypeCheckExpression(cast.Expression);
        // TODO: Check if castable
        return cast.TargetType;
    }

    private NubType TypeCheckStructInitializer(StructInitializerNode structInit)
    {
        var initialized = new HashSet<string>();
        
        var structType = structInit.StructType;
        if (structType is not NubStructType customType)
        {
            throw new TypeCheckingException($"Type '{structType}' is not a struct type");
        }

        var definition = _definitions.OfType<StructDefinitionNode>().FirstOrDefault(s => s.Name == structInit.StructType.Name);
        if (definition == null)
        {
            throw new TypeCheckingException($"Struct type '{customType.Name}' is not defined");
        }

        foreach (var initializer in structInit.Initializers)
        {
            var definitionField = definition.Fields.FirstOrDefault(f => f.Name == initializer.Key);
            if (definitionField == null)
            {
                throw new TypeCheckingException($"Field '{initializer.Key}' does not exist in struct '{customType.Name}'");
            }

            var initializerType = TypeCheckExpression(initializer.Value);
            if (!AreTypesCompatible(initializerType, definitionField.Type))
            {
                throw new TypeCheckingException($"Cannot initialize field '{initializer.Key}' of type '{definitionField.Type}' with expression of type '{initializerType}'");
            }
            
            initialized.Add(initializer.Key);
        }
        
        foreach (var field in definition.Fields.Where(f => f.Value.HasValue))
        {
            initialized.Add(field.Name);
        }

        foreach (var field in definition.Fields)
        {
            if (!initialized.Contains(field.Name))
            {
                throw new TypeCheckingException($"Struct field '{field.Name}' is not initialized on type '{customType.Name}'");
            }
        }

        return structType;
    }

    private NubType TypeCheckStructFieldAccess(StructFieldAccessorNode fieldAccess)
    {
        var structType = TypeCheckExpression(fieldAccess.Struct);
        if (structType is not NubStructType customType)
        {
            throw new TypeCheckingException($"Cannot access field '{fieldAccess.Field}' on non-struct type '{structType}'");
        }

        var definition = _definitions.OfType<StructDefinitionNode>().FirstOrDefault(s => s.Name == customType.Name);
        if (definition == null)
        {
            throw new TypeCheckingException($"Struct type '{customType.Name}' is not defined");
        }

        var field = definition.Fields.FirstOrDefault(f => f.Name == fieldAccess.Field);
        if (field == null)
        {
            throw new TypeCheckingException($"Field '{fieldAccess.Field}' does not exist in struct '{customType.Name}'");
        }

        return field.Type;
    }

    private static bool AreTypesCompatible(NubType sourceType, NubType targetType)
    {
        return targetType.Equals(NubPrimitiveType.Any) || sourceType.Equals(targetType);
    }

    private static bool IsNumeric(NubType type)
    {
        if (type is not NubPrimitiveType primitiveType)
        {
            return false;
        }

        switch (primitiveType.Kind)
        {
            case PrimitiveTypeKind.I8:
            case PrimitiveTypeKind.I16:
            case PrimitiveTypeKind.I32:
            case PrimitiveTypeKind.I64:
            case PrimitiveTypeKind.U8:
            case PrimitiveTypeKind.U16:
            case PrimitiveTypeKind.U32:
            case PrimitiveTypeKind.U64:
            case PrimitiveTypeKind.F32:
            case PrimitiveTypeKind.F64:
                return true;
            default:
                return false;
        }
    }
}