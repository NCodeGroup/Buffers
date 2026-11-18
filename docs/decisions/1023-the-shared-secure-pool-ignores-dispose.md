# 1023. The shared secure pool ignores Dispose

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group
- **Amends:** ADR-1015, for `SecureMemoryPool<T>.Shared`, its rule that disposing the pool empties the cache and makes
  `Rent` throw `ObjectDisposedException`.

## Context

`SecureMemoryPool<T>.Shared` is one instance per element type. `BufferFactory` rents from it for every sensitive
rental (`ChoosePool` in `src/NCode.Buffers/BufferFactory.cs`), and shipping code takes its buffers through
`BufferFactory` (ADR-1003), so every sensitive rental in a process depends on it. `MemoryPool<T>.Dispose()` is public,
so any code that holds `Shared` can dispose it, such as a `using` declaration on it or a container that disposes what
it was given.

A process-wide instance has no single owner: no caller can know that no other caller still needs it. The runtime's
`MemoryPool<T>.Shared` ignores `Dispose`: a rental from it after `MemoryPool<byte>.Shared.Dispose()` succeeds, as the
reproduction in [#91](https://github.com/NCodeGroup/Buffers/issues/91) shows.

## Decision

**`SecureMemoryPool<T>.Shared` ignores `Dispose`: it stays usable, keeps its cache and takes buffers back. A pool
created with `new SecureMemoryPool<T>()` keeps ADR-1015's rule: disposing it drops its cache and its later returns,
and makes its `Rent` throw `ObjectDisposedException`.**

The instance `Shared` holds is created with a private `IsShared` flag, and `Dispose(bool)` returns at once when the
flag is set.

## Options considered

- A non-disposable wrapper type for `Shared`. The property's type, `SecureMemoryPool<T>`, is shipped public API, and
  `MemoryPool<T>` is `IDisposable`: a wrapper is either a different property type, which breaks callers, or still a
  `MemoryPool<T>` whose `Dispose` must be ignored, which is this decision.
- A private sealed subclass whose `Dispose(bool)` does nothing. It works, but gives `Shared` a runtime type no caller
  can name, and adds a type where one flag does.
- Throwing `InvalidOperationException` from `Dispose` on `Shared`. `Dispose` must not throw (CA1065 and the .NET
  design guidelines), and every `using` on `Shared` would fail at the end of its block.
- Documenting that `Shared` must not be disposed, and changing nothing. One component could still deny sensitive
  buffers to every other component in the process.
- Ignoring `Dispose` on `Shared` (chosen).

## Consequences

- A component that disposes `Shared` affects no other component.
- An application that wants to drop the cached buffers or stop sensitive rentals creates its own pool and disposes
  that.
- `Shared`'s cache is dropped only by the Gen2 trimming under memory pressure that ADR-1015 describes.

## References

- `src/NCode.Buffers/SecureMemoryPool.cs` and `src/NCode.Buffers/BufferFactory.cs`
- ADR-1015 and ADR-1003
- [#91](https://github.com/NCodeGroup/Buffers/issues/91)
- `SecureMemoryPoolTests.Shared_WhenDisposed_RentStillReturnsABuffer`,
  `SecureMemoryPoolTests.Shared_WhenDisposed_IsNotDisposed`,
  `SecureMemoryPoolTests.Shared_WhenDisposed_KeepsItsCachedBuffers`,
  `SecureMemoryPoolTests.Shared_WhenDisposed_TakesBackTheBuffersItRents` and
  `BufferFactoryTests.Rent_WhenSensitiveAndTheSecureSharedPoolWasDisposed_ReturnsABuffer`
