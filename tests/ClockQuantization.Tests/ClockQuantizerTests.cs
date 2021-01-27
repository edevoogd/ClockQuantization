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
            Assert.Equal(context.DateTimeOffsetToClockOffset(now), quantizer.CurrentInterval!.ClockOffset);

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
            Assert.Equal(context.DateTimeOffsetToClockOffset(now), quantizer.CurrentInterval!.ClockOffset);

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
            Assert.Equal(context.DateTimeOffsetToClockOffset(now), quantizer.CurrentInterval!.ClockOffset);

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
            Assert.Equal(context.DateTimeOffsetToClockOffset(now), quantizer.CurrentInterval!.ClockOffset);

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
            const double targetP = 0.95;
            const int intervalCount = 25;
            var maxIntervalTimeSpan = TimeSpan.FromMilliseconds(42.5);
            var tolerance = TimeSpan.FromMilliseconds(15);  // this is just about Timer precision?

            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions: null);
            var quantizer = new ClockQuantizer(context, maxIntervalTimeSpan);
            var considered = new System.Threading.Semaphore(intervalCount, intervalCount);
            var visited = new System.Threading.Semaphore(0, intervalCount);

            var list = new List<long>(intervalCount);
            quantizer.MetronomeTicked += Quantizer_MetronomeTicked;

            // Kick off!
            var start = quantizer.Advance().ClockOffset;

            for (int i = 0; i < intervalCount; i++)
            {
                visited.WaitOne(maxIntervalTimeSpan + TimeSpan.FromMilliseconds(50));
            }

            Assert.Equal(intervalCount, list.Count);

            // Estimate weighed average skew
            double weighedSkewSum = 0.0;
            for (int i = 0; i < intervalCount; i++)
            {
                double skew = (double)list[i] - (i * maxIntervalTimeSpan.TotalMilliseconds * context.ClockOffsetUnitsPerMillisecond) - start;
                var weighedSkewSumContribution = skew * (i + 1);
                weighedSkewSum += weighedSkewSumContribution;
            }

            var weighedAverageSkew = weighedSkewSum / (intervalCount * (intervalCount + 1) / 2.0);

            var jitters = 0;
            var outliers = 0;
            for (int i = 0; i < intervalCount; i++)
            {
                var delta = list[i] - (start + weighedAverageSkew);
                var lower = (i * maxIntervalTimeSpan - tolerance).TotalMilliseconds * context.ClockOffsetUnitsPerMillisecond;
                var upper = (i * maxIntervalTimeSpan + tolerance).TotalMilliseconds * context.ClockOffsetUnitsPerMillisecond;

                if (!(delta >= lower && (delta <= upper)))
                {
                    outliers++;
                }
                if (delta > upper)
                {
                    jitters++;
                }
            }

#if false
            // Ensure that we observed at least 1 jitter and validated that the gap was not registered in the event
            Assert.True(jitters > 0, $"#jitters: {jitters}");
