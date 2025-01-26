namespace Nub.Lang.Parsing;

public abstract class ExpressionNode : Node
{
    private Type? _type;
    public Type Type
    {
        get => _type ?? throw new Exception("Tried to access expression type before type was populated");
        set => _type = value;
    }
}