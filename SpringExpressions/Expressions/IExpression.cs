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

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace SpringExpressions
{
      // todo: error: strongly typed results, contexts?
       // todo: error: this is ALSO the result of parse method!!!! so.... when we are building Linq.Expression? Not in parse!!!!

    /// <summary>
    /// Interface that all navigation expression nodes have to implement.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [TypeConverter(typeof(ExpressionConverter))]
    public interface IExpression
    {
        /// <summary>
        /// Returns expression value.
        /// </summary>
        /// <returns>Value of the expression.</returns>
        object GetValue();

        /// <summary>
        /// Returns expression value.
        /// </summary>
        /// <param name="context">Object to evaluate expression against.</param>
        /// <returns>Value of the expression.</returns>
        object GetValue(object context);

        /// <summary>
        /// Returns expression value.
        /// </summary>
        /// <param name="context">Object to evaluate expression against.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <returns>Value of the expression.</returns>
        object GetValue(object context, IDictionary<string, object> variables);

           // todo: error: czy na pewno?
        TResult GetValue<TResult, TContext>(TContext context, IDictionary<string, object> variables = null);

        /// <summary>
        /// Sets expression value.
        /// </summary>
        /// <param name="context">Object to evaluate expression against.</param>
        /// <param name="newValue">New value for the last node of the expression.</param>
        void SetValue(object context, object newValue);

        /// <summary>
        /// Sets expression value.
        /// </summary>
        /// <param name="context">Object to evaluate expression against.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <param name="newValue">New value for the last node of the expression.</param>
        void SetValue(object context, IDictionary<string, object> variables, object newValue);
    }



          // todo: error: czy na pewno?
         // todo: serio? jak siê mamy dowiedzieæ, czy jest kompilowalne
    public interface ITypedExpression<TValue, in TContext>
    {
        TValue GetValue(TContext context, IDictionary<string, object> variables = null);
        void SetValue(TContext context, TValue value, IDictionary<string, object> variables = null);
    }
}