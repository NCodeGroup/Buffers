# 1013. Pinned buffers are allocated without being cleared

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

`SecureMemory<T>` and `SecureArrayLifetime<T>` allocate with `GC.AllocateUninitializedArray<T>(length, pinned: true)`
(`SecureMemory.cs` lines 24–25, `SecureArrayLifetime.cs` line 62). Microsoft's documentation of that method says "The
uninitialized array can contain invalid valuetype instances or sensitive information created by other parts of the
application." ([GC.AllocateUninitializedArray](https://learn.microsoft.com/dotnet/api/system.gc.allocateuninitializedarray),
read 2026-11-16).

A pooled buffer taken from the cache holds zeros, because `Dispose` zeroed it before it went back. A buffer the pool
allocates, a buffer above a page and a `SecureArrayLifetime<T>` array are not cleared by the library.

The repository records no reason for this choice: no decision record, commit message, code comment or document states
one. This record states the rule as the code keeps it, so that a change to it is decided here.

## Decision

**The library allocates pinned memory uninitialized and clears it only on `Dispose`.**

## Options considered

- `GC.AllocateArray<T>(length, pinned: true)`, which returns a zeroed array. Not chosen; no reason is recorded.
- Uninitialized allocation (chosen).

## Consequences

- Read only what you wrote.
- The length `Rent` returns says nothing about the contents.

## References

- `src/NCode.Buffers/SecureMemory.cs` and `src/NCode.Buffers/SecureArrayLifetime.cs`
- `docs/site/start/limits.mdx`
- `docs/site/reference/pitfalls.mdx`
- [GC.AllocateUninitializedArray](https://learn.microsoft.com/dotnet/api/system.gc.allocateuninitializedarray)
