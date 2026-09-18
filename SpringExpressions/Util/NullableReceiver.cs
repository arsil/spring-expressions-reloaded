using System;

using JetBrains.Annotations;

using SpringCore;

using SpringExpressions.Expressions.Compiling;

using LExpression = System.Linq.Expressions.Expression;

namespace SpringExpressions.Util
{
    /// <summary>
    /// A member written after a <c>Nullable&lt;T&gt;</c> is read from the value inside it, not from the
    /// nullable wrapper - <c>ShippedOn.Year</c> is the year of the date, and it fails when there is no
    /// date.
    /// </summary>
    /// <remarks>
    /// <p>
    /// The interpreter always did this, because it never sees a wrapper: a boxed nullable holding a
    /// value <i>is</i> a boxed <c>DateTime</c>. The compiled path saw the wrapper and resolved members
    /// against <c>Nullable&lt;T&gt;</c>, so the two were reading different objects, and it showed in
    /// both directions:
    /// </p>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Seventeen ordinary shapes had no compiled form at all</b> - <c>SomeDate.Year</c>,
    /// <c>SomeDate.AddDays(1)</c>, <c>SomeSpan.TotalMinutes</c>, <c>SomeMoney.Amount</c> on a caller's
    /// own struct - because <c>Nullable&lt;T&gt;</c> declares none of them. They fell back and the
    /// interpreter answered every one.
    /// </description></item>
    /// <item><description>
    /// <b>And the members it does declare diverged</b>: <c>NoNumber.ToString()</c> was <c>""</c>
    /// compiled - which is what C# gives - and an exception interpreted, because the interpreter has a
    /// bare null and no <c>ToString</c> to find on it.
    /// </description></item>
    /// </list>
    /// <p>
    /// <b>What this costs, and it is the only cost:</b> <c>HasValue</c> and <c>Value</c> stop resolving
    /// on the compiled path, since the receiver is the underlying value now and an <c>int</c> has
    /// neither. They never worked interpreted, so no weakly typed caller could rely on them; a caller
    /// who wants the question writes <c>SomeNumber != null</c>.
    /// </p>
    /// </remarks>
    public static class NullableReceiver
    {
        /// <summary>
        /// Raised when the member is read and the nullable holds nothing - the same exception, and the
        /// same shape of message, the interpreter raises for a null in the middle of a path. Generic so
        /// it can stand in a conditional beside the member access it replaces, whatever that answers.
        /// </summary>
        [UsedImplicitly]
        public static T Fail<T>(string memberName)
        {
            throw new NullValueInNestedPathException(
                "Cannot retrieve the value of a field or property '" + memberName
                + "', because the value it is read from is null.");
        }

        /// <summary>
        /// <c>receiver.HasValue ? memberOfTheValue : Fail&lt;T&gt;(name)</c>.
        /// </summary>
        /// <param name="buildMemberOfTheValue">
        /// Builds the member access, given the receiver to read <c>Value</c> from. A builder rather than
        /// a finished expression because the receiver is mentioned twice - once for <c>HasValue</c> and
        /// once inside the member access - and mentioning an emitted operand twice evaluates it twice:
        /// <c>Maybe().ToString()</c> called <c>Maybe()</c> two times compiled against one interpreted.
        /// The receiver goes into a block variable and the builder is handed that.
        /// </param>
        [NotNull]
        public static LExpression GuardWithHasValue(
            [NotNull] LExpression nullableReceiver,
            [NotNull] Func<LExpression, LExpression> buildMemberOfTheValue,
            [NotNull] string memberName)
        {
            return OperandLocals.UseOnce(
                nullableReceiver,
                receiver =>
                {
                    var memberOfTheValue = buildMemberOfTheValue(receiver);

                    return LExpression.Condition(
                        LExpression.Property(receiver, "HasValue"),
                        memberOfTheValue,
                        LExpression.Call(
                            MiFail.MakeGenericMethod(memberOfTheValue.Type),
                            LExpression.Constant(memberName)));
                });
        }

