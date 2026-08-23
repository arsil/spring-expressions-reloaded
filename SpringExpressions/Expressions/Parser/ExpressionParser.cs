// $ANTLR 2.7.6 (2005-12-22): "Expression.g" -> "ExpressionParser.cs"$

namespace SpringExpressions.Parser
{
	// Generate the header common to all output files.
	using System;
	
	using TokenBuffer              = antlr.TokenBuffer;
	using TokenStreamException     = antlr.TokenStreamException;
	using TokenStreamIOException   = antlr.TokenStreamIOException;
	using ANTLRException           = antlr.ANTLRException;
	using LLkParser = antlr.LLkParser;
	using Token                    = antlr.Token;
	using IToken                   = antlr.IToken;
	using TokenStream              = antlr.TokenStream;
	using RecognitionException     = antlr.RecognitionException;
	using NoViableAltException     = antlr.NoViableAltException;
	using MismatchedTokenException = antlr.MismatchedTokenException;
	using SemanticException        = antlr.SemanticException;
	using ParserSharedInputState   = antlr.ParserSharedInputState;
	using BitSet                   = antlr.collections.impl.BitSet;
	using AST                      = antlr.collections.AST;
	using ASTPair                  = antlr.ASTPair;
	using ASTFactory               = antlr.ASTFactory;
	using ASTArray                 = antlr.collections.impl.ASTArray;
	
	internal 	class ExpressionParser : antlr.LLkParser
	{
		public const int EOF = 1;
		public const int NULL_TREE_LOOKAHEAD = 3;
		public const int EXPR = 4;
		public const int OPERAND = 5;
		public const int FALSE = 6;
		public const int TRUE = 7;
		public const int AND = 8;
		public const int OR = 9;
		public const int XOR = 10;
		public const int IN = 11;
		public const int IS = 12;
		public const int BETWEEN = 13;
		public const int LIKE = 14;
		public const int MATCHES = 15;
		public const int NULL_LITERAL = 16;
		public const int TYPEOF = 17;
		public const int AS = 18;
		public const int LPAREN = 19;
		public const int SEMI = 20;
		public const int RPAREN = 21;
		public const int ASSIGN = 22;
		public const int DEFAULT = 23;
		public const int QMARK = 24;
		public const int COLON = 25;
		public const int PLUS = 26;
		public const int MINUS = 27;
		public const int STAR = 28;
		public const int DIV = 29;
		public const int MOD = 30;
		public const int POWER = 31;
		public const int LESS_THAN = 32;
		public const int GREATER_THAN = 33;
		public const int TYPE = 34;
		public const int ID = 35;
		public const int DOT = 36;
		public const int LBRACKET = 37;
		public const int COMMA = 38;
		public const int RBRACKET = 39;
		public const int BANG = 40;
		public const int SAFE_DOT = 41;
		public const int SAFE_LBRACKET = 42;
		public const int POUND = 43;
		public const int DOLLAR = 44;
		public const int AT = 45;
		public const int PROJECT = 46;
		public const int RCURLY = 47;
		public const int SELECT = 48;
		public const int SELECT_FIRST = 49;
		public const int SELECT_LAST = 50;
		public const int QUOTE = 51;
		public const int STRING_LITERAL = 52;
		public const int LAMBDA = 53;
		public const int PIPE = 54;
		public const int LITERAL_new = 55;
		public const int LCURLY = 56;
		public const int INTEGER_LITERAL = 57;
		public const int HEXADECIMAL_INTEGER_LITERAL = 58;
		public const int REAL_LITERAL = 59;
		public const int EQUAL = 60;
		public const int NOT_EQUAL = 61;
		public const int LESS_THAN_OR_EQUAL = 62;
		public const int GREATER_THAN_OR_EQUAL = 63;
		public const int WS = 64;
		public const int BACKTICK = 65;
		public const int BACKSLASH = 66;
		public const int DOT_ESCAPED = 67;
		public const int APOS = 68;
		public const int NUMERIC_LITERAL = 69;
		public const int DECIMAL_DIGIT = 70;
		public const int INTEGER_TYPE_SUFFIX = 71;
		public const int HEX_DIGIT = 72;
		public const int EXPONENT_PART = 73;
		public const int SIGN = 74;
		public const int REAL_TYPE_SUFFIX = 75;
		
		
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
		
		protected void initialize()
		{
			tokenNames = tokenNames_;
			initializeFactory();
		}
		
		
		protected ExpressionParser(TokenBuffer tokenBuf, int k) : base(tokenBuf, k)
		{
			initialize();
		}
		
		public ExpressionParser(TokenBuffer tokenBuf) : this(tokenBuf,2)
		{
		}
		
		protected ExpressionParser(TokenStream lexer, int k) : base(lexer,k)
		{
			initialize();
		}
		
		public ExpressionParser(TokenStream lexer) : this(lexer,2)
		{
		}
		
		public ExpressionParser(ParserSharedInputState state) : base(state,2)
		{
			initialize();
		}
		
	public void expr() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST expr_AST = null;
		
		try {      // for error handling
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			match(Token.EOF_TYPE);
			expr_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_0_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = expr_AST;
	}
	
	public void expression() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST expression_AST = null;
		
		try {      // for error handling
			logicalOrExpression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{
				switch ( LA(1) )
				{
				case ASSIGN:
				{
					{
						SpringExpressions.AssignNode tmp2_AST = null;
						tmp2_AST = (SpringExpressions.AssignNode) astFactory.create(LT(1), "SpringExpressions.AssignNode");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp2_AST);
						match(ASSIGN);
						logicalOrExpression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					break;
				}
				case DEFAULT:
				{
					{
						SpringExpressions.DefaultNode tmp3_AST = null;
						tmp3_AST = (SpringExpressions.DefaultNode) astFactory.create(LT(1), "SpringExpressions.DefaultNode");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp3_AST);
						match(DEFAULT);
						logicalOrExpression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					break;
				}
				case QMARK:
				{
					{
						SpringExpressions.TernaryNode tmp4_AST = null;
						tmp4_AST = (SpringExpressions.TernaryNode) astFactory.create(LT(1), "SpringExpressions.TernaryNode");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp4_AST);
						match(QMARK);
						expression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						match(COLON);
						expression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					break;
				}
				case EOF:
				case SEMI:
				case RPAREN:
				case COLON:
				case COMMA:
				case RBRACKET:
				case RCURLY:
				{
					break;
				}
				default:
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				 }
			}
			expression_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_1_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = expression_AST;
	}
	
	public void exprList() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST exprList_AST = null;
		
		try {      // for error handling
			match(LPAREN);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{ // ( ... )+
				int _cnt4=0;
				for (;;)
				{
					if ((LA(1)==SEMI))
					{
						match(SEMI);
						expression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						if (_cnt4 >= 1) { goto _loop4_breakloop; } else { throw new NoViableAltException(LT(1), getFilename());; }
					}
					
					_cnt4++;
				}
_loop4_breakloop:				;
			}    // ( ... )+
			match(RPAREN);
			if (0==inputState.guessing)
			{
				exprList_AST = (SpringExpressions.SpringAST)currentAST.root;
				exprList_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,"expressionList","SpringExpressions.ExpressionListNode"), (AST)exprList_AST);
				currentAST.root = exprList_AST;
				if ( (null != exprList_AST) && (null != exprList_AST.getFirstChild()) )
					currentAST.child = exprList_AST.getFirstChild();
				else
					currentAST.child = exprList_AST;
				currentAST.advanceChildToEnd();
			}
			exprList_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = exprList_AST;
	}
	
	public void logicalOrExpression() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST logicalOrExpression_AST = null;
		
		try {      // for error handling
			logicalXorExpression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==OR))
					{
						SpringExpressions.OpOR tmp9_AST = null;
						tmp9_AST = (SpringExpressions.OpOR) astFactory.create(LT(1), "SpringExpressions.OpOR");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp9_AST);
						match(OR);
						logicalXorExpression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop13_breakloop;
					}
					
				}
_loop13_breakloop:				;
			}    // ( ... )*
			logicalOrExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_3_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = logicalOrExpression_AST;
	}
	
	public void parenExpr() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST parenExpr_AST = null;
		
		try {      // for error handling
			match(LPAREN);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			match(RPAREN);
			parenExpr_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = parenExpr_AST;
	}
	
	public void logicalXorExpression() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST logicalXorExpression_AST = null;
		
		try {      // for error handling
			logicalAndExpression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==XOR))
					{
						SpringExpressions.OpXOR tmp12_AST = null;
						tmp12_AST = (SpringExpressions.OpXOR) astFactory.create(LT(1), "SpringExpressions.OpXOR");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp12_AST);
						match(XOR);
						logicalAndExpression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop16_breakloop;
					}
					
				}
