# 1016. Zeroing and fixed-time comparison call CryptographicOperations

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

The `Dispose` of `SecureMemory<T>` (`SecureMemory.cs` line 34), `SecureArrayLifetime<T>` (`SecureArrayLifetime.cs`
line 110) and `SecureSpanLifetime<T>` (`SecureSpanLifetime.cs` line 75) calls `CryptographicOperations.ZeroMemory`.
`CryptographicOperationsExtensions.ZeroMemory<T>` and `FixedTimeEquals<T>` view their spans as bytes with
`MemoryMarshal.AsBytes` and call `CryptographicOperations.ZeroMemory` and `CryptographicOperations.FixedTimeEquals`.

One path does not. `FixedSpanBufferWriter<T>.Reset()` (`FixedSpanBufferWriter.cs` line 110) and
`FixedMemoryBufferWriter<T>.Reset()` (`FixedMemoryBufferWriter.cs` line 137) clear the written part with
`Span<T>.Clear()`, while their XML docs say to use `Reset` when the buffer holds sensitive data.

Microsoft documents ZeroMemory: "This method exists to future-proof against potential optimizations in the .NET runtime
that could eliminate memory writes that aren't followed by memory reads."
([CryptographicOperations.ZeroMemory](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory),
read 2026-11-16). It documents FixedTimeEquals: "Determines the equality of two byte sequences in an amount of time
that depends on the length of the sequences, but not their values."
([CryptographicOperations.FixedTimeEquals](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.fixedtimeequals),
read 2026-11-16).

The repository records no reason for this choice: no decision record, commit message, code comment or document states
one. This record states the rule as the code keeps it, so that a change to it is decided here.

## Decision

**The secure owners' `Dispose`, `ZeroMemory<T>` and `FixedTimeEquals<T>` zero and compare memory only through
`CryptographicOperations`.**

## Options considered

- `Span<T>.Clear` or `Array.Clear`. Not chosen; no reason is recorded.
- A zeroing or comparison loop of the library's own. Not chosen; no reason is recorded.
- `CryptographicOperations` (chosen).

## Consequences

- The zeroing and timing guarantees are the ones Microsoft documents for those two methods.
- `FixedTimeEquals<T>` compares bytes, so values with more than one encoding (a `double`, a struct with padding) can
  compare unequal.
- The fixed writers' `Reset()` is outside this rule: it clears with `Span<T>.Clear()`, which carries no such guarantee.
  Moving it to `CryptographicOperations` is decided by a change to this record.

## References

- `src/NCode.Buffers/CryptographicOperationsExtensions.cs`
- `src/NCode.Buffers/FixedSpanBufferWriter.cs` and `src/NCode.Buffers/FixedMemoryBufferWriter.cs`
- [CryptographicOperations.ZeroMemory](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory)
- [CryptographicOperations.FixedTimeEquals](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.fixedtimeequals)
