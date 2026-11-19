# 1029. Secret<T> holds a secret it never prints

- **Status:** Accepted
- **Date:** 2026-11-19
- **Deciders:** NCode Group

## Context

A secret that a type keeps in a field is a `byte[]` or a `string`. `ToString()`, string interpolation, a log line and a
debugger's variable view each print it, and nothing zeroes it when the owner is done with it.

Microsoft's DE0001 says "Don't use SecureString for new code."
([DE0001](https://github.com/dotnet/platform-compat/blob/master/docs/DE0001.md), read 2026-11-18), so `SecureString`
is not the answer.

The package already rents sensitive buffers that are zeroed when their owner is disposed, but it has no type that holds
one as a value, hides it from printing and compares it in fixed time. `ROADMAP.md` keeps key storage out of scope.

## Decision

**`Secret<T>` is a sealed class that owns one sensitive rental, never prints its value, and gives the value back only
through `Reveal()` and its siblings; it lives in memory only, with no serialization, persistence or encryption.**

- A static class `Secret` makes it: `Secret.Create<T>(ReadOnlySpan<T>)` copies a value in, and
  `Secret.Create<T, TState>(int length, TState state, SpanAction<T, TState> action)` lets the caller fill the buffer in
  place. A non-generic factory keeps static members off the generic type. `T` is `unmanaged` (ADR-1006).
- The second `Create` checks `length` (ADR-1021) and `action` before it rents, and when the action throws it disposes
  the rental, which zeroes it, and rethrows.
- The constructor is internal, and always takes a rental from `BufferFactory.Rent` with `isSensitive: true`.
- `ToString()` returns `"[REDACTED]"`, `DebuggerDisplay` is `"[REDACTED]"`, and the fields are hidden from the
  debugger, so a log, an interpolation and a debugger show no content.
- `Reveal()` returns a `ReadOnlySpan<T>` and `RevealMemory()` a `ReadOnlyMemory<T>`; `CopyTo(Span<T>)` copies out and
  throws `ArgumentException` when the destination is too short.
- `FixedTimeEquals(ReadOnlySpan<T>)` and `Equals(Secret<T>?)` compare contents with
  `CryptographicOperations.FixedTimeEquals` (ADR-1016). A null is not equal, and the same instance is equal while it
  is not disposed.
- `GetHashCode()` returns `Length`, so it never depends on the content.
- `Dispose()` takes the owner with `Interlocked.Exchange` and disposes it once. After it, every member that reads the
  value throws `ObjectDisposedException`, and `Equals` throws it too when it is given the same instance; `Equals`
  given a null returns `false` without reading the value.

## Options considered

- `SecureString`. Rejected: Microsoft does not recommend it for new code, it holds `char` only, and it doesn't hide
  the value from a caller that reads it back into a `string`.
- A `struct`. Rejected: copies of a struct share one owner, so disposing one copy zeroes the value under the others,
  and `default(Secret<T>)` holds nothing and can't be told from a secret that is empty.
- A sealed class that owns a sensitive rental (chosen). There is one owner per value, `null` and an empty secret are
  different things, and `Dispose` zeroes the value once.

## Consequences

- `Equals` compares contents in fixed time but reveals the length, as `CryptographicOperations.FixedTimeEquals` does.
- `GetHashCode()` is the length, so a `Dictionary` or a `HashSet` keyed by secrets puts every secret of one length in
  one bucket.
- `Reveal()` is the one way to read the value, so a review finds every read by searching for it. A span from it is
  invalid after `Dispose`, and it is the caller's copy, not the secret's, that outlives the secret if the caller
  copies it out.
- `Secret<T>` lives in memory only. It does not serialize, persist or encrypt, so key storage stays with the caller
  and with the platform.
- A new type that holds a secret and is printed or logged follows the same rule: it prints a fixed placeholder, never
  its content.

## References

- [`src/NCode.Buffers/BufferFactory.cs`](../../src/NCode.Buffers/BufferFactory.cs): `BufferFactory.Rent`, the source
  of the sensitive rental.
- [`ROADMAP.md`](../../ROADMAP.md): key storage is out of scope.
- [ADR-1006](1006-the-secure-apis-constrain-their-element-type-to-unmanaged.md): the secure APIs constrain their
  element type to `unmanaged`.
- [ADR-1016](1016-zeroing-and-fixed-time-comparison-call-cryptographicoperations.md): zeroing and fixed-time comparison
  call `CryptographicOperations`.
- [ADR-1021](1021-a-negative-length-or-size-throws-argumentoutofrangeexception-before-anything-is-allocated.md): a
  negative length is checked before anything is allocated.
