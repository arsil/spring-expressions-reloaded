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
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using SpringExpressions.Expressions.GenericProcessors;
using SpringExpressions.Expressions.LinqExpressionHelpers;
using SpringExpressions.Processors;
using SpringExpressions.Util;
using SpringUtil;
using SpringReflection.Dynamic;
using DistinctProcessor = SpringExpressions.Processors.DistinctProcessor;
using LExpression = System.Linq.Expressions.Expression;
using SpringExpressions.Expressions.Compiling.Expressions;

namespace SpringExpressions
{
    /// <summary>
    /// Represents parsed method node in the navigation expression.
    /// </summary>
    /// <author>Aleksandar Seovic</author>
    public class MethodNode : NodeWithArguments
    {
        private const BindingFlags BINDING_FLAGS
            = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static
            | BindingFlags.IgnoreCase;

        private static readonly IDictionary collectionProcessorMap = new Hashtable();
        private static readonly IDictionary extensionMethodProcessorMap = new Hashtable();

        private bool initialized = false;
        private ParameterInfo[] cachedParameters;
        private SafeMethod cachedInstanceMethod;
        private int cachedInstanceMethodHash;

        /// <summary>
        /// Static constructor. Initializes a map of special collection processor methods.
        /// </summary>
        static MethodNode()
        {
            collectionProcessorMap.Add("count", new CountAggregator());
            collectionProcessorMap.Add("sum", new SumAggregator());
            collectionProcessorMap.Add("max", new MaxAggregator());
            collectionProcessorMap.Add("min", new MinAggregator());
            collectionProcessorMap.Add("average", new AverageAggregator());
            collectionProcessorMap.Add("sort", new Processors.SortProcessor());
            collectionProcessorMap.Add("orderBy", new Processors.OrderByProcessor());
            collectionProcessorMap.Add("distinct", new DistinctProcessor());
            collectionProcessorMap.Add("nonNull", new NonNullProcessor());
            collectionProcessorMap.Add("reverse", new Processors.ReverseProcessor());
            collectionProcessorMap.Add("convert", new ConversionProcessor());

            extensionMethodProcessorMap.Add("date", new DateConversionProcessor());
        }

        /// <summary>
        /// Create a new instance
        /// </summary>
        public MethodNode()
        {
        }

                [NotNull]
	    protected override LExpression GetExpressionTreeIfPossible(
            LExpression contextExpression,
            CompilationContext compilationContext)
	    {
// todo: być może trzeba to lockować!
			string methodName = getText();

            // A method written after a Nullable<T> is called on the value inside it, the same way
            // PropertyOrFieldNode reads a member from it - see NullableReceiver. Without this,
            // 'ShippedOn.AddDays(1)' had no compiled form, because Nullable<T> declares no AddDays,
            // while 'NoNumber.ToString()' resolved against the wrapper and answered "" where the
            // interpreter - which only ever sees the unwrapped value - could find no method at all.
            if (Nullable.GetUnderlyingType(contextExpression.Type) != null)
            {
                return SpringExpressions.Util.NullableReceiver.GuardWithHasValue(
                    contextExpression,
                    receiver => GetExpressionTreeIfPossible(
                        LExpression.Property(receiver, "Value"), compilationContext),
                    methodName);
            }

		    var instance = contextExpression;

			var node = this.getFirstChild();
			var arguments = new List<LExpression>();
		    var argumentsTypes = new List<Type>();

			while (node != null)
			{
				//if (node.getFirstChild() is LambdaExpressionNode)
				//{
				//	argList.Add((BaseNode)node.getFirstChild());
				//}
				//else if (node is NamedArgumentNode)
				//{
				//	namedArgs.Add(node.getText(), node);
				//}
				//else

				var arg = GetExpressionTreeIfPossible((BaseNode) node, contextExpression, compilationContext);

				arguments.Add(arg);
				argumentsTypes.Add(arg.Type);

				node = node.getNextSibling();
			}

            if (typeof(ICollection).IsAssignableFrom(instance.Type)
                || MethodBaseHelpers.IsGenericEnumerable(instance.Type)
                || (contextExpression is ConstantExpression constExpression
                    && constExpression.Value == null))
            {
                var result = TryCollectionProcessors(
                    instance, methodName, argumentsTypes, arguments, compilationContext);
                if (result != null)
                    return result;
            }

                var argumentTypesArray = argumentsTypes.ToArray();


            // todo: obsługiwać inne typy?

		    MethodInfo methodInfo = null;
            LExpression[] resolvedArguments = null;


			var contextExpressionType = contextExpression.Type;


            if (contextExpressionType == typeof(Type)
                && contextExpression.NodeType == ExpressionType.Constant)
			{
				// System.Type or underlaying type (e.g. Int32)
				contextExpressionType = (Type)((ConstantExpression)contextExpression).Value;
				instance = null;

				// try inner type (e.g. Int32)
				var innerResolved = ResolveMethod(
					this, contextExpressionType, methodName, arguments, argumentTypesArray);

				if (innerResolved != null)
				{
					methodInfo = innerResolved.Item1;
					resolvedArguments = innerResolved.Item2;
				}
				else
				{
					// not found - going back to System.Type
					contextExpressionType = contextExpression.Type;
					instance = contextExpression;
				}

			}

		    if (methodInfo == null)
		    {
			    var resolved = ResolveMethod(
				    this, contextExpressionType, methodName, arguments, argumentTypesArray);

			    if (resolved != null)
			    {
				    methodInfo = resolved.Item1;
				    resolvedArguments = resolved.Item2;
			    }
            }

                      // todo: error: extensionMethodProcessorMap
            if (methodInfo == null && methodName == "date")
            {
                // common date() method...
                if (arguments.Count == 1)
                {
                    methodInfo = dateTimeParseMi;
                }
                else if (arguments.Count == 2)
                {
                    methodInfo = dateTimeParseExactMi;
                    arguments.Add(LExpression.Constant(
                        CultureInfo.InvariantCulture, typeof(CultureInfo)));
                }

                // static method
                instance = null;
            }

            if (methodInfo == null)
            {
                throw CannotCompile(
                    $"Method '{methodName}' with the specified number and types of arguments does not exist.");
            }

            // The candidate scan may have retyped null literals or packed a params array; the
            // conversion gate always runs on whatever argument list is actually emitted.
            var finalArguments = resolvedArguments != null
                ? new List<LExpression>(resolvedArguments)
                : arguments;

            ConvertParameters(this, methodInfo, finalArguments);
			return BuildCall(instance, methodInfo, finalArguments);
	    }


