using System;
using System.Runtime.CompilerServices;

namespace ClockQuantization
{
    /// <summary>
    /// <see cref="ClockQuantizer"/> is a utility class that abstracts quantization of the reference clock. Essentially, the reference clock continuum is divided into discrete intervals with a <i>maximum</i> length of <see cref="ClockQuantizer.MaxIntervalTimeSpan"/>.
    /// A so-called metronome is used to start a new <see cref="Interval"/> every time when <see cref="ClockQuantizer.MaxIntervalTimeSpan"/> has passed. A <see cref="Interval"/> may be cut short when an "out-of-cadance" advance operation is performed - such operation is triggered by
    /// <see cref="Advance()"/> calls, as well as by <see cref="ISystemClockTemporalContext.ClockAdjusted"/> and <see cref="ISystemClockTemporalContext.MetronomeTicked"/> events.
    /// </summary>
    /// <remarks>Under certain conditions, an advance operation may be incurred by <see cref="EnsureInitializedExactTimeSerialPosition(ref LazyTimeSerialPosition, bool)"/> calls.</remarks>
    public class ClockQuantizer //: IAsyncDisposable, IDisposable
    {
        private struct AdvancePreparationInfo
        {
            public Interval Interval;
            public ClockQuantizer.NewIntervalEventArgs EventArgs;

            public AdvancePreparationInfo(Interval interval, ClockQuantizer.NewIntervalEventArgs eventArgs)
            {
                Interval = interval;
                EventArgs = eventArgs;
            }
        }

        private readonly ISystemClock _clock;
        private Interval? _currentInterval;
        private readonly System.Threading.Timer? _metronome;


        // Properties
        /// <summary>
        /// The maximum <see cref="TimeSpan"/> of each <see cref="Interval"/>, defined at <see cref="ClockQuantizer"/> construction.
        /// </summary>
        public readonly TimeSpan MaxIntervalTimeSpan;

        /// <value>The current <see cref="Interval"/> in the <see cref="ClockQuantizer"/>'s temporal context.</value>
        /// <remarks>A <see cref="ClockQuantizer"/> starts in an inhibited state. Only after the first advance operation, will <see cref="CurrentInterval"/> have a non-<see langword="null"/> value.</remarks>
        public Interval? CurrentInterval { get => _currentInterval; }

        /// <value>Returns the <see cref="ISystemClock.UtcNow"/> value of the reference clock.</value>
        /// <remarks>Depending on the actual reference clock implementation, this may or may not incur an expensive system call.</remarks>
        public DateTimeOffset UtcNow { get => NewDisconnectedInterval().DateTimeOffset; }


        // Basic quantizer operations
        /// <summary>
        /// Establishes a new <b>lower bound</b> on the "last seen" exact <see cref="DateTimeOffset"/> within the
        /// <see cref="ClockQuantizer"/>'s temporal context: the reference clock's <see cref="ISystemClock.UtcNow"/>.
        /// </summary>
        /// <returns>The newly started <see cref="Interval"/>.</returns>
        public Interval Advance() => Advance(metronomic: false);


        // Basic position operations
        /// <summary>
        /// If <paramref name="position"/> does not have an exact <see cref="LazyTimeSerialPosition.DateTimeOffset"/> yet, it will be initialized with one. In every
        /// situation where initialization is still required, this will incur a call into the reference clock's <see cref="ISystemClock.UtcNow"/>.
        /// </summary>
        /// <param name="position">Reference to an (on-stack) <see cref="LazyTimeSerialPosition"/> which may or may not have been initialized.</param>
        /// <param name="advance">Indicates if the <see cref="ClockQuantizer"/> should perform an advance operation. This is advised in situations where non-exact
        /// positions may still be acquired in the same <see cref="CurrentInterval"/> and exact ordering (e.g. in a cache LRU eviction algorithm) might be adversely affected.</param>
        /// <remarks>
        /// <para>An advance operation will incur an <see cref="ClockQuantizer.Advanced"/> event.</para>
        /// <para>Depending on the actual reference clock implementation, this may or may not incur an expensive system call.</para>
        /// </remarks>
        public void EnsureInitializedExactTimeSerialPosition(ref LazyTimeSerialPosition position, bool advance)
        {
            if (!position.IsExact)    // test here as well to prevent unnecessary/unexpected Advance() if position was already initialzed
            {
                if (advance)
                {
                    var preparation = PrepareAdvance(metronomic: false);
                    Interval.EnsureInitializedTimeSerialPosition(preparation.Interval, ref position);
                    CommitAdvance(preparation);
                }
                else
                {
                    Interval.EnsureInitializedTimeSerialPosition(NewDisconnectedInterval(), ref position);
                }
            }
        }

