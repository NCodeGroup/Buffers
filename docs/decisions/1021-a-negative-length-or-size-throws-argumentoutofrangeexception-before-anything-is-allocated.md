# 1021. A negative length or size throws ArgumentOutOfRangeException before anything is allocated

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

The public APIs that take a length or a size answer a negative one in different ways (.NET 10.0.15, read
2026-11-17):

- `BufferFactory.CreatePinnedArray` checks its `length` and throws `ArgumentOutOfRangeException` with `ParamName`
  `length`, while `SecureArrayLifetime<T>.Create` and the `SecureArrayLifetime<T>` constructor, which it calls, check
  nothing: `GC.AllocateUninitializedArray<T>` throws `OverflowException` ("Arithmetic operation resulted in an
  overflow."), which neither documents.
- `BufferFactory.Rent` documents `ArgumentOutOfRangeException` for a negative `minBufferSize` and checks nothing
  itself. For -1, the pool rents a buffer of its default size, because `MemoryPool<T>.Rent` reads -1 as "the default
  size"; slicing it to -1 elements then throws `ArgumentOutOfRangeException` without a `ParamName`. For -2 and below,
  the pool throws, with `ParamName` `minBufferSize` from `SecureMemoryPool<T>` and `minimumBufferSize` from
  `MemoryPool<T>.Shared`.
- `CreatePooledBufferWriter` and `CreateArrayBufferWriter` check their arguments first, with
  `ArgumentOutOfRangeException.ThrowIfNegative` and `ThrowIfNegativeOrZero`.

The .NET design guidelines ask for `ArgumentOutOfRangeException` for an argument outside its range, thrown before the
member does any work, with `ParamName` set to the parameter's name.

## Decision

**Every public API of the package that takes a length or a size checks it first and throws
`ArgumentOutOfRangeException`, with `ParamName` the parameter's name, for a value outside its documented range, before
it allocates or rents anything; its XML documentation lists that exception.**

1. The `SecureArrayLifetime<T>` constructor calls `ArgumentOutOfRangeException.ThrowIfNegative(length)`, so
   `SecureArrayLifetime<T>.Create` and `BufferFactory.CreatePinnedArray` throw through it with `ParamName` `length`.
2. `BufferFactory.Rent` calls `ArgumentOutOfRangeException.ThrowIfNegative(minBufferSize)` before it chooses a pool.
3. `SecureMemoryPool<T>.Rent` keeps the `MemoryPool<T>` contract: -1 asks for the default size, and a value below -1
   throws `ArgumentOutOfRangeException`.

## Options considered

- Document `OverflowException` on `SecureArrayLifetime<T>`. Rejected: it names an arithmetic detail of the runtime,
  not the caller's mistake, and differs from `CreatePinnedArray` for the same input.
- Throw `ArgumentOutOfRangeException` from `BufferFactory.CreatePinnedArray` only. Rejected: the type's own
  constructor and `Create` are public and take the same argument.
- Check first everywhere, with `ArgumentOutOfRangeException` (chosen).

## Consequences

- A caller that passes a negative length to `SecureArrayLifetime<T>` or its `Create` gets `ArgumentOutOfRangeException`
  instead of an undocumented `OverflowException`. The public API's signatures do not change.
- The change ships in a minor release, not a major one: the `OverflowException` it replaces was never documented,
  and `BufferFactory.CreatePinnedArray` and `BufferFactory.Rent` already documented `ArgumentOutOfRangeException`
  for the same input, so code written against the documentation sees no change. A major version is kept for a
  change to documented behavior.
- `BufferFactory.Rent` with a negative size rents nothing and reports `minBufferSize`. Once the size is checked, the
  slice it takes of the rented buffer cannot fail, so `Rent` has no failure path after it rents and no `try`/`catch`.
- A new public API that takes a length or a size follows the same rule.

## References

- [ADR-1003](1003-shipping-code-takes-buffers-and-encodings-through-bufferfactory-and-secureencoding.md): shipping code
  takes buffers through `BufferFactory`.
- [Exception throwing guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/exception-throwing)
- [Using standard exception types](https://learn.microsoft.com/dotnet/standard/design-guidelines/using-standard-exception-types)
