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
using System.Collections.Generic;
using System.Reflection;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed variable node.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class VariableNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public VariableNode():base()
        {
        }

        
        // todo: bieda polega na tym, iż tracimy tutaj informacje o zwracanym typie...
        // todo: na etapie kompilacji nie mamy nawet tego typu! i to jest super smutne!
        // todo: być może nie ma sensu tego przerabiać na kompilowane wyrażenie...

        // #root and #this come from the compilation context, a named #variable from the variables
        // dictionary the caller passes on every call - so nothing here reads an EvaluationContext.
/* - bieda */
        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            // todo: bieda... bo stracimy typ... kurwa... co za bieda... ale dowcip... kurwa.. .bieda. totalna!
            // todo: i po co myśmy to robili... 
            string varName = getText();

            // #this
            if (varName == "this")
            {
                // todo: error: to musi być strongly typed! shit!!!! a nie jest... co jest super słabe!!!
                // zwraca object
                //return LExpression.Field(compilationContext.EvalContext, "ThisContext");
                return compilationContext.ThisExpression;
            }

            // #root
            if (varName == "root")
            {
                /* todo: error: to popsuło test:
        Assert.IsInstanceOf(typeof (Int32?), ExpressionEvaluator.GetValue(test, "#root"));
        Assert.IsTrue((bool) ExpressionEvaluator.GetValue(test, "#root != null"));

                // the binaty Equal is nod dfined fo rtye types int32 and object

                // nie działą not equal
                // ale działa equal
                 */

                  // todo: error; czy to się jakoś zmienia? root? oto jest pyhtanie!---------------------------------- teraz zakładamy, że się nie zmienia.... 
                return compilationContext.RootContextExpression;

                //return LExpression.Field(evalContext, "RootContext");
            }

            // any other variable, eg.  #var1  #beat  #i
            var arguments = new List<LExpression>
                { LExpression.Constant(varName, typeof(string)) };

            // getting object; the dictionary is the caller's own, passed in as a delegate parameter
            return LExpression.Call(
                compilationContext.VariablesExpression,
                VariablesDictionaryIndexerMi,
                arguments);
        }

        protected override LExpression GetExpressionTreeForSetterIfPossible(
            LExpression contextExpression, 
            CompilationContext compilationContext,
            LExpression newValueExpression)
        {
            string variableName = getText();

            ValidateForbiddenVariablesForSetter(variableName);

            var arguments = new List<LExpression>
                {
                    compilationContext.VariablesExpression,
                    LExpression.Constant(variableName, typeof(string)),
                    newValueExpression
                };

            return LExpression.Call(MiSetVariable, arguments);
        }

        /// <summary>
        /// Returns value of the variable represented by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            string varName = getText();
            if (varName == "this")
            {
                return evalContext.ThisContext;
            }

            if (varName == "root")
            {
                return evalContext.RootContext;
            }

            return evalContext.Variables[varName];
        }

        /// <summary>
        /// Sets value of the variable represented by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <param name="newValue">New value for this node.</param>
        protected override void Set(object context, EvaluationContext evalContext, object newValue)
        {
            var variableName = getText();

            ValidateForbiddenVariablesForSetter(variableName);
            SetVariable(evalContext.Variables, variableName, newValue);
        }

        private static void ValidateForbiddenVariablesForSetter(string variableName)
        {
            if (variableName == "this" || variableName == "root")
            {
                throw new ArgumentException(
                    "You cannot assign a value to intrinsic variable '" + variableName + "'.");
            }
        }

        private static object SetVariable(IDictionary<string, object> variables, string variableName, object newValue)
        {
            if (variables == null)
            {
                throw new InvalidOperationException(
                    "You need to provide variables dictionary to expression evaluation engine " +
                    "in order to be able to set variable values.");
            }

            variables[variableName] = newValue;

            return newValue;
        }

        private static readonly MethodInfo MiSetVariable
            = ((Func<IDictionary<string, object>, string, object, object>)SetVariable).Method;

        private static readonly MethodInfo VariablesDictionaryIndexerMi
            = typeof(IDictionary<string, object>)
                .GetMethod("get_Item", new[] { typeof(string) });

    }
}