        /// <summary>
        /// If <paramref name="position"/> does not have a <see cref="LazyTimeSerialPosition.DateTimeOffset"/> yet, it will be initialized with one.
        /// </summary>
        /// <param name="position">Reference to an (on-stack) <see cref="LazyTimeSerialPosition"/> which may or may not have been initialized.</param>
        /// <remarks>
        /// If the <see cref="ClockQuantizer"/> did not perform a first advance operation yet, the result will be an exact position
        /// (incurring a call into the reference clock's <see cref="ISystemClock.UtcNow"/>). Otherwise, returns a position bound to
        /// <see cref="CurrentInterval"/>'s <see cref="Interval.DateTimeOffset"/>, but with an incremented <see cref="LazyTimeSerialPosition.SerialPosition"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureInitializedTimeSerialPosition(ref LazyTimeSerialPosition position)
        {
            if (!position.HasValue)
            {
                Interval.EnsureInitializedTimeSerialPosition(_currentInterval ?? NewDisconnectedInterval(), ref position);
            }
        }

        // Events
        /// <summary>
        /// Represents the ephemeral conditions at the time of an advance operation.
        /// </summary>
        public class NewIntervalEventArgs : EventArgs
        {
            /// <summary>
            /// The <see cref="System.DateTimeOffset"/> within the temporal context when the new <see cref="Interval"/> was started.
            /// </summary>
            public readonly DateTimeOffset DateTimeOffset;

            /// <summary>
            /// <see langword="true"/> if the new <see cref="Interval"/> was created due to a metronome "tick", <see langword="false"/> otherwise.
            /// </summary>
            public readonly bool IsMetronomic;

            /// <summary>
            /// An optional <see cref="System.TimeSpan"/> value representing the gap between the start of the new interval and the expected end of
            /// <see cref="ClockQuantizer.CurrentInterval"/>, if such gap is in fact detected.
            /// </summary>
            public readonly TimeSpan? GapToPriorIntervalExpectedEnd;

            internal NewIntervalEventArgs(DateTimeOffset offset, bool metronomic, TimeSpan? gap)
            {
                DateTimeOffset = offset;
                IsMetronomic = metronomic;
                GapToPriorIntervalExpectedEnd = gap;
            }
        }

        /// <summary>
        /// This event is fired direclty after the start of a new <see cref="Interval"/> within the <see cref="ClockQuantizer"/>'s temporal context.
        /// </summary>
        public event EventHandler<NewIntervalEventArgs>? Advanced;

        /// <summary>
        /// This event is fired almost immediately after each "tick" of the metronome. Every <see cref="MetronomeTicked"/> event is preceeded by an advance operation and a corresponding <see cref="Advanced"/> event, ensuring that a new <see cref="CurrentInterval"/> reference
        /// has been established in the <see cref="ClockQuantizer"/>'s temporal context at the time of firing.
        /// </summary>
        /// <remarks>
        /// Under typical operating conditions, the intermittent elapse of every <see cref="MaxIntervalTimeSpan"/> interval is signaled by the <see cref="ClockQuantizer"/>'s built-in metronome.
        /// Alternatively, metronome "ticks" may be generated by an external source that is firing <see cref="ISystemClockTemporalContext.MetronomeTicked"/> events.
        /// </remarks>
        /// <seealso cref="ISystemClockTemporalContext"/>
        public event EventHandler<NewIntervalEventArgs>? MetronomeTicked;

        /// <summary>
        /// Raises the <see cref="Advanced"/> event. May be overriden in derived implementations.
        /// </summary>
        /// <param name="e">A <see cref="NewIntervalEventArgs"/> instance</param>
        protected virtual void OnAdvanced(NewIntervalEventArgs e) => Advanced?.Invoke(this, e);

