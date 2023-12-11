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
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

using SpringExpressions.Expressions.LinqExpressionHelpers;
using SpringExpressions.Parser.antlr.collections;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents lambda expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class LambdaExpressionNode : BaseNode
    {
        /// <summary>
        /// caches argumentNames of this instance
        /// </summary>
        private string[] argumentNames;
        
        /// <summary>
        /// caches body expression of this lambda function
        /// </summary>
        private BaseNode bodyExpression;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public LambdaExpressionNode()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected LambdaExpressionNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
        
        /// <summary>
        /// Gets argument names for this lambda expression.
        /// </summary>
        public string[] ArgumentNames
        {
            get
            {
                if(bodyExpression == null)
                {
                    InitializeLambda();
                }
                return argumentNames;
            }
        }

        /// <summary>
        /// Assigns value of the right operand to the left one.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            if(bodyExpression == null)
            {
                InitializeLambda();
            }

            object result = GetValue(bodyExpression, context, evalContext);
            return result;
        }

        /// <summary>
        /// Evaluates this node, switching local variables map to the ones specified in <paramref name="argValues"/>.
        /// </summary>
        protected override object Get(object context, EvaluationContext evalContext, object[] argValues)
        {
            string[] argNames = this.ArgumentNames;

            if (argValues.Length != argNames.Length)
            {
                throw new ArgumentMismatchException(string.Format("Invalid number of arguments - expected {0} arguments, but was called with {1}", argNames.Length, argValues.Length));
            }

            IDictionary arguments = new Hashtable();
            for (int i = 0; i < argValues.Length; i++)
            {
                arguments[argNames[i]] = argValues[i];
            }

            EvaluationContext ec = (EvaluationContext)evalContext;
            using (ec.SwitchLocalVariables(arguments))
            {
                object result = Get(context, ec);
                return result;
            }
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            int childrenCount = getNumberOfChildren();

            if (childrenCount > 1)
            {
                // todo: error: to jest prawdziwe tylko dla kolekcji! -----
                if (!MethodBaseHelpers.IsGenericEnumerable(contextExpression.Type, out var itemType))
                    return null;

                var blockNodes = new List<LExpression>();
                var parameterExpressions = new List<ParameterExpression>();
                var parameterTypes = new List<Type>();

                        // todo: error: serio? null context? this context?
                var newCompilationContext = new CompilationContext(
                    compilationContext.RootContextExpression, null);

                AST argsNode = this.getFirstChild();
                //var argumentNames = new string[argsNode.getNumberOfChildren()];

                AST argNode = argsNode.getFirstChild();

                while (argNode != null)
                {
                    var variableName = argNode.getText();

                    var paramExpression = LExpression.Parameter(
                        itemType, "param_" + variableName);

                    parameterExpressions.Add(paramExpression);
                    parameterTypes.Add(itemType);


                    // todo:
//                    var variableExpression = LExpression.Variable(itemType, argName);
  //                  blockNodes.Add(LExpression.Assign(variableExpression, paramExpression));

                    // todo: to s¹ parametry! tej! w body nowym!!!
                    //compilationContext.AddParameter()
                    newCompilationContext.AddLocalVariable(variableName, paramExpression);
                    //newCompilationContext.AddLocalVariable(argName, variableExpression);


                    argNode = argNode.getNextSibling();
                }

                var bodyExpressionNode = (BaseNode)argsNode.getNextSibling();

                         // todo: error: context expression??? const z null expression? object?
                var bodyExpr = GetExpressionTreeIfPossible(bodyExpressionNode, null, newCompilationContext);

                if (bodyExpr == null)
                    return null;

                blockNodes.Add(bodyExpr);

                var blockExpression = LExpression.Block(blockNodes);



                parameterTypes.Add(blockExpression.Type);
                var funcType = LExpression.GetFuncType(parameterTypes.ToArray());

                // todo:
                // Call: Expression.Lambda<>()
                var finalLambdaMi = _lambdaMi.MakeGenericMethod(funcType);
                var functionExpr = finalLambdaMi.Invoke(null,
                    new object[] { blockExpression, parameterExpressions.ToArray() });

                var compileMi = functionExpr.GetType().GetMethod("Compile", System.Type.EmptyTypes);

// todo: error: debug for exception!!!------------------------------------------------------------------------------------------------------------------------
Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

                // Call: .Compile()
                var compiledFunction = compileMi.Invoke(functionExpr, new object[0]);

                     // todo: error: czy na pewno to coœ powinno zwraca? czy mo¿e NIE! czy mo¿e inna metoda nie GetExpressionTreeIfPossible
                return LExpression.Constant(compiledFunction);
            }

            return null;
        }

        private readonly MethodInfo _lambdaMi = typeof(LExpression).GetMethods().FirstOrDefault(
            x => x.Name.Equals("Lambda", StringComparison.OrdinalIgnoreCase)
                && x.IsGenericMethod && x.GetParameters().Length == 2
                && x.GetParameters()[0].ParameterType == typeof(LExpression)
                && x.GetParameters()[1].ParameterType == typeof(ParameterExpression[]));

        private void InitializeLambda()
        {
            lock (this)
            {
                if (bodyExpression == null)
                {
                    if (this.getNumberOfChildren() == 1)
                    {
                        argumentNames = new string[0];
                        bodyExpression = (BaseNode)this.getFirstChild();
                    }
                    else
                    {
                        AST argsNode = this.getFirstChild();
                        argumentNames = new string[argsNode.getNumberOfChildren()];
                        AST argNode = argsNode.getFirstChild();
                        int i = 0;
                        while (argNode != null)
                        {
                            argumentNames[i++] = argNode.getText();
                            argNode = argNode.getNextSibling();
                        }

                        bodyExpression = (BaseNode)argsNode.getNextSibling();
                    }
                }
            }
        }
    }
}