        /// <summary>
        /// Resolution for the compiled backend, mirroring the interpreter tier for tier so the two
        /// backends can only ever pick the same method:
        /// - a single candidate needs no choosing - the interpreter can only pick it too - so it goes
        ///   straight to the conversion gate (which is also where a params-array arity mismatch keeps
        ///   its refusal);
        /// - with several candidates, every argument must be statically determinate: a literal, a
        ///   non-nullable value type, a sealed reference type, or a reference type no runtime subtype
        ///   of which could reach a candidate the static type does not match (see
        ///   IsStaticallyDeterminate). Anything else - an object-typed property, a variable - means
        ///   the interpreter would choose from runtime values this backend cannot see, so the shape
        ///   is refused and the interpreter serves it;
        /// - determinate arguments run the legacy tier (the same assignability scan the interpreter
        ///   runs, with the same C#-betterness tie-break), then the widening tier (the same C#
        ///   implicit-conversion rules the interpreter runs, ties refusing exactly where C# reports
        ///   CS0121).
        /// </summary>
        [CanBeNull]
        /// <param name="node">
        /// The node being compiled, carried only so a refusal can name it. This method serves
        /// <see cref="MethodNode"/>, <see cref="ConstructorNode"/> and <see cref="IndexerNode"/>, so it
        /// cannot reach an instance <c>CannotCompile</c> of its own.
        /// </param>
        internal static Tuple<MethodInfo, LExpression[]> ResolveMethod(
            [NotNull] BaseNode node,
            [NotNull] Type contextType,
            [NotNull] string methodName,
            [NotNull, ItemNotNull] List<LExpression> arguments,
            [NotNull, ItemNotNull] Type[] argumentTypes)
        {
            var candidates = GetCompiledCandidateMethods(contextType, methodName, argumentTypes.Length);

            if (candidates.Count == 0)
                return null;

            if (candidates.Count == 1)
                return Tuple.Create(
                    candidates[0], BindToSingleCandidate(node, candidates[0], arguments, methodName));

            for (var position = 0; position < arguments.Count; position++)
            {
                if (IsStaticallyDeterminate(arguments[position], candidates, position, arguments.Count))
                    continue;

                throw new CompileErrorException(
                    node,
                    $"Overload choice for method '{methodName}' depends on the runtime type of an "
                    + $"argument statically typed '{arguments[position].Type}'; there is no compiled form - the "
                    + "interpreter chooses from the runtime values. Add a cast to pick an overload.");
            }

            var scanned = MethodBaseHelpers.GetMethodByArgumentValues(node, candidates, arguments.ToArray());
            if (scanned != null)
                return scanned;

            var widened = ResolveByWidening(candidates, argumentTypes, out var ambiguous);
            if (ambiguous)
            {
                throw new CompileErrorException(
                    node,
                    $"Ambiguous match for method '{methodName}': the arguments convert implicitly to "
                    + "more than one overload and neither is better - C# refuses this call too. Add a "
                    + "cast to pick one.");
            }

            return widened != null ? Tuple.Create(widened, arguments.ToArray()) : null;
        }

