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
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.Serialization;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed integer literal node.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    [Serializable]
    public class IntLiteralNode : BaseNode
    {
        private object nodeValue;

        /// <summary>
        /// Create a new instance
        /// </summary>
        public IntLiteralNode()
        {
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public IntLiteralNode(string text)
        {
            this.Text = text;
        }

        /// <summary>
        /// Create a new instance from SerializationInfo
        /// </summary>
        protected IntLiteralNode(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Returns a value for the integer literal node.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            // todo: error: rise condition!-------------
            if (nodeValue == null)
            {
                lock (this)
                {
                    if (nodeValue == null)
                    {
                        string n = this.getText();
                        char lastChar = char.ToLowerInvariant(n[n.Length - 1]);

                        if (char.IsDigit(lastChar))
                        {
                            try
                            {
                                nodeValue = int.Parse(n);
                            }
                            catch (OverflowException)
                            {
                                nodeValue = long.Parse(n);
                            }
                        }
                        else
                        {
                            n = n.Substring(0, n.Length - 1);

                            if (lastChar == 'l')
                            {
                                if (char.ToLowerInvariant(n[n.Length - 1]) == 'u')
                                    nodeValue = ulong.Parse(n.Substring(0, n.Length - 1), CultureInfo.InvariantCulture);
                                else
                                    nodeValue = long.Parse(n, CultureInfo.InvariantCulture);
                            }
                            else if (lastChar == 'u')
                            {
                                nodeValue = uint.Parse(n, CultureInfo.InvariantCulture);
                            }
                        }
                    }
                }
            }

            return nodeValue;
        }

        protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
        {
            string n = getText();

            char lastChar = char.ToLowerInvariant(n[n.Length - 1]);

            if (char.IsDigit(lastChar))
            {
                try
                {
                    return LExpression.Constant(int.Parse(n, CultureInfo.InvariantCulture), typeof(int));
                }
                catch (OverflowException)
                {
                    return LExpression.Constant(long.Parse(n, CultureInfo.InvariantCulture), typeof(long));
                }
            }

            n = n.Substring(0, n.Length - 1);

            if (lastChar == 'l')
            {
                if (char.ToLowerInvariant(n[n.Length - 1]) == 'u')
                    return LExpression.Constant(ulong.Parse(n.Substring(0, n.Length - 1), CultureInfo.InvariantCulture), typeof(ulong));

                return LExpression.Constant(long.Parse(n, CultureInfo.InvariantCulture), typeof(long));
            }

            if (lastChar == 'u')
            {
                return LExpression.Constant(uint.Parse(n, CultureInfo.InvariantCulture), typeof(uint));
            }

            return null;
        }
    }
}