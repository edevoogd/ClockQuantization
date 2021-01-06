using ClockQuantization.Tests.Assets;
using System;
using System.Collections.Generic;
using Xunit;

namespace ClockQuantization.Tests
{
    public class ClockQuantizerTests
    {
        [Fact]
        public void ClockQuantizer_WithTestClock_AddWithGap_RaisesAdvanceEventWithDetectedGapTimeSpan()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var now = DateTimeOffset.UtcNow;
            var offset = TimeSpan.FromMinutes(1);
            var context = new SystemClockTemporalContext(now, metronomeOptions);
            var advanceEventRaised = false;

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            quantizer.Advance();    // Advance once to ensure that we have a CurrentInterval
            Assert.NotNull(quantizer.CurrentInterval);
            Assert.Equal(now, quantizer.CurrentInterval!.DateTimeOffset);

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            context.Add(offset);

            // Test
            Assert.True(advanceEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                advanceEventRaised = true;

                // Test
                Assert.Equal(now + offset, e.DateTimeOffset);
                Assert.False(e.IsMetronomic);

                Assert.True(e.GapToPriorIntervalExpectedEnd.HasValue);
                Assert.Equal(offset - metronomeOptions.MaxIntervalTimeSpan, e.GapToPriorIntervalExpectedEnd!);
            }
        }

