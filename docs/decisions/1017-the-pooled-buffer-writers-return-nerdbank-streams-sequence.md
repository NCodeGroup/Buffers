# 1017. The pooled buffer writers return Nerdbank.Streams' `Sequence<T>`

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group

## Context

`BufferFactory.CreatePooledBufferWriter` returns `Nerdbank.Streams.Sequence<T>` (`Sequence<byte>` for the overload
without a type argument). It creates the `Sequence<T>` over the pool that `isSensitive` chooses and sets its
`MinimumSpanLength`. `SequenceExtensions.GetSpanLease` and `ConsumeAsContiguousSpan` extend `Sequence<T>`.

`PublicAPI.Shipped.txt` names the type, so the package depends on Nerdbank.Streams: `NCode.Buffers.csproj` references
it without `PrivateAssets`, and its version is in `Directory.Packages.props`. The package's one other dependency is
JetBrains.Annotations.

The repository records no reason for this choice: no decision record, commit message, code comment or document states
one. This record states the rule as the code keeps it, so that a change to it is decided here.

## Decision

**The pooled buffer writer is Nerdbank.Streams' `Sequence<T>`, and the package depends on Nerdbank.Streams.**

## Options considered

- A writer type of the library's own over the same pools. Not chosen; no reason is recorded.
- `Sequence<T>` (chosen).

## Consequences

- Nerdbank.Streams is part of the public API. Replacing it changes the public API, which package validation reports
  ([ADR-1001](1001-each-package-is-validated-against-the-last-released-package.md)) and which Semantic Versioning makes
  a major version.
- Every consumer takes the dependency.

## References

- `src/NCode.Buffers/BufferFactory.cs`
- `src/NCode.Buffers/SequenceExtensions.cs`
- `src/NCode.Buffers/PublicAPI.Shipped.txt`
- [ADR-1001](1001-each-package-is-validated-against-the-last-released-package.md)
