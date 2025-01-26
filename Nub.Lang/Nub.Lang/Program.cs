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