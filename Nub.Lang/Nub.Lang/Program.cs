using Nub.Lang.Backend.Custom;
using Nub.Lang.Frontend.Lexing;
using Nub.Lang.Frontend.Parsing;
using Nub.Lang.Frontend.Typing;

List<ModuleNode> modules = [];

var lexer = new Lexer();
var parser = new Parser();

Parse(args[0]);

void Parse(string path)
{
    var files = Directory.EnumerateFiles(path, "*.nub", SearchOption.TopDirectoryOnly);

    List<Token> tokens = [];
    foreach (var file in files)
    {
        var src = File.ReadAllText(file);
        tokens.AddRange(lexer.Lex(src));
    }

    var module = parser.ParseModule(tokens, path);
    modules.Add(module);
    
    foreach (var import in module.Imports)
    {
        var importPath = Path.GetFullPath(import, module.Path);
        if (modules.All(m => m.Path != importPath))
        {
            Parse(importPath);
        }
    }
}

foreach (var moduleNode in modules)
{
    Console.WriteLine(moduleNode.Path);
}

var definitions = modules.SelectMany(f => f.Definitions).ToArray();

var typer = new ExpressionTyper(definitions);
typer.Populate();

var generator = new Generator(definitions);
var asm = generator.Generate();

Console.WriteLine(asm);

File.WriteAllText(args[1], asm);