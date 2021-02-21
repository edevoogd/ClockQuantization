using System;
using System.Runtime.CompilerServices;

namespace ClockQuantization.Tests.Assets
{
    /// <summary>
    /// Implements a test clock, as well as provides a compatible <see cref="ISystemClockTemporalContext"/> to observers.
    /// </summary>
    class SystemClockTemporalContext : ISystemClock, ISystemClockTemporalContext
    {
        private class ManualClock : ISystemClock
        {
            private DateTimeOffset _now;
            /// <inheritdoc/>
            public DateTimeOffset UtcNow { get => _now; }

            /// <inheritdoc/>
            public long UtcNowClockOffset => UtcNow.UtcTicks;

            public ManualClock(DateTimeOffset now)
            {
                _now = now;
            }

            public void Add(TimeSpan timeSpan)
            {
                _now = _now + timeSpan;
            }
            public void AdjustClock(DateTimeOffset now)
            {
                _now = now;
            }

            /// <inheritdoc/>
            public DateTimeOffset ClockOffsetToUtcDateTimeOffset(long offset) => new DateTimeOffset(offset, TimeSpan.Zero);

            /// <inheritdoc/>
            public long DateTimeOffsetToClockOffset(DateTimeOffset offset) => offset.UtcTicks;

            public long ClockOffsetUnitsPerMillisecond => TimeSpan.TicksPerMillisecond;
        }

        private ManualClock? _manual;
        public bool HasExternalClock => _manual is null;
        public DateTimeOffset UtcNow => GetUtcNow();

        private System.Threading.Timer? _metronome;
        private MetronomeOptions? _metronomeOptions;

        public bool ProvidesMetronome { get; private set; }
        public bool IsMetronomeRunning { get; private set; }

        public event EventHandler? ClockAdjusted;
        public event EventHandler? MetronomeTicked;

        private Func<DateTimeOffset> GetUtcNow;

        /// <summary>
        /// Creates a test clock without metronome that is linked to the system clock
        /// </summary>
        public SystemClockTemporalContext() : this(() => DateTimeOffset.UtcNow, metronomeOptions: null) { }

        public SystemClockTemporalContext(ISystemClock clock, MetronomeOptions? metronomeOptions) : this(() => clock.UtcNow, metronomeOptions)
        {
            if (clock is ManualClock manual)
            {
                _manual = manual;
            }
        }

        /// <summary>
        /// Creates a manual test clock, with or without metronome. <see cref="UtcNow"/> will have an initial value of <paramref name="now"/>.
        /// </summary>
        /// <param name="now"></param>
        /// <param name="metronomeOptions"></param>
        public SystemClockTemporalContext(DateTimeOffset now, MetronomeOptions? metronomeOptions) : this(new ManualClock(now), metronomeOptions) { }

        /// <summary>
        /// Creates a manual test clock, with or without metronome. <see cref="UtcNow"/> will have an initial value of <c>new DateTime(2013, 6, 15, 12, 34, 56, 789)</c>.
        /// </summary>
        /// <param name="metronomeOptions"></param>
        public SystemClockTemporalContext(MetronomeOptions? metronomeOptions) : this(new DateTime(2013, 6, 15, 12, 34, 56, 789), metronomeOptions) { }

        /// <summary>
        /// Creates a test clock with or without metronome.
        /// </summary>
        /// <param name="getUtcNow">A <see cref="Func{DateTimeOffset}"/> representing the canonical clock function</param>
        /// <param name="metronomeOptions"></param>
        public SystemClockTemporalContext(Func<DateTimeOffset> getUtcNow, MetronomeOptions? metronomeOptions)
        {
            GetUtcNow = getUtcNow;
            ProvidesMetronome = (_metronomeOptions = metronomeOptions) is not null;
            IsMetronomeRunning = ProvidesMetronome && ApplyMetronomeOptions(metronomeOptions!, Metronome_Ticked, out _metronome);

#if NET5_0 || NET5_0_OR_GREATER
            var utcNow = DateTimeOffset.UtcNow;
            var milliSecondsSinceGenesis = Environment.TickCount64; // The number of milliseconds elapsed since the system started.

            UtcGenesis = utcNow - TimeSpan.FromMilliseconds(milliSecondsSinceGenesis);
#endif
        }

#if NET5_0 || NET5_0_OR_GREATER
        public readonly DateTimeOffset UtcGenesis;
#endif


