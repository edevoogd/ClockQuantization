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

        /// <value>
        /// An offset (in ticks) representing the current system time in UTC.
        /// </value>
        /// <remarks>
        /// Depending on implementation this may be an absolute value based on <see cref="DateTimeOffset.UtcTicks"/>, a relative value based on <see cref="Environment.TickCount64"/> etc.
        /// </remarks>
        long UtcNowClockOffset { get; }

        /// <value>Represents the number of offset units (ticks) in 1 millisecond</value>
        /// <seealso cref="UtcNowClockOffset"/>
        /// <seealso cref="ClockOffsetToUtcDateTimeOffset"/>
        /// <seealso cref="DateTimeOffsetToClockOffset"/>
        long ClockOffsetUnitsPerMillisecond { get; }

        /// <summary>
        /// Converts clock-specific <paramref name="offset"/> to a <see cref="DateTimeOffset"/> in UTC.
        /// </summary>
        /// <param name="offset">The offset to convert</param>
        /// <returns>The corresponding <see cref="DateTimeOffset"/></returns>
        DateTimeOffset ClockOffsetToUtcDateTimeOffset(long offset);

        /// <summary>
        /// Converts <paramref name="offset"/> to a clock-specific offset.
        /// </summary>
        /// <param name="offset">The offset to convert</param>
        /// <returns>The corresponding clock-specific offset</returns>
        long DateTimeOffsetToClockOffset(DateTimeOffset offset);
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
