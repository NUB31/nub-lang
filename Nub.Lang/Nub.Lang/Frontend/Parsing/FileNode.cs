namespace Nub.Lang.Frontend.Parsing;

public class FileNode(IReadOnlyCollection<string> includes, IReadOnlyCollection<DefinitionNode> definitions) : Node
{
    public IReadOnlyCollection<string> Includes { get; } = includes;
    public IReadOnlyCollection<DefinitionNode> Definitions { get; } = definitions;
}