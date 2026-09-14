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
using JetBrains.Annotations;

using SpringExpressions.Parser.antlr.collections;

namespace SpringExpressions
{
    /// <summary>
    /// Base type for nodes that accept arguments.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public abstract class NodeWithArguments : BaseNode
    {
        private BaseNode[] args;
        private IDictionary namedArgs;

        /// <summary>
        /// A key for a resolution that depended on <paramref name="contextType"/> and the runtime
        /// types of <paramref name="values"/> - so that a node caching that resolution can tell when it
        /// no longer applies.
        /// </summary>
        /// <remarks>
        /// <b>The rule this exists for: a node that caches a resolution must key the cache on
        /// everything the resolution depended on.</b> Both defects it was written for behaved
        /// identically on both backends, so no sweep could see them - only a comparison between a
        /// reused expression and a fresh one:
        /// <p>
        /// <c>ConstructorNode</c> chose its constructor from the argument <i>values</i> and re-resolved
        /// only when the field was null, so <c>new Thing(#x)</c> with an <c>int</c> and then a
        /// <c>string</c> reused the int constructor and threw <c>InvalidCastException</c>.
        /// <c>IndexerNode</c> did the same with the container type and the index types, so
        /// <c>Item[0]</c> over two types that each declare <c>this[int]</c> threw
        /// <c>InvalidPropertyException</c> on the second. <c>MethodNode</c> had the shape right all
        /// along and is what these two now copy.
        /// </p>
        /// <p>
        /// Order-sensitive, and with no fixed table of primes to run off the end of - unlike
        /// <c>MethodNode</c>'s own older hash, which indexes a 350-entry array by argument position.
        /// A null value contributes its position but no type, since a null argument constrains the
        /// resolution differently and the values are not being compared here, only their types.
        /// </p>
        /// </remarks>
        protected static int ResolutionKeyOf([CanBeNull] Type contextType, [CanBeNull] object[] values)
        {
            unchecked
            {
                var key = contextType == null ? 0 : contextType.GetHashCode();

                if (values != null)
                {
                    foreach (var value in values)
                        key = (key * 397) ^ (value == null ? 0 : value.GetType().GetHashCode());
                }

                return key;
            }
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public NodeWithArguments()
        {
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public NodeWithArguments(string text)
        {
            this.setText(text);
        }

        /// <summary>
        /// Append an argument node to the list of child nodes
        /// </summary>
        /// <param name="argumentNode"></param>
        public void AddArgument(BaseNode argumentNode)
        {
            base.addChild(argumentNode);
        }

                /// <summary>
        /// Initializes the node. 
        /// </summary>
        private void InitializeNode()
        {
            lock (this)
            {
                if (args == null)
                {
                    List<BaseNode> argList = new List<BaseNode>();
                    namedArgs = new Hashtable();

                    AST node = this.getFirstChild();

                    while (node != null)
                    {
                        if (node.getFirstChild() is LambdaExpressionNode)
                        {
                            argList.Add((BaseNode) node.getFirstChild());
                        }
                        else if (node is NamedArgumentNode)
                        {
                            namedArgs.Add(node.getText(), node);
                        }
                        else
                        {
                            argList.Add((BaseNode) node);
                        }
                        node = node.getNextSibling();
                    }

                    args = argList.ToArray();
                }
            }
        }

        /// <summary>
        /// Which arguments are collections this engine builds, rather than ones the caller owns.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The interpreter's answer to the question the compiled path answers with
        /// <c>CompilationContext.IsConstructedCollection</c> - a registry it fills while emitting. At
        /// evaluation there is no such registry and the value cannot be asked: a
        /// <c>List&lt;object&gt;</c> the engine built and one the caller declared look identical. The
        /// node does know, because it is the same tree the compiler walks, so the question is
        /// structural: did this argument's node <i>construct</i> a collection?
        /// </p>
        /// <p>
        /// Structural means it stays in step by being kept in step - a new collection-producing node
        /// belongs on this list. The consequence of missing one is a shape that keeps the divergence
        /// it has today, not a wrong answer.
        /// </p>
        /// </remarks>
        [NotNull]
        protected bool[] ArgumentsThatBuildTheirOwnCollection()
        {
            InitializeNode();

            if (_argumentsThatBuildTheirOwnCollection == null)
            {
                var flags = new bool[args.Length];
                for (var i = 0; i < args.Length; i++)
                    flags[i] = BuildsItsOwnCollection(args[i]);

                _argumentsThatBuildTheirOwnCollection = flags;
            }

            return _argumentsThatBuildTheirOwnCollection;
        }

        private bool[] _argumentsThatBuildTheirOwnCollection;

        private static bool BuildsItsOwnCollection([CanBeNull] BaseNode node)
        {
            // A chain is an Expression whose children are its links, and its value is the last link's:
            // 'Ints.!{#this}' arrives here as Expression[PropertyOrFieldNode, ProjectionNode]. An
            // expression list propagates from its last element the same way - item 47's rule.
            if (node is Expression || node is ExpressionListNode)
            {
                var last = node.getFirstChild();
                if (last == null)
                    return false;

                while (last.getNextSibling() != null)
                    last = last.getNextSibling();

                return BuildsItsOwnCollection(last as BaseNode);
            }

            if (node is ListInitializerNode || node is MapInitializerNode)
                return true;

            if (node is ProjectionNode || node is SelectionNode)
                return true;

            // The set and dictionary operators. '+' also concatenates and adds, which is why the
            // value is tested as well as the node before anything is coerced.
            if (node is OpADD || node is OpSUBTRACT || node is OpMULTIPLY)
                return true;

            // sort(), distinct(), reverse(), nonNull(), orderBy(), convert() - a call whose name is a
            // collection processor builds the list it returns. A call by any other name does not.
            var method = node as MethodNode;
            return method != null && MethodNode.IsCollectionProcessorName(method.getText());
        }

        /// <summary>
        /// Asserts the argument count.
        /// </summary>
        /// <param name="requiredCount">The required count.</param>
        protected void AssertArgumentCount(int requiredCount)
        {
            InitializeNode();
            if (requiredCount != args.Length)
            {
                throw new ArgumentException("This expression node requires exactly " +
                                            requiredCount + " argument(s) and " +
                                            args.Length + " were specified.");
            }
        }

        /// <summary>
        /// Resolves the arguments.
        /// </summary>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>An array of argument values</returns>
        protected object[] ResolveArguments(EvaluationContext evalContext)
        {
            if (args == null)
            {
                InitializeNode();
            }

            int length = args.Length;
            object[] values = new object[length];
            for (int i = 0; i < length; i++)
            {
                values[i] = ResolveArgumentInternal(i, evalContext);
            }
            return values;
        }

        /// <summary>
        /// Resolves the named arguments.
        /// </summary>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>A dictionary of argument name to value mappings.</returns>
        protected IDictionary ResolveNamedArguments(EvaluationContext evalContext)
        {
            if (args == null)
            {
                InitializeNode();
            }
            
            if (namedArgs.Count == 0)
            {
                return null;
            }

            IDictionary namesAndValues = new Hashtable(namedArgs.Count);
            foreach (string name in namedArgs.Keys)
            {
                namesAndValues[name] = ResolveNamedArgument(name, evalContext);
            }
            return namesAndValues;
        }

        /// <summary>
        /// Resolves the argument.
        /// </summary>
        /// <param name="position">Argument position.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Resolved argument value.</returns>
        protected object ResolveArgument(int position, EvaluationContext evalContext)
        {
            if (args == null)
            {
                InitializeNode();
            }
            return ResolveArgumentInternal(position, evalContext);
        }

        /// <summary>
        /// Resolves the argument without ensuring <see cref="InitializeNode"/> was called.
        /// </summary>
        /// <param name="position">Argument position.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Resolved argument value.</returns>
        private object ResolveArgumentInternal(int position, EvaluationContext evalContext)
        {
            BaseNode arg = args[position];
            if (arg is LambdaExpressionNode)
            {
                return arg;
            }
            return GetValue(arg, evalContext.ThisContext, evalContext);
        }

        /// <summary>
        /// Resolves the named argument.
        /// </summary>
        /// <param name="name">Argument name.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Resolved named argument value.</returns>
        private object ResolveNamedArgument(string name, EvaluationContext evalContext)
        {
            return GetValue(((BaseNode)namedArgs[name]), evalContext.ThisContext, evalContext);
        }

    }
}