#region License

/*
 * Copyright � 2002-2011 the original author or authors.
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
using System.Runtime.Serialization;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed projection node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class ProjectionNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public ProjectionNode():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected ProjectionNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        protected override LExpression GetExpressionTreeIfPossible(LExpression contextExpression,
            CompilationContext compilationContext)
        {
            return null;
        }

        /// <summary>
        /// Returns a <see cref="IList"/> containing results of evaluation
        /// of projection expression against each node in the context.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            IEnumerable enumerable = context as IEnumerable;
            if(enumerable == null)
            {
                throw new ArgumentException(
                    "Projection can only be used on an instance of the type that implements IEnumerable.");
            }

            BaseNode expression = (BaseNode) this.getFirstChild();
            IList projectedList = new ArrayList();
            using (evalContext.SwitchThisContext())
            {
                foreach(object o in enumerable)
                {
                    evalContext.ThisContext = o;
                    projectedList.Add(GetValue(expression, o, evalContext));
                }
            }
            return projectedList;
        }
    }
}