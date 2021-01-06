using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ClockQuantization
{
    /// <summary>
    /// Represents an interval within a <see cref="ClockQuantizer"/>'s temporal context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Within the reference frame of an <see cref="Interval"/>, there is no notion of time; there is only notion of the order
    /// in which <see cref="LazyTimeSerialPosition"/>s are issued.
    /// </para>
    /// <para>
    /// Whereas <see cref="ClockQuantizer.CurrentInterval"/> is always progressing with intervals of at most <see cref="ClockQuantizer.MaxIntervalTimeSpan"/> length,
    /// several <see cref="Interval"/>s may be active concurrently.
    /// </para>
    /// </remarks>
    public class Interval
    {
        internal struct SnapshotGenerator
        {
            internal uint SerialPosition;
            internal readonly DateTimeOffset DateTimeOffset;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static ref readonly SnapshotGenerator WithNextSerialPosition(ref SnapshotGenerator generator)
            {
#if NET5_0
                Interlocked.Increment(ref generator.SerialPosition);
#else
                Interlocked.Add(ref Unsafe.As<uint, int>(ref generator.SerialPosition), 1);
#endif
                return ref generator;
            }

            internal SnapshotGenerator(in DateTimeOffset offset) { SerialPosition = 0u; DateTimeOffset = offset; }
        }

        private SnapshotGenerator _generator;

        /// <value>
        /// The <see cref="DateTimeOffset"/> within the temporal context when the <see cref="Interval"/> was started.
        /// </value>
        public DateTimeOffset DateTimeOffset { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _generator.DateTimeOffset; }

        internal Interval(in DateTimeOffset offset) => _generator = new SnapshotGenerator(in offset);


        /// <summary>
        /// If <paramref name="position"/> does not have a <see cref="LazyTimeSerialPosition.DateTimeOffset"/> yet, it will be initialized with one,
        /// based off <paramref name="interval"/>'s <see cref="Interval.DateTimeOffset"/> and its monotonically increasing internal serial position.
        /// </summary>
        /// <param name="interval">The interval to create the <see cref="LazyTimeSerialPosition"/> off.</param>
        /// <param name="position">Reference to an (on-stack) <see cref="LazyTimeSerialPosition"/> which may or may not have been initialized.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureInitializedTimeSerialPosition(Interval interval, ref LazyTimeSerialPosition position)
        {
            if (position.HasValue && interval._generator.SerialPosition > 0u)
            {
                return;
            }

            LazyTimeSerialPosition.ApplySnapshot(ref position, in SnapshotGenerator.WithNextSerialPosition(ref interval._generator));
        }

        /// <summary>
        /// Creates a new <see cref="LazyTimeSerialPosition"/> based off the <see cref="Interval"/>'s <see cref="Interval.DateTimeOffset"/> and its monotonically increasing internal serial position.
        /// </summary>
        /// <returns>A new <see cref="LazyTimeSerialPosition"/></returns>
        /// <remarks>
        /// A <see cref="LazyTimeSerialPosition"/> created at the time when a new <see cref="Interval"/> is created (e.g. during
        /// <seealso cref="ClockQuantizer.EnsureInitializedExactTimeSerialPosition(ref LazyTimeSerialPosition, bool)"/>) will have <see cref="LazyTimeSerialPosition.IsExact"/> equal
        /// to <see langword="true"/>.
        /// </remarks>
        public LazyTimeSerialPosition NewTimeSerialPosition() => new LazyTimeSerialPosition(in SnapshotGenerator.WithNextSerialPosition(ref _generator));

        internal Interval Seal()
        {
            // Prevent 'Exact' positions post initialization of the Interval; ensure SerialPosition > 0
#if NET5_0
            Interlocked.CompareExchange(ref _generator.SerialPosition, 1u, 0u);
#else
            Interlocked.CompareExchange(ref Unsafe.As<uint, int>(ref _generator.SerialPosition), 1, 0);
#endif

            return this;
        }
    }
}