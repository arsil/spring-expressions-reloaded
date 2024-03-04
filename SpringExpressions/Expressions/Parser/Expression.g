options
{
	language = "CSharp";
	namespace = "SpringExpressions.Parser";
}

class ExpressionParser extends Parser;

options {
	codeGenMakeSwitchThreshold = 3;
	codeGenBitsetTestThreshold = 4;
	classHeaderPrefix = "internal"; 
	buildAST=true;
	ASTLabelType = "SpringExpressions.SpringAST";
	k = 2;
}

tokens {
	EXPR;
	OPERAND;
	FALSE = "false";
	TRUE = "true";
	AND = "and";
	OR = "or";
	XOR = "xor";
	IN = "in";
	IS = "is";
	BETWEEN = "between";
	LIKE = "like";
	MATCHES = "matches";
	NULL_LITERAL = "null";
	TYPEOF = "typeof";
	AS = "as";
}

{
    // CLOVER:OFF
    
    public override void reportError(RecognitionException ex)
    {
		//base.reportError(ex);
        throw new antlr.TokenStreamRecognitionException(ex);
    }

    public override void reportError(string error)
    {
		//base.reportError(error);
        throw new RecognitionException(error);
    }
    
    private string GetRelationalOperatorNodeType(string op)
    {
        switch (op)
        {
            case "==" : return "SpringExpressions.OpEqual";
            case "!=" : return "SpringExpressions.OpNotEqual";
            case "<" : return "SpringExpressions.OpLess";
            case "<=" : return "SpringExpressions.OpLessOrEqual";
            case ">" : return "SpringExpressions.OpGreater";
            case ">=" : return "SpringExpressions.OpGreaterOrEqual";
            case "in" : return "SpringExpressions.OpIn";
            case "is" : return "SpringExpressions.OpIs";
            case "between" : return "SpringExpressions.OpBetween";
            case "like" : return "SpringExpressions.OpLike";
            case "matches" : return "SpringExpressions.OpMatches";
            default : 
                throw new ArgumentException("Node type for operator '" + op + "' is not defined.");
        }
    }
}

expr : expression EOF!;

