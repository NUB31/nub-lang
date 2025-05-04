namespace Nub.Lang.Frontend.Lexing;

public class LiteralToken(NubType type, string value) : Token
{
    public NubType Type { get; } = type;
    public string Value { get; } = value;
}