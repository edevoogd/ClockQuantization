using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ClockQuantization
{
    /// <summary>
    /// <para>
    /// Represents a point in time, expressed as a combination of <see cref="DateTimeOffset"/> and <see cref="SerialPosition"/>. Its value may be unitialized,
    /// as indicated by its <see cref="HasValue"/> property.
    /// </para>
    /// <para>
    /// When initialized (i.e. when <see cref="HasValue"/> equals <see langword="true"/>), the following rules apply:
    /// <list type="bullet">
    /// <item>Issuance of an "exact" <see cref="LazyTimeSerialPosition"/> can only occur at <see cref="Interval"/> start. By definition, <see cref="IsExact"/> will equal
    /// <see langword="true"/>, <see cref="SerialPosition"/> will equal <c>1u</c> and <see cref="DateTimeOffset"/> will equal <see cref="Interval.DateTimeOffset"/>.</item>
    /// <item>Any <see cref="LazyTimeSerialPosition"/> issued off the same <see cref="Interval"/> with <see cref="SerialPosition"/> N (N &gt; 1u) was issued
    /// at a later point in (continuous) time than the <see cref="LazyTimeSerialPosition"/> with <see cref="SerialPosition"/> equals N-1 and was issued at an earlier
    /// point in (continuous) time than any <see cref="LazyTimeSerialPosition"/> with <see cref="SerialPosition"/> &gt; N.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <remarks>
    /// With several methods available to lazily initialize a <see cref="LazyTimeSerialPosition"/> by reference, it is possible to create <see cref="LazyTimeSerialPosition"/>s
    /// on-stack and initialize them as late as possible and only if deemed necessary for the operation/decision at hand.
    /// <seealso cref="Interval.EnsureInitializedTimeSerialPosition(Interval, ref LazyTimeSerialPosition)"/>
    /// <seealso cref="ClockQuantizer.EnsureInitializedExactTimeSerialPosition(ref LazyTimeSerialPosition, bool)"/>
    /// <seealso cref="ClockQuantizer.EnsureInitializedTimeSerialPosition(ref LazyTimeSerialPosition)"/>
    /// </remarks>
    public struct LazyTimeSerialPosition

    {
        private static class ThrowHelper
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static InvalidOperationException CreateInvalidOperationException() => new InvalidOperationException();
        }

        private readonly struct Snapshot
        {
            public readonly DateTimeOffset DateTimeOffset;
            public readonly uint SerialPosition;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Snapshot(in Interval.SnapshotGenerator generator)
            {
                SerialPosition = generator.SerialPosition;
                DateTimeOffset = generator.DateTimeOffset;
            }
        }

        private Snapshot _snapshot;

        /// <value>Returns the <see cref="System.DateTimeOffset"/> assigned to the current value.</value>
        /// <exception cref="InvalidOperationException">When <see cref="HasValue"/> is <see langword="false"/>.</exception>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly DateTimeOffset DateTimeOffset { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => HasValue ? _snapshot.DateTimeOffset : throw ThrowHelper.CreateInvalidOperationException(); }

        /// <value>Returns the serial position assigned to the current value.</value>
        /// <exception cref="InvalidOperationException">When <see cref="HasValue"/> is <see langword="false"/>.</exception>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly uint SerialPosition { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => HasValue ? _snapshot.SerialPosition : throw ThrowHelper.CreateInvalidOperationException(); }

        /// <value>Returns <see langword="true"/> if a value is assigned, <see langword="false"/> otherwise.</value>
        public readonly bool HasValue { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _snapshot.SerialPosition > 0u; }

        /// <value>Returns <see langword="true"/> if a value is assigned and said value represents the first <see cref="SerialPosition"/> issued at <see cref="DateTimeOffset"/>. In other words,
        /// the value was assigned exactly at <see cref="DateTimeOffset"/>.</value>
        public readonly bool IsExact { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _snapshot.SerialPosition == 1u; }

        internal LazyTimeSerialPosition(in Interval.SnapshotGenerator generator) { _snapshot = new Snapshot(in generator); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ApplySnapshot(ref LazyTimeSerialPosition position, in Interval.SnapshotGenerator generator)
        {
            position._snapshot = new Snapshot(in generator);
        }
    }
}