        [Fact]
        public void ClockQuantizer_WithTestClock_AddWithoutGap_RaisesAdvanceEventWithoutDetectedGapTimeSpan()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = true,
                MaxIntervalTimeSpan = TimeSpan.FromMinutes(5),
            };
            var now = DateTimeOffset.UtcNow;
            var offset = TimeSpan.FromMinutes(5);
            var context = new SystemClockTemporalContext(now, metronomeOptions);
            var advanceEventRaised = false;

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            quantizer.Advance();    // Advance once to ensure that we have a CurrentInterval
            Assert.NotNull(quantizer.CurrentInterval);
            Assert.Equal(now, quantizer.CurrentInterval!.DateTimeOffset);

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            context.Add(offset);

            // Test
            Assert.True(advanceEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                advanceEventRaised = true;

                // Test
                Assert.Equal(now + offset, e.DateTimeOffset);
                Assert.False(e.IsMetronomic);

                Assert.False(e.GapToPriorIntervalExpectedEnd.HasValue);
            }
        }

        [Fact]
        public void ClockQuantizer_WithTestClock_AdjustClockWithGap_RaisesAdvanceEventWithDetectedGapTimeSpan()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var now = DateTimeOffset.UtcNow;
            var adjusted = now + TimeSpan.FromMinutes(1);
            var context = new SystemClockTemporalContext(now, metronomeOptions);
            var advanceEventRaised = false;

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            quantizer.Advance();    // Advance once to ensure that we have a CurrentInterval
            Assert.NotNull(quantizer.CurrentInterval);
            Assert.Equal(now, quantizer.CurrentInterval!.DateTimeOffset);

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            context.AdjustClock(adjusted);

            // Test
            Assert.True(advanceEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                advanceEventRaised = true;

                // Test
                Assert.Equal(adjusted, e.DateTimeOffset);
                Assert.False(e.IsMetronomic);

                Assert.True(e.GapToPriorIntervalExpectedEnd.HasValue);
                Assert.Equal(adjusted - now - metronomeOptions.MaxIntervalTimeSpan, e.GapToPriorIntervalExpectedEnd!);
            }
        }

        [Fact]
        public void ClockQuantizer_WithTestClock_AdjustClockWithoutGap_RaisesAdvanceEventWithoutDetectedGapTimeSpan()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = true,
                MaxIntervalTimeSpan = TimeSpan.FromMinutes(5),
            };
            var now = DateTimeOffset.UtcNow;
            var adjusted = now + TimeSpan.FromMinutes(5);
            var context = new SystemClockTemporalContext(now, metronomeOptions);
            var advanceEventRaised = false;

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            quantizer.Advance();    // Advance once to ensure that we have a CurrentInterval
            Assert.NotNull(quantizer.CurrentInterval);
            Assert.Equal(now, quantizer.CurrentInterval!.DateTimeOffset);

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            context.AdjustClock(adjusted);

            // Test
            Assert.True(advanceEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                advanceEventRaised = true;

                // Test
                Assert.Equal(adjusted, e.DateTimeOffset);
                Assert.False(e.IsMetronomic);

                Assert.False(e.GapToPriorIntervalExpectedEnd.HasValue);
            }
        }

        [Fact]
        public void ClockQuantizer_WithExternalManualMetronome_FireMetronomeTicked_RaisesEventsInOrderWithExpectedDateTimeOffset()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = context.UtcNow;

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            quantizer.MetronomeTicked += Quantizer_MetronomeTicked;
            quantizer.Advanced += Quantizer_Advanced;

            DateTimeOffset advanceEventRaisedAt = default;
            DateTimeOffset metronomeEventRaisedAt = default;
            DateTimeOffset firedAt = DateTimeOffset.UtcNow;
            context.FireMetronomeTicked();

            // Assert that expected events were raised ...
            Assert.True(advanceEventRaisedAt != default);
            Assert.True(metronomeEventRaisedAt != default);

            // ... in the correct order ...
            Assert.True(advanceEventRaisedAt < metronomeEventRaisedAt);

            // ... and after the metronome tick was fired
            Assert.True(advanceEventRaisedAt > firedAt);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                advanceEventRaisedAt = DateTimeOffset.UtcNow;

                Assert.Equal(e.DateTimeOffset, now);
                Assert.True(e.IsMetronomic);
            }

            void Quantizer_MetronomeTicked(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                metronomeEventRaisedAt = DateTimeOffset.UtcNow;

                Assert.Equal(e.DateTimeOffset, now);
                Assert.True(e.IsMetronomic);
            }
        }

        [Fact]
        public void ClockQuantizer_WithExternalManualMetronome_FireMetronomeTickedWithFutureDateTimeOffset_RaisesEventsInOrderWithExpectedDateTimeOffset()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = DateTimeOffset.UtcNow;    // note, not the clock's UtcNow!

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            quantizer.Advanced += Quantizer_Advanced;
            quantizer.MetronomeTicked += Quantizer_MetronomeTicked;

            DateTimeOffset advanceEventRaisedAt = default;
            DateTimeOffset metronomeEventRaisedAt = default;
            DateTimeOffset firedAt = DateTimeOffset.UtcNow;
            context.FireMetronomeTicked(now);

            // Assert that expected events were raised ...
            Assert.True(advanceEventRaisedAt != default);
            Assert.True(metronomeEventRaisedAt != default);

            // ... in the correct order ...
            Assert.True(advanceEventRaisedAt < metronomeEventRaisedAt);

            // ... and after the metronome tick was fired
            Assert.True(advanceEventRaisedAt > firedAt);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                advanceEventRaisedAt = DateTimeOffset.UtcNow;

                Assert.Equal(e.DateTimeOffset, now);
                Assert.True(e.IsMetronomic);
            }

            void Quantizer_MetronomeTicked(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                metronomeEventRaisedAt = DateTimeOffset.UtcNow;

                Assert.Equal(e.DateTimeOffset, now);
                Assert.True(e.IsMetronomic);
            }
        }

        [Fact]
        public void ClockQuantizer_WithExternalManualMetronome_FireMetronomeTickedWithFutureDateTimeOffsetAndGap_RaisesEventsInOrderWithExpectedDateTimeOffsetAndDetectedGap()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = DateTimeOffset.UtcNow;    // note, not the clock's UtcNow!
            var gap = now - (context.UtcNow + metronomeOptions.MaxIntervalTimeSpan);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);
            Assert.True(gap > TimeSpan.Zero);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            quantizer.Advance();
            Assert.NotNull(quantizer.CurrentInterval);

            quantizer.Advanced += Quantizer_Advanced;
            quantizer.MetronomeTicked += Quantizer_MetronomeTicked;

            DateTimeOffset advanceEventRaisedAt = default;
            DateTimeOffset metronomeEventRaisedAt = default;
            DateTimeOffset firedAt = DateTimeOffset.UtcNow;
            context.FireMetronomeTicked(now);

            // Assert that expected events were raised ...
            Assert.True(advanceEventRaisedAt != default);
            Assert.True(metronomeEventRaisedAt != default);

            // ... in the correct order ...
            Assert.True(advanceEventRaisedAt < metronomeEventRaisedAt);

            // ... and after the metronome tick was fired
            Assert.True(advanceEventRaisedAt > firedAt);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                advanceEventRaisedAt = DateTimeOffset.UtcNow;

                Assert.Equal(e.DateTimeOffset, now);
                Assert.True(e.IsMetronomic);

                Assert.True(e.GapToPriorIntervalExpectedEnd.HasValue);
                Assert.Equal(gap, e.GapToPriorIntervalExpectedEnd);
            }

            void Quantizer_MetronomeTicked(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                metronomeEventRaisedAt = DateTimeOffset.UtcNow;

                Assert.Equal(e.DateTimeOffset, now);
                Assert.True(e.IsMetronomic);

                Assert.True(e.GapToPriorIntervalExpectedEnd.HasValue);
                Assert.Equal(gap, e.GapToPriorIntervalExpectedEnd);
            }
        }

        [Fact]
        public void ClockQuantizer_WithInternalMetronome_RaisesPeriodicMetronomeTickedEventsWithMetronomeJitterGapsIgnored()
        {
            // Juggling interval parameters to ensure we have an as little flaky as possible P95 test within approx. 1 second
            const int intervalCount = 25;
            var maxIntervalTimeSpan = TimeSpan.FromMilliseconds(42.5);
            var tolerance = TimeSpan.FromMilliseconds(15);  // this is just about Timer precision?

            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions: null);
            var quantizer = new ClockQuantizer(context, maxIntervalTimeSpan);
            var considered = new System.Threading.Semaphore(intervalCount, intervalCount);
            var visited = new System.Threading.Semaphore(0, intervalCount);

            var list = new List<DateTimeOffset>(intervalCount);
            quantizer.MetronomeTicked += Quantizer_MetronomeTicked;

            // Kick off!
            var jitters = 0;
            var outliers = 0;
            var start = quantizer.Advance().DateTimeOffset;

            for (int i = 0; i < intervalCount; i++)
            {
                visited.WaitOne(maxIntervalTimeSpan + TimeSpan.FromMilliseconds(250));
            }

            Assert.Equal(intervalCount, list.Count);

            for (int i = 0; i < intervalCount; i++)
            {
                var offset = list[i] - start;
                if (!(offset >= i * maxIntervalTimeSpan - tolerance && offset <= i * maxIntervalTimeSpan + tolerance))
                {
                    outliers++;
                }
                if (offset > i * maxIntervalTimeSpan)
                {
                    jitters++;
                }
            }

            // Ensure that we observed at least 1 jitter and validated that the gap was not registered in the event
            Assert.True(jitters > 0);

            // Ensure 95% of measurements within tolerance
            var p = (double)outliers / (double)intervalCount;
            Assert.True(p <= 0.05, $"P95 not achieved; actual: {1.0 - p}");

            void Quantizer_MetronomeTicked(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                if (considered.WaitOne(0))
                {
                    list.Add(e.DateTimeOffset);

                    // Even jitter should not be registered on *internal* metronome
                    Assert.False(e.GapToPriorIntervalExpectedEnd.HasValue);
                    
                    visited.Release();
                }
            }
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactTimeSerialPosition_WithDefaultRef_ByDefinitionMustInitializeExactTimeSerialPosition()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            // Execute
            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: false);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactTimeSerialPosition_WithoutAdvanceAndWithExactRef_ByDefinitionRemainsUntouched()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: false);

            Assert.True(position.HasValue);
            Assert.True(position.IsExact);

            var copy = position;

            // Execute
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: false);

            // Test
            Assert.Equal(copy, position);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactTimeSerialPosition_WithAdvanceAndWithExactRef_ByDefinitionRemainsUntouchedAndDoesNotAdvanceNorRaiseAdvancedEvent()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);
            var visited = new System.Threading.ManualResetEvent(false);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: false);

            Assert.True(position.HasValue);
            Assert.True(position.IsExact);

            var copy = position;

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: true);
            Assert.Same(interval, quantizer.CurrentInterval);

            // Test
            Assert.Equal(copy, position);
            Assert.False(visited.WaitOne(TimeSpan.FromMilliseconds(250)));

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                // We should not get here...
                visited.Set();
            }
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactTimeSerialPosition_WithNonExactRef_ByDefinitionMustReInitializeExactTimeSerialPosition()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedTimeSerialPosition(ref position);

            Assert.True(position.HasValue);
            Assert.False(position.IsExact);

            var copy = position;

            // Execute
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: false);

            // Test
            Assert.NotEqual(copy, position);
            Assert.True(position.IsExact);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedTimeSerialPosition_AfterFirstAdvanceWithDefaultRef_ByDefinitionMustInitializeNonExactTimeSerialPosition()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            quantizer.Advance();

            // Execute
            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedTimeSerialPosition(ref position);

            // Test
            Assert.True(position.HasValue);
            Assert.False(position.IsExact);
            Assert.Equal(quantizer.CurrentInterval!.DateTimeOffset, position.DateTimeOffset);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedTimeSerialPosition_BeforeFirstAdvanceWithDefaultRef_ByDefinitionMustInitializeExactTimeSerialPosition()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            // Execute
            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedTimeSerialPosition(ref position);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
            Assert.Null(quantizer.CurrentInterval);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedTimeSerialPosition_WithExactRef_ByDefinitionRemainsUntouched()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: false);

            Assert.True(position.HasValue);
            Assert.True(position.IsExact);

            var copy = position;

            // Execute
            quantizer.EnsureInitializedTimeSerialPosition(ref position);

            // Test
            Assert.Equal(copy, position);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedTimeSerialPosition_WithNonExactRef_ByDefinitionRemainsUntouched()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedTimeSerialPosition(ref position);

            Assert.True(position.HasValue);
            Assert.False(position.IsExact);

            var copy = position;

            // Execute
            quantizer.EnsureInitializedTimeSerialPosition(ref position);

            // Test
            Assert.Equal(copy, position);
        }

        [Fact]
        public void ClockQuantizer_Advance_YieldsNewSealedIntervalAndRaisesEvent()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var isAdvancedEventRaised = false;

            // Pre-requisites check
            Assert.True(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval1 = quantizer.Advance();

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            var interval2 = quantizer.Advance();

            // Test
            Assert.NotSame(interval1, interval2);
            Assert.True(interval1.DateTimeOffset < interval2.DateTimeOffset);

            // Ensure interval2 was sealed (and hence a new position can by definition not be "exact")
            var position = default(LazyTimeSerialPosition);
            Interval.EnsureInitializedTimeSerialPosition(interval2, ref position);
            Assert.False(position.IsExact);

            Assert.True(isAdvancedEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                isAdvancedEventRaised = true;
            }
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactTimeSerialPosition_WithoutAdvanceAndWithDefaultRef_DoesNotAdvanceCurrentInterval()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);

            // Pre-requisites check
            Assert.True(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();
            Assert.Same(interval, quantizer.CurrentInterval);

            // Execute
            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance:false);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
            Assert.Same(interval, quantizer.CurrentInterval);
            Assert.True(quantizer.CurrentInterval!.DateTimeOffset < position.DateTimeOffset);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactTimeSerialPosition_WithAdvanceAndWithDefaultRef_AdvancesCurrentIntervalAndRaisesEvent()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var isAdvancedEventRaised = false;

            // Pre-requisites check
            Assert.True(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();
            Assert.Same(interval, quantizer.CurrentInterval);

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: true);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
            Assert.NotSame(interval, quantizer.CurrentInterval);
            Assert.True(quantizer.CurrentInterval!.DateTimeOffset == position.DateTimeOffset);

            Assert.True(isAdvancedEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                isAdvancedEventRaised = true;
            }
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactTimeSerialPosition_WithAdvanceAndWithDefaultRef_DoesNotGetBrokenByInteractionInAdvancedEvent()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var isAdvancedEventRaised = false;

            // Pre-requisites check
            Assert.True(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();
            Assert.Same(interval, quantizer.CurrentInterval);

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            var position = default(LazyTimeSerialPosition);
            quantizer.EnsureInitializedExactTimeSerialPosition(ref position, advance: true);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
            Assert.NotSame(interval, quantizer.CurrentInterval);

            // quantizer.CurrentInterval advanced once more in the event handler, so we cannot validate position.DateTimeOffset against it
            Assert.False(quantizer.CurrentInterval!.DateTimeOffset == position.DateTimeOffset);

            Assert.True(isAdvancedEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                var recurse = !isAdvancedEventRaised;
                isAdvancedEventRaised = true;

                // Let's see if we can break the advance/serial logic
                var currentInterval = quantizer.CurrentInterval;

                // Test that quantizer.CurrentInterval already advanced
                Assert.NotNull(currentInterval);
                Assert.NotSame(interval, currentInterval);

                var interferingTimeSerialPosition1 = default(LazyTimeSerialPosition);
                Interval.EnsureInitializedTimeSerialPosition(interval, ref interferingTimeSerialPosition1);
                Assert.True(interferingTimeSerialPosition1.HasValue);

                var interferingTimeSerialPosition2 = currentInterval!.NewTimeSerialPosition();
                Assert.True(interferingTimeSerialPosition2.HasValue);

                var interferingTimeSerialPosition3 = default(LazyTimeSerialPosition);
                quantizer.EnsureInitializedExactTimeSerialPosition(ref interferingTimeSerialPosition3, advance: recurse /* let's not recurse more than once... */);
                Assert.True(interferingTimeSerialPosition3.IsExact);
            }
        }
    }
}
