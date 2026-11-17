# 1015. The secure pool's cache has no size limit and is dropped under memory pressure

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

The cache of `SecureMemoryPool<T>` is a `ConcurrentQueue<SecureMemory<T>>` (`SecureMemoryPool.cs` line 91) with no count
or byte limit. A buffer enters it when a pooled owner is disposed, after `Dispose` zeroed it, unless the pool is disposed
(`Return`, lines 181–187).

The constructor registers `Gen2GcCallback` (line 119), a file copied as-is from dotnet/runtime (`Gen2GcCallback.cs`
lines 10–12). On roughly every Gen2 collection it calls `TrimMemory` (lines 198–212). `TrimMemory` empties the whole
cache when `GC.GetGCMemoryInfo().MemoryLoadBytes` is at least `HighMemoryLoadThresholdBytes * HighPressureThreshold`.
`HighPressureThreshold` defaults to 0.90, and each pool can set it.

Disposing the pool empties the cache, later returns are dropped, and `Rent` throws `ObjectDisposedException`. The
collector frees the dropped buffers, which `Dispose` already zeroed.

The repository records no reason for this choice: no decision record, commit message, code comment or document states
one. This record states the rule as the code keeps it, so that a change to it is decided here.

## Decision

**The cache keeps every returned buffer until a Gen2 collection finds memory pressure or the pool is disposed, and then
drops all of them.**

## Options considered

- A limit on the cached buffers. Not chosen; no reason is recorded.
- Trimming part of the cache. Not chosen; no reason is recorded.
- Trimming by age. Not chosen; no reason is recorded.
- One unbounded cache, trimmed whole (chosen).

## Consequences

- The cache grows to the largest number of buffers rented at once and keeps them.
- The library emits no metrics (`docs/metrics.md`), so measure in your workload.

## References

- `src/NCode.Buffers/SecureMemoryPool.cs` and `src/NCode.Buffers/Gen2GcCallback.cs`
- `SecureMemoryPoolTests.TrimMemory_WhenHighPressure_ClearsQueue`,
  `SecureMemoryPoolTests.TrimMemory_WhenLowPressure_KeepsQueue` and `SecureMemoryPoolTests.Dispose_ClearsMemoryQueue`
