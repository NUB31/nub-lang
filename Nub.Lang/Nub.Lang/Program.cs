using Nub.Lang.Backend.Custom;
using Nub.Lang.Frontend.Lexing;
using Nub.Lang.Frontend.Parsing;
using Nub.Lang.Frontend.Typing;

var src = File.ReadAllText(args[0]);

var lexer = new Lexer(src);
var tokens = lexer.Lex();

var parser = new Parser(tokens);
var definitions = parser.Parse();

var typer = new ExpressionTyper(definitions);
typer.Populate();

var generator = new CustomGenerator(definitions);
var asm = generator.Generate();

Console.WriteLine(asm);

File.WriteAllText(args[1], asm);