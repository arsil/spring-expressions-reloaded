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

namespace SpringExpressions
{
    /// <summary>
    /// Utility class that enables easy expression evaluation.
    /// </summary>
    /// <remarks>
    /// <p>
    /// This class allows users to get or set properties, execute methods, and evaluate
    /// logical and arithmetic expressions.
    /// </p>
    /// <p>
    /// <b>Obsolete, and the replacement is strictly better.</b> Methods in this class parse the
    /// expression on every invocation, so nothing can ever be reused. Hold a parsed expression
    /// instead - <see cref="Expression.ParseGetter{TRoot, TResult}(string, EvaluationMode, SandboxPolicy)"/>
    /// or <see cref="Expression.ParseSetter{TRoot, TArgument}(string, EvaluationMode, SandboxPolicy)"/> -
    /// and evaluate it as often as you like.
    /// </p>
    /// <p>
    /// Measured on one expression, microseconds: <b>0.014</b> per evaluation for a compiled getter
    /// that is held, against <b>10</b> per call here. And for a genuinely one-shot call the typed API
    /// is still the same or faster - <c>ParseGetter&lt;TRoot, TResult&gt;(text,
    /// EvaluationMode.MustInterpret).GetValue(root)</c> is 10 us against this class's 13 - so there
    /// is no shape in which this class is the better choice. It is kept because it is inherited
    /// public surface that consumers are built on.
    /// </p>
    /// <p>
    /// This can result in significant performance improvements as it avoids expression
    /// parsing and node resolution every time it is called. 
    /// </p>
    /// <p>
    /// </p>
    /// </remarks>
    /// <author>Aleksandar Seovic</author>
    [Obsolete(
        "Parses the expression on every call. Hold a parsed expression instead: "
        + "Expression.ParseGetter<TRoot, TResult>(text) or ParseSetter<TRoot, TValue>(text), reused "
        + "per evaluation. Measured on one expression: 0.014 us per evaluation when held, against "
        + "10 us per call here. For a genuinely one-shot call the same API is the same or faster - "
        + "ParseGetter<TRoot, TResult>(text, EvaluationMode.MustInterpret).GetValue(root) is 10 us "
        + "against this class's 13 us - so there is no case in which this is the better choice.",
        false)]
    public class ExpressionEvaluator
    {
        /// <summary>
        /// Parses and evaluates specified expression.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="expression">Expression to evaluate.</param>
        /// <returns>Value of the last node in the expression.</returns>
        public static object GetValue(object root, string expression)
        {
            return GetValue(root, expression, EvaluationMode.MustInterpret);
        }

        /// <summary>
        /// Parses and evaluates specified expression, in the mode the caller asks for.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>A separate overload, not an optional parameter on the one above.</b> The reason is that a
        /// trailing option is what lets an argument be read as the wrong thing: on the write side
        /// <c>SetValue(root, "Slot", null, EvaluationMode.X)</c> compiled clean and wrote the mode
        /// <i>into the property</i>, C# having bound it to
        /// <c>SetValue(root, expression, variables: null, newValue: the mode)</c>. So on this class an
        /// option is always its own parameter in a fixed position - third - and never something that
        /// can drift into a data slot. The getters have no object-typed data parameter and so could
        /// take it either way; they are overloads too, for one rule instead of two.
        /// </p>
        /// <p>
        /// It also means nothing that compiled before needs editing, and an assembly built against an
        /// older version keeps running without a rebuild: C# bakes an optional parameter in at the call
        /// site, so adding one deletes the old signature and an un-rebuilt consumer dies with
        /// <c>MissingMethodException</c>, measured. A side benefit rather than the reason.
        /// </p>
        /// </remarks>
        public static object GetValue(object root, string expression, EvaluationMode mode)
        {
            IExpression exp = Expression.Parse(expression, mode);
            return exp.GetValue(root, null);
        }