        /// <inheritdoc/>
        public long UtcNowClockOffset
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0 || NET5_0_OR_GREATER
            get => HasExternalClock ? Environment.TickCount64 : _manual!.UtcNowClockOffset;
#else
            get => HasExternalClock ? UtcNow.UtcTicks : _manual!.UtcNowClockOffset;
#endif
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTimeOffset ClockOffsetToUtcDateTimeOffset(long offset)
        {
            if (!HasExternalClock)
            {
                return _manual!.ClockOffsetToUtcDateTimeOffset(offset);
            }

#if NET5_0 || NET5_0_OR_GREATER
            return UtcGenesis + TimeSpan.FromMilliseconds(offset);
#else
            return new DateTimeOffset(offset, TimeSpan.Zero);
#endif
        }

        /// <inheritdoc/>
        public long DateTimeOffsetToClockOffset(DateTimeOffset offset)
        {
            if (!HasExternalClock)
            {
                return _manual!.DateTimeOffsetToClockOffset(offset);
            }

#if NET5_0 || NET5_0_OR_GREATER
            return (long) (offset.UtcDateTime - UtcGenesis).TotalMilliseconds;
#else
            return offset.UtcTicks;
#endif
        }

        public long ClockOffsetUnitsPerMillisecond
        {
            get
            {
                if (!HasExternalClock)
                {
                    return _manual!.ClockOffsetUnitsPerMillisecond;
                }

#if NET5_0 || NET5_0_OR_GREATER
                return 1;
#else
                return TimeSpan.TicksPerMillisecond;
#endif
            }
        }

        private bool ApplyMetronomeOptions(MetronomeOptions metronomeOptions, System.Threading.TimerCallback callback, out System.Threading.Timer? metronome)
        {
            metronome = null;

            if (!metronomeOptions.IsManual)
            {
                if (metronomeOptions.MaxIntervalTimeSpan.Negate() >= metronomeOptions.MaxIntervalTimeSpan)
                {
                    throw new ArgumentOutOfRangeException(nameof(metronomeOptions.MaxIntervalTimeSpan), $"Value must be greater than TimeSpan.Zero");
                }
                var running = !metronomeOptions.StartSuspended;
                metronome = new System.Threading.Timer(callback, null, running ? TimeSpan.Zero : System.Threading.Timeout.InfiniteTimeSpan, metronomeOptions.MaxIntervalTimeSpan);

                return running;
            }

            return false;
        }



        protected void OnClockAdjusted(EventArgs e) => ClockAdjusted?.Invoke(this, e);
        protected void OnMetronomeTicked(EventArgs e) => MetronomeTicked?.Invoke(this, e);

        private void Metronome_Ticked(object? state)
        {
            // For manual clocks with built-in metronome, we must first update _now
            _manual?.Add(_metronomeOptions!.MaxIntervalTimeSpan);

            OnMetronomeTicked(EventArgs.Empty);
        }

        public void FireMetronomeTicked(DateTimeOffset now)
        {
            if (_metronome is not null || _manual is null) throw new InvalidOperationException();
            if (now < _manual.UtcNow) throw new ArgumentOutOfRangeException(nameof(now), $"Value compared to clock reference is in the past.");

            _manual.AdjustClock(now);
            OnMetronomeTicked(EventArgs.Empty);
        }

        public void FireMetronomeTicked()
        {
            if (_metronome is not null) throw new InvalidOperationException();

            OnMetronomeTicked(EventArgs.Empty);
        }

        public void AdjustClock(DateTimeOffset now)
        {
            if (_manual is null) throw new InvalidOperationException();

            _manual.AdjustClock(now);

            OnClockAdjusted(EventArgs.Empty);
        }

        public void Add(TimeSpan timeSpan)
        {
            if (_manual is null) throw new InvalidOperationException();

            AdjustClock(_manual.UtcNow + timeSpan);
        }

        public void SuspendMetronome()
        {
            if (_metronome is null || !IsMetronomeRunning) throw new InvalidOperationException();

            _metronome.Change(System.Threading.Timeout.InfiniteTimeSpan, _metronomeOptions!.MaxIntervalTimeSpan);
            IsMetronomeRunning = false;
        }

        public void ResumeMetronome()
        {
            if (_metronome is null || IsMetronomeRunning) throw new InvalidOperationException();

            IsMetronomeRunning = true;
            _metronome.Change(TimeSpan.Zero, _metronomeOptions!.MaxIntervalTimeSpan);
        }
    }
}
