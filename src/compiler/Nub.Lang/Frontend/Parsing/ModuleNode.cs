namespace Nub.Lang.Frontend.Parsing;

public class ModuleNode(string path, List<string> imports, List<DefinitionNode> definitions) : Node
{
    public string Path { get; } = path;
    public List<string> Imports { get; } = imports;
    public List<DefinitionNode> Definitions { get; } = definitions;
}