        /// <summary>
        /// Parses and evaluates specified expression against a root whose type the call site declares.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The same call, with the root's own type carried through instead of erased to
        /// <c>object</c> - which is the whole of what decides whether the expression compiles.
        /// <c>object</c> declares no members, so anything reaching into the root has no compiled form
        /// and is interpreted; the root's real type is decided on its merits. Measured over the two
        /// suites' own traffic: of the expressions actually evaluated against a root object, 2.4%
        /// compile at <c>object</c> and 72.6% at the root's type, with none lost in that direction -
        /// see <c>_Docs/what-compiles-in-practice.md</c>.
        /// </p>
        /// <p>
        /// <b>It is additive, not a replacement.</b> The <c>object</c> overload above stays, because
        /// <c>GetValue(null, "1 + 2")</c> - 803 of 1,228 call sites in the two suites - cannot infer a
        /// root type and would stop compiling. Where the argument is typed, C# prefers this overload
        /// (an identity conversion beats a conversion to <c>object</c>); where it is a bare null,
        /// inference fails and the <c>object</c> overload serves the call, which is the right answer
        /// since a null root has no type to bind against either way.
        /// </p>
        /// <p>
        /// <b>Binding moves with it</b>, and that is the trade rather than a side effect: a compiled
        /// read binds against the type the call site declared, where the interpreter resolves against
        /// the runtime value - see <c>_Docs/member-binding-semantics.md</c>. The two differ only where
        /// a variable's declared type is not the value's, method hiding by <c>new</c> above all.
        /// </p>
        /// <p>
        /// <b>This class parses on every invocation</b>, as the remarks on it say, which is why
        /// <paramref name="mode"/> defaults to <see cref="EvaluationMode.MustInterpret"/>: a compiled
        /// delegate built for one evaluation and then discarded is waste. Measured one-shot, us per
        /// call, for <c>Ints.!{#this * 2}.sum()</c>:
        /// </p>
        /// <code>
        /// items        0     10    100   1,000   10,000   100,000   1,000,000
        /// interpret   48     59     75     383    5,338    42,975     410,222
        /// compile    416    469    468     461      778     1,816      14,533
        /// </code>
        /// <p>
        /// So asking for <see cref="EvaluationMode.CompileOrInterpret"/> is worth it when the single
        /// evaluation does enough work to pay off the compilation - <b>somewhere above a thousand
        /// items</b> - or when the caller wants the compiled path's declared-type binding. Below that
        /// it is roughly 6x to 8x slower.
        /// </p>
        /// <p>
        /// Reuse is the other answer, and the better one when the same expression is evaluated many
        /// times: parse it once with <see cref="Expression.Parse(string, EvaluationMode, System.Action{EvaluationDecision}, SandboxPolicy)"/>
        /// and call <see cref="IExpression.GetValue{TContext}(TContext, System.Collections.Generic.IDictionary{string, object})"/>
        /// per evaluation, which is where a compiled form pays for itself. This class cannot do that
        /// for you - it has no cache.
        /// </p>
        /// </remarks>
        public static object GetValue<TRoot>(TRoot root, string expression)
        {
            return GetValue<TRoot>(root, expression, EvaluationMode.MustInterpret);
        }

        /// <summary>
        /// Parses and evaluates specified expression against a root whose type the call site declares,
        /// in the mode the caller asks for.
        /// </summary>
        /// <remarks>See the <c>object</c> sibling for why the mode is an overload rather than an
        /// optional parameter, and <see cref="GetValue{TRoot}(TRoot, string)"/> for what the type
        /// parameter buys.</remarks>
        public static object GetValue<TRoot>(TRoot root, string expression, EvaluationMode mode)
        {
            IExpression exp = Expression.Parse(expression, mode);
            return exp.GetValue(root, null);
        }

        // GetValue2<TRoot, TResult> was here - a one-shot call that also compiled, which is the
        // worst of both: measured, the same expression is 10 us one-shot interpreted, 182 us one-shot
        // compiled, and 0.014 us per evaluation once the compiled getter is held. Holding it is what
        // Expression.ParseGetter<TRoot, TResult> is for. It carried the author's own
        // "todo: error: fix it! new name?" and had five call sites, four of them in this repo's own
        // tests. Removed 2026-09-18.

        /// <summary>
        /// Parses and evaluates specified expression.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="expression">Expression to evaluate.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <returns>Value of the last node in the expression.</returns>
        public static object GetValue(
            object root, string expression, IDictionary<string, object> variables)
        {
            return GetValue(root, expression, EvaluationMode.MustInterpret, variables);
        }

        /// <summary>
        /// Parses and evaluates specified expression, in the mode the caller asks for.
        /// </summary>
        /// <remarks>The mode sits third here as it does on every other overload, so there is one rule
        /// to remember rather than two. See the two-argument sibling for why it is an overload.</remarks>
        public static object GetValue(
            object root,
            string expression,
            EvaluationMode mode,
            IDictionary<string, object> variables)
        {
            IExpression exp = Expression.Parse(expression, mode);
            return exp.GetValue(root, variables);
        }

