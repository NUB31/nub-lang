namespace Nub.Lang.Frontend.Lexing;

public class IdentifierToken(string value) : Token
{
    public string Value { get; } = value;
}