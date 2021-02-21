# Introduction

Clock Quantization is proposed as a method to decrease the frequency of determining the exact time, while still being
able to make time-dependent decisions.

Consider an <i>interval</i> defined as a time span for which we register a "start" `ClockOffset` (not necessarily `DateTimeOffset`) and keep an internal
<i>serial position</i>. The latter is incremented every time that we issue a new so-called <i>time-serial position</i>. Every time
that such <i>time-serial position</i> is created off said <i>interval</i>, it will assume two properties:
1. A `ClockOffset` which is the copy of the interval's `ClockOffset`
2. A `SerialPosition` which is a copy of the interval's internal serial position at the time of issuance.

An internal "metronome" ensures that a new interval is periodically started after `MaxIntervalTimeSpan` has passed.

The result:
* The continuous clock is quantized into regular intervals with known maximum length, allowing system calls (e.g. `DateTimeOffset.UtcNow`)
  to be required less frequent and amortized across multiple operations.
* The status of "events" that occur <u>outside</u> of the interval can be reasoned about with absolute certainty. E.g., taking `interval` as a reference frame:
  * A cache item that expires before `ClockOffsetToUtcDateTimeOffset(interval.ClockOffset)` will definitely have expired
  * A cache item that expires after `ClockOffsetToUtcDateTimeOffset(interval.ClockOffset) + MaxIntervalTimeSpan` will definitely expire in the future (i.e. it has
    not expired yet)
* The status of "events" that occur <u>inside</u> the interval is in doubt, but policies could define how to deal with that. E.g.,
  with the same example of cache item expiration, one of the following policies could be applied:
  * Precise &rarr; fall back to determining a precise `DateTimeOffset`, incurring a call onto the clock and applying the "normal" logic.
  * Optimistic &rarr; just consider the item not expired yet
  * Pessimistic &rarr; just consider the item as already expired
* The amount of uncertainty (i.e. in-doubt "events") becomes a function of `MaxIntervalTimeSpan`. The smaller `MaxIntervalTimeSpan`,
  the smaller the amount of uncertainty (and number of times that we still need to regress to calling onto the clock to determine
  the exact time).
* The <i>time-serial positions</i> within an interval can be ordered by their `SerialPosition` property, still allowing for e.g.
  strict LRU ordering. Alternatively, it also makes it possible to apply LRU ordering to a <i>cluster</i> of cache entries (all having
  equal `LastAccessed.ClockOffset` - now also a <i>time-serial position</i>).

# Context
This repo stems from additional research after I posted a [comment](https://github.com/dotnet/runtime/pull/45842#issuecomment-742100677) in PR [dotnet/runtime#45842](https://github.com/dotnet/runtime/pull/45842). In that comment, I did not take into
account the fact that `CacheEntry.LastAccessed` is often updated with `DateTimeOffset.UtcNow` in order to support:
* Sliding expiration
* LRU-based cache eviction during cache compaction

After some local experimentation with `Lazy<T>` (as suggested in my initial comment) and
[`LazyInitializer.EnsureInitialized()`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.lazyinitializer.ensureinitialized?view=net-5.0#System_Threading_LazyInitializer_EnsureInitialized__1___0__System_Boolean__System_Object__System_Func___0__),
I did get some promising results, but realized that it resulted in some quite convoluted code. Hence, I decided to first create and
share an abstraction that reflects the idea behind a potential direction for further optimization.

##### 2021-01-27
Triggered by [@filipnavara](https://github.com/dotnet/runtime/pull/45842#issuecomment-761235581), the latest incarnation of Clock Quantization caters to a non-arbitrary clock-specific representation of <i>time-serial positions</i>, allowing reference clocks to:
* Define the unit scale of clock offset ticks (`ISystemClock.ClockOffsetUnitsPerMillisecond`)
* Take full control of back-and-forth conversion between clock-specific time representation and `System.DateTimeOffset`
  * `ISystemClock.ClockOffsetToUtcDateTimeOffset()`
  * `ISystemClock.DateTimeOffsetToClockOffset()`.

Under `#if NET5_0` we can now use `Environment.TickCount64` to implement `ISystemClock.UtcNowClockOffset`. This particular property is not available in
.NET Standard, where we can fall back to using (the less efficient) `DateTimeOffset.UtcNow.UtcTicks`.

| TargetFramework   | `UtcNowClockOffset`               | `ClockOffsetUnitsPerMillisecond`        | Underlying offset definition                        |
| ----------------- | ----------------------------------- | ------------------------------------------ | ----------------------------------------- |
| .NET 5            | `Environment.TickCount64`         | 1                                          | Number of milliseconds elapsed since the system started |
| .NET Standard 2.0 | `DateTimeOffset.UtcNow.UtcTicks` | `TimeSpan.TicksPerMillisecond` (10,000) | Number of 100-nanosecond intervals that have elapsed since 1/1/0001 12:00AM |

Obviously, custom reference clocks (e.g. for testing and/or replay) could define their own offset reference and unit scale. It is up to clock implementers to ensure that
their time representation doesn't overflow in realistic scenarios (such as would e.g. be the case with `Environment.TickCount` which wraps after 24.9 days and again every other approx. 49.8 days).

Local experimentation shows further advantage to using `Environment.TickCount64` to speed up expiration check scenarios with a "Precise" policy being imposed on cache item expiration.

# Remarks
* The `ISystemClockTemporalContext` abstraction introduced as part of this concept might be useful in other scenarios where a
  synthetic clock and/or timer must be imposed onto a subject/system (e.g. event replay, unit tests).
* The solution in this repo contains a test project. It serves two purposes:
  1. Ensure that I didn't leave too many gaps
  2. Document the idea behind `ISystemClockTemporalContext`, without actually writing documentation

# Remaining work
- [X] Implement `IDisposable` and/or `IAsyncDisposable` on `ClockQuantizer` &rarr; [#6](https://github.com/edevoogd/ClockQuantization/pull/6)
- [ ] Integrate this proposal in a [local fork](https://github.com/edevoogd/runtime) of Microsoft.Extensions.Caching.Memory as a PoC
