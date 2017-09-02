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
using System.Reflection;
using System.Runtime.Serialization;
using SpringCore.TypeResolution;
using SpringExpressions;
using Expression = System.Linq.Expressions.Expression;

// do not change the namespace!
namespace SpringContext.Support
{
    /// <summary>
    /// Represents a reference to a Spring-managed object.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class ReferenceNode : BaseNode
    {
        /// <summary>
        /// Create a new instance
        /// </summary>
        public ReferenceNode():base()
        {
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected ReferenceNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

	    protected override Expression GetExpressionTreeIfPossible(Expression contextExpression, Expression evalContext)
	    {
			if (getNumberOfChildren() == 2)
			{
				string typeName = getFirstChild().getText();
				string objectName = getFirstChild().getNextSibling().getText();

				var type = TypeResolutionUtils.ResolveType(typeName);

				return Expression.Convert(
					Expression.Call(
						createObjectMi, Expression.Constant(type), Expression.Constant(objectName)), type);
			}
			else
			{
				string typeName = getFirstChild().getText();
				var type = TypeResolutionUtils.ResolveType(typeName);

				return Expression.Convert(
					Expression.Call(
						createObjectMi, Expression.Constant(type), Expression.Constant(
							null, typeof(string))), type);
			}

		}

		/// <summary>
		/// Returns a value for the integer literal node.
		/// </summary>
		/// <param name="context">Context to evaluate expressions against.</param>
		/// <param name="evalContext">Current expression evaluation context.</param>
		/// <returns>Node's value.</returns>
		protected override object Get(object context, EvaluationContext evalContext)
        {
            if (getNumberOfChildren() == 2)
            {
                string typeName = getFirstChild().getText();
                string objectName = getFirstChild().getNextSibling().getText();

                var type = TypeResolutionUtils.ResolveType(typeName);

                return ReferenceObjectFactory.InvokeCreateObject(type, objectName);

            }
            else
            {
                string typeName = getFirstChild().getText();
                var type = TypeResolutionUtils.ResolveType(typeName);

                return ReferenceObjectFactory.InvokeCreateObject(type, null);
            }
        }

	    private MethodInfo createObjectMi =
		    typeof(ReferenceObjectFactory).GetMethod("InvokeCreateObject", 
				BindingFlags.NonPublic | BindingFlags.Static);
    }
}