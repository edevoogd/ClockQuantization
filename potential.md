# Introduction
## Definitions

To start with, here's an explanation of the tags that are used to describe the characteristics of the various cache entries that were used in the benchmarks:
| Entry          | Description                                                                                                                        |
| -------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| **Expired**    | A cache entry that was previously marked expired; effectively, the `State.IsExpired` flag is set in its internal state             |
| **Plain**      | A cache entry that does not have absolute expiration nor sliding expiration                                                        |
| **Absolute<**  | A cache entry with absolute expiration, where the entry expired prior to the start of the current clock quantizer interval         |
| **Absolute?!** | A cache entry with absolute expiration, where the entry will expire sometime within the current clock quantizer interval           |
| **Absolute>**  | A cache entry with absolute expiration, where the entry will expire after the expected end of the current clock quantizer interval |
| **Sliding<**   | A cache entry with sliding expiration, where the entry expired prior to the start of the current clock quantizer interval          |
| **Sliding?!**  | A cache entry with sliding expiration, where the entry will expire sometime within the current clock quantizer interval            |
| **Sliding>**   | A cache entry with sliding expiration, where the entry will expire after the expected end of the current clock quantizer interval  |
| **Expiring<**  | Shorthand for **Absolute<** or **Sliding<**                                                                                        |
| **Expiring?!** | Shorthand for **Absolute?!** or **Sliding?!**                                                                                      |
| **Expiring>**  | Shorthand for **Absolute>** or **Sliding>**                                                                                        |

<br/>

## First impressions

The table below lists the execution time ratios of <u>clock access plus expiry check</u> in `MemoryCache.TryGetValue()` _with clock quantization_ compared to the _original_ approach:

| Entry          | .NET 4.8 (netstandard2.0) | .NET Core 3.1 (netstandard2.0) | .NET 5.0<sup>*</sup> | .NET 6.0 Preview 1<sup>*</sup> |
| -------------- | ------------------------: | -----------------------------: | -------------------: | -----------------------------: |
| **Expired**    | 0.27                      | 0.095                          | 0.015                | 0.019                          |
| **Plain**      | 0.38                      | 0.22                           | 0.17                 | 0.20                           |
| **Expiring<**  | 0.35                      | 0.19                           | 0.07                 | 0.09                           |
| **Expiring?!** | <u>**1.50**</u>           | <u>**1.51**</u>                | **0.85**             | **0.94**                       |
| **Expiring>**  | 0.40                      | 0.31                           | 0.25                 | 0.28                           |

