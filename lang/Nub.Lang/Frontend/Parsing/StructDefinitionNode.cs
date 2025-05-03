namespace Nub.Lang.Frontend.Parsing;

public class StructDefinitionNode(string name, List<StructMember> members) : DefinitionNode
{
    public string Name { get; } = name;
    public List<StructMember> Members { get; } = members;
}