        /// <summary>
        /// With one candidate there is nothing to choose, but its parameter list still has to be
        /// bound: omitted optional parameters are filled from their declared defaults and the
        /// trailing params array is built. Shared with <see cref="ConstructorNode"/>, whose
        /// single-candidate branch is the same shape.
        /// </summary>
        /// <remarks>
        /// This branch used to hand the arguments over untouched, so every call that did not already
        /// have one argument per parameter reached the arity refusal in
        /// <see cref="ConvertParameters"/> and fell back to the interpreter. Deciding it here and
        /// nowhere else matters: the several-candidates path must keep going through the scan, whose
        /// tiers weigh applicability as well as binding shape, and through the overload gate, which
        /// refuses a shape the runtime values would decide.
        /// </remarks>
        [NotNull, ItemNotNull]
        /// <param name="node">The node being compiled, so a refusal can name it - see ResolveMethod.</param>
        internal static LExpression[] BindToSingleCandidate(
            [NotNull] BaseNode node,
            [NotNull] MethodBase candidate,
            [NotNull, ItemNotNull] List<LExpression> arguments,
            [NotNull] string calleeName)
        {
            var argumentArray = arguments.ToArray();

            LExpression[] bound;
            switch (ArgumentBindingUtils.TryBind(candidate.GetParameters(), argumentArray, out bound))
            {
                case ArgumentBinding.Exact:
                case ArgumentBinding.WithOmittedOptionals:
                case ArgumentBinding.Expanded:
                    return bound;

                case ArgumentBinding.Undecidable:
                    throw new CompileErrorException(
                        node,
                        $"The last argument of '{calleeName}' is statically typed "
                        + $"'{argumentArray[argumentArray.Length - 1].Type}', so whether it is the "
                        + "params array itself or the one element of it depends on whether it is null "
                        + "at runtime; there is no compiled form - the interpreter reads it from the "
                        + "value. Add a cast to the array type to pass it whole.");

                default:
                    // Not a binding at all - the conversion gate reports why, having the parameter
                    // types to name.
                    return argumentArray;
            }
        }

        // A value that could make the interpreter's runtime-value resolution choose differently from
        // this backend's static resolution. Fully known here: literals (null included - the scans
        // break null ties by C#'s betterness now, on both backends alike), non-nullable value types
        // (exactly one possible runtime type), and sealed non-array reference types (arrays excluded
        // because covariance lets a string[] live in an object[]-typed slot). A non-sealed reference
        // type is determinate too when no candidate parameter is reachable by a runtime subtype that
        // the static type does not already match: then every possible runtime value matches exactly
        // the same candidate set, the interpreter's exact-or-betterness pick lands on the same method
        // this backend picks, and the call may compile - Method(object)/Method(B) with a B-typed
        // argument binds Method(B) whatever B subtype arrives. A candidate parameter below the static
        // type, or an interface the static type does not implement, or any candidate with a different
        // arity (params arrays), keeps the shape refused. The residual edge is deliberate: an
        // argument holding null at runtime meets the interpreter's null matching, which can tie on an
        // incomparable candidate set where this pick called one overload with null - a null-only edge,
        // accepted because refusing it would decompile every string-argument call against overloads
        // (CompareTo(string) versus CompareTo(object) is everywhere); comparable sets no longer have
        // the edge at all, betterness resolving both backends to the same overload.
        internal static bool IsStaticallyDeterminate<T>(
            [NotNull] LExpression argument,
            [NotNull, ItemNotNull] IList<T> candidates,
            int position,
            int argumentCount) where T : MethodBase
        {
            if (argument is ConstantExpression)
                return true;

            var type = argument.Type;

            if (type.IsValueType)
                return Nullable.GetUnderlyingType(type) == null;

            if (type.IsSealed && !type.IsArray)
                return true;

            foreach (var candidate in candidates)
            {
                var parameters = candidate.GetParameters();

                if (parameters.Length != argumentCount)
                    return false;

                var parameterType = parameters[position].ParameterType;

                if (parameterType.IsAssignableFrom(type))
                    continue;

                if (type.IsAssignableFrom(parameterType) || parameterType.IsInterface)
                    return false;
            }

            return true;
        }