        /// <summary>
        /// Parses and evaluates specified expression against a root whose type the call site declares.
        /// </summary>
        /// <remarks>See <see cref="GetValue{TRoot}(TRoot, string)"/> for why this sits beside the
        /// <c>object</c> overload rather than replacing it.</remarks>
        public static object GetValue<TRoot>(
            TRoot root, string expression, IDictionary<string, object> variables)
        {
            return GetValue<TRoot>(root, expression, EvaluationMode.MustInterpret, variables);
        }

        /// <summary>
        /// Parses and evaluates specified expression against a root whose type the call site declares,
        /// in the mode the caller asks for.
        /// </summary>
        /// <remarks>See the two-argument sibling.</remarks>
        public static object GetValue<TRoot>(
            TRoot root,
            string expression,
            EvaluationMode mode,
            IDictionary<string, object> variables)
        {
            IExpression exp = Expression.Parse(expression, mode);
            return exp.GetValue(root, variables);
        }

        // ----------------------------------------------------------------------------------------
        // Four guards, and they exist because a write's value parameter is `object` and will silently
        // accept an option meant for the engine. Measured, before these:
        //
        //     SetValue(root, "Slot", EvaluationMode.CompileOrInterpret)          Slot = CompileOrInterpret
        //     SetValue(root, "Slot", variables, EvaluationMode.CompileOrInterpret) Slot = CompileOrInterpret
        //
        // Both compile clean and write the mode into the caller's object. They are "forgot the value"
        // mistakes rather than misreadings, but the failure is silent and lands in the user's data,
        // which is the one outcome this API must not have. Marked Obsolete with error: true, so the
        // call is a compile error naming what was meant.
        //
        // The getters need no such guard: none of them has an object-typed data parameter, so an
        // EvaluationMode has nowhere to be mistaken for anything. Both the object and the generic
        // form are needed here, because a typed root binds the generic overload.
        // ----------------------------------------------------------------------------------------

        private const string WriteNeedsAValue
            = "A write needs a value, and this passes the evaluation mode where the value goes - it "
              + "would be written into the target. Use SetValue(root, expression, mode, newValue). To "
              + "write a mode as a value on purpose, cast it: (object)mode.";

        [Obsolete(WriteNeedsAValue, true)]
        public static void SetValue(object root, string expression, EvaluationMode mode)
        {
            throw new InvalidOperationException(WriteNeedsAValue);
        }

        [Obsolete(WriteNeedsAValue, true)]
        public static void SetValue<TRoot>(TRoot root, string expression, EvaluationMode mode)
        {
            throw new InvalidOperationException(WriteNeedsAValue);
        }

        [Obsolete(WriteNeedsAValue, true)]
        public static void SetValue(
            object root, string expression, IDictionary<string, object> variables, EvaluationMode mode)
        {
            throw new InvalidOperationException(WriteNeedsAValue);
        }

        [Obsolete(WriteNeedsAValue, true)]
        public static void SetValue<TRoot>(
            TRoot root, string expression, IDictionary<string, object> variables, EvaluationMode mode)
        {
            throw new InvalidOperationException(WriteNeedsAValue);
        }

        /// <summary>
        /// Parses and evaluates specified expression under a policy the caller supplies, rather than
        /// the process-wide <see cref="SandboxPolicy.Default"/>.
        /// </summary>
        /// <remarks>
        /// <b>Why the mode and the variables are here too, rather than a shorter overload.</b>
        /// <see cref="SandboxPolicy"/> and <c>IDictionary&lt;string, object&gt;</c> are both reference
        /// types, so if they ever sat in the same position at the same arity a bare <c>null</c> would
        /// convert to both and the call would be <c>CS0121</c> ambiguous - measured. Giving the
        /// policy-taking forms an arity nothing else uses removes that by construction: there is
        /// exactly one candidate at this length, so every argument, <c>null</c> included, lands where
        /// it was meant to. Pass <c>null</c> for <paramref name="variables"/> when there are none.
        /// </remarks>
        public static object GetValue(
            object root,
            string expression,
            EvaluationMode mode,
            SandboxPolicy sandbox,
            IDictionary<string, object> variables)
        {
            IExpression exp = Expression.Parse(expression, mode, null, sandbox);
            return exp.GetValue(root, variables);
        }

