REM ============================================================
REM ANTLR 2.7.6 C# code generation
REM
REM This removes previously generated files first.
REM This is important because stale generated files can affect
REM ANTLR 2.7.6 generation.
REM ============================================================

del /Q ExpressionLexer.cs 2>NUL
del /Q ExpressionParser.cs 2>NUL
del /Q ExpressionParserTokenTypes.cs 2>NUL
del /Q ExpressionParserTokenTypes.txt 2>NUL

antlr-2.7.6.exe Expression.g
