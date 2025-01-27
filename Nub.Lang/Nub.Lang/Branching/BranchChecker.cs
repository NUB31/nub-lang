using Nub.Lang.Parsing;

namespace Nub.Lang.Branching;

public class BranchChecker
{
    private readonly IReadOnlyCollection<DefinitionNode> _definitions;

    public BranchChecker(IReadOnlyCollection<DefinitionNode> definitions)
    {
        _definitions = definitions;
    }

    public void Check()
    {
        foreach (var funcDefinition in _definitions.OfType<LocalFuncDefinitionNode>())
        {
            if (funcDefinition.ReturnType.HasValue)
            {
                CheckBlock(funcDefinition.Body);
            }
        }
    }

    private void CheckBlock(BlockNode block)
    {
        if (!block.Statements.Any(s => s is ReturnNode))
        {
            throw new Exception("Block must contain a return statement");
        }
    }
}