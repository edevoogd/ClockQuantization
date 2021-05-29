# Commit descriptions

This log describes the commits in branch [`memorycache-clockquantization-jit-now`](https://github.com/edevoogd/runtime/tree/memorycache-clockquantization-jit-now).

## Groundwork
The first set of 4 commits lays the foundation for the eventual changes. They ensure that new temporal primitives and the new clock quantization primitives get introduced, while ensuring that existing tests leverage these newly introduced temporal primitives. Also, a new `net5.0` target is introduced to ensure that we can leverage new features introduced in .NET 5.0.

* Commit [`64ec768`](https://github.com/edevoogd/runtime/commit/64ec768b740117b43bfba133613ade99fee4a58a) - temporal primitives:
  * Introduction of abstractions for system clock and temporal context, in preparation of clock quantization
  * Main interfaces: `Microsoft.Extensions.Internal.ClockQuantization.ISystemClock`, `Microsoft.Extensions.Internal.ClockQuantization.ISystemClockTemporalContext`
  * Implementation of shims around external clocks (incl. `ManualClock` for testing purposes), as well as a built-in TFM-optimized `InternalClock`
  * Factory method determines most appropriate shim based on traits of the provided `Microsoft.Extensions.Internal.ISystemClock` (may be `null` &rarr; `InternalClock`)
  * We may drop some specific shims based on insights gained along the way
* Commit [`74a49af`](https://github.com/edevoogd/runtime/commit/74a49afb855498c56d289ec6f193672814e541ef) - `Microsoft.Extensions.Internal.TestClock` reacting to temporal primitives:
  * Extends the existing `TestClock` with `Microsoft.Extensions.Internal.ClockQuantization.ISystemClockTemporalContext` traits (internal interface)
  * An internal `ClockAdjusted` event will be raised when the clock is adjusted in `TestClock.Add()`
  * Note: the original public API surface of `TestClock` was left untouched
* Commit [`66ae3e5`](https://github.com/edevoogd/runtime/commit/66ae3e501722e8e9a7e1415039172785d0cd6f4c) - additional references and unlocking TFM-specific capabilities for .NET 5.0+:
  * Adding `net5.0` TFM to unlock `Environment.TickCount64` for .NET 5.0 and .NET 6.0
  * TFM-specific references for `net461`:
    * System.Runtime.CompilerServices.Unsafe (`Interlocked.*` operations acting on `uint`)
    * System.Threading.Tasks.Extensions (`ValueTask`)
    * Microsoft.Bcl.AsyncInterfaces (`IAsyncDisposable`)
  * TFM-specific references for `netstandard2.0`
    * System.Threading.Tasks.Extensions (`ValueTask`)
    * Microsoft.Bcl.AsyncInterfaces (`IAsyncDisposable`)
  * TFM-specific references for `net5.0`
    * System.Runtime
    * System.Threading
    * System.Threading.ThreadPool
    * System.Collections
    * System.Collections.Concurrent
* Commit [`132ac84`](https://github.com/edevoogd/runtime/commit/132ac84299aa67df86f0384ff0c8a449c971991e) - clock quantization primitives:
  * `LazyClockOffsetSerialPosition`
  * `LazyClockOffsetSerialPosition.Snapshot`
  * `Interval`
  * `Interval.SnapshotTracker`

## Replacing the original clock
Expecting worse performance due to additional layers of abstraction and unit conversions. Main purpose is to exercise (most) clock access functions and unit conversion functions:
* `UtcNow`
* `UtcNowClockOffset` (the option that is faster in .NET 5.0+)
* `DateTimeOffsetToClockOffset()`
* `ClockOffsetToUtcDateTimeOffset()`
* `TimeSpanToClockOffsetUnits()`
* `ClockOffsetUnitsToTimeSpan()`

<br/>

* Commit [`9376f4f`](https://github.com/edevoogd/runtime/commit/9376f4f43949972ed5c7f58674117e12e132e590) - `ClockQuantizer` and `TemporalContextDriver`:
  * Adds the heart of clock quantization, as prototyped in repo [edevoogd/ClockQuantization](https://github.com/edevoogd/ClockQuantization)
  * More details about support class `TemporalContextDriver` are described in PR [edevoogd/ClockQuantization#7](https://github.com/edevoogd/ClockQuantization/pull/7)
* Commit [`aafd8b6`](https://github.com/edevoogd/runtime/commit/aafd8b6eaeaaa55352134a1d93aee9dc32fb293e) - adapt time-related `CacheEntry` properties and backing storage
  * Adapt time-related `CacheEntry` properties and backing storage to leverage `LazyClockOffsetSerialPosition` (clock offset plus serial position) and `long` (clock offset only)
    * `LastAccessed` (also introducing new property `LastAccessedClockOffsetSerialPosition`)
    * `AbsoluteExpration`
    * `SlidingExpiration`
  * Half-hearted approach to leave as much in tact as possible, hopefully making it easier to review commit diffs

## Rewiring clock access, expiry checks and LRU bookkeeping/eviction

* Commit [`556a852`](https://github.com/edevoogd/runtime/commit/556a852bced55e39c96e4f48a88ec401a813b5de) - optimize expiration scan trigger checks
  * Rewriting bookkeeping and checks in terms of clock offset (units)
  * If possible, ride the wave of an existing exact `LazyClockOffsetSerialPosition`; if that doesn't exist, fall back to the most inexpensive clock access method: `UtcNowClockOffset`
* Commit [`71fa1b0`](https://github.com/edevoogd/runtime/commit/71fa1b02a088bcae26a8279558cb83a9b2656f25) - optimize `TryGetValue()` with interval-based expiration checks and `LazyClockOffsetSerialPosition`
  * Introduction of interval-based entry expiration checks
  * Most appropriate clock-offset-serial position determined based on entry type and temporal context; applied as late as possible (and only when needed)
  * One downside for entries with sliding expiration: if an entry has not yet expired, we incur a so-called "advance operation" (__allocating!__) to ensure proper expiration tracking, as well as proper LRU ordering of subsequent entry retrievals
* Commit [`f8e0a12`](https://github.com/edevoogd/runtime/commit/f8e0a12d56642cc03711d28d0b2ac0fe209640cf) - optimize `SetEntry()` with interval-based expiration checks and `LazyClockOffsetSerialPosition`
  * Upfront, obtain an exact clock-offset-serial position only for entries with sliding expiration; we incur a so-called "advance operation" (__allocating!__)
  * Be opportunistic in determining absolute expiration if "relative to now" absolute expiration is at play; if possible, ride the wave of an existing exact `LazyClockOffsetSerialPosition`; if that doesn't exist, fall back to the most inexpensive clock access method: `UtcNowClockOffset`
  * Leverage interval-based expiration checks introduced with commit [`71fa1b0`](https://github.com/edevoogd/runtime/commit/71fa1b02a088bcae26a8279558cb83a9b2656f25)


## Optimizing and code clean-up

* Commit [`b803e28`](https://github.com/edevoogd/runtime/commit/b803e28398ffc21fb436dcf51ef3c35e1b03507e) - additional `LazyClockOffsetSerialPosition` capabilities
  * Public method `AssignExactClockOffsetSerialPosition()` to (re-)initialize an instance reference with an "exact" clock offset
  * Implement `IComparable<T>`
* Commit [`5a25899`](https://github.com/edevoogd/runtime/commit/5a258997381cca0620f9ff98ec1879e2004b383b) - rationalize `CacheEntry` time-based properties & expiration checks
  * `LastAccessed` property removed (now fully superseded by `LastAccessedClockOffsetSerialPosition`)
  * Implement `AbsoluteExpirationClockOffset` as an `internal` field
  * Implement `LastAccessedClockOffsetSerialPosition` as an `internal` field
  * Implement `SlidingExpirationClockOffsetUnits` as an `internal` property (`private set`)
  * Introduction of `CheckExpired(..., bool absoluteExpirationUndecided, bool slidingExpirationUndecided)`
    * Allowing for more optimal use from places where we already know the values of these flags - e.g., in `MemoryCache.SetEntry()`
    * Existing `CheckExpired()` method rewired onto new method
  * `DateTimeOffset`-based expiration methods left in place for test purposes, but rewired onto the `LazyClockOffsetSerialPosition`-based implementations
* Commit [`7b5db3b`](https://github.com/edevoogd/runtime/commit/7b5db3b0cc33ad40d3f1a3a733ca2d13ff109b6d) - optimize `MemoryCache`
  * Further optimize `SetEntry()`; amongst others, leverage the newly introduced `CacheEntry.CheckExpired(..., bool absoluteExpirationUndecided, bool slidingExpirationUndecided)` method
  * Rewire `MemoryCache.ScanForExpiredItems()` onto the `LazyClockOffsetSerialPosition`-based expiration check
  * Rewire `MemoryCache.Compact()` onto the `LazyClockOffsetSerialPosition`-based expiration check
  * Rewire `MemoryCache.Compact().ExpirePriorityBucket()` onto the `LazyClockOffsetSerialPosition` custom comparer
* Commit [`70d15a2`](https://github.com/edevoogd/runtime/commit/70d15a25d31f78047de0fd1b44cfa84dc574c7f6) - add missing `#nullable` pragmas
