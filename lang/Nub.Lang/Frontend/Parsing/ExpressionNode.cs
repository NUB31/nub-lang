namespace Nub.Lang.Frontend.Parsing;

public abstract class ExpressionNode : Node
{
    private NubType? _type;
    public NubType Type
    {
        get => _type ?? throw new Exception("Tried to access expression type before type was populated");
        set => _type = value;
    }
}