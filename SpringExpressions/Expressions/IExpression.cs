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
using System.ComponentModel;

namespace SpringExpressions
{
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

           // todo: error: zmieniæ mo¿e nazwy IGetterExpression

          // todo: error: problem jest taki, ¿e trzeba by ka¿d¹ klasê zrobiæ generyczn¹! tej!
          // todo: error: a to jest chujnia z grzybni¹!
    public interface IStronglyTypedExpression
    { }

           // todo: error: czy na pewno? - czy mo¿e osobny interface dla Get Set Execute
          // todo: serio? jak siê mamy dowiedzieæ, czy jest kompilowalne
    public interface IGetterExpression<in TRoot, out TResult> : IStronglyTypedExpression
    {
        TResult GetValue(TRoot context, IDictionary<string, object> variables = null);
    }

    public interface IGetterExpression<out TResult> : IStronglyTypedExpression
    {
        TResult GetValue(IDictionary<string, object> variables = null);
    }

    public interface ISetterExpression<in TRoot, in TValue> : IStronglyTypedExpression
    {
        void SetValue(TRoot context, TValue newValue, IDictionary<string, object> variables = null);
    }

    public interface ISetterExpression<in TValue> : IStronglyTypedExpression
    {
        void SetValue(TValue newValue, IDictionary<string, object> variables = null);
    }

    public interface IVoidExpression : IStronglyTypedExpression
    {
        void Execute(IDictionary<string, object> variables = null);
    }

    public interface IVoidExpression<in TRoot> : IStronglyTypedExpression
    {
        void Execute(TRoot context, IDictionary<string, object> variables = null);
    }

        // todo: error: SwitchOnCompileFailure SwitchOnExecutionFailure?
       // todO: error: czy mo¿e rozbiæ jakoœ te opcje???
    [Flags]
    public enum CompileOptions
    {
        None,

        CompileOnParse = 1,
        CompileOnFirstExecution = 2,

        MustCompile = 16,
        TryCompileSwitchToInterpreterOnFailure = 32,
        MustUseInterpreter = 64,

        Default = CompileOnFirstExecution | TryCompileSwitchToInterpreterOnFailure
    }
}
