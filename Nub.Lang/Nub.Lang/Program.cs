using Nub.Lang.Lexing;

var src = File.ReadAllText(args[0]);

var lexer = new Lexer(src);
var tokens = lexer.Lex();