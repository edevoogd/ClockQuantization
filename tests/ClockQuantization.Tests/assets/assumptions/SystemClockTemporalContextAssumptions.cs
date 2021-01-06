using ClockQuantization.Tests.Assets;
using System;
using Xunit;


namespace ClockQuantization.Assumptions
{
    /// <summary>
    /// This set of tests verifies a couple of basic assumptions about the <see cref="SystemClockTemporalContext"/>
    /// to ensure that tests leveraging it can rely on its expected behavior. Consider it a poor-man's
    /// way of documenting the clock driver's behaviour 😎.
    /// </summary>
    public class SystemClockTemporalContextAssumptions
    {
        [Fact]
        public void TestClock_Add_RaisesClockAdjustedEventAndAdjustsUtcNow()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = context.UtcNow;
            var timeSpan = TimeSpan.FromMinutes(1);
            var clockAdjustedEventRaised = false;

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute
            context.ClockAdjusted += Context_ClockAdjusted;
            context.Add(timeSpan);

            // Test
            Assert.True(clockAdjustedEventRaised);

            void Context_ClockAdjusted(object? sender, EventArgs e)
            {
                // Test
                Assert.Equal(now + timeSpan, context.UtcNow);
                clockAdjustedEventRaised = true;
            }
        }

        [Fact]
        public void TestClock_AdjustClock_RaisesClockAdjustedEventAndAdjustsUtcNow()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = context.UtcNow + TimeSpan.FromMinutes(1);
            var clockAdjustedEventRaised = false;

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute
            context.ClockAdjusted += Context_ClockAdjusted;
            context.AdjustClock(now);

            // Test
            Assert.True(clockAdjustedEventRaised);

