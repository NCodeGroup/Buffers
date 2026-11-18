# 1014. SecureSpanLifetime zeroes the caller's span and does not pin it

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

`SecureSpanLifetime<T>(Span<T> span)` stores the caller's span and zeroes it with `CryptographicOperations.ZeroMemory`
on `Dispose`. `span.GetSecureLifetime()` creates one (`BufferExtensions.cs` lines 61–64).

Nothing pins the memory. A span can be `stackalloc` memory, native memory or part of a managed array. An array on the
ordinary heap can be moved by a compacting collection before the lifetime exists, which leaves a copy at its old
address. `SecureArrayLifetime<T>` is the type that allocates pinned memory. `README.md` states "`SecureSpanLifetime<T>`
doesn't pin."

The repository records no reason for this choice: no decision record, commit message, code comment or document states
one. This record states the rule as the code keeps it, so that a change to it is decided here.

## Decision

**`SecureSpanLifetime<T>` zeroes the memory it is given, wherever it is, and pins nothing.**

## Options considered

- Pin the memory behind the span while the lifetime lasts. Not chosen; no reason is recorded.
- No pinning (chosen).

## Consequences

- Use it over `stackalloc` memory or memory from `Rent` or `CreatePinnedArray`.
- It cannot reach a copy made before it.

## References

- `src/NCode.Buffers/SecureSpanLifetime.cs` and `src/NCode.Buffers/BufferExtensions.cs`
- `docs/site/start/limits.mdx` item 7