        /// <summary>
        /// Raises the <see cref="MetronomeTicked"/> event. May be overriden in derived implementations.
        /// </summary>
        /// <param name="e">A <see cref="NewIntervalEventArgs"/> instance</param>
        /// <remarks>
        /// <see cref="MetronomeTicked"/> events are always preceded with an <see cref="Advanced"/> event. The value of <paramref name="e"/> is the same in both consecutive events.
        /// </remarks>
        protected virtual void OnMetronomeTicked(NewIntervalEventArgs e) => MetronomeTicked?.Invoke(this, e);


        // Construction

        /// <summary>
        /// Creates a new <see cref="ClockQuantizer"/> instance.
        /// </summary>
        /// <param name="clock">The reference <see cref="ISystemClock"/></param>
        /// <param name="maxIntervalTimeSpan">The maximum <see cref="TimeSpan"/> of each <see cref="Interval"/></param>
        /// <remarks>
        /// If <paramref name="clock"/> also implements <see cref="ISystemClockTemporalContext"/>, the <see cref="ClockQuantizer"/> will pick up on external
        /// <see cref="ISystemClockTemporalContext.ClockAdjusted"/> events. Also, if <see cref="ISystemClockTemporalContext.ProvidesMetronome"/> is <see langword="true"/>,
        /// the <see cref="ClockQuantizer"/> will pick up on external <see cref="ISystemClockTemporalContext.MetronomeTicked"/> events, instead of relying on an internal metronome.
        /// </remarks>
        public ClockQuantizer(ISystemClock clock, TimeSpan maxIntervalTimeSpan)
        {
            _clock = clock;
            MaxIntervalTimeSpan = maxIntervalTimeSpan;
            bool metronomic = true;

            if (clock is ISystemClockTemporalContext context)
            {
                context.ClockAdjusted += Context_ClockAdjusted;
                if (context.ProvidesMetronome)
                {
                    // Allow external "pulse" on metronome ticks
                    context.MetronomeTicked += Context_MetronomeTicked;
                    metronomic = false;
                }
            }

            if (metronomic)
            {
                // Create a suspended timer. Timer will be started at first call to Advance().
                _metronome = new System.Threading.Timer(Metronome_TimerCallback, null, System.Threading.Timeout.InfiniteTimeSpan, maxIntervalTimeSpan);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Interval NewDisconnectedInterval() => new Interval(_clock.UtcNow);

        private Interval Advance(bool metronomic)
        {
            var preparation = PrepareAdvance(metronomic);
            return CommitAdvance(preparation);
        }

        private AdvancePreparationInfo PrepareAdvance(bool metronomic)
        {
            // Start metronome (if not imposed externally) on first Advance and consider first Advance as a metronomic event.
            if (_currentInterval is null && _metronome is not null)
            {
                metronomic = true;
                _metronome.Change(MaxIntervalTimeSpan, MaxIntervalTimeSpan);
            }

            var previousInterval = _currentInterval;
            var interval = NewDisconnectedInterval();
            TimeSpan? detectedGap = null;
            if (previousInterval is not null)
            {
                // Ignore potential *internal* metronome gap due to tiny clock jitter
                if (!metronomic || _metronome is null)
                {
                    var gap = interval.DateTimeOffset - (previousInterval.DateTimeOffset + MaxIntervalTimeSpan);
                    if (gap > TimeSpan.Zero)
                    {
                        detectedGap = gap;
                    }
                }
            }

            var e = new NewIntervalEventArgs(interval.DateTimeOffset, metronomic, detectedGap);

            return new AdvancePreparationInfo(interval, e);
        }

        private Interval CommitAdvance(AdvancePreparationInfo preparation)
        {
            _currentInterval = preparation.Interval.Seal(); ;
            var e = preparation.EventArgs;
            OnAdvanced(e);

            if (e.IsMetronomic)
            {
                OnMetronomeTicked(e);
            }

            return preparation.Interval;
        }

        private void Metronome_TimerCallback(object? _) => Context_MetronomeTicked(null, EventArgs.Empty);

        private void Context_MetronomeTicked(object? _, EventArgs __) => Advance(metronomic: true);

        private void Context_ClockAdjusted(object? _, EventArgs __) => Advance(metronomic: false);
    }
}