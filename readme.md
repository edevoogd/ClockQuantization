# Introduction

Clock Quantization is proposed as a method to decrease the frequency of determining the exact time, while still being
able to make time-dependent decisions.

Consider an <i>interval</i> defined as a time span for which we register a "start" `DateTimeOffset` and keep an internal
<i>serial position</i>. The latter is incremented every time that we issue a new so-called <i>time-serial position</i>. Every time
that such <i>time-serial position</i> is created off said <i>interval</i>, it will assume two properties:
1. A `DateTimeOffset` which is the copy of the interval's `DateTimeOffset`
2. A `SerialPosition` which is a copy of the interval's internal serial position at the time of issuance.

An internal "metronome" ensures that a new interval is periodically started after `MaxIntervalTimeSpan` has passed.

The result:
* The continuous clock is quantized into regular intervals with known maximum length, allowing system calls (e.g. `DateTimeOffset.UtcNow`)
  to be required less frequent and amortized across multiple operations.
* The status of "events" that occur <u>outside</u> of the interval can be reasoned about with absolute certainty. E.g., taking `interval` as a reference frame:
  * A cache item that expires before `interval.DateTimeOffset` will definitely have expired
  * A cache item that expires after `interval.DateTimeOffset + MaxIntervalTimeSpan` will definitely expire in the future (i.e. it has
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
  equal `LastAccessed.DateTimeOffset` - now also a <i>time-serial position</i>).

# Context
This repo stems from additional research after I posted a [comment](https://github.com/dotnet/runtime/pull/45842#issuecomment-742100677) in PR [dotnet/runtime#45842](https://github.com/dotnet/runtime/pull/45842). In that comment, I did not take into
account the fact that `CacheEntry.LastAccessed` is often updated with `DateTimeOffset.UtcNow` in order to support:
* Sliding expiration
* LRU-based cache eviction during cache compaction

After some local experimentation with `Lazy<T>` (as suchested in my initial comment) and
[`LazyInitializer.EnsureInitialized()`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.lazyinitializer.ensureinitialized?view=net-5.0#System_Threading_LazyInitializer_EnsureInitialized__1___0__System_Boolean__System_Object__System_Func___0__),
I did get some promising results, but realized that it resulted in some quite convoluted code. Hence, I decided to first create and
share an abstraction that reflects the idea behind a potential direction for further optimization.

# Remarks
* The `ISystemClockTemporalContext` abstraction introduced as part of this concept might be useful in other scenarios where a
  synthetic clock and/or timer must be imposed onto a subject/system (e.g. event replay, unit tests).
* The solution in this repo contains a test project. It serves two purposes:
  1. Ensure that I didn't leave too many gaps
  2. Document the idea behind `ISystemClockTemporalContext`, without actually writing documentation

# Remaining work
- [ ] Implement `IDisposable` and/or `IAsyncDisposable` on `ClockQuantizer`
- [ ] Integrate this proposal in a [local fork](https://github.com/edevoogd/runtime) of Microsoft.Extensions.Caching.Memory as a PoC