exprList 
    : LPAREN! expression (SEMI! expression)+ RPAREN!
        { #exprList = #([EXPR,"expressionList","SpringExpressions.ExpressionListNode"], #exprList); }
    ;

expression	:	logicalOrExpression 
				(
					(ASSIGN^ <AST = SpringExpressions.AssignNode> logicalOrExpression) 
				|   (DEFAULT^ <AST = SpringExpressions.DefaultNode> logicalOrExpression) 
				|	(QMARK^ <AST = SpringExpressions.TernaryNode> expression COLON! expression)
				)?
			;
			
parenExpr
    : LPAREN! expression RPAREN!;
    
logicalOrExpression : logicalXorExpression (OR^ <AST = SpringExpressions.OpOR> logicalXorExpression)* ;

logicalXorExpression : logicalAndExpression (XOR^ <AST = SpringExpressions.OpXOR> logicalAndExpression)* ;
                        
logicalAndExpression : relationalExpression (AND^ <AST = SpringExpressions.OpAND> relationalExpression)* ;                        

relationalExpression
    :   e1:sumExpr 
          (op:relationalOperator! e2:sumExpr
            {#relationalExpression = #(#[EXPR, op_AST.getText(), GetRelationalOperatorNodeType(op_AST.getText())], #relationalExpression);} 
          )?
    ;

sumExpr  : prodExpr (
                        (PLUS^ <AST = SpringExpressions.OpADD> 
                        | MINUS^ <AST = SpringExpressions.OpSUBTRACT>) prodExpr)* ; 

prodExpr : powExpr (
                        (STAR^ <AST = SpringExpressions.OpMULTIPLY> 
                        | DIV^ <AST = SpringExpressions.OpDIVIDE> 
                        | MOD^ <AST = SpringExpressions.OpMODULUS>) powExpr)* ;

powExpr  : postCastUnaryExpression (POWER^ <AST = SpringExpressions.OpPOWER> postCastUnaryExpression)? ;

postCastUnaryExpression : unaryExpression (AS! TYPE! pcn:name! RPAREN!
	{ #postCastUnaryExpression = #([EXPR, pcn_AST.getText(), "SpringExpressions.CastNode"], #postCastUnaryExpression); })?
	;


unaryExpression 
	:	(PLUS^ <AST = SpringExpressions.OpUnaryPlus> 
	    | MINUS^ <AST = SpringExpressions.OpUnaryMinus> 
	    | BANG^ <AST = SpringExpressions.OpNOT>) unaryExpression
	|	primaryExpression
	;




unaryOperator
	: PLUS | MINUS | BANG
    ;
    
primaryExpression : startNode (node)?
			{ #primaryExpression = #([EXPR,"expression","SpringExpressions.Expression"], #primaryExpression); };

startNode 
    : 
    (   (LPAREN expression SEMI) => exprList 
    |   parenExpr
    |   methodOrProperty 
    |   functionOrVar 
    |   localFunctionOrVar
    |   reference
    |   indexer 
    |   literal 
    |   type 
    |   constructor
    |   projection 
    |   selection 
    |   firstSelection 
    |   lastSelection 
    |   listInitializer
    |   mapInitializer
    |   lambda
    |   attribute
    )
    ;

node : 
    (   methodOrProperty 
    |   indexer 
    |   projection 
    |   selection 
    |   firstSelection 
    |   lastSelection 
    |   exprList
    |   DOT! 
    )+
    ;

functionOrVar 
    : (POUND ID LPAREN) => function
    | var
    ;

function : POUND! ID^ <AST = SpringExpressions.FunctionNode> methodArgs
    ;
    
var : POUND! ID^ <AST = SpringExpressions.VariableNode>;

localFunctionOrVar 
    : (DOLLAR ID LPAREN) => localFunction
    | localVar
    ;

localFunction 
    : DOLLAR! ID^ <AST = SpringExpressions.LocalFunctionNode> methodArgs
    ;

localVar 
    : DOLLAR! ID^ <AST = SpringExpressions.LocalVariableNode>
    ;

methodOrProperty
	: (ID LPAREN)=> ID^ <AST = SpringExpressions.MethodNode> methodArgs
	| property
	;

methodArgs
	:  LPAREN! (argument (COMMA! argument)*)? RPAREN!
	;

property
    :  ID <AST = SpringExpressions.PropertyOrFieldNode>
    ;

reference
	:  (AT! LPAREN! quotableName COLON) =>
		AT! LPAREN! cn:quotableName! COLON! id:quotableName! RPAREN!
		{ #reference = #([EXPR, "ref", "SpringContext.Support.ReferenceNode"], #cn, #id); }

	|  AT! LPAREN! localid:quotableName! RPAREN!
       { #reference = #([EXPR, "ref", "SpringContext.Support.ReferenceNode"], null, #localid); }
	;

indexer
	:  LBRACKET^ <AST = SpringExpressions.IndexerNode> argument (COMMA! argument)* RBRACKET!
	;

projection
	:	
		PROJECT^ <AST = SpringExpressions.ProjectionNode> expression RCURLY!
	;

selection
	:	
		SELECT^ <AST = SpringExpressions.SelectionNode> expression (COMMA! expression)* RCURLY!
	;

firstSelection
	:	
		SELECT_FIRST^ <AST = SpringExpressions.SelectionFirstNode> expression RCURLY!
	;

lastSelection
	:	
		SELECT_LAST^ <AST = SpringExpressions.SelectionLastNode> expression RCURLY!
	;


type
    :   type_T
    |   type_of
    ;


type_T
	:   TYPE! tn:name! RPAREN!
             { #type_T = #([EXPR, tn_AST.getText(), "SpringExpressions.TypeNode"], #type_T); } 
    ;

type_of
    :   TYPEOF! LPAREN! to:name! RPAREN!
             { #type_of = #([EXPR, to_AST.getText(), "SpringExpressions.TypeNode"], #type_of); } 
    ;

name
	:	ID^ <AST = SpringExpressions.QualifiedIdentifier> (~(RPAREN|COLON|QUOTE))*
	;


quotableName
    :	STRING_LITERAL^ <AST = SpringExpressions.QualifiedIdentifier>
    |	name
    ;
    
attribute
	:	AT! LBRACKET! tn:qualifiedId! (ctorArgs)? RBRACKET!
		   { #attribute = #([EXPR, tn_AST.getText(), "SpringExpressions.AttributeNode"], #attribute); }
	;

lambda
    :   LAMBDA! (argList)? PIPE! expression RCURLY!
		   { #lambda = #([EXPR, "lambda", "SpringExpressions.LambdaExpressionNode"], #lambda); }
	;

argList : (ID (COMMA! ID)*)
		   { #argList = #([EXPR, "args"], #argList); }
	;

constructor
	:	("new" qualifiedId LPAREN) => "new"! type:qualifiedId! ctorArgs
		   { #constructor = #([EXPR, type_AST.getText(), "SpringExpressions.ConstructorNode"], #constructor); }
	|   arrayConstructor
	;

arrayConstructor
	:	 "new"! type:qualifiedId! arrayRank (listInitializer)?
	       { #arrayConstructor = #([EXPR, type_AST.getText(), "SpringExpressions.ArrayConstructorNode"], #arrayConstructor); }
	;
    
arrayRank
    :   LBRACKET^ (expression (COMMA! expression)*)? RBRACKET!
    ;

listInitializer
    :   LCURLY^ <AST = SpringExpressions.ListInitializerNode> expression (COMMA! expression)* RCURLY!
    ;

mapInitializer
    :   POUND! LCURLY^ <AST = SpringExpressions.MapInitializerNode> mapEntry (COMMA! mapEntry)* RCURLY!
    ;
      
mapEntry
    :   expression COLON! expression
          { #mapEntry = #([EXPR, "entry", "SpringExpressions.MapEntryNode"], #mapEntry); }
    ;
     
ctorArgs : LPAREN! (namedArgument (COMMA! namedArgument)*)? RPAREN!;
            
argument : expression;

namedArgument 
    :   (ID ASSIGN) => ID^ <AST = SpringExpressions.NamedArgumentNode> ASSIGN! expression 
    |   argument 
    ;

qualifiedId 
	: ID^ <AST = SpringExpressions.QualifiedIdentifier> (DOT ID)*
    ;
    
literal
	:	NULL_LITERAL <AST = SpringExpressions.NullLiteralNode>
	|   INTEGER_LITERAL <AST = SpringExpressions.IntLiteralNode>
	|   HEXADECIMAL_INTEGER_LITERAL <AST = SpringExpressions.HexLiteralNode>
	|   REAL_LITERAL <AST = SpringExpressions.RealLiteralNode>
	|	STRING_LITERAL <AST = SpringExpressions.StringLiteralNode>
	|   boolLiteral
	;

boolLiteral
    :   TRUE <AST = SpringExpressions.BooleanLiteralNode>
    |   FALSE <AST = SpringExpressions.BooleanLiteralNode>
    ;
    
relationalOperator
    :   EQUAL 
    |   NOT_EQUAL
    |   LESS_THAN
    |   LESS_THAN_OR_EQUAL      
    |   GREATER_THAN            
    |   GREATER_THAN_OR_EQUAL 
    |   IN   
    |   IS   
    |   BETWEEN   
    |   LIKE   
    |   MATCHES   
    ; 
    


class ExpressionLexer extends Lexer;

options {
    charVocabulary = '\u0000' .. '\uFFFE'; 
	classHeaderPrefix = "internal"; 
	k = 2;
	testLiterals = false;
}

{
    // CLOVER:OFF
}

WS	:	(' '
	|	'\t'
	|	'\n'
	|	'\r')
		{ _ttype = Token.SKIP; }
	;

AT: '@'
  ;

BACKTICK: '`'
  ;
  
BACKSLASH: '\\'
  ;
    
PIPE: '|'
  ;

BANG: '!'
  ;

QMARK: '?'
  ;

DOLLAR: '$'
  ;

POUND: '#'
  ;
    
LPAREN:	'('
	;

RPAREN:	')'
	;

LBRACKET:	'['
	;

RBRACKET:	']'
	;

LCURLY:	'{'
	;

RCURLY:	'}'
	;

COMMA : ','
   ;

SEMI: ';'
  ;

COLON: ':'
  ;

ASSIGN: '='
  ;

DEFAULT: "??"
  ;
  
PLUS: '+'
  ;

MINUS: '-'
  ;
   
DIV: '/'
  ;

STAR: '*'
  ;

MOD: '%'
  ;

POWER: '^'
  ;
  
EQUAL: "=="
  ;

NOT_EQUAL: "!="
  ;

LESS_THAN: '<'
  ;

LESS_THAN_OR_EQUAL: "<="
  ;

GREATER_THAN: '>'
  ;

GREATER_THAN_OR_EQUAL: ">="
  ;

PROJECT: "!{"
  ;
  
SELECT: "?{"
  ;

SELECT_FIRST: "^{"
  ;
  
SELECT_LAST: "${"
  ;

TYPE: "T("
  ;

LAMBDA: "{|"
  ;

DOT_ESCAPED: "\\."
  ;
  
QUOTE: '\''
  ;


STRING_LITERAL
	:	QUOTE! (APOS|~'\'')* QUOTE!
	;

protected
APOS : QUOTE! QUOTE
    ;
  
ID
options {
	testLiterals = true;
}
	: ('a'..'z'|'A'..'Z'|'_') ('a'..'z'|'A'..'Z'|'_'|'0'..'9')*
	;

NUMERIC_LITERAL

	// real
	:	('.' DECIMAL_DIGIT) =>
		 '.' (DECIMAL_DIGIT)+ (EXPONENT_PART)? (REAL_TYPE_SUFFIX)?
		{$setType(REAL_LITERAL);}
			
	|	((DECIMAL_DIGIT)+ '.' DECIMAL_DIGIT) =>
		 (DECIMAL_DIGIT)+ '.' (DECIMAL_DIGIT)+ (EXPONENT_PART)? (REAL_TYPE_SUFFIX)?
		{$setType(REAL_LITERAL);}
		
	|	((DECIMAL_DIGIT)+ (EXPONENT_PART)) =>
		 (DECIMAL_DIGIT)+ (EXPONENT_PART) (REAL_TYPE_SUFFIX)?
		{$setType(REAL_LITERAL);}
		
	|	((DECIMAL_DIGIT)+ (REAL_TYPE_SUFFIX)) =>
		 (DECIMAL_DIGIT)+ (REAL_TYPE_SUFFIX)		
		{$setType(REAL_LITERAL);}
		 
	// integer
	|	 (DECIMAL_DIGIT)+ (INTEGER_TYPE_SUFFIX)?	
		{$setType(INTEGER_LITERAL);}
	
	// just a dot
	| '.'{$setType(DOT);}
	;

	
HEXADECIMAL_INTEGER_LITERAL
	:	"0x"   (HEX_DIGIT)+   (INTEGER_TYPE_SUFFIX)?
	;

protected
DECIMAL_DIGIT
	: 	'0'..'9'
	;
	
protected	
INTEGER_TYPE_SUFFIX
	:
	(	options {generateAmbigWarnings=false;}
		:	"UL"	| "LU" 	| "ul"	| "lu"
		|	"UL"	| "LU" 	| "uL"	| "lU"
		|	"U"		| "L"	| "u"	| "l"
	)
	;
		
protected
HEX_DIGIT
	:	'0' | '1' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | 
		'A' | 'B' | 'C' | 'D' | 'E' | 'F'  |
		'a' | 'b' | 'c' | 'd' | 'e' | 'f'
	;	
	
protected	
EXPONENT_PART
	:	"e"  (SIGN)*  (DECIMAL_DIGIT)+
	|	"E"  (SIGN)*  (DECIMAL_DIGIT)+
	;	
	
protected
SIGN
	:	'+' | '-'
	;
	
protected
REAL_TYPE_SUFFIX
	: 'F' | 'f' | 'D' | 'd' | 'M' | 'm'
	;
