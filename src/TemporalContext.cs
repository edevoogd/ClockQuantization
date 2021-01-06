using System;

namespace ClockQuantization
{
    /// <summary>
    /// Abstracts the system clock to facilitate synthetic clocks (e.g. for replay or testing).
    /// </summary>
    public interface ISystemClock
    {
        /// <value>
        /// The current system time in UTC.
        /// </value>
        DateTimeOffset UtcNow { get; }
    }

    /// <summary>
    /// Represents traits of the temporal context
    /// </summary>
    public interface ISystemClockTemporalContext
    {
        /// <value>
        /// <see langword="true"/> if the temporal context provides a metronome feature - i.e., if it fires <see cref="MetronomeTicked"/> events.
        /// </value>
        bool ProvidesMetronome { get; }

        /// <summary>
        /// An event that can be raised to inform listeners that the <see cref="ISystemClock"/> was adjusted.
        /// </summary>
        /// <remarks>
        /// This will typically be used with synthetic clocks only.
        /// </remarks>
        event EventHandler? ClockAdjusted;

        /// <summary>
        /// An event that can be raised to inform listeners that a metronome "tick" occurred.
        /// </summary>
        event EventHandler? MetronomeTicked;
    }
}