_loop16_breakloop:				;
			}    // ( ... )*
			logicalXorExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_4_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = logicalXorExpression_AST;
	}
	
	public void logicalAndExpression() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST logicalAndExpression_AST = null;
		
		try {      // for error handling
			relationalExpression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==AND))
					{
						SpringExpressions.OpAND tmp13_AST = null;
						tmp13_AST = (SpringExpressions.OpAND) astFactory.create(LT(1), "SpringExpressions.OpAND");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp13_AST);
						match(AND);
						relationalExpression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop19_breakloop;
					}
					
				}
_loop19_breakloop:				;
			}    // ( ... )*
			logicalAndExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_5_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = logicalAndExpression_AST;
	}
	
	public void relationalExpression() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST relationalExpression_AST = null;
		SpringExpressions.SpringAST e1_AST = null;
		SpringExpressions.SpringAST op_AST = null;
		SpringExpressions.SpringAST e2_AST = null;
		
		try {      // for error handling
			sumExpr();
			if (0 == inputState.guessing)
			{
				e1_AST = (SpringExpressions.SpringAST)returnAST;
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{
				if ((tokenSet_6_.member(LA(1))))
				{
					relationalOperator();
					if (0 == inputState.guessing)
					{
						op_AST = (SpringExpressions.SpringAST)returnAST;
					}
					sumExpr();
					if (0 == inputState.guessing)
					{
						e2_AST = (SpringExpressions.SpringAST)returnAST;
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					if (0==inputState.guessing)
					{
						relationalExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
						relationalExpression_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,op_AST.getText(),GetRelationalOperatorNodeType(op_AST.getText())), (AST)relationalExpression_AST);
						currentAST.root = relationalExpression_AST;
						if ( (null != relationalExpression_AST) && (null != relationalExpression_AST.getFirstChild()) )
							currentAST.child = relationalExpression_AST.getFirstChild();
						else
							currentAST.child = relationalExpression_AST;
						currentAST.advanceChildToEnd();
					}
				}
				else if ((tokenSet_7_.member(LA(1)))) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			relationalExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_7_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = relationalExpression_AST;
	}
	
	public void sumExpr() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST sumExpr_AST = null;
		
		try {      // for error handling
			prodExpr();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==PLUS||LA(1)==MINUS))
					{
						{
							if ((LA(1)==PLUS))
							{
								SpringExpressions.OpADD tmp14_AST = null;
								tmp14_AST = (SpringExpressions.OpADD) astFactory.create(LT(1), "SpringExpressions.OpADD");
								astFactory.makeASTRoot(ref currentAST, (AST)tmp14_AST);
								match(PLUS);
							}
							else if ((LA(1)==MINUS)) {
								SpringExpressions.OpSUBTRACT tmp15_AST = null;
								tmp15_AST = (SpringExpressions.OpSUBTRACT) astFactory.create(LT(1), "SpringExpressions.OpSUBTRACT");
								astFactory.makeASTRoot(ref currentAST, (AST)tmp15_AST);
								match(MINUS);
							}
							else
							{
								throw new NoViableAltException(LT(1), getFilename());
							}
							
						}
						prodExpr();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop25_breakloop;
					}
					
				}
_loop25_breakloop:				;
			}    // ( ... )*
			sumExpr_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_8_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = sumExpr_AST;
	}
	
	public void relationalOperator() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST relationalOperator_AST = null;
		
		try {      // for error handling
			switch ( LA(1) )
			{
			case EQUAL:
			{
				SpringExpressions.SpringAST tmp16_AST = null;
				tmp16_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp16_AST);
				match(EQUAL);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case NOT_EQUAL:
			{
				SpringExpressions.SpringAST tmp17_AST = null;
				tmp17_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp17_AST);
				match(NOT_EQUAL);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case LESS_THAN:
			{
				SpringExpressions.SpringAST tmp18_AST = null;
				tmp18_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp18_AST);
				match(LESS_THAN);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case LESS_THAN_OR_EQUAL:
			{
				SpringExpressions.SpringAST tmp19_AST = null;
				tmp19_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp19_AST);
				match(LESS_THAN_OR_EQUAL);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case GREATER_THAN:
			{
				SpringExpressions.SpringAST tmp20_AST = null;
				tmp20_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp20_AST);
				match(GREATER_THAN);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case GREATER_THAN_OR_EQUAL:
			{
				SpringExpressions.SpringAST tmp21_AST = null;
				tmp21_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp21_AST);
				match(GREATER_THAN_OR_EQUAL);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case IN:
			{
				SpringExpressions.SpringAST tmp22_AST = null;
				tmp22_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp22_AST);
				match(IN);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case IS:
			{
				SpringExpressions.SpringAST tmp23_AST = null;
				tmp23_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp23_AST);
				match(IS);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case BETWEEN:
			{
				SpringExpressions.SpringAST tmp24_AST = null;
				tmp24_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp24_AST);
				match(BETWEEN);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case LIKE:
			{
				SpringExpressions.SpringAST tmp25_AST = null;
				tmp25_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp25_AST);
				match(LIKE);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case MATCHES:
			{
				SpringExpressions.SpringAST tmp26_AST = null;
				tmp26_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp26_AST);
				match(MATCHES);
				relationalOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			default:
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			 }
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_9_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = relationalOperator_AST;
	}
	
	public void prodExpr() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST prodExpr_AST = null;
		
		try {      // for error handling
			powExpr();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if (((LA(1) >= STAR && LA(1) <= MOD)))
					{
						{
							switch ( LA(1) )
							{
							case STAR:
							{
								SpringExpressions.OpMULTIPLY tmp27_AST = null;
								tmp27_AST = (SpringExpressions.OpMULTIPLY) astFactory.create(LT(1), "SpringExpressions.OpMULTIPLY");
								astFactory.makeASTRoot(ref currentAST, (AST)tmp27_AST);
								match(STAR);
								break;
							}
							case DIV:
							{
								SpringExpressions.OpDIVIDE tmp28_AST = null;
								tmp28_AST = (SpringExpressions.OpDIVIDE) astFactory.create(LT(1), "SpringExpressions.OpDIVIDE");
								astFactory.makeASTRoot(ref currentAST, (AST)tmp28_AST);
								match(DIV);
								break;
							}
							case MOD:
							{
								SpringExpressions.OpMODULUS tmp29_AST = null;
								tmp29_AST = (SpringExpressions.OpMODULUS) astFactory.create(LT(1), "SpringExpressions.OpMODULUS");
								astFactory.makeASTRoot(ref currentAST, (AST)tmp29_AST);
								match(MOD);
								break;
							}
							default:
							{
								throw new NoViableAltException(LT(1), getFilename());
							}
							 }
						}
						powExpr();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop29_breakloop;
					}
					
				}