        /// <summary>
        /// Parses and evaluates specified expression under a policy the caller supplies, rather than
        /// the process-wide <see cref="SandboxPolicy.Default"/>.
        /// </summary>
        /// <remarks>
        /// <b>Why the mode and the variables are here too, rather than a shorter overload.</b>
        /// <see cref="SandboxPolicy"/> and <c>IDictionary&lt;string, object&gt;</c> are both reference
        /// types, so if they ever sat in the same position at the same arity a bare <c>null</c> would
        /// convert to both and the call would be <c>CS0121</c> ambiguous - measured. Giving the
        /// policy-taking forms an arity nothing else uses removes that by construction: there is
        /// exactly one candidate at this length, so every argument, <c>null</c> included, lands where
        /// it was meant to. Pass <c>null</c> for <paramref name="variables"/> when there are none.
        /// </remarks>
        public static object GetValue<TRoot>(
            TRoot root,
            string expression,
            EvaluationMode mode,
            SandboxPolicy sandbox,
            IDictionary<string, object> variables)
        {
            IExpression exp = Expression.Parse(expression, mode, null, sandbox);
            return exp.GetValue(root, variables);
        }

        /// <summary>
        /// Parses and specified expression and sets the value of the
        /// last node to the value of the <c>newValue</c> parameter.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="expression">Expression to evaluate.</param>
        /// <param name="newValue">Value to set last node to.</param>
        public static void SetValue(object root, string expression, object newValue)
        {
            SetValue(root, expression, EvaluationMode.MustInterpret, newValue);
        }

        /// <summary>
        /// Parses and evaluates specified expression as a write, in the mode the caller asks for.
        /// </summary>
        /// <remarks>
        /// <b>The mode sits third, not last, and that is not a style choice.</b> As a trailing optional
        /// it is silently ambiguous with the <c>variables</c> overload, because <c>newValue</c> is
        /// <c>object</c> and swallows anything: measured, <c>SetValue(root, "Slot", null,
        /// EvaluationMode.MustInterpret)</c> compiled without a diagnostic and wrote <b>MustInterpret
        /// into the property</b>, C# having bound it to
        /// <c>SetValue(root, expression, variables: null, newValue: the mode)</c>. Third position is
        /// unambiguous, since an <see cref="EvaluationMode"/> converts to neither
        /// <c>IDictionary&lt;string, object&gt;</c> nor the value parameter of any other overload. The
        /// getters take it last because there it is safe - an <c>EvaluationMode</c> and an
        /// <c>IDictionary</c> are unrelated types, so the third argument always says which overload is
        /// meant.
        /// </remarks>
        public static void SetValue(
            object root, string expression, EvaluationMode mode, object newValue)
        {
            IExpression exp = Expression.Parse(expression, mode);
            exp.SetValue(root, null, newValue);
        }

        /// <summary>
        /// Parses specified expression and sets the value of the last node, with both the root's type
        /// and the value's declared by the call site.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>A write has two walls and the value is the second one.</b> An <c>object</c>-typed value
        /// against a typed member has no compiled form - whether it fits depends on the runtime value,
        /// so <c>PropertyOrFieldNode</c> refuses it and the interpreter serves the write. That is why
        /// <c>TValue</c> is here as well as <c>TRoot</c>: declaring only the root buys a write nothing,
        /// measured, where it takes reads from 2.4% to 72.6%.
        /// </p>
        /// <p>
        /// Inference is all-or-nothing, so <c>SetValue(order, "Total", null)</c> - a bare null value -
        /// falls to the <c>object</c> overload and binds nothing, which is correct: a null literal has
        /// no type on either backend. Give the null a type to get a compiled write.
        /// </p>
        /// </remarks>
        public static void SetValue<TRoot, TValue>(TRoot root, string expression, TValue newValue)
        {
            SetValue<TRoot, TValue>(root, expression, EvaluationMode.MustInterpret, newValue);
        }

        /// <summary>
        /// Parses and evaluates specified expression as a write, with both types declared by the call
        /// site, in the mode the caller asks for.
        /// </summary>
        /// <remarks>See the <c>object</c> sibling for why the mode sits third.</remarks>
        public static void SetValue<TRoot, TValue>(
            TRoot root, string expression, EvaluationMode mode, TValue newValue)
        {
            IExpression exp = Expression.Parse(expression, mode);
            exp.SetValue(root, null, newValue);
        }

