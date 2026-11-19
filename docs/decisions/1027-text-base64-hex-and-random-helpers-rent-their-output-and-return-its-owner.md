# 1027. Text, Base64, hex and random helpers rent their output and return its owner

- **Status:** Accepted
- **Date:** 2026-11-19
- **Deciders:** NCode Group

## Context

The BCL methods that turn text into bytes, bytes into text, or nothing into random bytes each return a new array that
nothing zeroes: `Encoding.GetBytes(string)`, `Convert.FromBase64String`, `Convert.FromHexString(string)` and
`RandomNumberGenerator.GetBytes(int)`. When the result is a secret, that array stays on the managed heap, readable,
until the garbage collector reuses its memory.

A caller that wants a zeroed buffer writes the rent-then-fill code by hand. NCode.Identity does: its
`JoseSerializer.DecodeBase64Url(chars, isSensitive, out bytes)`, `RentJwsSigningInput` and `RentNormalized` each size
a buffer, rent it, fill it and slice it, and each repeats the cleanup when the fill throws.

The BCL has span-based methods that fill a caller's buffer: `Encoding.GetBytes(ReadOnlySpan<char>, Span<byte>)`,
`Convert.TryFromBase64Chars`, `System.Buffers.Text.Base64Url`, `Convert.FromHexString` with a span destination and
`RandomNumberGenerator.Fill`. None of them rents, sizes or cleans up.

## Decision

**A helper that produces text, Base64, Base64url, hex or random bytes into a buffer rents the buffer from
`BufferFactory.Rent` and returns its `IMemoryOwner<T>`; the value comes back through an `out` parameter, sliced to the
exact length written.**

- Each helper takes `isSensitive` and passes it to `BufferFactory.Rent`, so a sensitive result comes from the secure
  pool and is zeroed when its owner is disposed.
- Each helper has an `out Span<T>` form and an `out Memory<T>` form, so a synchronous caller and an async caller
  both avoid a conversion.
- A helper checks its arguments first (a null encoding, an undefined format, a negative length), then sizes the output,
  then rents, then fills. Nothing is rented for an argument that is wrong.
- A helper that can fail after it rents (invalid Base64 or hex, or an `Encoding` subclass that throws while filling)
  disposes the owner before the exception reaches the caller. Disposing zeroes the buffer when it is sensitive.
- The Base64, Base64url and hex helpers use the BCL's `Convert` and `System.Buffers.Text.Base64Url` span methods, so the
  package takes no dependency on NCode.Base64Url.

## Options considered

- Return arrays, as the BCL does. Rejected: nothing zeroes them, which is the problem.
- Depend on NCode.Base64Url for Base64url. Rejected: the BCL's `System.Buffers.Text.Base64Url` and the `Convert` span
  methods do the same work without a second package.
- `Try…` methods that fill a caller's span. Rejected: the caller still sizes the buffer and rents it, and cleans it
  up when the fill throws, which is the boilerplate the helpers remove.
- Rent and return the owner, as `BufferFactory.Rent` does (chosen). The caller disposes one object, the same way it
  disposes every other rental from the package.

## Consequences

- One owner per call: the caller disposes it, and a sensitive result is zeroed then.
- The `out` value has the exact length of the result, not the length of the rental, so the caller needs no count.
- Invalid input returns the rental to its pool, zeroed when it is sensitive, before the exception reaches the caller,
  so a failed call leaks nothing and leaves no secret behind.
- A caller that needs a `byte[]` still uses the BCL; these helpers are for results that are held in a pooled buffer.
- A new helper that produces a secret into a buffer follows the same rule.

## References

- [`src/NCode.Buffers/BufferFactory.cs`](../../src/NCode.Buffers/BufferFactory.cs): `BufferFactory.Rent`, the model for
  the owner-returning shape.
- [ADR-1003](1003-shipping-code-takes-buffers-and-encodings-through-bufferfactory-and-secureencoding.md): shipping code
  takes buffers through `BufferFactory`.
- [ADR-1021](1021-a-negative-length-or-size-throws-argumentoutofrangeexception-before-anything-is-allocated.md): a
  negative length is checked before anything is allocated.
