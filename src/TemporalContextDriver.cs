using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace ClockQuantization
{
    // Isolate some of the context/metronome madness from the core ClockQuantizer implementation
    internal class TemporalContextDriver : ClockQuantization.ISystemClock, ISystemClockTemporalContext, IDisposable, IAsyncDisposable
    {
        private readonly ClockQuantization.ISystemClock _clock;
        private readonly TimeSpan _metronomeIntervalTimeSpan;
        private System.Threading.Timer? _metronome;
        private EventArgs? _pendingClockAdjustedEventArgs;

        public TemporalContextDriver(ClockQuantization.ISystemClock clock, TimeSpan metronomeIntervalTimeSpan)
        {
            _clock = clock;
            _metronomeIntervalTimeSpan = metronomeIntervalTimeSpan;

            AttachExternalTemporalContext(this, clock, out var haveExternalMetronome);

            if (haveExternalMetronome)
            {
                IsQuiescent = false;
            }

            static void AttachExternalTemporalContext(TemporalContextDriver driver, ClockQuantization.ISystemClock clock, out bool haveExternalMetronome)
            {
                haveExternalMetronome = false;
                if (clock is ISystemClockTemporalContext context)
                {
                    context.ClockAdjusted += driver.Context_ClockAdjusted;
                    if (haveExternalMetronome = context.ProvidesMetronome)
                    {
                        // Allow external "pulse" on metronome ticks
                        context.MetronomeTicked += driver.Context_MetronomeTicked;
                    }
                }
            }
        }

        private int _isExternalTemporalContextDetached;
        private void DetachExternalTemporalContext()
        {
            if (Interlocked.CompareExchange(ref _isExternalTemporalContextDetached, 1, 0) == 0)
            {
                if (_clock is ISystemClockTemporalContext context)
                {
                    context.ClockAdjusted -= Context_ClockAdjusted;
                    if (context.ProvidesMetronome)
                    {
                        context.MetronomeTicked -= Context_MetronomeTicked;
                    }
                }
            }
        }

        private void EnsureInternalMetronome(out bool starting)
        {
            starting = false;

            if (_metronome is null && HasInternalMetronome)
            {
                // Create a paused metronome timer
                var metronome = new Timer(Metronome_TimerCallback, null, Timeout.InfiniteTimeSpan, _metronomeIntervalTimeSpan);
                if (Interlocked.CompareExchange(ref _metronome, metronome, null) is null)
                {
                    // Resume the newly created metronome timer
                    _metronome!.Change(_metronomeIntervalTimeSpan, _metronomeIntervalTimeSpan);
                    starting = true;
                }
                else
                {
                    // Wooops... another thread outpaced us...
                    metronome.Dispose();
                }
            }
        }

        private void DisposeInternalMetronome()
        {
            if (Interlocked.Exchange(ref _metronome, null) is Timer metronome)
            {
                metronome.Dispose();
            }
        }

        private async ValueTask DisposeInternalMetronomeAsync()
        {
#if !(NETSTANDARD2_1 || NETCOREAPP3_0 || NETCOREAPP3_1 || NET5_0 || NET5_0_OR_GREATER)
            await default(ValueTask).ConfigureAwait(continueOnCapturedContext: false);
#endif

            if (Interlocked.Exchange(ref _metronome, null) is Timer metronome)
            {
#if NETSTANDARD2_1 || NETCOREAPP3_0 || NETCOREAPP3_1 || NET5_0 || NET5_0_OR_GREATER
                if (metronome is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
                    return;
                }
#endif
                metronome.Dispose();
            }
        }

        public bool TryEnsureMetronomeRunning(out bool starting)
        {
            starting = false;
            if (HasInternalMetronome)
            {
                Unquiesce(out starting);
            }

            return true;
        }

        protected bool IsQuiescent { get; private set; } = true;

        public void Quiesce()
        {
            IsQuiescent = true;

            // Dispose of internal metronome, if applicable.
            DisposeInternalMetronome();
        }

        public void Unquiesce() => Unquiesce(out var _);

        private void Unquiesce(out bool starting)
        {
            starting = false;
            EventArgs? pendingClockAdjustedEventArgs = Interlocked.Exchange(ref _pendingClockAdjustedEventArgs, null);

            if (pendingClockAdjustedEventArgs is not null)
            {
                // Make sure that we briefly postpone any metronome event that may occur during the process of unquiescing
                lock (this)
                {
                    IsQuiescent = false;    // Set to false already to make sure that the ClockAdjusted event fires
                    OnClockAdjusted(pendingClockAdjustedEventArgs);
                }
            }

            IsQuiescent = false;
            // TODO: Restore internal metronome, re-acquire unmanaged timer resources
            if (HasInternalMetronome)
            {
                // Create a ** suspended ?? ** timer. Timer will be started at first call to Advance().
                EnsureInternalMetronome(out starting);
            }
        }

        public bool HasInternalMetronome
        {
            get => _clock is not ISystemClockTemporalContext context || !context.ProvidesMetronome;
        }

        protected virtual void OnClockAdjusted(EventArgs e)
        {
            if (!IsQuiescent)
            {
                ClockAdjusted?.Invoke(this, e);
            }
            else
            {
                // Retain the latest ClockAdjusted event until we unquiesce
                Interlocked.Exchange(ref _pendingClockAdjustedEventArgs, e);
            }
        }

        protected virtual void OnMetronomeTicked(EventArgs e)
        {
            if (!IsQuiescent)
            {
                // Make sure that we briefly postpone a metronome event that occurs during the process of unquiescing
                lock (this)
                {
                    MetronomeTicked?.Invoke(this, e);
                }
            }
        }

        private void Context_ClockAdjusted(object? _, EventArgs __) => OnClockAdjusted(EventArgs.Empty);

        private void Context_MetronomeTicked(object? _, EventArgs __) => OnMetronomeTicked(EventArgs.Empty);

        private void Metronome_TimerCallback(object? _) => OnMetronomeTicked(EventArgs.Empty);


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
            // This method is re-entrant
            DetachExternalTemporalContext();

            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            // This method is re-entrant
            DetachExternalTemporalContext();

            await DisposeAsyncCore();

            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // This method is re-entrant
                DisposeInternalMetronome();
            }
        }

        /// <inheritdoc/>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            // This method is re-entrant
            await DisposeInternalMetronomeAsync();
        }

        #endregion
    }
}