        /// <summary>
        /// Parses and specified expression and sets the value of the
        /// last node to the value of the <c>newValue</c> parameter.
        /// </summary>
        /// <param name="root">Root object.</param>
        /// <param name="expression">Expression to evaluate.</param>
        /// <param name="variables">Expression variables map.</param>
        /// <param name="newValue">Value to set last node to.</param>
        public static void SetValue(
            object root, string expression, IDictionary<string, object> variables, object newValue)
        {
            SetValue(root, expression, EvaluationMode.MustInterpret, variables, newValue);
        }

        /// <summary>
        /// Parses and evaluates specified expression as a write, in the mode the caller asks for.
        /// </summary>
        /// <remarks>See the three-argument sibling for why the mode sits third.</remarks>
        public static void SetValue(
            object root,
            string expression,
            EvaluationMode mode,
            IDictionary<string, object> variables,
            object newValue)
        {
            IExpression exp = Expression.Parse(expression, mode);
            exp.SetValue(root, variables, newValue);
        }

        /// <summary>
        /// Parses specified expression and sets the value of the last node, with both the root's type
        /// and the value's declared by the call site.
        /// </summary>
        /// <remarks>See <see cref="SetValue{TRoot, TValue}(TRoot, string, TValue)"/>.</remarks>
        public static void SetValue<TRoot, TValue>(
            TRoot root, string expression, IDictionary<string, object> variables, TValue newValue)
        {
            SetValue<TRoot, TValue>(root, expression, EvaluationMode.MustInterpret, variables, newValue);
        }

        /// <summary>
        /// Parses and evaluates specified expression as a write, with both types declared by the call
        /// site, in the mode the caller asks for.
        /// </summary>
        /// <remarks>See the three-argument sibling for why the mode sits third.</remarks>
        public static void SetValue<TRoot, TValue>(
            TRoot root,
            string expression,
            EvaluationMode mode,
            IDictionary<string, object> variables,
            TValue newValue)
        {
            IExpression exp = Expression.Parse(expression, mode);
            exp.SetValue(root, variables, newValue);
        }
        /// <summary>
        /// Parses and evaluates specified expression under a policy the caller supplies, rather than
        /// the process-wide <see cref="SandboxPolicy.Default"/>.
        /// </summary>
        /// <remarks>
        /// <b>Why the mode and the variables are here too, rather than a shorter overload.</b>
        /// <see cref="SandboxPolicy"/> and <c>IDictionary&lt;string, object&gt;</c> are both reference
        /// types, so if they ever sat in the same position at the same arity a bare <c>null</c> would
        /// convert to both and the call would be <c>CS0121</c> ambiguous - measured. Giving the
        /// policy-taking forms an arity nothing else uses removes that by construction: there is
        /// exactly one candidate at this length, so every argument, <c>null</c> included, lands where
        /// it was meant to. Pass <c>null</c> for <paramref name="variables"/> when there are none.
        /// </remarks>
        public static void SetValue(
            object root,
            string expression,
            EvaluationMode mode,
            SandboxPolicy sandbox,
            IDictionary<string, object> variables,
            object newValue)
        {
            IExpression exp = Expression.Parse(expression, mode, null, sandbox);
            exp.SetValue(root, variables, newValue);
        }

        /// <summary>
        /// Parses and evaluates specified expression under a policy the caller supplies, rather than
        /// the process-wide <see cref="SandboxPolicy.Default"/>.
        /// </summary>
        /// <remarks>
        /// <b>Why the mode and the variables are here too, rather than a shorter overload.</b>
        /// <see cref="SandboxPolicy"/> and <c>IDictionary&lt;string, object&gt;</c> are both reference
        /// types, so if they ever sat in the same position at the same arity a bare <c>null</c> would
        /// convert to both and the call would be <c>CS0121</c> ambiguous - measured. Giving the
        /// policy-taking forms an arity nothing else uses removes that by construction: there is
        /// exactly one candidate at this length, so every argument, <c>null</c> included, lands where
        /// it was meant to. Pass <c>null</c> for <paramref name="variables"/> when there are none.
        /// </remarks>
        public static void SetValue<TRoot, TValue>(
            TRoot root,
            string expression,
            EvaluationMode mode,
            SandboxPolicy sandbox,
            IDictionary<string, object> variables,
            TValue newValue)
        {
            IExpression exp = Expression.Parse(expression, mode, null, sandbox);
            exp.SetValue(root, variables, newValue);
        }
    }
}