#endif

            // Ensure targetP % of measurements within tolerance
            var p = (double)outliers / (double)intervalCount;
            Assert.True(p <= (1 - targetP), $"P{(int) (targetP * 100.0)} not achieved; actual: {1.0 - p}");

            void Quantizer_MetronomeTicked(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                if (considered.WaitOne(0))
                {
                    list.Add(context.DateTimeOffsetToClockOffset(e.DateTimeOffset));

                    // Jitter should not be administered on *internal* metronome events
                    Assert.False(e.GapToPriorIntervalExpectedEnd.HasValue);
                    
                    visited.Release();
                }
            }
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactClockOffsetSerialPosition_WithDefaultRef_ByDefinitionMustInitializeExactClockOffsetSerialPosition()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            // Execute
            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: false);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactClockOffsetSerialPosition_WithoutAdvanceAndWithExactRef_ByDefinitionRemainsUntouched()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: false);

            Assert.True(position.HasValue);
            Assert.True(position.IsExact);

            var copy = position;

            // Execute
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: false);

            // Test
            Assert.Equal(copy, position);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactClockOffsetSerialPosition_WithAdvanceAndWithExactRef_ByDefinitionRemainsUntouchedAndDoesNotAdvanceNorRaiseAdvancedEvent()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);
            var visited = new System.Threading.ManualResetEvent(false);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: false);

            Assert.True(position.HasValue);
            Assert.True(position.IsExact);

            var copy = position;

            quantizer.Advanced += Quantizer_Advanced;

            // Execute
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: true);
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
        public void ClockQuantizer_EnsureInitializedExactClockOffsetSerialPosition_WithNonExactRef_ByDefinitionMustReInitializeExactClockOffsetSerialPosition()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedClockOffsetSerialPosition(ref position);

            Assert.True(position.HasValue);
            Assert.False(position.IsExact);

            var copy = position;

            // Execute
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: false);

            // Test
            Assert.NotEqual(copy, position);
            Assert.True(position.IsExact);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedClockOffsetSerialPosition_AfterFirstAdvanceWithDefaultRef_ByDefinitionMustInitializeNonExactClockOffsetSerialPosition()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            quantizer.Advance();

            // Execute
            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedClockOffsetSerialPosition(ref position);

            // Test
            Assert.True(position.HasValue);
            Assert.False(position.IsExact);
            Assert.Equal(quantizer.CurrentInterval!.ClockOffset, position.ClockOffset);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedClockOffsetSerialPosition_BeforeFirstAdvanceWithDefaultRef_ByDefinitionMustInitializeExactClockOffsetSerialPosition()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);

            // Execute
            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedClockOffsetSerialPosition(ref position);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
            Assert.Null(quantizer.CurrentInterval);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedClockOffsetSerialPosition_WithExactRef_ByDefinitionRemainsUntouched()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: false);

            Assert.True(position.HasValue);
            Assert.True(position.IsExact);

            var copy = position;

            // Execute
            quantizer.EnsureInitializedClockOffsetSerialPosition(ref position);

            // Test
            Assert.Equal(copy, position);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedClockOffsetSerialPosition_WithNonExactRef_ByDefinitionRemainsUntouched()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedClockOffsetSerialPosition(ref position);

            Assert.True(position.HasValue);
            Assert.False(position.IsExact);

            var copy = position;

            // Execute
            quantizer.EnsureInitializedClockOffsetSerialPosition(ref position);

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
            Assert.True(interval1.ClockOffset <= interval2.ClockOffset);

            // Ensure interval2 was sealed (and hence a new position can by definition not be "exact")
            var position = default(LazyClockOffsetSerialPosition);
            Interval.EnsureInitializedClockOffsetSerialPosition(interval2, ref position);
            Assert.False(position.IsExact);

            Assert.True(isAdvancedEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                isAdvancedEventRaised = true;
            }
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactClockOffsetSerialPosition_WithoutAdvanceAndWithDefaultRef_DoesNotAdvanceCurrentInterval()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);

            // Pre-requisites check
            Assert.True(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();
            Assert.Same(interval, quantizer.CurrentInterval);

            // Execute
            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: false);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
            Assert.Same(interval, quantizer.CurrentInterval);
            Assert.True(quantizer.CurrentInterval!.ClockOffset <= position.ClockOffset);
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactClockOffsetSerialPosition_WithAdvanceAndWithDefaultRef_AdvancesCurrentIntervalAndRaisesEvent()
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
            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: true);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
            Assert.NotSame(interval, quantizer.CurrentInterval);
            Assert.True(quantizer.CurrentInterval!.ClockOffset == position.ClockOffset);

            Assert.True(isAdvancedEventRaised);

            void Quantizer_Advanced(object? sender, ClockQuantizer.NewIntervalEventArgs e)
            {
                isAdvancedEventRaised = true;
            }
        }

        [Fact]
        public void ClockQuantizer_EnsureInitializedExactClockOffsetSerialPosition_WithAdvanceAndWithDefaultRef_DoesNotGetBrokenByInteractionInAdvancedEvent()
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
            var position = default(LazyClockOffsetSerialPosition);
            quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref position, advance: true);

            // Test
            Assert.True(position.HasValue);
            Assert.True(position.IsExact);
            Assert.NotSame(interval, quantizer.CurrentInterval);

            // quantizer.CurrentInterval advanced once more in the event handler, so we cannot validate position.Offset against it
            Assert.True(quantizer.CurrentInterval!.ClockOffset >= position.ClockOffset);

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

                var interferingClockOffsetSerialPosition1 = default(LazyClockOffsetSerialPosition);
                Interval.EnsureInitializedClockOffsetSerialPosition(interval, ref interferingClockOffsetSerialPosition1);
                Assert.True(interferingClockOffsetSerialPosition1.HasValue);

                var interferingClockOffsetSerialPosition2 = currentInterval!.NewClockOffsetSerialPosition();
                Assert.True(interferingClockOffsetSerialPosition2.HasValue);

                var interferingClockOffsetSerialPosition3 = default(LazyClockOffsetSerialPosition);
                quantizer.EnsureInitializedExactClockOffsetSerialPosition(ref interferingClockOffsetSerialPosition3, advance: recurse /* let's not recurse more than once... */);
                Assert.True(interferingClockOffsetSerialPosition3.IsExact);
            }
        }
    }
}
