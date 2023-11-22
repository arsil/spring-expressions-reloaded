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
using System.Runtime.Serialization;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed variable node.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class VariableNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public VariableNode():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected VariableNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
        

        // todo: bieda polega na tym, i¿ tracimy tutaj informacje o zwracanym typie...
        // todo: na etapie kompilacji nie mamy nawet tego typu! i to jest super smutne!
        // todo: byæ mo¿e nie ma sensu tego przerabiaæ na kompilowane wyra¿enie...

        // todo: nie mamy tutaj w evalContext ani Root ani ThisContext ani Variables!
/* - bieda */
        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression, 
            LExpression evalContext)
        {
            // todo: bieda... bo stracimy typ... kurwa... co za bieda... ale dowcip... kurwa.. .bieda. totalna!
            // todo: i po co myœmy to robili... 
            string varName = getText();

            // #this
            if (varName == "this")
            {
                      // todo: error: to musi byæ strongly typed! shit!!!! a nie jest... co jest super s³abe!!!
                // zwraca object
                return LExpression.Field(evalContext, "ThisContext");
            }

            // #root
            if (varName == "root")
            {
                /* todo: error: to popsu³o test:
        Assert.IsInstanceOf(typeof (Int32?), ExpressionEvaluator.GetValue(test, "#root"));
        Assert.IsTrue((bool) ExpressionEvaluator.GetValue(test, "#root != null"));

                // the binaty Equal is nod dfined fo rtye types int32 and object

                // nie dzia³¹ not equal
                // ale dzia³a equal
                 */


                // zwraca object
                // todo: error; czy to siê jakoœ zmienia? root? oto jest pyhtanie!---------------------------------- teraz zak³adamy, ¿e siê nie zmienia.... 
                return contextExpression;

                //return LExpression.Field(evalContext, "RootContext");
            }

            // any other variable, eg.  #var1  #beat  #i
            var arguments = new List<LExpression>
                { LExpression.Constant(varName, typeof(string)) };

            // getting object
            return LExpression.Call(
                LExpression.Field(evalContext, "Variables"), 
                VariablesDictionaryIndexerMi,
                arguments);
        }

	    /// <summary>
        /// Returns value of the variable represented by this node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            string varName = this.getText();
            if (varName == "this")
            {
                return evalContext.ThisContext;
            }
            else if (varName == "root")
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
            string varName = this.getText();
            if (varName == "this" || varName == "root")
            {
                throw new ArgumentException("You cannot assign a value to intrinsic variable '" + varName + "'.");
            }
            if (evalContext.Variables == null)
            {
                throw new InvalidOperationException(
                    "You need to provide variables dictionary to expression evaluation engine in order to be able to set variable values.");
            }
            evalContext.Variables[varName] = newValue;
        }

        private static readonly MethodInfo VariablesDictionaryIndexerMi
            = typeof(IDictionary<string, object>)
                .GetMethod("get_Item", new[] { typeof(string) });

    }
}