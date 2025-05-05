namespace Nub.Lang.Frontend.Parsing;

public class StructDefinitionNode(string name, List<StructField> members) : DefinitionNode
{
    public string Name { get; } = name;
    public List<StructField> Members { get; } = members;
}