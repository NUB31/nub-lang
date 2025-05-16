namespace Nub.Lang.Frontend.Lexing;

public class ModifierToken(Modifier symbol) : Token
{
    public Modifier Modifier { get; } = symbol;
}

public enum Modifier
{
    Extern,
    Global
}