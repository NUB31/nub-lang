using Nub.Lang.Branching;
using Nub.Lang.Generation;
using Nub.Lang.Lexing;
using Nub.Lang.Parsing;
using Nub.Lang.Typing;

var src = File.ReadAllText(args[0]);

var lexer = new Lexer(src);
var tokens = lexer.Lex();

var parser = new Parser(tokens);
var definitions = parser.Parse();

var typer = new ExpressionTyper(definitions);
typer.Populate();

var branchChecker = new BranchChecker(definitions);
branchChecker.Check();

var generator = new Generator(definitions);
var asm = generator.Generate();

Console.WriteLine(asm);

File.WriteAllText(args[1], asm);