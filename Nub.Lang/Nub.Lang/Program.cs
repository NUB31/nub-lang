using System.Diagnostics;
using Nub.Lang.Backend.Custom;
using Nub.Lang.Frontend.Lexing;
using Nub.Lang.Frontend.Parsing;
using Nub.Lang.Frontend.Typing;

var rootPath = Path.GetDirectoryName(args[0]);
var rootFileName = Path.GetFileName(args[0]);
Debug.Assert(rootPath != null && rootFileName != null);

Dictionary<string, FileNode> files = [];

Queue<string> queue = [];
queue.Enqueue(rootFileName);

while (queue.TryDequeue(out var path))
{
    var src = File.ReadAllText(Path.Combine(rootPath, path));
    
    var lexer = new Lexer(src);
    var tokens = lexer.Lex();
    
    var parser = new Parser(tokens);
    var file = parser.ParseFile(path);
    files[path] = file;

    foreach (var include in file.Includes)
    {
        if (!files.ContainsKey(include))
        {
            queue.Enqueue(include);
        }
    }
}

foreach (var file in files)
{
    var typer = new ExpressionTyper(file.Value, files);
    typer.Populate();
}

var generator = new Generator(files.Values.SelectMany(f => f.Definitions).ToList());
var asm = generator.Generate();

Console.WriteLine(asm);

File.WriteAllText(args[1], asm);