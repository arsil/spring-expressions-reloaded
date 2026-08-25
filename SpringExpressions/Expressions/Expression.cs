#region License

/*
 * Copyright © 2002-2011 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using SpringExpressions.Parser;
using SpringExpressions.Parser.antlr;
using SpringExpressions.Parser.antlr.collections;
using SpringCore;
using SpringExpressions.Expressions.Compiling.Expressions;
using SpringReflection.Dynamic;
using SpringUtil;
using StringUtils = SpringUtil.StringUtils;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Container object for the parsed expression.
    /// </summary>
    /// <remarks>
    /// <p>
    /// Preparing this object once and reusing it many times for expression
    /// evaluation can result in significant performance improvements, as 
    /// expression parsing and reflection lookups are only performed once. 
    /// </p>
    /// </remarks>
    /// <author>Aleksandar Seovic</author>
    public class Expression : BaseNode
    {
        /// <summary>
        /// Contains a list of reserved variable names.
        /// You must not use any variable names with the reserved prefix!
        /// </summary>
        public class ReservedVariableNames
        {
            /// <summary>
            /// Variable Names using this prefix are reserved for internal framework use
            /// </summary>
            public static readonly string RESERVEDPREFIX = "____spring_";

            /// <summary>
            /// variable name of the currently processed object factory, if any
            /// </summary>
            internal static readonly string CurrentObjectFactory = RESERVEDPREFIX + "CurrentObjectFactory";
        }

        private class ASTNodeCreator : Parser.antlr.ASTNodeCreator
        {
            private readonly SafeConstructor ctor;
            private readonly string name;

            public ASTNodeCreator(ConstructorInfo ctor)
            {
                this.ctor = new SafeConstructor(ctor);
                this.name = ctor.DeclaringType.FullName;
            }

            public override AST Create()
            {
                return (AST) ctor.Invoke(new object[0]);
            }

            public override string ASTNodeTypeName
            {
                get { return name; }
            }
        }

        private class SpringASTFactory : ASTFactory
        {
            private static readonly Type BASENODE_TYPE;
            private static readonly Hashtable Typename2Creator;

            static SpringASTFactory()
            {
                BASENODE_TYPE = typeof (SpringAST);

                Typename2Creator = new Hashtable();
                foreach (Type type in typeof(SpringASTFactory).Assembly.GetTypes())
                {
                    if (BASENODE_TYPE.IsAssignableFrom(type))
                    {
                        if (type.IsAbstract || type.IsInterface)
                            continue;

                        ConstructorInfo ctor = type.GetConstructor(new Type[0]);
                        if (ctor != null)
                        {
                            ASTNodeCreator creator = new ASTNodeCreator(ctor);
                            Typename2Creator[creator.ASTNodeTypeName] = creator;
                        }
                    }
                }
                Typename2Creator[BASENODE_TYPE.FullName] = SpringAST.Creator;
            }

            public SpringASTFactory() : base(BASENODE_TYPE)
            {
                base.defaultASTNodeTypeObject_ = BASENODE_TYPE;
                base.typename2creator_ = Typename2Creator;
            }
        }

        private class SpringExpressionParser : ExpressionParser
        {
            public SpringExpressionParser( TokenStream lexer )
                : base( lexer )
            {
                base.astFactory = new SpringASTFactory();
                base.initialize();
            }
        }

        static Expression()
        {
            // Ensure antlr is loaded (fixes GAC issues)!
            Assembly antlrAss = typeof( Parser.antlr.LLkParser ).Assembly;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Expression"/> class
        /// by parsing specified expression string.
        /// </summary>
        /// <param name="expression">Expression to parse.</param>
        public static IWeaklyTypedExpression Parse(
            string expression,
            EvaluationMode mode = EvaluationMode.CompileOrInterpret)
        {
            return Wrap(ParseAst(expression), mode);
        }

        /// <summary>
        /// Parses an expression and returns its root AST node.
        /// </summary>
        /// <remarks>
        /// The parse itself. <see cref="Parse"/> is this plus <see cref="Wrap"/>; the strongly typed
        /// factories below use this directly, because they hand the tree to a compiler rather than
        /// evaluating it.
        /// </remarks>
        internal static BaseNode ParseAst(string expression)
        {
            if (StringUtils.HasText( expression ))
            {
                ExpressionLexer lexer = new ExpressionLexer( new StringReader( expression ) );
                ExpressionParser parser = new SpringExpressionParser( lexer );

                try
                {
                    parser.expr();
                }
                catch (TokenStreamRecognitionException ex)
                {
                    throw new SyntaxErrorException( ex.recog.Message, ex.recog.getLine(), ex.recog.getColumn(), expression );
                }

                var springAst = parser.getAST();
                /****
                using (TextWriter tw = Console.Out)
                    springAst.xmlSerialize(tw);
                ****/
                return (BaseNode)springAst;
            }
            else
            {
                return new Expression();
            }
        }

        /// <summary>
        /// Makes an AST node evaluable.
        /// </summary>
        /// <remarks>
        /// Use this for a node that did not come from <see cref="Parse"/> - one built by hand, or one taken
        /// out of a larger tree. The result owns the compiled form of that node: compiled state lives here
        /// rather than on the tree, so the same node can be evaluated against several context types, each
        /// getting its own compiled form.
        /// </remarks>
        /// <param name="expressionNode">The node to evaluate; must not be null.</param>
        [NotNull]
        public static IWeaklyTypedExpression Wrap(
            [NotNull] BaseNode expressionNode,
            EvaluationMode mode = EvaluationMode.CompileOrInterpret)
        {
            AssertUtils.ArgumentNotNull(expressionNode, "expressionNode");

            return new WeaklyTypedExpression(expressionNode, mode);
        }

            // todo: error: a możę ParseAndCompile()
            // todo: error: compile options!
        [NotNull]
        public static IGetterExpression<TRoot, TResult> ParseGetter<TRoot, TResult>(
            [NotNull] string expression,
            EvaluationMode mode = EvaluationMode.CompileOrInterpret)
        {
            AssertUtils.ArgumentHasText(expression, "expression");

            return new GetterExpression<TRoot, TResult>(ParseAst(expression), mode);
        }

        [NotNull]
        public static IGetterExpression<TResult> ParseGetter<TResult>(
            [NotNull] string expression,
            EvaluationMode mode = EvaluationMode.CompileOrInterpret)
        {
            AssertUtils.ArgumentHasText(expression, "expression");

            return new GetterExpression<TResult>(ParseAst(expression), mode);
        }

        [NotNull]
        public static ISetterExpression<TRoot, TArgument> ParseSetter<TRoot, TArgument>(
            [NotNull] string expression,
            EvaluationMode mode = EvaluationMode.CompileOrInterpret)
        {
            AssertUtils.ArgumentHasText(expression, "expression");

            return new SetterExpression<TRoot, TArgument>(ParseAst(expression), mode);
        }

        [NotNull]
        public static ISetterExpression<TArgument> ParseSetter<TArgument>(
            [NotNull] string expression,
            EvaluationMode mode = EvaluationMode.CompileOrInterpret)
        {
            AssertUtils.ArgumentHasText(expression, "expression");

            return new SetterExpression<TArgument>(ParseAst(expression), mode);
        }

        [NotNull]
        public static IVoidExpression<TRoot> ParseVoidExpression<TRoot>(
            [NotNull] string expression,
            EvaluationMode mode = EvaluationMode.CompileOrInterpret)
        {
            AssertUtils.ArgumentHasText(expression, "expression");

            return new VoidExpression<TRoot>(ParseAst(expression), mode);
        }

        [NotNull]
        public static IVoidExpression ParseVoidExpression(
            [NotNull] string expression,
            EvaluationMode mode = EvaluationMode.CompileOrInterpret)
        {
            AssertUtils.ArgumentHasText(expression, "expression");

            return new VoidExpression(ParseAst(expression), mode);
        }


        /// <summary>
        /// Registers lambda expression under the specified <paramref name="functionName"/>.
        /// </summary>
        /// <param name="functionName">Function name to register expression as.</param>
        /// <param name="lambdaExpression">Lambda expression to register.</param>
        /// <param name="variables">Variables dictionary that the function will be registered in.</param>
        public static void RegisterFunction(
            [NotNull] string functionName,
            [NotNull] string lambdaExpression, IDictionary variables )
        {
            AssertUtils.ArgumentHasText( functionName, "functionName" );
            AssertUtils.ArgumentHasText( lambdaExpression, "lambdaExpression" );

            ExpressionLexer lexer = new ExpressionLexer( new StringReader( lambdaExpression ) );
            ExpressionParser parser = new SpringExpressionParser( lexer );

            try
            {
                parser.lambda();
            }
            catch (TokenStreamRecognitionException ex)
            {
                throw new SyntaxErrorException( ex.recog.Message, ex.recog.getLine(), ex.recog.getColumn(), lambdaExpression );
            }
            variables[functionName] = parser.getAST();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Expression"/> class
        /// by parsing specified primary expression string.
        /// </summary>
        /// <param name="expression">Primary expression to parse.</param>
        internal static IExpression ParsePrimary( string expression )
        {
            if (StringUtils.HasText( expression ))
            {
                ExpressionLexer lexer = new ExpressionLexer( new StringReader( expression ) );
                ExpressionParser parser = new SpringExpressionParser( lexer );

                try
                {
                    parser.primaryExpression();
                }
                catch (TokenStreamRecognitionException ex)
                {
                    throw new SyntaxErrorException( ex.recog.Message, ex.recog.getLine(), ex.recog.getColumn(), expression );
                }
                return Wrap((BaseNode)parser.getAST());
            }
            else
            {
                return Wrap(new Expression());
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Expression"/> class
        /// by parsing specified property expression string.
        /// </summary>
        /// <param name="expression">Property expression to parse.</param>
        internal static IExpression ParseProperty( string expression )
        {
            if (StringUtils.HasText( expression ))
            {
                ExpressionLexer lexer = new ExpressionLexer( new StringReader( expression ) );
                ExpressionParser parser = new SpringExpressionParser( lexer );

                try
                {
                    parser.property();
                }
                catch (TokenStreamRecognitionException ex)
                {
                    throw new SyntaxErrorException( ex.recog.Message, ex.recog.getLine(), ex.recog.getColumn(), expression );
                }
                return Wrap((BaseNode)parser.getAST());
            }
            else
            {
                return Wrap(new Expression());
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Expression"/> class.
        /// </summary>
        public Expression()
        { }

                /// <summary>
        /// Evaluates this expression for the specified root object and returns 
        /// value of the last node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Value of the last node.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            object result = context;

            var node = (BaseNode) getFirstChild();
            while (node != null)
            {
                // A node reached through '?.' or '?[' is skipped when the value flowing into it is null,
                // and the rest of the chain is abandoned with it: 'a?.B.C' yields null rather than trying
                // '.C' on nothing. Each node is still visited at most once, so a chain rooted in a method
                // call such as 'GetItems()?[0]' invokes that method exactly once.
                if (node.IsNullConditional && result == null)
                    return null;

                result = GetValue(node, result, evalContext);
                node = (BaseNode) node.getNextSibling();
            }

            return result;
        }

	    protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
	    {
	        return BuildChainExpression((BaseNode)getFirstChild(), contextExpression, compilationContext);
	    }

        /// <summary>
        /// Builds the expression tree for an access chain, threading each node's result into the next.
        /// </summary>
        /// <remarks>
        /// Recursive rather than a loop because a null-conditional link has to place <i>everything that
        /// follows it</i> inside the non-null branch of a conditional: 'a?.B.C' must skip both '.B' and
        /// '.C' when 'a' is null.
        /// </remarks>
        /// <returns>
        /// The chain expression, or null when some node cannot be compiled - the caller then falls back
        /// to interpreting the whole expression.
        /// </returns>
        private static LExpression BuildChainExpression(
            BaseNode node,
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            if (node == null)
                return contextExpression;

            var nextNode = (BaseNode)node.getNextSibling();

            if (!node.IsNullConditional)
            {
                var appliedNode = GetExpressionTreeIfPossible(node, contextExpression, compilationContext);

                return BuildChainExpression(nextNode, appliedNode, compilationContext);
            }

            var contextType = contextExpression.Type;
            var underlyingType = Nullable.GetUnderlyingType(contextType);

            // A non-nullable value type can never be null, so there is nothing to test: emit the plain
            // access and skip the conditional altogether rather than generating a test that is always false.
            if (contextType.IsValueType && underlyingType == null)
            {
                var appliedNode = GetExpressionTreeIfPossible(node, contextExpression, compilationContext);

                return BuildChainExpression(nextNode, appliedNode, compilationContext);
            }

            // The left-hand side is assigned to a temporary and the rest of the chain reads the temporary.
            // Referring to contextExpression twice instead - once in the null test, once in the access -
            // would duplicate that whole subtree and evaluate it twice, so 'GetItems()?[0]' would call
            // GetItems() twice. The temporary is what keeps evaluation to exactly once.
            var temporary = LExpression.Variable(contextType, "nullConditionalOperand");

            LExpression isNullTest;
            LExpression nonNullContext;

            if (underlyingType != null)
            {
                // Nullable<T>: test HasValue and unwrap for the access. Comparing a Nullable<T> to null with
                // LExpression.Equal produces a lifted bool?, which Condition will not accept as its test.
                isNullTest = LExpression.Not(LExpression.Property(temporary, "HasValue"));
                nonNullContext = LExpression.Property(temporary, "Value");
            }
            else
            {
                isNullTest = LExpression.Equal(temporary, LExpression.Constant(null, contextType));
                nonNullContext = temporary;
            }

            var applied = GetExpressionTreeIfPossible(node, nonNullContext, compilationContext);

            var restOfChain = BuildChainExpression(nextNode, applied, compilationContext);

            // A chain ending in a void call produces no value, so there is nothing to return from either
            // branch and Condition would demand two matching non-void branches. Guard the call instead.
            if (restOfChain.Type == typeof(void))
            {
                return LExpression.Block(
                    new[] { temporary },
                    LExpression.Assign(temporary, contextExpression),
                    LExpression.IfThen(LExpression.Not(isNullTest), restOfChain));
            }

            // Short-circuiting has to be able to yield null, so a value-typed result widens to Nullable<T>:
            // 'a?.Count' is an int? that is null when 'a' is null, never a default 0.
            var resultType = LiftToNullable(restOfChain.Type);

            return LExpression.Block(
                resultType,
                new[] { temporary },
                LExpression.Assign(temporary, contextExpression),
                LExpression.Condition(
                    isNullTest,
                    LExpression.Default(resultType),
                    resultType == restOfChain.Type
                        ? restOfChain
                        : LExpression.Convert(restOfChain, resultType)));
        }

        /// <summary>
        /// Widens a value type to its nullable form so that it can carry null; other types are unchanged.
        /// </summary>
        private static Type LiftToNullable(Type type)
        {
            return type.IsValueType && Nullable.GetUnderlyingType(type) == null
                ? typeof(Nullable<>).MakeGenericType(type)
                : type;
        }

        /// <summary>
        /// Rejects assignment to a chain containing a null-conditional operator.
        /// </summary>
        /// <remarks>
        /// Such a chain has no well-defined target when it short-circuits - there is nothing to assign to -
        /// so the whole expression is refused rather than silently assigning nothing or throwing later from
        /// somewhere less obvious.
        /// </remarks>
        private void AssertChainIsAssignable()
        {
            for (var node = (BaseNode)getFirstChild(); node != null; node = (BaseNode)node.getNextSibling())
            {
                if (node.IsNullConditional)
                    throw new NotSupportedException(
                        "A null-conditional access ('?.' or '?[') cannot be used as the target of an assignment.");
            }
        }

        protected override LExpression GetExpressionTreeForSetterIfPossible(
            LExpression contextExpression, 
            CompilationContext compilationContext,
            LExpression newValueExpression)
        {
            AssertChainIsAssignable();

            LExpression target = contextExpression;
            if (getNumberOfChildren() > 0)
            {
                var node = getFirstChild();

                for (int i = 0; i < getNumberOfChildren() - 1; i++)
                {
                    try
                    {
                        target = GetExpressionTreeIfPossible(((BaseNode)node), target, compilationContext);

                        node = node.getNextSibling();
                    }
                    catch (NotReadablePropertyException e)
                    {
                        throw new NotWritablePropertyException(
                            "Cannot read the value of '" + node.getText() + "' property in the expression.", e);
                    }
                }

                return GetExpressionTreeForSetterIfPossible((BaseNode)node, target, compilationContext, newValueExpression);
            }

            throw new NotSupportedException("You cannot set the value for an empty expression.");
        }

        /// <summary>
		/// Evaluates this expression for the specified root object and sets 
		/// value of the last node.
		/// </summary>
		/// <param name="context">Context to evaluate expressions against.</param>
		/// <param name="evalContext">Current expression evaluation context.</param>
		/// <param name="newValue">Value to set last node to.</param>
		/// <exception cref="NotSupportedException">If navigation expression is empty.</exception>
		protected override void Set( object context, EvaluationContext evalContext, object newValue )
        {
            AssertChainIsAssignable();

            object target = context;

            if (this.getNumberOfChildren() > 0)
            {
                AST node = this.getFirstChild();

                for (int i = 0; i < this.getNumberOfChildren() - 1; i++)
                {
                    try
                    {
                        target = GetValue(((BaseNode)node), target, evalContext);
                        node = node.getNextSibling();
                    }
                    catch (NotReadablePropertyException e)
                    {
                        throw new NotWritablePropertyException( "Cannot read the value of '" + node.getText() + "' property in the expression.", e );
                    }
                }
                SetValue(((BaseNode)node), target, evalContext, newValue);
            }
            else
            {
                throw new NotSupportedException( "You cannot set the value for an empty expression." );
            }
        }

        /// <summary>
        /// Evaluates this expression for the specified root object and returns 
        /// <see cref="PropertyInfo"/> of the last node, if possible.
        /// </summary>
        /// <param name="context">Context to evaluate expression against.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <returns>Value of the last node.</returns>
        internal PropertyInfo GetPropertyInfo( object context, IDictionary<string, object> variables )
        {
            if (this.getNumberOfChildren() > 0)
            {
                object target = context;
                AST node = this.getFirstChild();

                for (int i = 0; i < this.getNumberOfChildren() - 1; i++)
                {
                    target = Wrap((BaseNode)node).GetValue(target, variables);
                    node = node.getNextSibling();
                }

                if (node is PropertyOrFieldNode)
                {
                    return (PropertyInfo)((PropertyOrFieldNode)node).GetMemberInfo( target );
                }
                else if (node is IndexerNode)
                {
                    return ((IndexerNode)node).GetPropertyInfo( target, variables );
                }
                else
                {
                    throw new FatalReflectionException( "Cannot obtain PropertyInfo from an expression that does not resolve to a property or an indexer." );
                }
            }

            throw new FatalReflectionException( "Cannot obtain PropertyInfo for empty property name." );
        }
    }
}


