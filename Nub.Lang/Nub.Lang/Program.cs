using Nub.Lang.Backend.Custom;
using Nub.Lang.Frontend.Lexing;
using Nub.Lang.Frontend.Parsing;
using Nub.Lang.Frontend.Typing;

namespace Nub.Lang;

internal static class Program
{
    private static readonly Lexer Lexer = new();
    private static readonly Parser Parser = new();
    
    public static void Main(string[] args)
    {
        var modules = RunFrontend(args[0]);

        var definitions = modules.SelectMany(f => f.Definitions).ToArray();

        var typer = new ExpressionTyper(definitions);
        typer.Populate();

        var generator = new Generator(definitions);
        var asm = generator.Generate();

        Console.WriteLine(asm);

        File.WriteAllText(args[1], asm);
    }

    private static IEnumerable<ModuleNode> RunFrontend(string path)
    {
        List<ModuleNode> modules = [];
        RunFrontend(path, modules);
        return modules;
    }

    private static void RunFrontend(string path, List<ModuleNode> modules)
    {
        var files = Directory.EnumerateFiles(path, "*.nub", SearchOption.TopDirectoryOnly);

        List<Token> tokens = [];
        foreach (var file in files)
        {
            var src = File.ReadAllText(file);
            tokens.AddRange(Lexer.Lex(src));
        }

        var module = Parser.ParseModule(tokens, path);
        modules.Add(module);

        foreach (var import in module.Imports)
        {
            var importPath = Path.GetFullPath(import, module.Path);
            if (modules.All(m => m.Path != importPath))
            {
                RunFrontend(importPath, modules);
            }
        }
    }
}