            void Context_ClockAdjusted(object? sender, EventArgs e)
            {
                // Test
                Assert.Equal(now, context.UtcNow);
                clockAdjustedEventRaised = true;
            }
        }

        [Fact]
        public void TestClock_WithManualMetronome_FireMetronomeTicked_RaisesMetronomeTickedEventAndDoesNotAdjustUtcNow()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = context.UtcNow;
            var metronomeTickedEventRaised = false;

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute
            context.MetronomeTicked += Context_MetronomeTicked;
            context.FireMetronomeTicked(now);

            // Test
            Assert.True(metronomeTickedEventRaised);

            void Context_MetronomeTicked(object? sender, EventArgs e)
            {
                // Test that UtcNow was *not* adjusted
                Assert.Equal(now, context.UtcNow);
                metronomeTickedEventRaised = true;
            }
        }

        [Fact]
        public void TestClock_WithManualMetronome_FireMetronomeTickedWithFutureDateTimeOffset_RaisesMetronomeTickedEventAndAdjustsUtcNow()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = DateTimeOffset.UtcNow;    // note, not the clock's UtcNow!
            var metronomeTickedEventRaised = false;

            // Test pre-requisite checks
            Assert.False(context.HasExternalClock);
            Assert.NotEqual(now, context.UtcNow);

            // Execute
            context.MetronomeTicked += Context_MetronomeTicked;
            context.FireMetronomeTicked(now);

            // Test
            Assert.True(metronomeTickedEventRaised);

            void Context_MetronomeTicked(object? sender, EventArgs e)
            {
                // Test that UtcNow was already updated
                Assert.Equal(now, context.UtcNow);
                metronomeTickedEventRaised = true;
            }
        }

        [Fact]
        public void TestClock_WithManualMetronome_FireMetronomeTickedWithFutureDateTimeOffset_DoesNotRaiseClockAdjustedEvent()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = DateTimeOffset.UtcNow;    // note, not the clock's UtcNow!
            var visited = new System.Threading.ManualResetEvent(false);

            // Test pre-requisite checks
            Assert.False(context.HasExternalClock);
            Assert.NotEqual(now, context.UtcNow);

            // Execute
            context.ClockAdjusted += Context_ClockAdjusted;
            context.FireMetronomeTicked(now);

            // Test that the test clock will update UtcNow, but not raise the ClockAdjusted event
            Assert.False(visited.WaitOne(metronomeOptions.MaxIntervalTimeSpan + TimeSpan.FromMilliseconds(250)));
            Assert.Equal(now, context.UtcNow);

            void Context_ClockAdjusted(object? sender, EventArgs e)
            {
                // We should not get here...
                visited.Set();
            }
        }

        [Fact]
        public void TestClock_WithManualMetronome_FireMetronomeTickedWithPastDateTimeOffset_ThrowsArgumentOutOfRangeException()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = context.UtcNow;

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute & test
            var offset = now + TimeSpan.FromSeconds(-5);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.FireMetronomeTicked(offset));

            // Test internal assumption that the test clock will *not* update UtcNow
            Assert.Equal(now, context.UtcNow);
        }

        [Fact]
        public void ExternalClock_Add_ThrowsInvalidOperationException()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var timeSpan = TimeSpan.FromMinutes(1);

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute & tests
            Assert.Throws<InvalidOperationException>(() => context.Add(timeSpan));
        }

        [Fact]
        public void ExternalClock_AdjustClock_ThrowsInvalidOperationException()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var now = context.UtcNow + TimeSpan.FromMinutes(1);

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute & tests
            Assert.Throws<InvalidOperationException>(() => context.AdjustClock(now));
        }

        [Fact]
        public void ExternalClock_WithManualMetronome_FireMetronomeTickedWithFutureDateTimeOffset_ThrowsInvalidOperationException()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var now = context.UtcNow;

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute & test
            var offset = now + TimeSpan.FromSeconds(5);
            Assert.Throws<InvalidOperationException>(() => context.FireMetronomeTicked(offset));
        }

        [Fact]
        public void ExternalClock_WithManualMetronome_FireMetronomeTickedWithPastDateTimeOffset_ThrowsInvalidOperationException()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var now = context.UtcNow;

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute & test
            var offset = now + TimeSpan.FromSeconds(-5);
            Assert.Throws<InvalidOperationException>(() => context.FireMetronomeTicked(offset));
        }

        [Fact]
        public void ExternalClock_WithManualMetronome_FireMetronomeTicked_RaisesMetronomeTickedEvent()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var metronomeTickedEventRaised = false;
            var start = context.UtcNow;

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute
            context.MetronomeTicked += Context_MetronomeTicked;
            context.FireMetronomeTicked();

            // Test
            Assert.True(metronomeTickedEventRaised);

            void Context_MetronomeTicked(object? sender, EventArgs e)
            {
                metronomeTickedEventRaised = true;

                // Validate that clock has advanced
                Assert.True(context.UtcNow > start);
            }
        }

        [Fact]
        public void ExternalClock_WithRunningTemporalContextMetronome_RaisesMetronomeTickedEvent()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var start = context.UtcNow;
            var visited = new System.Threading.ManualResetEvent(false);

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute
            context.MetronomeTicked += Context_MetronomeTicked;
            context.ResumeMetronome();

            // Test
            Assert.True(context.IsMetronomeRunning);
            Assert.True(visited.WaitOne(metronomeOptions.MaxIntervalTimeSpan + TimeSpan.FromMilliseconds(50)));

            void Context_MetronomeTicked(object? sender, EventArgs e)
            {
                context.SuspendMetronome();

                // Validate that clock has advanced
                Assert.True(context.UtcNow > start);

                visited.Set();
            }
        }

        [Fact]
        public void ExternalClock_WithSuspendedTemporalContextMetronome_DoesNotRaiseMetronomeTickedEvent()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);
            var start = context.UtcNow;
            var visited = new System.Threading.ManualResetEvent(false);

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute
            context.MetronomeTicked += Context_MetronomeTicked;

            // Test
            Assert.False(context.IsMetronomeRunning);
            Assert.False(visited.WaitOne(metronomeOptions.MaxIntervalTimeSpan + TimeSpan.FromMilliseconds(250)));

            void Context_MetronomeTicked(object? sender, EventArgs e)
            {
                // We should not get here...
                visited.Set();
            }
        }

        [Fact]
        public void ExternalClock_WithSuspendedTemporalContextMetronome_SuspendMetronome_ThrowsInvalidOperationException()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute & test
            Assert.False(context.IsMetronomeRunning);
            Assert.Throws<InvalidOperationException>(() => context.SuspendMetronome());
        }

        [Fact]
        public void ExternalClock_WithRunningTemporalContextMetronome_ResumeMetronome_ThrowsInvalidOperationException()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = false,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(() => DateTimeOffset.UtcNow, metronomeOptions);

            // Test pre-requisite check
            Assert.True(context.HasExternalClock);

            // Execute & test
            Assert.True(context.IsMetronomeRunning);
            Assert.Throws<InvalidOperationException>(() => context.ResumeMetronome());
        }

        [Fact]
        public void TestClock_WithRunningTemporalContextMetronome_AdjustsUtcNowAndRaisesMetronomeTickedEvent()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(metronomeOptions);
            var start = context.UtcNow;
            var visited = new System.Threading.ManualResetEvent(false);

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute
            context.MetronomeTicked += Context_MetronomeTicked;
            context.ResumeMetronome();

            // Test
            Assert.True(context.IsMetronomeRunning);
            Assert.True(visited.WaitOne(metronomeOptions.MaxIntervalTimeSpan + TimeSpan.FromMilliseconds(50)));

            void Context_MetronomeTicked(object? sender, EventArgs e)
            {
                context.SuspendMetronome();

                // Validate that clock has advanced
                Assert.True(context.UtcNow > start);

                visited.Set();
            }
        }

        [Fact]
        public void TestClock_WithSuspendedTemporalContextMetronome_DoesNotRaiseMetronomeTickedEvent()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(metronomeOptions);
            var start = context.UtcNow;
            var visited = new System.Threading.ManualResetEvent(false);

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute
            context.MetronomeTicked += Context_MetronomeTicked;

            // Test
            Assert.False(context.IsMetronomeRunning);
            Assert.False(visited.WaitOne(metronomeOptions.MaxIntervalTimeSpan + TimeSpan.FromMilliseconds(250)));

            void Context_MetronomeTicked(object? sender, EventArgs e)
            {
                // We should not get here...
                visited.Set();
            }
        }

        [Fact]
        public void TestClock_WithSuspendedTemporalContextMetronome_SuspendMetronome_ThrowsInvalidOperationException()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(metronomeOptions);

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute & test
            Assert.False(context.IsMetronomeRunning);
            Assert.Throws<InvalidOperationException>(() => context.SuspendMetronome());
        }

        [Fact]
        public void TestClock_WithRunningTemporalContextMetronome_ResumeMetronome_ThrowsInvalidOperationException()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = false,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(metronomeOptions);

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute & test
            Assert.True(context.IsMetronomeRunning);
            Assert.Throws<InvalidOperationException>(() => context.ResumeMetronome());
        }

        [Fact]
        public void TestClock_WithTemporalContextMetronome_FireMetronomeTicked_ThrowsInvalidOperationException()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = context.UtcNow;

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute & test
            Assert.Throws<InvalidOperationException>(() => context.FireMetronomeTicked());
        }

        [Fact]
        public void TestClock_WithTemporalContextMetronome_FireMetronomeTickedWithFutureDateTimeOffset_ThrowsInvalidOperationException()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = context.UtcNow;

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute & test
            var offset = now + TimeSpan.FromSeconds(5);
            Assert.Throws<InvalidOperationException>(() => context.FireMetronomeTicked(offset));
        }

        [Fact]
        public void TestClock_WithTemporalContextMetronome_FireMetronomeTickedWithPastDateTimeOffset_ThrowsInvalidOperationException()
        {
            var metronomeOptions = new MetronomeOptions
            {
                IsManual = false,
                StartSuspended = true,
                MaxIntervalTimeSpan = TimeSpan.FromMilliseconds(5),
            };
            var context = new SystemClockTemporalContext(metronomeOptions);
            var now = context.UtcNow;

            // Test pre-requisite check
            Assert.False(context.HasExternalClock);

            // Execute & test
            var offset = now + TimeSpan.FromSeconds(-5);
            Assert.Throws<InvalidOperationException>(() => context.FireMetronomeTicked(offset));
        }
    }
}
