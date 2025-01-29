namespace Nub.Lang.Frontend.Parsing;

public class ModuleNode(string path, IReadOnlyCollection<string> imports, IReadOnlyCollection<DefinitionNode> definitions) : Node
{
    public string Path { get; } = path;
    public IReadOnlyCollection<string> Imports { get; } = imports;
    public IReadOnlyCollection<DefinitionNode> Definitions { get; } = definitions;
}