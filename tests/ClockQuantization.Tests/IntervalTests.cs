using ClockQuantization.Tests.Assets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ClockQuantization.Tests
{
    public class IntervalTests
    {
        [Fact]
        public void Interval_NewClockOffsetSerialPosition_ByDefinitionCannotBeExact()
        {
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            // Execute
            var position = interval.NewClockOffsetSerialPosition();

            // A position acquired after the creation of an interval *by definition* can never be exact

            // Test
            Assert.True(position.HasValue);
            Assert.False(position.IsExact);
        }

        [Fact]
        public void Interval_EnsureInitializedClockOffsetSerialPosition_ByDefinitionCannotBeExact()
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
            Interval.EnsureInitializedClockOffsetSerialPosition(interval, ref position);

            // A position acquired after the creation of an interval *by definition* can never be exact

            // Test
            Assert.True(position.HasValue);
            Assert.False(position.IsExact);
        }

        [Fact]
        public void Interval_ConsecutivelyAcquiredClockOffsetSerialPositionsAreIssuedNonStrictlyMonotonically()
        {
            const int positionCount = 100;
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            // Execute
            var sequence = new List<uint>(positionCount);
            for (var i = 0; i < positionCount; i++)
            {
                var position = default(LazyClockOffsetSerialPosition);
                Interval.EnsureInitializedClockOffsetSerialPosition(interval, ref position);
                sequence.Add(position.SerialPosition);

                Assert.Equal(interval.ClockOffset, position.ClockOffset);
            }

            // Test
            for (var i = 1; i < positionCount; i++)
            {
                Assert.True(sequence[i] >= sequence[i - 1]);
            }
        }


        [Fact]
        public void Interval_ConcurrentlyAcquiredClockOffsetSerialPositionsAreIssuedNonStrictlyMonotonically()
        {
            const int positionPerPartitionCount = 16 * 1024;
            const int partitionCount = 16;
            var metronomeOptions = MetronomeOptions.Manual;
            var now = DateTimeOffset.UtcNow;
            var context = new SystemClockTemporalContext(now, metronomeOptions);

            // Pre-requisites check
            Assert.False(context.HasExternalClock);

            var quantizer = new ClockQuantizer(context, metronomeOptions.MaxIntervalTimeSpan);
            var interval = quantizer.Advance();

            // Execute
            const int sampleCount = partitionCount * positionPerPartitionCount;
            var stringOfPerRangeSequences = new uint[sampleCount];
            List<Tuple<int, int>> ranges = new List<Tuple<int, int>>();

            int failedPerRangeSequenceCount = 0;

            Parallel.ForEach(Partitioner.Create(0, sampleCount, positionPerPartitionCount),
                (range) =>
                {
                    lock (ranges)
                    {
                        ranges.Add(range);
                    }

                    // Execute
                    for (var i = range.Item1; i < range.Item2; i++)
                    {
                        var position = default(LazyClockOffsetSerialPosition);
                        Interval.EnsureInitializedClockOffsetSerialPosition(interval, ref position);
                        stringOfPerRangeSequences[i] = position.SerialPosition;
                    }

                    // Test: monotonically increasing within this partition
                    for (var i = range.Item1 + 1; i < range.Item2; i++)
                    {
                        if (stringOfPerRangeSequences[i] < stringOfPerRangeSequences[i - 1])
                        {
                            Interlocked.Increment(ref failedPerRangeSequenceCount);
                            break;
                        }
                    }
                });

            // Verify that clock quantizer didn't advance while weren't looking...
            Assert.Same(interval, quantizer.CurrentInterval);

            // Verify that partitions did not overlap and were strictly consecutive.
            ranges.Sort((t1, t2) => t1.Item1.CompareTo(t2.Item1));
            var previousRange = default(Tuple<int,int>);
            foreach (var range in ranges)
            {
                if (previousRange != null)
                {
                    Assert.True(previousRange.Item2 == range.Item1);
                }
                previousRange = range;
            }

            Assert.Equal(0, failedPerRangeSequenceCount);

            // Some tests across partitions (concurrent acquisition of serials)
            foreach (var range in ranges)
            {
                // Check lowest number in each partition
                Assert.True(stringOfPerRangeSequences[range.Item1] > 1);    // First serial issued after creating sealed interval == 2

                // Check highest number in each partition
                Assert.True(stringOfPerRangeSequences[range.Item2 - 1] <= sampleCount + 1);
            }
        }
    }
}
