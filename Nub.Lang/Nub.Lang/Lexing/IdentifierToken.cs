namespace Nub.Lang.Lexing;

public class IdentifierToken(string value) : Token
{
    public string Value { get; } = value;
}