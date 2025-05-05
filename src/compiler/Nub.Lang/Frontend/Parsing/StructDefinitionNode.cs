namespace Nub.Lang.Frontend.Parsing;

public class StructDefinitionNode(string name, List<StructField> fields) : DefinitionNode
{
    public string Name { get; } = name;
    public List<StructField> Fields { get; } = fields;
}