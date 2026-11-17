# 1011. Buffers have no finalizer and are zeroed only by Dispose

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

`SecureMemory<T>` (an internal class), `SecureArrayLifetime<T>`, `SecureSpanLifetime<T>` and `RefSpanLease<T>` (readonly
ref structs) zero or return memory only in `Dispose`. No buffer type declares a finalizer. The one finalizer in `src/` is
on the internal `Gen2GcCallback`, which schedules pool trimming and zeroes nothing.

A struct, a ref struct included, cannot declare a finalizer. `SecureMemory<T>` is a class and could.
`docs/architecture.md` states "There are no finalizers on buffers."

The repository records no reason for this choice: no decision record, commit message, code comment or document states
one. This record states the rule as the code keeps it, so that a change to it is decided here.

## Decision

**Memory is zeroed only by `Dispose`; no buffer type has a finalizer.**

## Options considered

- A finalizer on `SecureMemory<T>` that zeroes its array. Not chosen; no reason is recorded.
- No finalizer (chosen).

## Consequences

- Every owner is disposed, with `using`.
- An owner dropped without `Dispose` keeps its contents, as the site sample `SiteNoFinalizer` shows.
- A pooled buffer that is never disposed never returns to the pool.

## References

- `src/NCode.Buffers/SecureMemory.cs`, `src/NCode.Buffers/SecureArrayLifetime.cs` and
  `src/NCode.Buffers/SecureSpanLifetime.cs`
- `tests/NCode.Buffers.Site.Tests/Concepts/HowSecretsLeakSamples.cs`
- [Memory<T> and Span<T> usage guidelines](https://learn.microsoft.com/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