        /// <summary>
        /// Hands the receiver back, or raises the same exception <see cref="Fail{T}"/> raises when it
        /// is null - so a member access can be guarded <i>in place</i> rather than wrapped.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>This is the shape a write needs, and it is simpler than the one a read needs.</b>
        /// <see cref="GuardAgainstNullReference"/> builds a conditional, because a read has to
        /// <i>produce</i> a value when the receiver is null and there is nothing to produce but an
        /// exception; that costs a hoist as well, since the receiver is mentioned twice. A write has no
        /// such problem: guard the receiver where it stands and the tree is still
        /// <c>Assign(Property(receiver, pi), value)</c> - an <c>Assign</c> at the root, which is what
        /// the void compiler admits, and the receiver mentioned once, so nothing needs hoisting.
        /// </p>
        /// <p>
        /// The read form reuses <see cref="Fail{T}"/> outright, so the two cannot report the same
        /// situation differently.
        /// </p>
        /// </remarks>
        /// <summary>
        /// Hands the receiver of a <b>write</b> back, or raises the exception the interpreter raises
        /// when it is null.
        /// </summary>
        /// <remarks>
        /// <p>
        /// <b>A write is guarded in place; a read is wrapped.</b> A read has to <i>produce</i> a value
        /// when the receiver is null, so <see cref="GuardAgainstNullReference"/> builds a conditional
        /// and hoists the receiver, which is mentioned twice. A write cannot: the void compiler admits
        /// a void call or an <c>Assign</c> and nothing else, so wrapping the assignment would refuse
        /// every void expression that writes through a path. Guarding the receiver where it stands
        /// leaves an <c>Assign</c> at the root and mentions the receiver once.
        /// </p>
        /// <p>
        /// <b>The read path deliberately does not use this shape</b>, though it could. Measured, per
        /// evaluation: one member read 13.3 ns as a conditional against 16.1 as a call, two reads 14.1
        /// against 20.4, three reads and an addition 15.4 against 24.1 - the call does not inline away
        /// and the cost compounds per member. A write pays it once and has no alternative.
        /// </p>
        /// </remarks>
        [UsedImplicitly]
        public static T RequireForWrite<T>(T receiver, string memberName) where T : class
        {
            if (receiver == null)
            {
                throw new NullValueInNestedPathException(
                    "Cannot set the value of a field or property '" + memberName
                    + "', because the value it is written to is null.");
            }

            return receiver;
        }

        /// <summary>
        /// The container guard for reading through an indexer. It names no member, because an indexer
        /// has no name the caller wrote - the interpreter's message does not name one either.
        /// </summary>
        [UsedImplicitly]
        public static T RequireContainerForRead<T>(T container) where T : class
        {
            if (container == null)
            {
                throw new NullValueInNestedPathException(
                    "Cannot retrieve the value of the indexer because the context for its "
                    + "resolution is null.");
            }

            return container;
        }

        /// <summary>The write twin of <see cref="RequireContainerForRead{T}"/>.</summary>
        [UsedImplicitly]
        public static T RequireContainerForWrite<T>(T container) where T : class
        {
            if (container == null)
            {
                throw new NullValueInNestedPathException(
                    "Cannot set the value of the indexer because the context for its "
                    + "resolution is null.");
            }

            return container;
        }

        /// <summary>
        /// Guards the container of an indexer, or hands it back where nothing can be null.
        /// </summary>
        /// <remarks>
        /// Applied once at the top of each emit, so every branch below - an array, a generic
        /// dictionary's TryGetValue, an accessor call - is guarded by one edit. The guard preserves
        /// the expression's type, so the branch selection below is unaffected.
        /// </remarks>
        [NotNull]
        public static LExpression GuardContainer([NotNull] LExpression container, bool forWrite)
        {
            if (container.Type.IsValueType)
                return container;

            var guard = forWrite ? MiRequireContainerForWrite : MiRequireContainerForRead;

            return LExpression.Call(guard.MakeGenericMethod(container.Type), container);
        }