_loop29_breakloop:				;
			}    // ( ... )*
			prodExpr_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_10_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = prodExpr_AST;
	}
	
	public void powExpr() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST powExpr_AST = null;
		
		try {      // for error handling
			postCastUnaryExpression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{
				if ((LA(1)==POWER))
				{
					SpringExpressions.OpPOWER tmp30_AST = null;
					tmp30_AST = (SpringExpressions.OpPOWER) astFactory.create(LT(1), "SpringExpressions.OpPOWER");
					astFactory.makeASTRoot(ref currentAST, (AST)tmp30_AST);
					match(POWER);
					postCastUnaryExpression();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
				}
				else if ((tokenSet_11_.member(LA(1)))) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			powExpr_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_11_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = powExpr_AST;
	}
	
	public void postCastUnaryExpression() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST postCastUnaryExpression_AST = null;
		SpringExpressions.SpringAST pts_AST = null;
		
		try {      // for error handling
			unaryExpression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{
				if ((LA(1)==AS))
				{
					match(AS);
					asTypeSlot();
					if (0 == inputState.guessing)
					{
						pts_AST = (SpringExpressions.SpringAST)returnAST;
					}
					if (0==inputState.guessing)
					{
						postCastUnaryExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
						postCastUnaryExpression_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,pts_AST.getText(),"SpringExpressions.CastNode"), (AST)postCastUnaryExpression_AST);
						currentAST.root = postCastUnaryExpression_AST;
						if ( (null != postCastUnaryExpression_AST) && (null != postCastUnaryExpression_AST.getFirstChild()) )
							currentAST.child = postCastUnaryExpression_AST.getFirstChild();
						else
							currentAST.child = postCastUnaryExpression_AST;
						currentAST.advanceChildToEnd();
					}
				}
				else if ((tokenSet_12_.member(LA(1)))) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			postCastUnaryExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_12_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = postCastUnaryExpression_AST;
	}
	
	public void unaryExpression() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST unaryExpression_AST = null;
		
		try {      // for error handling
			if ((LA(1)==PLUS||LA(1)==MINUS||LA(1)==BANG))
			{
				{
					switch ( LA(1) )
					{
					case PLUS:
					{
						SpringExpressions.OpUnaryPlus tmp32_AST = null;
						tmp32_AST = (SpringExpressions.OpUnaryPlus) astFactory.create(LT(1), "SpringExpressions.OpUnaryPlus");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp32_AST);
						match(PLUS);
						break;
					}
					case MINUS:
					{
						SpringExpressions.OpUnaryMinus tmp33_AST = null;
						tmp33_AST = (SpringExpressions.OpUnaryMinus) astFactory.create(LT(1), "SpringExpressions.OpUnaryMinus");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp33_AST);
						match(MINUS);
						break;
					}
					case BANG:
					{
						SpringExpressions.OpNOT tmp34_AST = null;
						tmp34_AST = (SpringExpressions.OpNOT) astFactory.create(LT(1), "SpringExpressions.OpNOT");
						astFactory.makeASTRoot(ref currentAST, (AST)tmp34_AST);
						match(BANG);
						break;
					}
					default:
					{
						throw new NoViableAltException(LT(1), getFilename());
					}
					 }
				}
				unaryExpression();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				unaryExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((tokenSet_13_.member(LA(1)))) {
				primaryExpression();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				unaryExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_14_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = unaryExpression_AST;
	}
	
	public void asTypeSlot() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST asTypeSlot_AST = null;
		
		try {      // for error handling
			if ((LA(1)==TYPE))
			{
				match(TYPE);
				name();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				match(RPAREN);
				asTypeSlot_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==ID)) {
				asTypeName();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				asTypeSlot_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_12_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = asTypeSlot_AST;
	}
	
	public void asPrefixCast() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST asPrefixCast_AST = null;
		SpringExpressions.SpringAST ats_AST = null;
		
		try {      // for error handling
			match(AS);
			match(LESS_THAN);
			asTypeSlot();
			if (0 == inputState.guessing)
			{
				ats_AST = (SpringExpressions.SpringAST)returnAST;
			}
			match(GREATER_THAN);
			match(LPAREN);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			match(RPAREN);
			if (0==inputState.guessing)
			{
				asPrefixCast_AST = (SpringExpressions.SpringAST)currentAST.root;
				asPrefixCast_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,ats_AST.getText(),"SpringExpressions.CastNode"), (AST)asPrefixCast_AST);
				currentAST.root = asPrefixCast_AST;
				if ( (null != asPrefixCast_AST) && (null != asPrefixCast_AST.getFirstChild()) )
					currentAST.child = asPrefixCast_AST.getFirstChild();
				else
					currentAST.child = asPrefixCast_AST;
				currentAST.advanceChildToEnd();
			}
			asPrefixCast_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = asPrefixCast_AST;
	}
	
	public void name() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST name_AST = null;
		
		try {      // for error handling
			SpringExpressions.QualifiedIdentifier tmp42_AST = null;
			tmp42_AST = (SpringExpressions.QualifiedIdentifier) astFactory.create(LT(1), "SpringExpressions.QualifiedIdentifier");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp42_AST);
			match(ID);
			{    // ( ... )*
				for (;;)
				{
					if ((tokenSet_15_.member(LA(1))))
					{
						{
							SpringExpressions.SpringAST tmp43_AST = null;
							tmp43_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
							astFactory.addASTChild(ref currentAST, (AST)tmp43_AST);
							match(tokenSet_15_);
						}
					}
					else
					{
						goto _loop109_breakloop;
					}
					
				}
_loop109_breakloop:				;
			}    // ( ... )*
			name_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_16_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = name_AST;
	}
	
	public void asTypeName() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST asTypeName_AST = null;
		
		try {      // for error handling
			SpringExpressions.QualifiedIdentifier tmp44_AST = null;
			tmp44_AST = (SpringExpressions.QualifiedIdentifier) astFactory.create(LT(1), "SpringExpressions.QualifiedIdentifier");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp44_AST);
			match(ID);
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==DOT))
					{
						SpringExpressions.SpringAST tmp45_AST = null;
						tmp45_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp45_AST);
						match(DOT);
						SpringExpressions.SpringAST tmp46_AST = null;
						tmp46_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp46_AST);
						match(ID);
					}
					else
					{
						goto _loop38_breakloop;
					}
					
				}
_loop38_breakloop:				;
			}    // ( ... )*
			{
				bool synPredMatched41 = false;
				if (((LA(1)==LESS_THAN) && (LA(2)==ID)))
				{
					int _m41 = mark();
					synPredMatched41 = true;
					inputState.guessing++;
					try {
						{
							match(LESS_THAN);
							bareTypeNameList();
							match(GREATER_THAN);
						}
					}
					catch (RecognitionException)
					{
						synPredMatched41 = false;
					}
					rewind(_m41);
					inputState.guessing--;
				}
				if ( synPredMatched41 )
				{
					SpringExpressions.SpringAST tmp47_AST = null;
					tmp47_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
					astFactory.addASTChild(ref currentAST, (AST)tmp47_AST);
					match(LESS_THAN);
					bareTypeNameList();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					SpringExpressions.SpringAST tmp48_AST = null;
					tmp48_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
					astFactory.addASTChild(ref currentAST, (AST)tmp48_AST);
					match(GREATER_THAN);
				}
				else if ((tokenSet_17_.member(LA(1))) && (tokenSet_18_.member(LA(2)))) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==LBRACKET))
					{
						SpringExpressions.SpringAST tmp49_AST = null;
						tmp49_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp49_AST);
						match(LBRACKET);
						{    // ( ... )*
							for (;;)
							{
								if ((LA(1)==COMMA))
								{
									SpringExpressions.SpringAST tmp50_AST = null;
									tmp50_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
									astFactory.addASTChild(ref currentAST, (AST)tmp50_AST);
									match(COMMA);
								}
								else
								{
									goto _loop44_breakloop;
								}
								
							}
_loop44_breakloop:							;
						}    // ( ... )*
						SpringExpressions.SpringAST tmp51_AST = null;
						tmp51_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp51_AST);
						match(RBRACKET);
					}
					else
					{
						goto _loop45_breakloop;
					}
					
				}
_loop45_breakloop:				;
			}    // ( ... )*
			asTypeName_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_12_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = asTypeName_AST;
	}
	
	public void bareTypeNameList() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST bareTypeNameList_AST = null;
		
		try {      // for error handling
			bareTypeName();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==COMMA))
					{
						SpringExpressions.SpringAST tmp52_AST = null;
						tmp52_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp52_AST);
						match(COMMA);
						bareTypeName();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop48_breakloop;
					}
					
				}
_loop48_breakloop:				;
			}    // ( ... )*
			bareTypeNameList_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_19_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = bareTypeNameList_AST;
	}
	
	public void bareTypeName() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST bareTypeName_AST = null;
		
		try {      // for error handling
			SpringExpressions.SpringAST tmp53_AST = null;
			tmp53_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
			astFactory.addASTChild(ref currentAST, (AST)tmp53_AST);
			match(ID);
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==DOT))
					{
						SpringExpressions.SpringAST tmp54_AST = null;
						tmp54_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp54_AST);
						match(DOT);
						SpringExpressions.SpringAST tmp55_AST = null;
						tmp55_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp55_AST);
						match(ID);
					}
					else
					{
						goto _loop51_breakloop;
					}
					
				}
_loop51_breakloop:				;
			}    // ( ... )*
			{
				if ((LA(1)==LESS_THAN))
				{
					SpringExpressions.SpringAST tmp56_AST = null;
					tmp56_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
					astFactory.addASTChild(ref currentAST, (AST)tmp56_AST);
					match(LESS_THAN);
					bareTypeNameList();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					SpringExpressions.SpringAST tmp57_AST = null;
					tmp57_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
					astFactory.addASTChild(ref currentAST, (AST)tmp57_AST);
					match(GREATER_THAN);
				}
				else if ((LA(1)==GREATER_THAN||LA(1)==LBRACKET||LA(1)==COMMA)) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==LBRACKET))
					{
						SpringExpressions.SpringAST tmp58_AST = null;
						tmp58_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp58_AST);
						match(LBRACKET);
						{    // ( ... )*
							for (;;)
							{
								if ((LA(1)==COMMA))
								{
									SpringExpressions.SpringAST tmp59_AST = null;
									tmp59_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
									astFactory.addASTChild(ref currentAST, (AST)tmp59_AST);
									match(COMMA);
								}
								else
								{
									goto _loop55_breakloop;
								}
								
							}
_loop55_breakloop:							;
						}    // ( ... )*
						SpringExpressions.SpringAST tmp60_AST = null;
						tmp60_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp60_AST);
						match(RBRACKET);
					}
					else
					{
						goto _loop56_breakloop;
					}
					
				}
