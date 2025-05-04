using Nub.Lang.Backend;
using Nub.Lang.Frontend.Lexing;
using Nub.Lang.Frontend.Parsing;
using Nub.Lang.Frontend.Typing;

namespace Nub.Lang;

internal static class Program
{
    private static readonly Lexer Lexer = new();
    private static readonly Parser Parser = new();
    
    public static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage:       nub <input-dir> <output-file>");
            Console.WriteLine("Example:     nub src out.asm");
            return 1;
        }
        
        var input = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]);
        
        if (!Directory.Exists(input))
        {
            Console.WriteLine($"Error: Input directory '{input}' does not exist.");
            return 1;
        }
        
        var outputDir = Path.GetDirectoryName(output);
        if (outputDir == null || !Directory.Exists(outputDir))
        {
            Console.WriteLine($"Error: Output directory '{outputDir}' does not exist.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(Path.GetFileName(output)))
        {
            Console.WriteLine("Error: Output path must specify a file, not a directory.");
            return 1;
        }

        var modules = RunFrontend(input);
        var definitions = modules.SelectMany(f => f.Definitions).ToList();

        var typer = new ExpressionTyper(definitions);
        typer.Populate();

        var generator = new Generator(definitions);
        var asm = generator.Generate();

        File.WriteAllText(output, asm);
        return 0;
    }

    private static List<ModuleNode> RunFrontend(string path)
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