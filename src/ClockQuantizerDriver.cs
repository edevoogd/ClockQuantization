using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace ClockQuantization
{
    // Isolate some of the metronome madness from the core ClockQuantizer implementation
    internal class ClockQuantizerDriver : ClockQuantization.ISystemClock, ISystemClockTemporalContext, IAsyncDisposable, IDisposable
    {
        private readonly ClockQuantization.ISystemClock _clock;
        private readonly TimeSpan _metronomeIntervalTimeSpan;
        private System.Threading.Timer? _metronome;


        public ClockQuantizerDriver(ClockQuantization.ISystemClock clock, TimeSpan metronomeIntervalTimeSpan)
        {
            _clock = clock;
            _metronomeIntervalTimeSpan = metronomeIntervalTimeSpan;

            var haveExternalMetronome = false;
            if (clock is ISystemClockTemporalContext context)
            {
                context.ClockAdjusted += Context_ClockAdjusted;
                if (haveExternalMetronome = context.ProvidesMetronome)
                {
                    // Allow external "pulse" on metronome ticks
                    context.MetronomeTicked += Context_MetronomeTicked;
                }
            }

            if (!haveExternalMetronome)
            {
                // Create a suspended timer. Timer will be started at first call to Advance().
                _metronome = new Timer(Metronome_TimerCallback, null, Timeout.InfiniteTimeSpan, metronomeIntervalTimeSpan);
            }
        }

        public bool TryEnsureMetronomeRunning(out bool starting)
        {
            starting = false;
            if (HasInternalMetronome)
            {
                starting = true;
                _metronome!.Change(_metronomeIntervalTimeSpan, _metronomeIntervalTimeSpan);
            }

            return true;
        }

        public bool HasInternalMetronome { get => _metronome is not null; }

        protected virtual void OnClockAdjusted(EventArgs e) => ClockAdjusted?.Invoke(this, e);
        protected virtual void OnMetronomeTicked(EventArgs e) => MetronomeTicked?.Invoke(this, e);

        private void Context_ClockAdjusted(object? _, EventArgs __) => OnClockAdjusted(EventArgs.Empty);

        private void Metronome_TimerCallback(object? _) => Context_MetronomeTicked(null, EventArgs.Empty);

        private void Context_MetronomeTicked(object? _, EventArgs __) => OnMetronomeTicked(EventArgs.Empty);


        #region ISystemClock

        /// <inheritdoc/>
        public DateTimeOffset UtcNow { get => _clock.UtcNow; }
        /// <inheritdoc/>
        public long UtcNowClockOffset { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _clock.UtcNowClockOffset; }
        /// <inheritdoc/>
        public long ClockOffsetUnitsPerMillisecond { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _clock.ClockOffsetUnitsPerMillisecond; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTimeOffset ClockOffsetToUtcDateTimeOffset(long offset) => _clock.ClockOffsetToUtcDateTimeOffset(offset);
        /// <inheritdoc/>
        public long DateTimeOffsetToClockOffset(DateTimeOffset offset) => _clock.DateTimeOffsetToClockOffset(offset);

        #endregion

        #region ISystemClockTemporalContext

        /// <inheritdoc/>
        public bool ProvidesMetronome { get; } = true;
        /// <inheritdoc/>
        public event EventHandler? ClockAdjusted;
        /// <inheritdoc/>
        public event EventHandler? MetronomeTicked;

        #endregion

        #region IAsyncDisposable/IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();

            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _metronome?.Dispose();
            }

            _metronome = null;
        }

        /// <inheritdoc/>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_metronome is null)
            {
                goto done;
            }

#if NETSTANDARD2_1 || NETCOREAPP3_0 || NETCOREAPP3_1 || NET5_0 || NET5_0_OR_GREATER
            if (_metronome is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                goto finish;
            }
#else
            await default(ValueTask).ConfigureAwait(false);
#endif
            _metronome!.Dispose();

#if NETSTANDARD2_1 || NETCOREAPP3_0 || NETCOREAPP3_1 || NET5_0 || NET5_0_OR_GREATER
finish:
#endif
            _metronome = null;
done:
            ;
        }

        #endregion
    }
}