_loop56_breakloop:				;
			}    // ( ... )*
			bareTypeName_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_20_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = bareTypeName_AST;
	}
	
	public void primaryExpression() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST primaryExpression_AST = null;
		
		try {      // for error handling
			startNode();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{
				if ((tokenSet_21_.member(LA(1))))
				{
					node();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
				}
				else if ((tokenSet_14_.member(LA(1)))) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			if (0==inputState.guessing)
			{
				primaryExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
				primaryExpression_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,"expression","SpringExpressions.Expression"), (AST)primaryExpression_AST);
				currentAST.root = primaryExpression_AST;
				if ( (null != primaryExpression_AST) && (null != primaryExpression_AST.getFirstChild()) )
					currentAST.child = primaryExpression_AST.getFirstChild();
				else
					currentAST.child = primaryExpression_AST;
				currentAST.advanceChildToEnd();
			}
			primaryExpression_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_14_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = primaryExpression_AST;
	}
	
	public void unaryOperator() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST unaryOperator_AST = null;
		
		try {      // for error handling
			switch ( LA(1) )
			{
			case PLUS:
			{
				SpringExpressions.SpringAST tmp61_AST = null;
				tmp61_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp61_AST);
				match(PLUS);
				unaryOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case MINUS:
			{
				SpringExpressions.SpringAST tmp62_AST = null;
				tmp62_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp62_AST);
				match(MINUS);
				unaryOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case BANG:
			{
				SpringExpressions.SpringAST tmp63_AST = null;
				tmp63_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp63_AST);
				match(BANG);
				unaryOperator_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			default:
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			 }
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_0_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = unaryOperator_AST;
	}
	
	public void startNode() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST startNode_AST = null;
		
		try {      // for error handling
			{
				switch ( LA(1) )
				{
				case AS:
				{
					asPrefixCast();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case ID:
				{
					methodOrProperty();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case DOLLAR:
				{
					localFunctionOrVar();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case LBRACKET:
				{
					indexer();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case FALSE:
				case TRUE:
				case NULL_LITERAL:
				case STRING_LITERAL:
				case INTEGER_LITERAL:
				case HEXADECIMAL_INTEGER_LITERAL:
				case REAL_LITERAL:
				{
					literal();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case TYPEOF:
				case TYPE:
				{
					type();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case LITERAL_new:
				{
					constructor();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case PROJECT:
				{
					projection();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case SELECT:
				{
					selection();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case SELECT_FIRST:
				{
					firstSelection();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case SELECT_LAST:
				{
					lastSelection();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case LCURLY:
				{
					listInitializer();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				case LAMBDA:
				{
					lambda();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					break;
				}
				default:
					bool synPredMatched65 = false;
					if (((LA(1)==LPAREN) && (tokenSet_9_.member(LA(2)))))
					{
						int _m65 = mark();
						synPredMatched65 = true;
						inputState.guessing++;
						try {
							{
								match(LPAREN);
								expression();
								match(SEMI);
							}
						}
						catch (RecognitionException)
						{
							synPredMatched65 = false;
						}
						rewind(_m65);
						inputState.guessing--;
					}
					if ( synPredMatched65 )
					{
						exprList();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else if ((LA(1)==LPAREN) && (tokenSet_9_.member(LA(2)))) {
						parenExpr();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else if ((LA(1)==POUND) && (LA(2)==ID)) {
						functionOrVar();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else if ((LA(1)==AT) && (LA(2)==LPAREN)) {
						reference();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else if ((LA(1)==POUND) && (LA(2)==LCURLY)) {
						mapInitializer();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else if ((LA(1)==AT) && (LA(2)==LBRACKET)) {
						attribute();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				break; }
			}
			startNode_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = startNode_AST;
	}
	
	public void node() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST node_AST = null;
		
		try {      // for error handling
			{ // ( ... )+
				int _cnt68=0;
				for (;;)
				{
					switch ( LA(1) )
					{
					case SAFE_DOT:
					{
						match(SAFE_DOT);
						safeMember();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case SAFE_LBRACKET:
					{
						safeIndexer();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case ID:
					{
						methodOrProperty();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case LBRACKET:
					{
						indexer();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case PROJECT:
					{
						projection();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case SELECT:
					{
						selection();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case SELECT_FIRST:
					{
						firstSelection();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case SELECT_LAST:
					{
						lastSelection();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case LPAREN:
					{
						exprList();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
						break;
					}
					case DOT:
					{
						match(DOT);
						break;
					}
					default:
					{
						if (_cnt68 >= 1) { goto _loop68_breakloop; } else { throw new NoViableAltException(LT(1), getFilename());; }
					}
					break; }
					_cnt68++;
				}
_loop68_breakloop:				;
			}    // ( ... )+
			node_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_14_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = node_AST;
	}
	
	public void methodOrProperty() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST methodOrProperty_AST = null;
		
		try {      // for error handling
			bool synPredMatched85 = false;
			if (((LA(1)==ID) && (LA(2)==LPAREN)))
			{
				int _m85 = mark();
				synPredMatched85 = true;
				inputState.guessing++;
				try {
					{
						match(ID);
						match(LPAREN);
					}
				}
				catch (RecognitionException)
				{
					synPredMatched85 = false;
				}
				rewind(_m85);
				inputState.guessing--;
			}
			if ( synPredMatched85 )
			{
				SpringExpressions.MethodNode tmp66_AST = null;
				tmp66_AST = (SpringExpressions.MethodNode) astFactory.create(LT(1), "SpringExpressions.MethodNode");
				astFactory.makeASTRoot(ref currentAST, (AST)tmp66_AST);
				match(ID);
				methodArgs();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				methodOrProperty_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==ID) && (tokenSet_2_.member(LA(2)))) {
				property();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				methodOrProperty_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = methodOrProperty_AST;
	}
	
	public void functionOrVar() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST functionOrVar_AST = null;
		
		try {      // for error handling
			bool synPredMatched75 = false;
			if (((LA(1)==POUND) && (LA(2)==ID)))
			{
				int _m75 = mark();
				synPredMatched75 = true;
				inputState.guessing++;
				try {
					{
						match(POUND);
						match(ID);
						match(LPAREN);
					}
				}
				catch (RecognitionException)
				{
					synPredMatched75 = false;
				}
				rewind(_m75);
				inputState.guessing--;
			}
			if ( synPredMatched75 )
			{
				function();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				functionOrVar_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==POUND) && (LA(2)==ID)) {
				var();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				functionOrVar_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = functionOrVar_AST;
	}
	
	public void localFunctionOrVar() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST localFunctionOrVar_AST = null;
		
		try {      // for error handling
			bool synPredMatched80 = false;
			if (((LA(1)==DOLLAR) && (LA(2)==ID)))
			{
				int _m80 = mark();
				synPredMatched80 = true;
				inputState.guessing++;
				try {
					{
						match(DOLLAR);
						match(ID);
						match(LPAREN);
					}
				}
				catch (RecognitionException)
				{
					synPredMatched80 = false;
				}
				rewind(_m80);
				inputState.guessing--;
			}
			if ( synPredMatched80 )
			{
				localFunction();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				localFunctionOrVar_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==DOLLAR) && (LA(2)==ID)) {
				localVar();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				localFunctionOrVar_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = localFunctionOrVar_AST;
	}
	
	public void reference() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST reference_AST = null;
		SpringExpressions.SpringAST cn_AST = null;
		SpringExpressions.SpringAST id_AST = null;
		SpringExpressions.SpringAST localid_AST = null;
		
		try {      // for error handling
			bool synPredMatched93 = false;
			if (((LA(1)==AT) && (LA(2)==LPAREN)))
			{
				int _m93 = mark();
				synPredMatched93 = true;
				inputState.guessing++;
				try {
					{
						match(AT);
						match(LPAREN);
						quotableName();
						match(COLON);
					}
				}
				catch (RecognitionException)
				{
					synPredMatched93 = false;
				}
				rewind(_m93);
				inputState.guessing--;
			}
			if ( synPredMatched93 )
			{
				match(AT);
				match(LPAREN);
				quotableName();
				if (0 == inputState.guessing)
				{
					cn_AST = (SpringExpressions.SpringAST)returnAST;
				}
				match(COLON);
				quotableName();
				if (0 == inputState.guessing)
				{
					id_AST = (SpringExpressions.SpringAST)returnAST;
				}
				match(RPAREN);
				if (0==inputState.guessing)
				{
					reference_AST = (SpringExpressions.SpringAST)currentAST.root;
					reference_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,"ref","SpringContext.Support.ReferenceNode"), (AST)cn_AST, (AST)id_AST);
					currentAST.root = reference_AST;
					if ( (null != reference_AST) && (null != reference_AST.getFirstChild()) )
						currentAST.child = reference_AST.getFirstChild();
					else
						currentAST.child = reference_AST;
					currentAST.advanceChildToEnd();
				}
				reference_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==AT) && (LA(2)==LPAREN)) {
				match(AT);
				match(LPAREN);
				quotableName();
				if (0 == inputState.guessing)
				{
					localid_AST = (SpringExpressions.SpringAST)returnAST;
				}
				match(RPAREN);
				if (0==inputState.guessing)
				{
					reference_AST = (SpringExpressions.SpringAST)currentAST.root;
					reference_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,"ref","SpringContext.Support.ReferenceNode"), (AST)null, (AST)localid_AST);
					currentAST.root = reference_AST;
					if ( (null != reference_AST) && (null != reference_AST.getFirstChild()) )
						currentAST.child = reference_AST.getFirstChild();
					else
						currentAST.child = reference_AST;
					currentAST.advanceChildToEnd();
				}
				reference_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = reference_AST;
	}
	
	public void indexer() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST indexer_AST = null;
		
		try {      // for error handling
			SpringExpressions.IndexerNode tmp74_AST = null;
			tmp74_AST = (SpringExpressions.IndexerNode) astFactory.create(LT(1), "SpringExpressions.IndexerNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp74_AST);
			match(LBRACKET);
			argument();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==COMMA))
					{
						match(COMMA);
						argument();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop96_breakloop;
					}
					
				}
_loop96_breakloop:				;
			}    // ( ... )*
			match(RBRACKET);
			indexer_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = indexer_AST;
	}
	
	public void literal() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST literal_AST = null;
		
		try {      // for error handling
			switch ( LA(1) )
			{
			case NULL_LITERAL:
			{
				SpringExpressions.NullLiteralNode tmp77_AST = null;
				tmp77_AST = (SpringExpressions.NullLiteralNode) astFactory.create(LT(1), "SpringExpressions.NullLiteralNode");
				astFactory.addASTChild(ref currentAST, (AST)tmp77_AST);
				match(NULL_LITERAL);
				literal_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case INTEGER_LITERAL:
			{
				SpringExpressions.IntLiteralNode tmp78_AST = null;
				tmp78_AST = (SpringExpressions.IntLiteralNode) astFactory.create(LT(1), "SpringExpressions.IntLiteralNode");
				astFactory.addASTChild(ref currentAST, (AST)tmp78_AST);
				match(INTEGER_LITERAL);
				literal_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case HEXADECIMAL_INTEGER_LITERAL:
			{
				SpringExpressions.HexLiteralNode tmp79_AST = null;
				tmp79_AST = (SpringExpressions.HexLiteralNode) astFactory.create(LT(1), "SpringExpressions.HexLiteralNode");
				astFactory.addASTChild(ref currentAST, (AST)tmp79_AST);
				match(HEXADECIMAL_INTEGER_LITERAL);
				literal_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case REAL_LITERAL:
			{
				SpringExpressions.RealLiteralNode tmp80_AST = null;
				tmp80_AST = (SpringExpressions.RealLiteralNode) astFactory.create(LT(1), "SpringExpressions.RealLiteralNode");
				astFactory.addASTChild(ref currentAST, (AST)tmp80_AST);
				match(REAL_LITERAL);
				literal_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case STRING_LITERAL:
			{
				SpringExpressions.StringLiteralNode tmp81_AST = null;
				tmp81_AST = (SpringExpressions.StringLiteralNode) astFactory.create(LT(1), "SpringExpressions.StringLiteralNode");
				astFactory.addASTChild(ref currentAST, (AST)tmp81_AST);
				match(STRING_LITERAL);
				literal_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			case FALSE:
			case TRUE:
			{
				boolLiteral();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				literal_AST = (SpringExpressions.SpringAST)currentAST.root;
				break;
			}
			default:
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			 }
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = literal_AST;
	}
	
	public void type() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST type_AST = null;
		
		try {      // for error handling
			if ((LA(1)==TYPE))
			{
				type_T();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				type_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==TYPEOF)) {
				type_of();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				type_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = type_AST;
	}
	
	public void constructor() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST constructor_AST = null;
		SpringExpressions.SpringAST type_AST = null;
		
		try {      // for error handling
			bool synPredMatched121 = false;
			if (((LA(1)==LITERAL_new) && (LA(2)==ID)))
			{
				int _m121 = mark();
				synPredMatched121 = true;
				inputState.guessing++;
				try {
					{
						match(LITERAL_new);
						qualifiedId();
						match(LPAREN);
					}
				}
				catch (RecognitionException)
				{
					synPredMatched121 = false;
				}
				rewind(_m121);
				inputState.guessing--;
			}
			if ( synPredMatched121 )
			{
				match(LITERAL_new);
				qualifiedId();
				if (0 == inputState.guessing)
				{
					type_AST = (SpringExpressions.SpringAST)returnAST;
				}
				ctorArgs();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				if (0==inputState.guessing)
				{
					constructor_AST = (SpringExpressions.SpringAST)currentAST.root;
					constructor_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,type_AST.getText(),"SpringExpressions.ConstructorNode"), (AST)constructor_AST);
					currentAST.root = constructor_AST;
					if ( (null != constructor_AST) && (null != constructor_AST.getFirstChild()) )
						currentAST.child = constructor_AST.getFirstChild();
					else
						currentAST.child = constructor_AST;
					currentAST.advanceChildToEnd();
				}
				constructor_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==LITERAL_new) && (LA(2)==ID)) {
				arrayConstructor();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				constructor_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = constructor_AST;
	}
	
	public void projection() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST projection_AST = null;
		
		try {      // for error handling
			SpringExpressions.ProjectionNode tmp83_AST = null;
			tmp83_AST = (SpringExpressions.ProjectionNode) astFactory.create(LT(1), "SpringExpressions.ProjectionNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp83_AST);
			match(PROJECT);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			match(RCURLY);
			projection_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = projection_AST;
	}
	
	public void selection() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST selection_AST = null;
		
		try {      // for error handling
			SpringExpressions.SelectionNode tmp85_AST = null;
			tmp85_AST = (SpringExpressions.SelectionNode) astFactory.create(LT(1), "SpringExpressions.SelectionNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp85_AST);
			match(SELECT);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==COMMA))
					{
						match(COMMA);
						expression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop100_breakloop;
					}
					
				}
_loop100_breakloop:				;
			}    // ( ... )*
			match(RCURLY);
			selection_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = selection_AST;
	}
	
	public void firstSelection() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST firstSelection_AST = null;
		
		try {      // for error handling
			SpringExpressions.SelectionFirstNode tmp88_AST = null;
			tmp88_AST = (SpringExpressions.SelectionFirstNode) astFactory.create(LT(1), "SpringExpressions.SelectionFirstNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp88_AST);
			match(SELECT_FIRST);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			match(RCURLY);
			firstSelection_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = firstSelection_AST;
	}
	
	public void lastSelection() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST lastSelection_AST = null;
		
		try {      // for error handling
			SpringExpressions.SelectionLastNode tmp90_AST = null;
			tmp90_AST = (SpringExpressions.SelectionLastNode) astFactory.create(LT(1), "SpringExpressions.SelectionLastNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp90_AST);
			match(SELECT_LAST);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			match(RCURLY);
			lastSelection_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = lastSelection_AST;
	}
	
	public void listInitializer() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST listInitializer_AST = null;
		
		try {      // for error handling
			SpringExpressions.ListInitializerNode tmp92_AST = null;
			tmp92_AST = (SpringExpressions.ListInitializerNode) astFactory.create(LT(1), "SpringExpressions.ListInitializerNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp92_AST);
			match(LCURLY);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==COMMA))
					{
						match(COMMA);
						expression();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop130_breakloop;
					}
					
				}
_loop130_breakloop:				;
			}    // ( ... )*
			match(RCURLY);
			listInitializer_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = listInitializer_AST;
	}
	
	public void mapInitializer() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST mapInitializer_AST = null;
		
		try {      // for error handling
			match(POUND);
			SpringExpressions.MapInitializerNode tmp96_AST = null;
			tmp96_AST = (SpringExpressions.MapInitializerNode) astFactory.create(LT(1), "SpringExpressions.MapInitializerNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp96_AST);
			match(LCURLY);
			mapEntry();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==COMMA))
					{
						match(COMMA);
						mapEntry();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop133_breakloop;
					}
					
				}
_loop133_breakloop:				;
			}    // ( ... )*
			match(RCURLY);
			mapInitializer_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = mapInitializer_AST;
	}
	
	public void lambda() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST lambda_AST = null;
		
		try {      // for error handling
			match(LAMBDA);
			{
				if ((LA(1)==ID))
				{
					argList();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
				}
				else if ((LA(1)==PIPE)) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			match(PIPE);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			match(RCURLY);
			if (0==inputState.guessing)
			{
				lambda_AST = (SpringExpressions.SpringAST)currentAST.root;
				lambda_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,"lambda","SpringExpressions.LambdaExpressionNode"), (AST)lambda_AST);
				currentAST.root = lambda_AST;
				if ( (null != lambda_AST) && (null != lambda_AST.getFirstChild()) )
					currentAST.child = lambda_AST.getFirstChild();
				else
					currentAST.child = lambda_AST;
				currentAST.advanceChildToEnd();
			}
			lambda_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = lambda_AST;
	}
	
	public void attribute() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST attribute_AST = null;
		SpringExpressions.SpringAST tn_AST = null;
		
		try {      // for error handling
			match(AT);
			match(LBRACKET);
			qualifiedId();
			if (0 == inputState.guessing)
			{
				tn_AST = (SpringExpressions.SpringAST)returnAST;
			}
			{
				if ((LA(1)==LPAREN))
				{
					ctorArgs();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
				}
				else if ((LA(1)==RBRACKET)) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			match(RBRACKET);
			if (0==inputState.guessing)
			{
				attribute_AST = (SpringExpressions.SpringAST)currentAST.root;
				attribute_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,tn_AST.getText(),"SpringExpressions.AttributeNode"), (AST)attribute_AST);
				currentAST.root = attribute_AST;
				if ( (null != attribute_AST) && (null != attribute_AST.getFirstChild()) )
					currentAST.child = attribute_AST.getFirstChild();
				else
					currentAST.child = attribute_AST;
				currentAST.advanceChildToEnd();
			}
			attribute_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = attribute_AST;
	}
	
	public void safeMember() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST safeMember_AST = null;
		SpringExpressions.SpringAST mp_AST = null;
		
		try {      // for error handling
			methodOrProperty();
			if (0 == inputState.guessing)
			{
				mp_AST = (SpringExpressions.SpringAST)returnAST;
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			if (0==inputState.guessing)
			{
				mp_AST.IsNullConditional = true;
			}
			safeMember_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = safeMember_AST;
	}
	
	public void safeIndexer() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST safeIndexer_AST = null;
		IToken  sb = null;
		SpringExpressions.IndexerNode sb_AST = null;
		
		try {      // for error handling
			sb = LT(1);
			sb_AST = (SpringExpressions.IndexerNode) astFactory.create(sb, "SpringExpressions.IndexerNode");
			astFactory.makeASTRoot(ref currentAST, (AST)sb_AST);
			match(SAFE_LBRACKET);
			argument();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==COMMA))
					{
						match(COMMA);
						argument();
						if (0 == inputState.guessing)
						{
							astFactory.addASTChild(ref currentAST, (AST)returnAST);
						}
					}
					else
					{
						goto _loop72_breakloop;
					}
					
				}
_loop72_breakloop:				;
			}    // ( ... )*
			match(RBRACKET);
			if (0==inputState.guessing)
			{
				sb_AST.IsNullConditional = true;
			}
			safeIndexer_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = safeIndexer_AST;
	}
	
	public void argument() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST argument_AST = null;
		
		try {      // for error handling
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			argument_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_22_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = argument_AST;
	}
	
	public void function() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST function_AST = null;
		
		try {      // for error handling
			match(POUND);
			SpringExpressions.FunctionNode tmp108_AST = null;
			tmp108_AST = (SpringExpressions.FunctionNode) astFactory.create(LT(1), "SpringExpressions.FunctionNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp108_AST);
			match(ID);
			methodArgs();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			function_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = function_AST;
	}
	
	public void var() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST var_AST = null;
		
		try {      // for error handling
			match(POUND);
			SpringExpressions.VariableNode tmp110_AST = null;
			tmp110_AST = (SpringExpressions.VariableNode) astFactory.create(LT(1), "SpringExpressions.VariableNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp110_AST);
			match(ID);
			var_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = var_AST;
	}
	
	public void methodArgs() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST methodArgs_AST = null;
		
		try {      // for error handling
			match(LPAREN);
			{
				if ((tokenSet_9_.member(LA(1))))
				{
					argument();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					{    // ( ... )*
						for (;;)
						{
							if ((LA(1)==COMMA))
							{
								match(COMMA);
								argument();
								if (0 == inputState.guessing)
								{
									astFactory.addASTChild(ref currentAST, (AST)returnAST);
								}
							}
							else
							{
								goto _loop89_breakloop;
							}
							
						}
_loop89_breakloop:						;
					}    // ( ... )*
				}
				else if ((LA(1)==RPAREN)) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			match(RPAREN);
			methodArgs_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = methodArgs_AST;
	}
	
	public void localFunction() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST localFunction_AST = null;
		
		try {      // for error handling
			match(DOLLAR);
			SpringExpressions.LocalFunctionNode tmp115_AST = null;
			tmp115_AST = (SpringExpressions.LocalFunctionNode) astFactory.create(LT(1), "SpringExpressions.LocalFunctionNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp115_AST);
			match(ID);
			methodArgs();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			localFunction_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = localFunction_AST;
	}
	
	public void localVar() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST localVar_AST = null;
		
		try {      // for error handling
			match(DOLLAR);
			SpringExpressions.LocalVariableNode tmp117_AST = null;
			tmp117_AST = (SpringExpressions.LocalVariableNode) astFactory.create(LT(1), "SpringExpressions.LocalVariableNode");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp117_AST);
			match(ID);
			localVar_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = localVar_AST;
	}
	
	public void property() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST property_AST = null;
		
		try {      // for error handling
			SpringExpressions.PropertyOrFieldNode tmp118_AST = null;
			tmp118_AST = (SpringExpressions.PropertyOrFieldNode) astFactory.create(LT(1), "SpringExpressions.PropertyOrFieldNode");
			astFactory.addASTChild(ref currentAST, (AST)tmp118_AST);
			match(ID);
			property_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = property_AST;
	}
	
	public void quotableName() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST quotableName_AST = null;
		
		try {      // for error handling
			if ((LA(1)==STRING_LITERAL))
			{
				SpringExpressions.QualifiedIdentifier tmp119_AST = null;
				tmp119_AST = (SpringExpressions.QualifiedIdentifier) astFactory.create(LT(1), "SpringExpressions.QualifiedIdentifier");
				astFactory.makeASTRoot(ref currentAST, (AST)tmp119_AST);
				match(STRING_LITERAL);
				quotableName_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==ID)) {
				name();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				quotableName_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_16_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = quotableName_AST;
	}
	
	public void type_T() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST type_T_AST = null;
		SpringExpressions.SpringAST tn_AST = null;
		
		try {      // for error handling
			match(TYPE);
			name();
			if (0 == inputState.guessing)
			{
				tn_AST = (SpringExpressions.SpringAST)returnAST;
			}
			match(RPAREN);
			if (0==inputState.guessing)
			{
				type_T_AST = (SpringExpressions.SpringAST)currentAST.root;
				type_T_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,tn_AST.getText(),"SpringExpressions.TypeNode"), (AST)type_T_AST);
				currentAST.root = type_T_AST;
				if ( (null != type_T_AST) && (null != type_T_AST.getFirstChild()) )
					currentAST.child = type_T_AST.getFirstChild();
				else
					currentAST.child = type_T_AST;
				currentAST.advanceChildToEnd();
			}
			type_T_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = type_T_AST;
	}
	
	public void type_of() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST type_of_AST = null;
		SpringExpressions.SpringAST to_AST = null;
		
		try {      // for error handling
			match(TYPEOF);
			match(LPAREN);
			name();
			if (0 == inputState.guessing)
			{
				to_AST = (SpringExpressions.SpringAST)returnAST;
			}
			match(RPAREN);
			if (0==inputState.guessing)
			{
				type_of_AST = (SpringExpressions.SpringAST)currentAST.root;
				type_of_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,to_AST.getText(),"SpringExpressions.TypeNode"), (AST)type_of_AST);
				currentAST.root = type_of_AST;
				if ( (null != type_of_AST) && (null != type_of_AST.getFirstChild()) )
					currentAST.child = type_of_AST.getFirstChild();
				else
					currentAST.child = type_of_AST;
				currentAST.advanceChildToEnd();
			}
			type_of_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = type_of_AST;
	}
	
	public void qualifiedId() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST qualifiedId_AST = null;
		
		try {      // for error handling
			SpringExpressions.QualifiedIdentifier tmp125_AST = null;
			tmp125_AST = (SpringExpressions.QualifiedIdentifier) astFactory.create(LT(1), "SpringExpressions.QualifiedIdentifier");
			astFactory.makeASTRoot(ref currentAST, (AST)tmp125_AST);
			match(ID);
			{    // ( ... )*
				for (;;)
				{
					if ((LA(1)==DOT))
					{
						SpringExpressions.SpringAST tmp126_AST = null;
						tmp126_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp126_AST);
						match(DOT);
						SpringExpressions.SpringAST tmp127_AST = null;
						tmp127_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
						astFactory.addASTChild(ref currentAST, (AST)tmp127_AST);
						match(ID);
					}
					else
					{
						goto _loop145_breakloop;
					}
					
				}
_loop145_breakloop:				;
			}    // ( ... )*
			qualifiedId_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_23_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = qualifiedId_AST;
	}
	
	public void ctorArgs() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST ctorArgs_AST = null;
		
		try {      // for error handling
			match(LPAREN);
			{
				if ((tokenSet_9_.member(LA(1))))
				{
					namedArgument();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					{    // ( ... )*
						for (;;)
						{
							if ((LA(1)==COMMA))
							{
								match(COMMA);
								namedArgument();
								if (0 == inputState.guessing)
								{
									astFactory.addASTChild(ref currentAST, (AST)returnAST);
								}
							}
							else
							{
								goto _loop138_breakloop;
							}
							
						}
_loop138_breakloop:						;
					}    // ( ... )*
				}
				else if ((LA(1)==RPAREN)) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			match(RPAREN);
			ctorArgs_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = ctorArgs_AST;
	}
	
	public void argList() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST argList_AST = null;
		
		try {      // for error handling
			{
				SpringExpressions.SpringAST tmp131_AST = null;
				tmp131_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
				astFactory.addASTChild(ref currentAST, (AST)tmp131_AST);
				match(ID);
				{    // ( ... )*
					for (;;)
					{
						if ((LA(1)==COMMA))
						{
							match(COMMA);
							SpringExpressions.SpringAST tmp133_AST = null;
							tmp133_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
							astFactory.addASTChild(ref currentAST, (AST)tmp133_AST);
							match(ID);
						}
						else
						{
							goto _loop118_breakloop;
						}
						
					}
_loop118_breakloop:					;
				}    // ( ... )*
			}
			if (0==inputState.guessing)
			{
				argList_AST = (SpringExpressions.SpringAST)currentAST.root;
				argList_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,"args"), (AST)argList_AST);
				currentAST.root = argList_AST;
				if ( (null != argList_AST) && (null != argList_AST.getFirstChild()) )
					currentAST.child = argList_AST.getFirstChild();
				else
					currentAST.child = argList_AST;
				currentAST.advanceChildToEnd();
			}
			argList_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_24_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = argList_AST;
	}
	
	public void arrayConstructor() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST arrayConstructor_AST = null;
		SpringExpressions.SpringAST type_AST = null;
		
		try {      // for error handling
			match(LITERAL_new);
			qualifiedId();
			if (0 == inputState.guessing)
			{
				type_AST = (SpringExpressions.SpringAST)returnAST;
			}
			arrayRank();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			{
				if ((LA(1)==LCURLY))
				{
					listInitializer();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
				}
				else if ((tokenSet_2_.member(LA(1)))) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			if (0==inputState.guessing)
			{
				arrayConstructor_AST = (SpringExpressions.SpringAST)currentAST.root;
				arrayConstructor_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,type_AST.getText(),"SpringExpressions.ArrayConstructorNode"), (AST)arrayConstructor_AST);
				currentAST.root = arrayConstructor_AST;
				if ( (null != arrayConstructor_AST) && (null != arrayConstructor_AST.getFirstChild()) )
					currentAST.child = arrayConstructor_AST.getFirstChild();
				else
					currentAST.child = arrayConstructor_AST;
				currentAST.advanceChildToEnd();
			}
			arrayConstructor_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = arrayConstructor_AST;
	}
	
	public void arrayRank() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST arrayRank_AST = null;
		
		try {      // for error handling
			SpringExpressions.SpringAST tmp135_AST = null;
			tmp135_AST = (SpringExpressions.SpringAST) astFactory.create(LT(1));
			astFactory.makeASTRoot(ref currentAST, (AST)tmp135_AST);
			match(LBRACKET);
			{
				if ((tokenSet_9_.member(LA(1))))
				{
					expression();
					if (0 == inputState.guessing)
					{
						astFactory.addASTChild(ref currentAST, (AST)returnAST);
					}
					{    // ( ... )*
						for (;;)
						{
							if ((LA(1)==COMMA))
							{
								match(COMMA);
								expression();
								if (0 == inputState.guessing)
								{
									astFactory.addASTChild(ref currentAST, (AST)returnAST);
								}
							}
							else
							{
								goto _loop127_breakloop;
							}
							
						}
_loop127_breakloop:						;
					}    // ( ... )*
				}
				else if ((LA(1)==RBRACKET)) {
				}
				else
				{
					throw new NoViableAltException(LT(1), getFilename());
				}
				
			}
			match(RBRACKET);
			arrayRank_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_25_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = arrayRank_AST;
	}
	
	public void mapEntry() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST mapEntry_AST = null;
		
		try {      // for error handling
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			match(COLON);
			expression();
			if (0 == inputState.guessing)
			{
				astFactory.addASTChild(ref currentAST, (AST)returnAST);
			}
			if (0==inputState.guessing)
			{
				mapEntry_AST = (SpringExpressions.SpringAST)currentAST.root;
				mapEntry_AST = (SpringExpressions.SpringAST) astFactory.make((AST)(SpringExpressions.SpringAST) astFactory.create(EXPR,"entry","SpringExpressions.MapEntryNode"), (AST)mapEntry_AST);
				currentAST.root = mapEntry_AST;
				if ( (null != mapEntry_AST) && (null != mapEntry_AST.getFirstChild()) )
					currentAST.child = mapEntry_AST.getFirstChild();
				else
					currentAST.child = mapEntry_AST;
				currentAST.advanceChildToEnd();
			}
			mapEntry_AST = (SpringExpressions.SpringAST)currentAST.root;
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_26_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = mapEntry_AST;
	}
	
	public void namedArgument() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST namedArgument_AST = null;
		
		try {      // for error handling
			bool synPredMatched142 = false;
			if (((LA(1)==ID) && (LA(2)==ASSIGN)))
			{
				int _m142 = mark();
				synPredMatched142 = true;
				inputState.guessing++;
				try {
					{
						match(ID);
						match(ASSIGN);
					}
				}
				catch (RecognitionException)
				{
					synPredMatched142 = false;
				}
				rewind(_m142);
				inputState.guessing--;
			}
			if ( synPredMatched142 )
			{
				SpringExpressions.NamedArgumentNode tmp139_AST = null;
				tmp139_AST = (SpringExpressions.NamedArgumentNode) astFactory.create(LT(1), "SpringExpressions.NamedArgumentNode");
				astFactory.makeASTRoot(ref currentAST, (AST)tmp139_AST);
				match(ID);
				match(ASSIGN);
				expression();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				namedArgument_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((tokenSet_9_.member(LA(1))) && (tokenSet_27_.member(LA(2)))) {
				argument();
				if (0 == inputState.guessing)
				{
					astFactory.addASTChild(ref currentAST, (AST)returnAST);
				}
				namedArgument_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_28_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = namedArgument_AST;
	}
	
	public void boolLiteral() //throws RecognitionException, TokenStreamException
{
		
		returnAST = null;
		ASTPair currentAST = new ASTPair();
		SpringExpressions.SpringAST boolLiteral_AST = null;
		
		try {      // for error handling
			if ((LA(1)==TRUE))
			{
				SpringExpressions.BooleanLiteralNode tmp141_AST = null;
				tmp141_AST = (SpringExpressions.BooleanLiteralNode) astFactory.create(LT(1), "SpringExpressions.BooleanLiteralNode");
				astFactory.addASTChild(ref currentAST, (AST)tmp141_AST);
				match(TRUE);
				boolLiteral_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else if ((LA(1)==FALSE)) {
				SpringExpressions.BooleanLiteralNode tmp142_AST = null;
				tmp142_AST = (SpringExpressions.BooleanLiteralNode) astFactory.create(LT(1), "SpringExpressions.BooleanLiteralNode");
				astFactory.addASTChild(ref currentAST, (AST)tmp142_AST);
				match(FALSE);
				boolLiteral_AST = (SpringExpressions.SpringAST)currentAST.root;
			}
			else
			{
				throw new NoViableAltException(LT(1), getFilename());
			}
			
		}
		catch (RecognitionException ex)
		{
			if (0 == inputState.guessing)
			{
				reportError(ex);
				recover(ex,tokenSet_2_);
			}
			else
			{
				throw ex;
			}
		}
		returnAST = boolLiteral_AST;
	}
	
	public new SpringExpressions.SpringAST getAST()
	{
		return (SpringExpressions.SpringAST) returnAST;
	}
	
	private void initializeFactory()
	{
		if (astFactory == null)
		{
			astFactory = new ASTFactory("SpringExpressions.SpringAST");
		}
		initializeASTFactory( astFactory );
	}
	static public void initializeASTFactory( ASTFactory factory )
	{
		factory.setMaxNodeType(75);
	}
	
	public static readonly string[] tokenNames_ = new string[] {
		@"""<0>""",
		@"""EOF""",
		@"""<2>""",
		@"""NULL_TREE_LOOKAHEAD""",
		@"""EXPR""",
		@"""OPERAND""",
		@"""false""",
		@"""true""",
		@"""and""",
		@"""or""",
		@"""xor""",
		@"""in""",
		@"""is""",
		@"""between""",
		@"""like""",
		@"""matches""",
		@"""null""",
		@"""typeof""",
		@"""as""",
		@"""LPAREN""",
		@"""SEMI""",
		@"""RPAREN""",
		@"""ASSIGN""",
		@"""DEFAULT""",
		@"""QMARK""",
		@"""COLON""",
		@"""PLUS""",
		@"""MINUS""",
		@"""STAR""",
		@"""DIV""",
		@"""MOD""",
		@"""POWER""",
		@"""LESS_THAN""",
		@"""GREATER_THAN""",
		@"""TYPE""",
		@"""ID""",
		@"""DOT""",
		@"""LBRACKET""",
		@"""COMMA""",
		@"""RBRACKET""",
		@"""BANG""",
		@"""SAFE_DOT""",
		@"""SAFE_LBRACKET""",
		@"""POUND""",
		@"""DOLLAR""",
		@"""AT""",
		@"""PROJECT""",
		@"""RCURLY""",
		@"""SELECT""",
		@"""SELECT_FIRST""",
		@"""SELECT_LAST""",
		@"""QUOTE""",
		@"""STRING_LITERAL""",
		@"""LAMBDA""",
		@"""PIPE""",
		@"""new""",
		@"""LCURLY""",
		@"""INTEGER_LITERAL""",
		@"""HEXADECIMAL_INTEGER_LITERAL""",
		@"""REAL_LITERAL""",
		@"""EQUAL""",
		@"""NOT_EQUAL""",
		@"""LESS_THAN_OR_EQUAL""",
		@"""GREATER_THAN_OR_EQUAL""",
		@"""WS""",
		@"""BACKTICK""",
		@"""BACKSLASH""",
		@"""DOT_ESCAPED""",
		@"""APOS""",
		@"""NUMERIC_LITERAL""",
		@"""DECIMAL_DIGIT""",
		@"""INTEGER_TYPE_SUFFIX""",
		@"""HEX_DIGIT""",
		@"""EXPONENT_PART""",
		@"""SIGN""",
		@"""REAL_TYPE_SUFFIX"""
	};
	
	private static long[] mk_tokenSet_0_()
	{
		long[] data = { 2L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_0_ = new BitSet(mk_tokenSet_0_());
	private static long[] mk_tokenSet_1_()
	{
		long[] data = { 141562158776322L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_1_ = new BitSet(mk_tokenSet_1_());
	private static long[] mk_tokenSet_2_()
	{
		long[] data = { -1150732394136011006L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_2_ = new BitSet(mk_tokenSet_2_());
	private static long[] mk_tokenSet_3_()
	{
		long[] data = { 141562188136450L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_3_ = new BitSet(mk_tokenSet_3_());
	private static long[] mk_tokenSet_4_()
	{
		long[] data = { 141562188136962L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_4_ = new BitSet(mk_tokenSet_4_());
	private static long[] mk_tokenSet_5_()
	{
		long[] data = { 141562188137986L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_5_ = new BitSet(mk_tokenSet_5_());
	private static long[] mk_tokenSet_6_()
	{
		long[] data = { -1152921491721881600L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_6_ = new BitSet(mk_tokenSet_6_());
	private static long[] mk_tokenSet_7_()
	{
		long[] data = { 141562188138242L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_7_ = new BitSet(mk_tokenSet_7_());
	private static long[] mk_tokenSet_8_()
	{
		long[] data = { -1152779929533743358L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_8_ = new BitSet(mk_tokenSet_8_());
	private static long[] mk_tokenSet_9_()
	{
		long[] data = { 1132507061394800832L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_9_ = new BitSet(mk_tokenSet_9_());
	private static long[] mk_tokenSet_10_()
	{
		long[] data = { -1152779929332416766L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_10_ = new BitSet(mk_tokenSet_10_());
	private static long[] mk_tokenSet_11_()
	{
		long[] data = { -1152779927453368574L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_11_ = new BitSet(mk_tokenSet_11_());
	private static long[] mk_tokenSet_12_()
	{
		long[] data = { -1152779925305884926L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_12_ = new BitSet(mk_tokenSet_12_());
	private static long[] mk_tokenSet_13_()
	{
		long[] data = { 1132505961681846464L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_13_ = new BitSet(mk_tokenSet_13_());
	private static long[] mk_tokenSet_14_()
	{
		long[] data = { -1152779925305622782L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_14_ = new BitSet(mk_tokenSet_14_());
	private static long[] mk_tokenSet_15_()
	{
		long[] data = { -2251799849336848L, 4095L, 0L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_15_ = new BitSet(mk_tokenSet_15_());
	private static long[] mk_tokenSet_16_()
	{
		long[] data = { 35651584L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_16_ = new BitSet(mk_tokenSet_16_());
	private static long[] mk_tokenSet_17_()
	{
		long[] data = { -1152779787866931454L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_17_ = new BitSet(mk_tokenSet_17_());
	private static long[] mk_tokenSet_18_()
	{
		long[] data = { -20266198323167294L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_18_ = new BitSet(mk_tokenSet_18_());
	private static long[] mk_tokenSet_19_()
	{
		long[] data = { 8589934592L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_19_ = new BitSet(mk_tokenSet_19_());
	private static long[] mk_tokenSet_20_()
	{
		long[] data = { 283467841536L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_20_ = new BitSet(mk_tokenSet_20_());
	private static long[] mk_tokenSet_21_()
	{
		long[] data = { 2047531169611776L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_21_ = new BitSet(mk_tokenSet_21_());
	private static long[] mk_tokenSet_22_()
	{
		long[] data = { 824635817984L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_22_ = new BitSet(mk_tokenSet_22_());
	private static long[] mk_tokenSet_23_()
	{
		long[] data = { 687195291648L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_23_ = new BitSet(mk_tokenSet_23_());
	private static long[] mk_tokenSet_24_()
	{
		long[] data = { 18014398509481984L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_24_ = new BitSet(mk_tokenSet_24_());
	private static long[] mk_tokenSet_25_()
	{
		long[] data = { -1078674800098083070L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_25_ = new BitSet(mk_tokenSet_25_());
	private static long[] mk_tokenSet_26_()
	{
		long[] data = { 141012366262272L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_26_ = new BitSet(mk_tokenSet_26_());
	private static long[] mk_tokenSet_27_()
	{
		long[] data = { -2393087092457536L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_27_ = new BitSet(mk_tokenSet_27_());
	private static long[] mk_tokenSet_28_()
	{
		long[] data = { 274880004096L, 0L};
		return data;
	}
	public static readonly BitSet tokenSet_28_ = new BitSet(mk_tokenSet_28_());
	
}
}