<small>**<sup>*</sup>** The .NET 5.0 and .NET 6.0 Preview 1 implementations listed in this comparison contain an additional optimization suggested by [@filipnavara](https://github.com/dotnet/runtime/pull/45842#issuecomment-761235581) (more on that further on where we discuss runtimes/TFMs).</small>

<br/>

Bear in mind: these are just first impressions based on a _subset_ of operations from the original cache methods (more on this in the next section).

Notes & observations:
* **Expired**, **Plain**, **Expiring<** and **Expiring>** are markedly faster for all TFMs, due to amortization of clock access across all `MemoryCache.TryGetValue()` calls within an interval;
* **Expired** becomes a lot faster in .NET Core 3.1 and even more in .NET 5.0, which a.f.a.i.k.t. can only be attributed to performance improvements in JIT/CLR when evaluating `CacheEntry.IsExpired`.
* Only **Plain**, **Expiring?!** and **Expiring>** incur additional work to update `CacheEntry.LastAccessed` (this is used for sliding expiration and for LRU eviction during background scan & compaction; hence, there is no point in determining and updating this on expired entries)
* After bounds checks against `ClockQuantizer.CurrentInterval`, **Expiring?!** still incurs an _exact_ expiration check which requires clock access. On top of this, a so-called 'advance operation' is performed in `ClockQuantizer` in order to enable honouring current LRU behavior.


As can be seen, a .NET 5.0+ implementation with clock quantization is faster in all scenarios. Compared to .NET 5.0, ratios are slightly up again for .NET 6.0 Preview 1 due to additional performance improvements in `DateTimeOffset.UtcNow`, such as [dotnet/runtime#45281](https://github.com/dotnet/runtime/pull/45281), [dotnet/runtime#45479](https://github.com/dotnet/runtime/pull/45479) and [dotnet/runtime#46690](https://github.com/dotnet/runtime/pull/46690). I'm anticipating further "deterioration" with .NET 6.0 Preview 4 (already grabbing my pitchfork for [dotnet/runtime#50263](https://github.com/dotnet/runtime/pull/50263) :wink:)


## Synthetic benchmark test approach

In order to gage potential performance improvements, some synthetic benchmark tests were devised to cover clock access plus expiry check in what are assumed to be the most frequently exercised (performance-sensitive) methods involving cache expiration checks:
* `bool MemoryCache.TryGetValue(object key, out object result)`
    1. Mimic the original implementation &rarr; `CheckExpiredWithDateTimeOffset()`
    2. Mimic the implementation with clock quantization (local PoC) &rarr; `CheckExpiredWithByRefLazyClockOffsetSerialPosition()`
* `static void MemoryCache.ScanForExpiredItems(MemoryCache cache)`

    3. Mimic the original implementation &rarr; `CheckExpiredWithAmortizedDateTimeOffset()`

<br/>

```csharp
// TryGetValue - original
[Benchmark]
public bool CheckExpiredWithDateTimeOffset(CacheEntry entry)
{
    var now = DateTimeOffset.UtcNow;
    var expired = entry.CheckExpired(now);

    if (!expired)
    {
        // Mimic fetching LastAccessed for LRU purposes
        _lastAccessedDateTimeOffset = now;
    }

    return expired;
}

// TryGetValue - quantized
[Benchmark]
public bool CheckExpiredWithByRefLazyClockOffsetSerialPosition(CacheEntry entry)
{
    var position = default(LazyClockOffsetSerialPosition);
    var expired = entry.CheckExpired(ref position);
    
    if (!expired)
    {
        // Mimic fetching LastAccessed for LRU purposes
        _quantizer.EnsureInitializedClockOffsetSerialPosition(ref position);
        _lastAccessedPosition = position;
    }

    return expired;
}

// Background scan - original - clock access cost amortized across all iterated cache entries
private DateTimeOffset _now = DateTimeOffset.UtcNow;

[Benchmark]
public bool CheckExpiredWithAmortizedDateTimeOffset(CacheEntry entry)
{
    return entry.CheckExpired(_now);
}
```
<br/>

**Caveat**: cycles not covered in the synthetic tests of `MemoryCache.TryGetValue()`:
* `MemoryCache._entries.TryGetValue()`
* `CacheEntry.SetExpired(EvictionReason.Expired)` &rarr; this would invalidate benchmark results of expiring entries immediately after the first iteration (been there, done that, got the T-shirt... :hand_over_mouth:)
* `CacheEntry.CanPropagateOptions()`/`CacheEntry.PropagateOptions(CacheEntryHelper.Current)`
* `MemoryCache.StartScanForExpiredItemsIfNeeded(utcNow)` &rarr; this is expected to be replaced with a timer-based background expiration scan (see [dotnet/runtime#47239](https://github.com/dotnet/runtime/issues/47239)... which - come to think of it - might actually be driven from the built-in metronome in `ClockQuantizer`).

<br/>

# Notable differences between runtimes/TFMs

## Capabilities
There are notable differences between .NET Standard 2.0 and .NET 5.0+: 
* `Environment.TickCount64` was first introduced in .NET 5.0
* The .NET Standard 2.0 implementation falls back to `DateTimeOffset.Now.UtcTicks`

## Performance
First, let's have a look at a like-for-like comparison of potential `MemoryCache.TryGetValue()` performance improvements across `net48-netstandard20`, `netcore31-netstandard20` and `net50-netcoreapp50-datetimeoffset`. The "like-for-like" is in the fact that an interim `net50` implementation was benchmarked, which shared the exact same code for `ISystemClock.UtcNowClockOffset` with the other TFMs.

| Entry          | .NET 4.8 (netstandard2.0) | .NET Core 3.1 (netstandard2.0) | .NET 5.0 (`DateTimeOffset.Now.UtcTicks`) |
| -------------- | ------------------------: | -----------------------------: | ---------------------------------------: |
| **Expired**    | 0.27                      | 0.095                          | 0.015                                    |
| **Plain**      | 0.38                      | 0.22                           | 0.17                                     |
| **Expiring<**  | 0.35                      | 0.19                           | 0.07                                     |
| **Expiring?!** | **1.50**                  | **1.51**                       | <u>**1.63**</u>                          |
| **Expiring>**  | 0.40                      | 0.31                           | 0.25                                     |

<br/>

## The curious case of **Expiring?!**
So, why is **Expiring?!** performing worse than the original implementation? If an entry is deemed to expire within the current interval, we still need to determine the exact time to decide if the entry has expired at a specific (precise) point in time. This incurs a hit on top of the work already done until that point in time. Moreover, in order to ensure that LRU ordering is not messed up, the `CurrentInterval.ClockOffset` and sequence counter must be updated as well. For this purpose, a so-call 'advance operation' is performed, which starts (allocates) a completely new `Interval`, adding more overhead in this case.

The net result is that **Expiring?!** relative performance gets worse and worse as we move to more recent .NET versions, as the absolute performance of the original implementation gets better and better as we move to more recent .NET versions.

| Method                                             | .NET 4.8 (netstandard2.0) | .NET Core 3.1 (netstandard2.0) | .NET 5.0 (`DateTimeOffset.Now.UtcTicks`) |
| -------------------------------------------------- | ------------------------: | -----------------------------: | ---------------------------------------: |
|                     CheckExpiredWithDateTimeOffset |                 111.21 ns |                    109.1622 ns |                               91.7685 ns |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |                 167.13 ns |                    165.3159 ns |                              149.1493 ns |

<br/>

When we leverage `Environment.TickCount64` for .NET 5.0+ to determine an exact clock offset, we are able to reduce the overhead to determine the exact point in time and to compensate for some of the performance losses incurred by the 'advance operation', as illustrated below:

| Entry          | .NET 5.0 (`DateTimeOffset.Now.UtcTicks`) | .NET 5.0 (`Environment.TickCount64`) |
| -------------- | ---------------------------------------: | -----------------------------------: |
| **Expired**    | 0.015                                    | 0.015                                |
| **Plain**      | 0.17                                     | 0.17                                 |
| **Expiring<**  | 0.07                                     | 0.07                                 |
| **Expiring?!** | **1.63**                                 | **<u>0.85</u>**                      |
| **Expiring>**  | 0.25                                     | 0.25                                 |

<br/>

# Take-aways
## Achievements so far...
* Apply clock quantization to amortize clock access across an entire interval (gains for **all entries**)
* Leverage quantizer interval to quickly determine (non-)expiration outside of the current interval (gains for **Expiring<** and **Expiring>**)
* Acquire exact date-time as late as possible and only when really needed to determine "last accessed" for non-expired entries with sliding expiration (gains for **Expired**, **Plain**, **Expiring<** and **Absolute>**)
* Use clock-offset-serial positions to determine LRU ordering (gains for **Plain** and **Absolute>**; note that we still need to determine and register an _exact_ "last accessed" for **Sliding>**; also, we need an _exact_ clock reading to check expiration of **Expiring?!**)
* All in all, just a modest improvement for **Expiring?!**, but only for .NET 5.0+ because <u>we were able to compensate for some performance losses</u>, through leveraging `Environment.TickCount64` instead of `DateTimeOffset.Now.UtcTicks`. But, as mentioned, <u>this modest gain is already under threat</u> by continued `DateTimeOffset.Now` performance improvements in .NET 6.0

The table below lists the execution time ratios and speedup of <u>clock access plus expiry check</u> in `MemoryCache.TryGetValue()` _with clock quantization_ in .NET 6.0 Preview 1 compared to the _original_ approach:

| Entry          | Ratio | Speedup  |
| -------------- | ----: | -------: |
| **Expired**    | 0.019 |  52.6  X |
| **Plain**      |  0.20 |   5.00 X |
| **Expiring<**  |  0.09 |  11.1  X |
| **Expiring?!** |  0.94 |   1.06 X |
| **Expiring>**  |  0.28 |   3.57 X |

## Additional areas to investigate
* As gaged in Draft PR [dotnet/runtime#45842](https://github.com/dotnet/runtime/pull/45842), there is an estimated 10% gain to be had if the background expiration scan becomes timer-based (instead of checking in almost every public method). Most likely, this will be prerequisite to fully unlocking the potential described here. We might leverage the `ClockQuantizer.MetronomeTicked` event for this purpose.
* As mentioned, most cache entry types would benefit from an approach with clock quantization. However, results for **Expiring??!** are only slightly better and only for .NET 5.0+. Initially, I pondered upon introducing so-called [cache expiration policies](https://github.com/edevoogd/ClockQuantization/blob/develop/readme.md) (precise, optimistic, pessimistic), where the latter two would skip evaluating the clock, but instead just assume not expired or expired depending on the policy applied. More recently, I started considering an alternative that would combine clock quantization plus `Environment.TickCount` (which has been around [since .NET Framework 1.1](https://docs.microsoft.com/en-us/dotnet/api/system.environment.tickcount?view=net-5.0#applies-to)). This is a topic for another day... :smile:

# Detailed measurements

## Notes
* In the measurements presented, two alternative approaches were taken to handle **Expiring?!** cases. The alternative approaches apply two of the cache expiration policies mentioned earlier:
  * **Absolute?!**: precise &rarr; evaluate the clock offset at the point in time when the item's expiry is determined
  * **Sliding?!**: pessimistic &rarr; consider an item to be expired if its expiry occurs sometime within the current interval; effectively, this makes its performance comparable to the **Sliding<** case
* Measurements for **Sliding>** are incorrect, as we assumed that `Interval.ClockOffset` could be used as `LastAccessed`; this is especially bad if `CacheEntry.SlidingExpiration` << `ClockQuantizer.MaxIntervalTimeSpan`; a correct setup with the current mechanics would probably bring performance close to that of **Absolute?!**.


## .NET Framework 4.8
|                                             Method |     Entry  |      Mean | Ratio |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|--------------------------------------------------- |----------- |----------:|------:|-------:|------:|------:|----------:|
|                     CheckExpiredWithDateTimeOffset |   Expired  |  91.41 ns |  1.00 | 0.0229 |     - |     - |      48 B |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |   Expired  |  24.91 ns |  0.27 | 0.0229 |     - |     - |      48 B |
|            CheckExpiredWithAmortizedDateTimeOffset |   Expired  |  16.88 ns |  0.18 | 0.0229 |     - |     - |      48 B |
|                                                    |            |           |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |     Plain  |  91.51 ns |  1.00 | 0.0229 |     - |     - |      48 B |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |     Plain  |  34.33 ns |  0.38 | 0.0229 |     - |     - |      48 B |
|            CheckExpiredWithAmortizedDateTimeOffset |     Plain  |  16.97 ns |  0.19 | 0.0229 |     - |     - |      48 B |
|                                                    |            |           |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute<  | 109.91 ns |  1.00 | 0.0229 |     - |     - |      48 B |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute<  |  37.77 ns |  0.34 | 0.0229 |     - |     - |      48 B |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute<  |  34.49 ns |  0.31 | 0.0229 |     - |     - |      48 B |
|                                                    |            |           |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute?! | 111.21 ns |  1.00 | 0.0229 |     - |     - |      48 B |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute?! | 167.13 ns |  1.50 | 0.0648 |     - |     - |     136 B |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute?! |  34.15 ns |  0.31 | 0.0229 |     - |     - |      48 B |
|                                                    |            |           |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute>  | 117.33 ns |  1.00 | 0.0229 |     - |     - |      48 B |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute>  |  47.16 ns |  0.40 | 0.0229 |     - |     - |      48 B |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute>  |  34.31 ns |  0.29 | 0.0229 |     - |     - |      48 B |
|                                                    |            |           |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding<  | 120.51 ns |  1.00 | 0.0229 |     - |     - |      48 B |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding<  |  37.86 ns |  0.31 | 0.0229 |     - |     - |      48 B |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding<  |  43.82 ns |  0.36 | 0.0229 |     - |     - |      48 B |
|                                                    |            |           |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding?! | 120.79 ns |  1.00 | 0.0229 |     - |     - |      48 B |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding?! |  38.28 ns |  0.32 | 0.0229 |     - |     - |      48 B |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding?! |  44.02 ns |  0.36 | 0.0229 |     - |     - |      48 B |
|                                                    |            |           |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding>  | 119.98 ns |  1.00 | 0.0229 |     - |     - |      48 B |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding>  |  47.53 ns |  0.40 | 0.0229 |     - |     - |      48 B |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding>  |  43.87 ns |  0.37 | 0.0229 |     - |     - |      48 B |

<br/>

## .NET Core 3.1
|                                             Method |     Entry  |        Mean | Ratio |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|--------------------------------------------------- |----------- |------------:|------:|-------:|------:|------:|----------:|
|                     CheckExpiredWithDateTimeOffset |   Expired  |  89.0490 ns | 1.000 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |   Expired  |   8.4553 ns | 0.095 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |   Expired  |   0.2227 ns | 0.003 |      - |     - |     - |         - |
|                                                    |            |             |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |     Plain  |  90.1560 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |     Plain  |  20.1861 ns |  0.22 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |     Plain  |   1.1727 ns |  0.01 |      - |     - |     - |         - |
|                                                    |            |             |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute<  | 109.2124 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute<  |  20.4974 ns |  0.19 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute<  |  18.3478 ns |  0.17 |      - |     - |     - |         - |
|                                                    |            |             |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute?! | 109.1622 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute?! | 165.3159 ns |  1.51 | 0.0417 |     - |     - |      88 B |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute?! |  18.4172 ns |  0.17 |      - |     - |     - |         - |
|                                                    |            |             |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute>  | 108.8977 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute>  |  34.0164 ns |  0.31 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute>  |  18.4443 ns |  0.17 |      - |     - |     - |         - |
|                                                    |            |             |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding<  | 112.1062 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding<  |  20.6645 ns |  0.18 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding<  |  22.6527 ns |  0.20 |      - |     - |     - |         - |
|                                                    |            |             |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding?! | 112.1254 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding?! |  20.5879 ns |  0.18 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding?! |  23.3203 ns |  0.21 |      - |     - |     - |         - |
|                                                    |            |             |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding>  | 111.7185 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding>  |  34.2345 ns |  0.31 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding>  |  23.3087 ns |  0.21 |      - |     - |     - |         - |

<br/>

## .NET 5.0
|                                             Method |     Entry  |       Mean | Ratio |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|--------------------------------------------------- |----------- |-----------:|------:|-------:|------:|------:|----------:|
|                     CheckExpiredWithDateTimeOffset |   Expired  | 76.8534 ns | 1.000 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |   Expired  |  1.1853 ns | 0.015 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |   Expired  |  0.2175 ns | 0.003 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |     Plain  | 80.7803 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |     Plain  | 14.3872 ns |  0.17 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |     Plain  |  0.9288 ns |  0.01 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute<  | 92.6860 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute<  |  6.5928 ns |  0.07 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute<  | 13.7005 ns |  0.15 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute?! | 93.3439 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute?! | 78.8515 ns |  0.85 | 0.0421 |     - |     - |      88 B |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute?! | 13.8854 ns |  0.15 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute>  | 93.6829 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute>  | 23.4890 ns |  0.25 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute>  | 13.5127 ns |  0.14 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding<  | 95.6584 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding<  |  6.8726 ns |  0.07 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding<  | 15.8515 ns |  0.17 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding?! | 94.0571 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding?! |  6.8861 ns |  0.07 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding?! | 16.0458 ns |  0.17 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding>  | 93.8544 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding>  | 23.4395 ns |  0.25 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding>  | 15.7767 ns |  0.17 |      - |     - |     - |         - |

<br/>

## .NET 6.0 Preview 1
|                                             Method |     Entry  |       Mean | Ratio |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|--------------------------------------------------- |----------- |-----------:|------:|-------:|------:|------:|----------:|
|                     CheckExpiredWithDateTimeOffset |   Expired  | 69.6139 ns | 1.000 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |   Expired  |  1.2999 ns | 0.019 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |   Expired  |  0.5178 ns | 0.007 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |     Plain  | 71.4801 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |     Plain  | 14.5727 ns |  0.20 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |     Plain  |  0.9125 ns |  0.01 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute<  | 85.4359 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute<  |  6.7551 ns |  0.08 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute<  | 14.1472 ns |  0.17 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute?! | 85.1048 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute?! | 80.0187 ns |  0.94 | 0.0421 |     - |     - |      88 B |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute?! | 14.2747 ns |  0.17 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset | Absolute>  | 86.2135 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition | Absolute>  | 24.0333 ns |  0.28 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset | Absolute>  | 13.8873 ns |  0.16 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding<  | 87.1628 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding<  |  8.1351 ns |  0.09 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding<  | 16.7469 ns |  0.19 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding?! | 87.0065 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding?! |  8.2194 ns |  0.09 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding?! | 17.0814 ns |  0.20 |      - |     - |     - |         - |
|                                                    |            |            |       |        |       |       |           |
|                     CheckExpiredWithDateTimeOffset |  Sliding>  | 89.4248 ns |  1.00 |      - |     - |     - |         - |
| CheckExpiredWithByRefLazyClockOffsetSerialPosition |  Sliding>  | 23.9665 ns |  0.27 |      - |     - |     - |         - |
|            CheckExpiredWithAmortizedDateTimeOffset |  Sliding>  | 17.4319 ns |  0.19 |      - |     - |     - |         - |
