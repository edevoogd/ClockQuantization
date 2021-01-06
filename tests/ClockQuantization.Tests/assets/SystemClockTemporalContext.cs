using System;

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
            public DateTimeOffset UtcNow { get => _now; }

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
