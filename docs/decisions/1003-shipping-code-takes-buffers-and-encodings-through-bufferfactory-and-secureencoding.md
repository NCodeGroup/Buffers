# 1003. Shipping code takes buffers and encodings through BufferFactory and SecureEncoding

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** NCode Group

## Context

The library exists to make sensitivity explicit at every rental and to give strict encodings. Library code that calls
`Encoding.UTF8`, whose fallback silently replaces invalid bytes, or `MemoryPool<T>.Shared`, which offers no sensitivity
choice, undermines both. Project-template ADR-0102 bans APIs at build time, and the NCode identity repository bans the
same symbols.

## Decision

**`BannedSymbols.Encoding.txt` and `BannedSymbols.Buffers.txt` bind every project not named `*.Tests`, so shipping code
reaches pools, buffer writers and encodings only through `BufferFactory` and `SecureEncoding`.**

1. `Directory.Build.props` adds both lists.
2. `BufferFactory.cs` and `SecureEncoding.cs`, which implement the wrappers, and `SequenceExtensions.cs`, whose
   documentation names the shared pool, disable RS0030 at file scope with the reason on the line.
3. Any other exception is a `#pragma warning disable`/`restore` pair at one site, with a reason.

## Options considered

- No project bans. Rejected: review misses a lenient encoding.
- Bans in tests too. Rejected: tests use the standard encodings and pools as oracles.
- Bans in shipping code (chosen).

## Consequences

- A library file that needs a buffer or an encoding uses the wrappers.
- The lists come from the identity repository and stay in step with it by hand.

## References

- `BannedSymbols.Encoding.txt`, `BannedSymbols.Buffers.txt`, `Directory.Build.props`
- [ADR-0102](project-template/0102-disallowed-apis-are-banned-at-build-time.md): disallowed APIs are banned at build
  time.