        /// <summary>
        /// <c>RequireForWrite</c> applied to an emitted receiver, or the receiver unchanged where
        /// nothing can be null - a static member has none, and a value type has no null to hold.
        /// </summary>
        [NotNull]
        public static LExpression GuardWriteReceiver(
            [CanBeNull] LExpression receiver, bool memberIsStatic, [NotNull] string memberName)
        {
            if (receiver == null || memberIsStatic || receiver.Type.IsValueType)
                return receiver;

            return LExpression.Call(
                MiRequireForWrite.MakeGenericMethod(receiver.Type),
                receiver,
                LExpression.Constant(memberName));
        }

        /// <summary>
        /// <c>receiver != null ? member : Fail&lt;T&gt;(name)</c>, for a receiver that is a reference
        /// rather than a <c>Nullable&lt;T&gt;</c>.
        /// </summary>
        /// <remarks>
        /// <p>
        /// The same question one type-kind over, and the compiled path was answering it differently:
        /// a null <i>nullable</i> mid-path raised <see cref="NullValueInNestedPathException"/> through
        /// <see cref="GuardWithHasValue"/> while a null <i>reference</i> mid-path let the CLR raise a
        /// bare <c>NullReferenceException</c>. The interpreter has always raised the former for both -
        /// <c>PropertyOrFieldNode.Get</c> tests <c>context == null &amp;&amp; accessor.RequiresContext</c> -
        /// and the frozen suite pins it, so the compiled path was inconsistent with the interpreter
        /// and with itself at once.
        /// </p>
        /// <p>
        /// It stayed invisible while <c>ExpressionEvaluator</c> bound every call at <c>object</c>:
        /// nothing reaching into a root compiled at all, so the shape was only ever interpreted. It
        /// surfaced the moment that API carried the root's own type.
        /// </p>
        /// <p>
        /// <b>The getter only.</b> An assignment cannot be wrapped this way: the void compiler admits a
        /// void call or an <c>Assign</c> and nothing else, so putting a setter inside a
        /// <c>Condition</c> or a <c>Block</c> would refuse every void expression that writes through a
        /// path. The setter's null-receiver case therefore still raises the CLR's exception - recorded
        /// rather than fixed, and mostly unreachable, since a write through a path whose last member is
        /// not writable refuses at compile and the interpreter serves it.
        /// </p>
        /// </remarks>
        [NotNull]
        public static LExpression GuardAgainstNullReference(
            [NotNull] LExpression referenceReceiver,
            [NotNull] Func<LExpression, LExpression> buildMember,
            [NotNull] string memberName)
        {
            return OperandLocals.UseOnce(
                referenceReceiver,
                receiver =>
                {
                    var member = buildMember(receiver);

                    return LExpression.Condition(
                        LExpression.ReferenceNotEqual(
                            receiver, LExpression.Constant(null, receiver.Type)),
                        member,
                        LExpression.Call(
                            MiFail.MakeGenericMethod(member.Type),
                            LExpression.Constant(memberName)));
                });
        }

        private static readonly System.Reflection.MethodInfo MiFail
            = typeof(NullableReceiver).GetMethod(nameof(Fail));

        private static readonly System.Reflection.MethodInfo MiRequireForWrite
            = typeof(NullableReceiver).GetMethod(nameof(RequireForWrite));

        private static readonly System.Reflection.MethodInfo MiRequireContainerForRead
            = typeof(NullableReceiver).GetMethod(nameof(RequireContainerForRead));

        private static readonly System.Reflection.MethodInfo MiRequireContainerForWrite
            = typeof(NullableReceiver).GetMethod(nameof(RequireContainerForWrite));
    }
}
