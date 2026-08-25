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
using JetBrains.Annotations;

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
        /// <remarks>
        /// <typeparamref name="TContext"/> is inferred from the call site, so the expression binds members
        /// against the type the caller declared - the type C# itself would bind against - instead of the
        /// runtime type of whichever root the expression happened to see first. A caller holding an
        /// <c>object</c> gets <c>TContext = object</c>, which is exactly the old behaviour; a null literal
        /// has no type to infer, so it needs <c>GetValue&lt;object&gt;(null)</c> or a cast.
        /// </remarks>
        /// <param name="context">Object to evaluate expression against; may be null.</param>
        /// <returns>Value of the expression.</returns>
        object GetValue<TContext>(TContext context);

        /// <summary>
        /// Returns expression value.
        /// </summary>
        /// <param name="context">Object to evaluate expression against; may be null.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <returns>Value of the expression.</returns>
        object GetValue<TContext>(TContext context, IDictionary<string, object> variables);

        /// <summary>
        /// Sets expression value.
        /// </summary>
        /// <param name="context">Object to evaluate expression against; may be null.</param>
        /// <param name="newValue">New value for the last node of the expression.</param>
        void SetValue<TContext>(TContext context, object newValue);

        /// <summary>
        /// Sets expression value.
        /// </summary>
        /// <param name="context">Object to evaluate expression against; may be null.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <param name="newValue">New value for the last node of the expression.</param>
        void SetValue<TContext>(TContext context, IDictionary<string, object> variables, object newValue);
    }

           // todo: error: zmienić może nazwy IGetterExpression

          // todo: error: problem jest taki, że trzeba by każdą klasę zrobić generyczną! tej!
          // todo: error: a to jest chujnia z grzybnią!
    public interface IStronglyTypedExpression
    { }

    /// <summary>
    /// The weakly typed face of a parsed expression - what <see cref="Expression.Parse"/> returns.
    /// </summary>
    /// <remarks>
    /// An empty marker mirroring <see cref="IStronglyTypedExpression"/>, and it exists for the same
    /// reason: to say what kind of expression this is. <see cref="IExpression"/> alone does not - it
    /// reads like the root of a hierarchy when it is one specific face, and nothing derives from it on
    /// the strongly typed side. The inherited name is kept because Spring.NET consumers know it and
    /// <c>SpringExpressionsLegacyTests</c> is built on it; this name is added beside it rather than
    /// replacing it.
    /// </remarks>
    public interface IWeaklyTypedExpression : IExpression
    { }

           // todo: error: czy na pewno? - czy może osobny interface dla Get Set Execute
          // todo: serio? jak się mamy dowiedzieć, czy jest kompilowalne
    public interface IGetterExpression<in TRoot, out TResult> : IStronglyTypedExpression
    {
        TResult GetValue(TRoot context, [CanBeNull] IDictionary<string, object> variables = null);
    }

    public interface IGetterExpression<out TResult> : IStronglyTypedExpression
    {
        TResult GetValue([CanBeNull] IDictionary<string, object> variables = null);
    }

    public interface ISetterExpression<in TRoot, in TValue> : IStronglyTypedExpression
    {
        void SetValue(TRoot context, TValue newValue, [CanBeNull] IDictionary<string, object> variables = null);
    }

    public interface ISetterExpression<in TValue> : IStronglyTypedExpression
    {
        void SetValue(TValue newValue, IDictionary<string, object> variables = null);
    }

    public interface IVoidExpression : IStronglyTypedExpression
    {
        void Execute([CanBeNull] IDictionary<string, object> variables = null);
    }

    public interface IVoidExpression<in TRoot> : IStronglyTypedExpression
    {
        void Execute(TRoot context, [CanBeNull] IDictionary<string, object> variables = null);
    }

}
