using ClockQuantization.Tests.Assets;
using System;
using Xunit;

namespace ClockQuantization.Tests
{
    public class ConversionTests
    {
        [Fact]
        public void ClockQuantizer_TimeSpanToClockOffsetUnits_RoundTrips()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            var span = TimeSpan.FromMilliseconds(1);

            // Execute & test
            Assert.Equal(span, quantizer.ClockOffsetUnitsToTimeSpan(quantizer.TimeSpanToClockOffsetUnits(span)));
        }

        [Fact]
        public void ClockQuantizer_ClockOffsetUnitsToTimeSpan_RoundTrips()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            var units = 1;

            // Execute & test
            Assert.Equal(units, quantizer.TimeSpanToClockOffsetUnits(quantizer.ClockOffsetUnitsToTimeSpan(units)));
        }

        [Fact]
        public void ClockQuantizer_DateTimeOffsetToClockOffset_RoundTripsWithMillisecondPrecision()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            var now = quantizer.UtcNow;

            // Execute
            var difference = quantizer.ClockOffsetToUtcDateTimeOffset(quantizer.DateTimeOffsetToClockOffset(now)) - now;

            // Test
            Assert.True(difference > TimeSpan.FromMilliseconds(-1) && difference < TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public void ClockQuantizer_ClockOffsetToUtcDateTimeOffset_RoundTrips()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            var offset = quantizer.UtcNowClockOffset;

            // Execute & test
            Assert.Equal(offset, quantizer.DateTimeOffsetToClockOffset(quantizer.ClockOffsetToUtcDateTimeOffset(offset)));
        }
    }
}
