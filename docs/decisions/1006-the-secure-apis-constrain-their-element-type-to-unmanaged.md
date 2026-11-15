# 1006. The secure APIs constrain their element type to unmanaged

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** NCode Group

## Context

The secure APIs pin buffers and zero or compare them as bytes. `GC.AllocateUninitializedArray<T>(length, pinned: true)`
and `MemoryMarshal.AsBytes<T>` throw `ArgumentException` for a `T` that is or contains a reference. Under
`where T : struct` such a `T` compiles, and the call fails at run time: on `SecureMemoryPool<T>.Rent`, on creating a
`SecureArrayLifetime<T>`, on disposing a `SecureSpanLifetime<T>`, or on `ZeroMemory<T>` and `FixedTimeEquals<T>`, while
their documentation says `T` must be an unmanaged value type. 5.0.0 is an unreleased major version
([ADR-1000](1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md)), where a break can ship.

## Decision

**Every public generic API that pins, zeroes or compares its elements as bytes, or can rent from
`SecureMemoryPool<T>`, declares `where T : unmanaged`.**

1. The types `SecureMemoryPool<T>`, `SecureArrayLifetime<T>` and `SecureSpanLifetime<T>`, and the internal
   `SecureMemory<T>`.
2. `CryptographicOperations.ZeroMemory<T>` and `FixedTimeEquals<T>`; `BufferFactory.Rent<T>`, `CreatePinnedArray<T>` and
   `CreatePooledBufferWriter<T>`; the `Sequence<T>` extensions; and `Span<T>.GetSecureLifetime()`.
3. APIs that neither pin, zero nor rent secure buffers keep their constraint: `BufferFactory.CreateArrayBufferWriter<T>`
   and `Span<T>.GetFixedBufferWriter()` keep `struct`, and `Memory<T>.GetFixedBufferWriter()` has none.
4. The package validation diagnostics of the change are recorded in `src/NCode.Buffers/CompatibilitySuppressions.xml`
   until the validation baseline reaches 5.x
   ([ADR-1001](1001-each-package-is-validated-against-the-last-released-package.md)).

## Options considered

- Keep `struct` and document the `ArgumentException`. Rejected: the mistake surfaces at run time, often only on the
  sensitive path or at dispose.
- Keep `struct` and check `RuntimeHelpers.IsReferenceOrContainsReferences<T>()` for a clearer exception. Rejected: still
  a run-time failure.
- `unmanaged` on every generic API of `BufferFactory` and `BufferExtensions`. Rejected: it breaks callers of APIs that
  never pin or zero.
- `unmanaged` on the APIs that pin, zero, compare or rent secure buffers (chosen).

## Consequences

- A `T` with references fails to compile (CS8377) instead of throwing.
- A caller that passed such a `T` with `isSensitive: false`, which worked, no longer compiles; it uses
  `MemoryPool<T>.Shared` or `new Sequence<T>()` directly.
- A new API that pins, zeroes or rents secure buffers declares `unmanaged` too.

## References

- [#45](https://github.com/NCodeGroup/Buffers/issues/45)
- [ADR-1000](1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md): package versions come
  from version.json through Nerdbank.GitVersioning.
- [ADR-1001](1001-each-package-is-validated-against-the-last-released-package.md): each package is validated against
  the last released package.
- [API compatibility](../api-compatibility.md)
- [Unmanaged constraint](https://learn.microsoft.com/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters#unmanaged-constraint)