        // The compiled twin of GetCandidateMethods: name matched case-insensitively (BINDING_FLAGS
        // declares IgnoreCase, which the old exact-type GetMethod honoured), interfaces searched with
        // their inherited interfaces (GetMethods on an interface does not flatten them), and
        // hide-by-signature duplicates resolved toward the most derived declarer - which is what
        // Type.GetMethod did silently. The interpreter's GetCandidateMethods stays untouched: its
        // quirks are legacy behaviour.
        [NotNull, ItemNotNull]
        internal static IList<MethodInfo> GetCompiledCandidateMethods(
            [NotNull] Type type, [NotNull] string methodName, int argCount)
        {
            var searchTypes = !type.IsInterface
                ? new[] { type }
                : type.GetInterfaces().Union(new[] { type }).ToArray();

            var bySignature = new Dictionary<string, MethodInfo>();

            foreach (var searchType in searchTypes)
            {
                foreach (var method in searchType.GetMethods(BINDING_FLAGS | BindingFlags.FlattenHierarchy))
                {
                    if (!string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var parameters = method.GetParameters();

                    if (!ArgumentBindingUtils.CouldTakeArgumentCount(parameters, argCount))
                        continue;

                    var signature = method.Name.ToUpperInvariant() + "("
                        + string.Join(",", parameters.Select(p => p.ParameterType.AssemblyQualifiedName ?? p.ParameterType.Name).ToArray())
                        + ")";

                    if (bySignature.TryGetValue(signature, out var existing))
                    {
                        if (existing.DeclaringType != method.DeclaringType
                            && existing.DeclaringType != null
                            && existing.DeclaringType.IsAssignableFrom(method.DeclaringType))
                        {
                            // same signature declared lower in the hierarchy hides the one above
                            bySignature[signature] = method;
                        }
                    }
                    else
                    {
                        bySignature[signature] = method;
                    }
                }
            }

            return bySignature.Values.ToList();
        }

        /// <summary>
        /// The widening tier, shared by both backends: among candidates applicable to the argument
        /// types through assignability or C#'s implicit numeric conversions (a custom real-valued
        /// type going through its own operator first), the unique best per C#'s betterness rule. It
        /// runs only after the legacy tier found nothing, so no pick that resolved before this tier
        /// existed ever changes; params arrays stay the legacy tier's business. A null argument type
        /// (a null value at runtime) widens to nothing.
        /// </summary>
        [CanBeNull]
        internal static T ResolveByWidening<T>(
            [NotNull, ItemNotNull] IList<T> candidates,
            [NotNull, ItemCanBeNull] Type[] argumentTypes,
            out bool ambiguous) where T : MethodBase
        {
            ambiguous = false;

            var applicable = new List<T>();
            var parameterSets = new List<Type[]>();

            foreach (var candidate in candidates)
            {
                var parameters = candidate.GetParameters();
                if (parameters.Length != argumentTypes.Length)
                    continue;

                var isApplicable = true;
                var parameterSet = new Type[parameters.Length];

                for (var i = 0; i < parameters.Length && isApplicable; i++)
                {
                    parameterSet[i] = parameters[i].ParameterType;
                    isApplicable = argumentTypes[i] != null
                        && (parameterSet[i].IsAssignableFrom(argumentTypes[i])
                            || TypeCheckingUtils.HasImplicitWideningConversion(argumentTypes[i], parameterSet[i]));
                }

                if (isApplicable)
                {
                    applicable.Add(candidate);
                    parameterSets.Add(parameterSet);
                }
            }

            if (applicable.Count == 0)
                return null;

            if (applicable.Count == 1)
                return applicable[0];

            var best = TypeCheckingUtils.IndexOfUniqueBestParameterSet(parameterSets);
            if (best >= 0)
                return applicable[best];

            ambiguous = true;
            return null;
        }

        // Shared with ConstructorNode: the conversion gate is identical for method and constructor
        // arguments, so it takes MethodBase and labels its messages accordingly.
        /// <param name="node">The node being compiled, so a refusal can name it - see ResolveMethod.</param>
        internal static void ConvertParameters([NotNull] BaseNode node, [NotNull] MethodBase method, [NotNull, ItemNotNull] List<LExpression> arguments)
        {
            var methodParameters = method.GetParameters();

            var label = method is ConstructorInfo
                ? $"Constructor of '{method.DeclaringType?.Name}'"
                : $"Method '{method.Name}'";

            // One argument per parameter is all this can emit. Candidate gathering admits a count
            // mismatch only for a params array, and by now that has either been bound into this shape
            // or failed to bind - a trailing argument the array cannot hold, most often. The count
            // used to be walked off the end of methodParameters with an IndexOutOfRangeException,
            // which says nothing to a caller.
            if (arguments.Count != methodParameters.Length)
            {
                throw new CompileErrorException(
                    node,
                    $"{label} takes {methodParameters.Length} parameter(s) but was given "
                    + $"{arguments.Count} argument(s), and they do not bind to its params array.");
            }

            // An unconditional ConvertChecked used to sit here, and for one conversion class it
            // invented answers: a real argument against an integral parameter compiled to a truncation
            // - Echo(45.5) on Echo(int) gave 45 - where the interpreter's binder converts through
            // Convert.ChangeType and rounds - 46 - so the same call silently answered differently per
            // backend. That class is refused now and the interpreter serves it. Every other conversion
            // ConvertChecked can emit agrees with the interpreter's - integral widening and narrowing
            // (both sides throw on overflow), real to real, enum to integral, object downcasts checked
            // at runtime - so those keep their compiled form.
            for (int i = 0; i < arguments.Count; i++)
            {
                var parameterType = methodParameters[i].ParameterType;
                var argument = arguments[i];

                if (argument.Type == parameterType)
                    continue;

                // The null literal converts to any reference or nullable parameter type.
                if (argument is ConstantExpression constant && constant.Value == null
                    && (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null))
                {
                    arguments[i] = LExpression.Constant(null, parameterType);
                    continue;
                }

                if (TypeCheckingUtils.IsRealType(argument.Type)
                    && TypeCheckingUtils.IsIntegralKind(parameterType))
                {
                    throw new CompileErrorException(
                        node,
                        $"{label} parameter {i} is '{parameterType}' but the argument is "
                        + $"'{argument.Type}': a real-to-integral argument conversion rounds in the "
                        + "interpreter and would truncate compiled, so it has no compiled form.");
                }

                try
                {
                    arguments[i] = LExpression.ConvertChecked(argument, parameterType);
                }
                catch (InvalidOperationException ex)
                {
                    // No such conversion exists - a string argument against an int parameter, say.
                    // InvalidOperationException is not this codebase's "cannot compile" signal, so the
                    // weakly typed path's fallback would never see it.
                    throw new CompileErrorException(
                        node,
                        $"{label} parameter {i} is '{parameterType}' but the argument is "
                        + $"'{argument.Type}': {ex.Message}");
                }
            }
        }


        /// <summary>
        /// A string reaching a collection processor is its characters, which is what the compiled path
        /// and this engine's own projection nodes have always said.
        /// </summary>
        /// <remarks>
        /// <p>
        /// Upstream Spring.NET wrote the two halves of this differently in two files: <c>ProjectionNode</c>
        /// asks for <c>IEnumerable</c>, which a string is, so <c>Text.!{…}</c> has always worked and
        /// enumerated characters - while <c>ICollectionProcessor.Process</c> takes <c>ICollection</c>,
        /// which a string is not, so <c>Text.sort()</c> was refused. Nobody chose that; two interface
        /// names were typed. The compiled path then accepted a string for every processor with an
        /// open-generic implementation, which is where the divergence came from.
        /// </p>
        /// <p>
        /// C# reads a string the same way - <c>"cab".Min()</c> is <c>'a'</c> and <c>"cab".Distinct()</c>
        /// enumerates characters, because <c>string</c> is an <c>IEnumerable&lt;char&gt;</c>. Whether
        /// that is *desirable* is a fair question, and refusing everywhere was considered: nothing in
        /// either suite uses a string as a collection source, so it would have cost nothing to remove.
        /// It was not taken because it would have meant deviating from C# and from upstream at once, and
        /// changing both backends rather than one.
        /// </p>
        /// </remarks>
        /// <remarks>
        /// <p>
        /// The string conversion stays although a string is already an <see cref="IEnumerable"/> of its
        /// characters, because a <c>char[]</c> is an <see cref="ICollection"/> and so answers
        /// <c>count()</c> without being walked. Same items, same order, one less traversal.
        /// </p>
        /// </remarks>
        private static IEnumerable AsCollection(object context)
        {
            return context is string text ? text.ToCharArray() : (IEnumerable)context;
        }

        /// <summary>
        /// What a processor answers for a null source, or null where the compiled path cannot say it and
        /// has to keep throwing.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>A null collection has nothing in it, so it answers what the empty-collection ruling
        /// decided "there is no answer" looks like: null.</b> The interpreter's processors have said so
        /// all along for the six that return a collection - <c>Assert.IsNull(GetValue(null, "sort()"))</c>
        /// is pinned in the frozen suite, so it is inherited semantics rather than a choice - while the
        /// compiled path threw <see cref="ArgumentNullException"/> out of <c>Enumerable</c>, five
        /// divergent rows per source shape.
        /// </p>
        /// <p>
        /// <c>count()</c> answers <b>0</b>, not null. That is not propagation, and it is deliberate: it
        /// is what the interpreter has always answered, it is what an empty collection answers, and a
        /// count that is absent rather than zero is not what anyone means.
        /// </p>
        /// <p>
        /// <b><c>sum()</c> is the carve-out and keeps throwing on both backends.</b> Its result type is
        /// the item type itself - <c>Enumerable.Sum(IEnumerable&lt;int&gt;)</c> answers <c>int</c> - so
        /// there is no null to return, and the alternatives are worse than the gap: lifting every sum to
        /// <c>T?</c> would make a typed <c>int</c> request refuse it by the nullable-request ruling, and
        /// answering a zero would need an item type that a null source cannot be asked for. Both
        /// backends throw for a null source today, so this leaves agreement intact rather than creating
        /// a divergence.
        /// </p>
        /// </remarks>
        [CanBeNull]
        private static LExpression NullSourceAnswer([NotNull] string methodName, [NotNull] Type resultType)
        {
            if (methodName == "count")
                return resultType == typeof(int) ? LExpression.Constant(0) : null;

            if (methodName == "sum")
                return null;

            var canHoldNothing = !resultType.IsValueType || Nullable.GetUnderlyingType(resultType) != null;

            return canHoldNothing ? LExpression.Constant(null, resultType) : null;
        }

        private LExpression TryCollectionProcessors(
            LExpression instance,
            string methodName,
            List<Type> argumentsTypes,
            List<LExpression> arguments,
            CompilationContext compilationContext)
        {
            if (instance is ConstantExpression constExpression
                && constExpression.Value == null
                && argumentsTypes.Count == 0
                && arguments.Count == 0)
            {
                instance = LExpression.Constant(null, typeof(IEnumerable));
            }

            var processorArgumentTypes = new List<Type> { instance.Type };
            processorArgumentTypes.AddRange(argumentsTypes);

            var processorArguments = new List<LExpression> { instance };
            processorArguments.AddRange(arguments);

             // todo: error: processors:
             // Int32 Int64 UInt32 UInt64 Int16 UInt16 Byte SByte
             // single, double, decimal

            Type processorType = null;

                    // todo: error: każdy procesor musi mieć wszystkie metody!!! to jest słabe!!!
            //if (instance.Type.IsGenericType)

            if (MethodBaseHelpers.IsGenericEnumerable(instance.Type, out Type itemType))
            {
                if (GenericProcessorsFacade.TryGetMethodInfo(
                        methodName, instance.Type, itemType, processorArgumentTypes, out var mi))
                {
                    // A null source is guarded where the processor's result type can carry the answer -
                    // see NullSourceAnswer. The source goes into a local first so the null test and the
                    // call see one evaluation of it: 'SomeMethod().sort()' must not call SomeMethod
                    // twice.
                    var nullSourceAnswer = NullSourceAnswer(methodName, mi.ReturnType);
                    var source = nullSourceAnswer == null
                        ? instance
                        : LExpression.Variable(instance.Type, "source");

                    // min(), max() and average() ask for the nullable form of a non-nullable value item
                    // type, so that an empty collection answers null rather than throwing out of
                    // Enumerable. Value-type arguments are not covariant, so the source is lifted item
                    // by item; every other processor asks for the plain item type and is untouched.
                    // The lift wraps the local rather than the original, so it stays on the far side of
                    // the null test - it enumerates, and enumerating the null is the thing being
                    // avoided.
                    processorArguments[0] =
                        GenericProcessorsFacade.LiftSourceIfNullableItemsWanted(mi, source, itemType);

                    // Through BuildCall, so an argument the processor cannot accept is a refusal rather
                    // than an absorbed defect. 'Ds.orderBy({|a,b| $a - $b})' over a type whose
                    // subtraction yields a decimal hands Func<T,T,decimal> to a Func<T,T,int>
                    // parameter, and LExpression.Call's ArgumentException was reaching the absorber -
                    // so the caller's own mistake was reported as an internal compiler error with
                    // "please report it" attached, which is the one thing an emitter must never do.
                    LExpression result = BuildCall(null, mi, processorArguments);

                    if (nullSourceAnswer != null)
                    {
                        result = LExpression.Block(
                            new[] { (ParameterExpression)source },
                            LExpression.Assign(source, instance),
                            LExpression.Condition(
                                LExpression.ReferenceEqual(
                                    source, LExpression.Constant(null, instance.Type)),
                                nullSourceAnswer,
                                result));
                    }

                    // A list a processor builds is the engine's own, so Compiler reshapes the root to
                    // the List<object> the interpreter produces; a scalar result (sum, count, ...) has
                    // no list item type and is left alone. The weakly typed branch below needs no
                    // registration: it delegates to the interpreter processors, so it already returns
                    // List<object> at runtime.
                    //
                    // Registered against what is actually returned, not the inner call: the registry is
                    // keyed on the expression object, so registering the call and then wrapping it in
                    // the null guard would leave the root unrecognised and untouched by the reshaping.
                    if (CollectionOperandUtils.GetListItemType(result.Type) != null)
                        compilationContext.MarkAsConstructedCollection(result);

                    return result;
                }
            }

            // IEnumerable, not ICollection: the bridge's own parameter is IEnumerable now, and asking
            // for ICollection here refused a source declared IList<int> / ICollection<int> /
            // IReadOnlyList<int> - none of which statically satisfies the non-generic interface - so
            // convert(), whose only compiled form is this bridge, had no compiled form for them.
            if (typeof(IEnumerable).IsAssignableFrom(instance.Type))
            {
                processorType = typeof(WeaklyTypedCollectionProcessor);
                //var decProcMethodInfo = processorType.GetMethod(methodName, processorArgumentTypes.ToArray());
                
                
                var array = processorArgumentTypes.ToArray();
                array[0] = typeof(IEnumerable);

                var decProcMethodInfo = processorType.GetMethod(methodName, array);
                if (decProcMethodInfo != null)
                {
                    var result = LExpression.Call(decProcMethodInfo, processorArguments.ToArray());
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns node's value for the given context.
        /// </summary>
        /// <param name="context">Context to evaluate expressions against.</param>
        /// <param name="evalContext">Current expression evaluation context.</param>
        /// <returns>Node's value.</returns>
        protected override object Get(object context, EvaluationContext evalContext)
        {
            string methodName = this.getText();
            object[] argValues = ResolveArguments(evalContext);
            ICollectionProcessor localCollectionProcessor = null;
            IMethodCallProcessor methodCallProcessor = null;

            // resolve method, if necessary
            lock (this)
            {
                // check if it is a collection and the methodname denotes a collection processor.
                // IEnumerable, not ICollection: a HashSet<T>, a declared ISet<T> and a bare
                // IEnumerable<T> are not the non-generic ICollection, so asking for that refused them
                // here while the compiled path - whose first tier asks IsGenericEnumerable - answered.
                if ((context == null || context is IEnumerable))
                {
                    // predefined collection processor?
                    localCollectionProcessor = (ICollectionProcessor)collectionProcessorMap[methodName];

                    // user-defined collection processor?
                    if (localCollectionProcessor == null && evalContext.Variables != null)
                    {
                        object temp;
                        evalContext.Variables.TryGetValue(methodName, out temp);
                        localCollectionProcessor = temp as ICollectionProcessor;
                    }
                }

                // try extension methods
                methodCallProcessor = (IMethodCallProcessor)extensionMethodProcessorMap[methodName];
                {
                    // user-defined extension method processor?
                    if (methodCallProcessor == null && evalContext.Variables != null)
                    {
                        object temp;
                        evalContext.Variables.TryGetValue(methodName, out temp);
                        methodCallProcessor = temp as IMethodCallProcessor;
                    }
                }

                // try instance method
                if (context != null)
                {
                    // calculate checksum, if the cached method matches the current context
                    if (initialized)
                    {
                        int calculatedHash = CalculateMethodHash(context.GetType(), argValues);
                        initialized = (calculatedHash == cachedInstanceMethodHash);
                    }

                    if (!initialized)
                    {
                        Initialize(methodName, argValues, context);
                        initialized = true;
                    }
                }
            }

            if (localCollectionProcessor != null)
            {
                return localCollectionProcessor.Process(AsCollection(context), argValues);
            }
            else if (methodCallProcessor != null)
            {
                return methodCallProcessor.Process(context, argValues);
            }
            else if (cachedInstanceMethod != null)
            {
                // The unambiguous-name lookup matches on the name alone and never counts arguments,
                // so whether these ones fit the parameter list is decided here - filling any omitted
                // defaults and building any params array on the way. Both used to be got wrong: a
                // call omitting a default reached the invoker one argument short ("Invalid number of
                // arguments"), and the params array was packed unconditionally, which ran off the end
                // of a short argument list and packed a caller's own array inside a second one.
                object[] paramValues;
                if (ArgumentBindingUtils.TryBind(cachedParameters, argValues, out paramValues)
                    == ArgumentBinding.NotApplicable)
                {
                    throw new ArgumentException(string.Format(
                        "Method '{0}' with the specified number and types of arguments does not exist.",
                        methodName));
                }

                return cachedInstanceMethod.Invoke(context, paramValues);
            }
            else
            {
                throw new ArgumentException(string.Format("Method '{0}' with the specified number and types of arguments does not exist.", methodName));
            }
        }

        private int CalculateMethodHash(Type contextType, object[] argValues)
        {
            int hash = contextType.GetHashCode();
            for (int i = 0; i < argValues.Length; i++)
            {
                object arg = argValues[i];
                if (arg != null)
                    hash += s_primes[i] * arg.GetType().GetHashCode();
            }
            return hash;
        }

        private void Initialize(string methodName, object[] argValues, object context)
        {
            Type contextType = (context is Type ? context as Type : context.GetType());

            // check the context type first
            MethodInfo mi = GetBestMethod(contextType, methodName, BINDING_FLAGS, argValues);

            // if not found, probe the Type's type          
            if (mi == null)
            {
                mi = GetBestMethod(typeof(Type), methodName, BINDING_FLAGS, argValues);
            }

            if (mi == null)
            {
                return;
            }
            else
            {
                cachedParameters = mi.GetParameters();
                cachedInstanceMethod = new SafeMethod(mi);
                cachedInstanceMethodHash = CalculateMethodHash(contextType, argValues);
            }
        }

        /// <summary>
        /// Gets the best method given the name, argument values, for a given type.
        /// </summary>
        /// <param name="type">The type on which to search for the method.</param>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="bindingFlags">The binding flags.</param>
        /// <param name="argValues">The arg values.</param>
        /// <returns>Best matching method or null if none found.</returns>
        public static MethodInfo GetBestMethod(Type type, string methodName, BindingFlags bindingFlags, object[] argValues)
        {
            MethodInfo mi = null;
            try
            {
                mi = type.GetMethod(methodName, bindingFlags | BindingFlags.FlattenHierarchy);
            }
            catch (AmbiguousMatchException)
            {

                IList<MethodInfo> overloads = GetCandidateMethods(type, methodName, bindingFlags, argValues.Length);
                if (overloads.Count > 0)
                {
                    mi = ReflectionUtils.GetMethodByArgumentValues(overloads, argValues);

                    // The widening tier: the legacy scan above knows assignability but not numeric
                    // widening, so IntAgainstLong-style overload sets found nothing here since
                    // upstream. Where it finds nothing, the C# implicit-conversion rules get a turn -
                    // the same shared rules the compiled backend resolves by, so a widened call
                    // answers alike on both. Legacy picks never change: this runs only on "no match".
                    // A tie is reported the way this resolver has always reported ties, at evaluation.
                    if (mi == null)
                    {
                        mi = ResolveByWidening(
                            overloads,
                            Array.ConvertAll(argValues, v => v?.GetType()),
                            out var ambiguous);

                        if (ambiguous)
                        {
                            throw new AmbiguousMatchException(
                                $"Ambiguous match for method '{methodName}': the argument values "
                                + "convert implicitly to more than one overload and neither is better.");
                        }
                    }
                }
            }
            return mi;
        }



        private static IList<MethodInfo> GetCandidateMethods(Type type, string methodName, BindingFlags bindingFlags, int argCount)
        {
            MethodInfo[] methods = type.GetMethods(bindingFlags | BindingFlags.FlattenHierarchy);
            List<MethodInfo> matches = new List<MethodInfo>();

            foreach (MethodInfo method in methods)
            {
                if (method.Name == methodName
                    && ArgumentBindingUtils.CouldTakeArgumentCount(method.GetParameters(), argCount))
                {
                    matches.Add(method);
                }
            }

            return matches;
        }

        // used to calculate signature hash while caring for arg positions
        private static readonly int[] s_primes =
            {
                17, 19, 23, 29
                , 31, 37, 41, 43, 47, 53, 59, 61, 67, 71
                , 73, 79, 83, 89, 97, 101, 103, 107, 109, 113
                , 127, 131, 137, 139, 149, 151, 157, 163, 167, 173
                , 179, 181, 191, 193, 197, 199, 211, 223, 227, 229
                , 233, 239, 241, 251, 257, 263, 269, 271, 277, 281
                , 283, 293, 307, 311, 313, 317, 331, 337, 347, 349
                , 353, 359, 367, 373, 379, 383, 389, 397, 401, 409
                , 419, 421, 431, 433, 439, 443, 449, 457, 461, 463
                , 467, 479, 487, 491, 499, 503, 509, 521, 523, 541
                , 547, 557, 563, 569, 571, 577, 587, 593, 599, 601
                , 607, 613, 617, 619, 631, 641, 643, 647, 653, 659
                , 661, 673, 677, 683, 691, 701, 709, 719, 727, 733
                , 739, 743, 751, 757, 761, 769, 773, 787, 797, 809
                , 811, 821, 823, 827, 829, 839, 853, 857, 859, 863, 877, 881, 883, 887, 907, 911, 919, 929, 937, 941
                , 947, 953, 967, 971, 977, 983, 991, 997, 1009, 1013, 1019, 1021, 1031, 1033, 1039, 1049, 1051, 1061, 1063, 1069
                , 1087, 1091, 1093, 1097, 1103, 1109, 1117, 1123, 1129, 1151, 1153, 1163, 1171, 1181, 1187, 1193, 1201, 1213, 1217, 1223
                , 1229, 1231, 1237, 1249, 1259, 1277, 1279, 1283, 1289, 1291, 1297, 1301, 1303, 1307, 1319, 1321, 1327, 1361, 1367, 1373
                , 1381, 1399, 1409, 1423, 1427, 1429, 1433, 1439, 1447, 1451, 1453, 1459, 1471, 1481, 1483, 1487, 1489, 1493, 1499, 1511
                , 1523, 1531, 1543, 1549, 1553, 1559, 1567, 1571, 1579, 1583, 1597, 1601, 1607, 1609, 1613, 1619, 1621, 1627, 1637, 1657
                , 1663, 1667, 1669, 1693, 1697, 1699, 1709, 1721, 1723, 1733, 1741, 1747, 1753, 1759, 1777, 1783, 1787, 1789, 1801, 1811
                , 1823, 1831, 1847, 1861, 1867, 1871, 1873, 1877, 1879, 1889, 1901, 1907, 1913, 1931, 1933, 1949, 1951, 1973, 1979, 1987
                , 1993, 1997, 1999, 2003, 2011, 2017, 2027, 2029, 2039, 2053, 2063, 2069, 2081, 2083, 2087, 2089, 2099, 2111, 2113, 2129
                , 2131, 2137, 2141, 2143, 2153, 2161, 2179, 2203, 2207, 2213, 2221, 2237, 2239, 2243, 2251, 2267, 2269, 2273, 2281, 2287
                , 2293, 2297, 2309, 2311, 2333, 2339, 2341, 2347, 2351, 2357, 2371, 2377, 2381, 2383, 2389, 2393, 2399, 2411, 2417, 2423
            };

	    private static MethodInfo dateTimeParseMi = typeof(DateTime)
			.GetMethod("Parse",
		    BINDING_FLAGS,
		    null,
		    new [] {typeof(string)},
		    null);

		private static MethodInfo dateTimeParseExactMi = typeof(DateTime)
			.GetMethod("ParseExact",
			BINDING_FLAGS,
			null,
			new[] { typeof(string), typeof(string), typeof(CultureInfo) },
			null);
	}
}
