# Architecture

NCode.Buffers is one assembly, `NCode.Buffers`, shipped as one NuGet package. It has no runtime state beyond the
memory pools it creates, starts no threads, performs no I/O and opens no network connection. It depends on
Nerdbank.Streams, whose `Sequence<T>` the pooled buffer writers return.

## Components

```text
                     BufferFactory  (the entry point)
            isSensitive: true  │           │  isSensitive: false
                               ▼           ▼
                SecureMemoryPool<T>     MemoryPool<T>.Shared
                  │      ▲                    │
          rents   ▼      │ trims on Gen2 GC   ▼
             SecureMemory<T>  ◄── Gen2GcCallback
          (pinned, zeroed on Dispose)
                               │
         Sequence<T> (Nerdbank.Streams) built on either pool
                               │
          SequenceExtensions ─► RefSpanLease<T>, contiguous owner
```

| Component | Role |
| --- | --- |
| `BufferFactory` | Rents buffers and creates writers; `isSensitive` chooses the secure pool or the shared pool |
| `SecureMemoryPool<T>`, `SecureMemory<T>` | A `MemoryPool<T>` of pinned arrays from the Pinned Object Heap, each zeroed with `CryptographicOperations.ZeroMemory` before it is reused or released; buffers up to `PageSize` (4096 bytes) are cached, larger ones are allocated per rent |
| `Gen2GcCallback` | Calls the pool on each Gen2 collection, so it drops cached buffers when memory load passes `HighPressureThreshold` |
| `SecureArrayLifetime<T>`, `SecureSpanLifetime<T>` | `ref struct`s that zero an array they allocate pinned, or a span the caller has, on `Dispose` |
| `FixedSpanBufferWriter<T>`, `FixedMemoryBufferWriter<T>` | `IBufferWriter<T>` over a fixed span or `Memory<T>`, throwing when it would pass its capacity |
| `MemorySegment<T>`, `SequenceExtensions`, `RefSpanLease<T>`, `EmptyMemory<T>` | Build a `ReadOnlySequence<T>` from blocks, and read a sequence as one span, copying into a rented buffer only when it spans several segments |
| `StringSegments`, `StringExtensions` | Split a string or `ReadOnlyMemory<char>` into segments that slice the original |
| `SecureEncoding` | UTF-8 and ASCII encodings that throw on invalid input instead of substituting characters |
| `CryptographicOperationsExtensions`, `BufferExtensions` | `ZeroMemory` and `FixedTimeEquals` for spans of any unmanaged type, and span helpers |

## Design Rules

- Secret-holding APIs take only unmanaged element types, so a buffer can't hold references the zeroing would miss
  ([ADR-1006](decisions/1006-the-secure-apis-constrain-their-element-type-to-unmanaged.md)).
- Shipping code takes buffers only from `BufferFactory` and encodings only from `SecureEncoding`; analyzers ban the
  alternatives ([ADR-1003](decisions/1003-shipping-code-takes-buffers-and-encodings-through-bufferfactory-and-secureencoding.md)).
- Ownership is explicit: whatever rents memory returns an `IDisposable` owner, and the memory is zeroed and returned only
  through `Dispose`. There are no finalizers on buffers.
- The public API is tracked in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, and each pack is validated against
  the last release ([ADR-1001](decisions/1001-each-package-is-validated-against-the-last-released-package.md)).

## Build and Release

`ci.yml` builds, tests and packs every pull request and runs CodeQL. Merging a release's pull request makes
`tag-release.yml` tag it and publish the GitHub release, then start `publish.yml`, which tests and packs the tagged
commit, pushes to nuget.org through trusted publishing after a maintainer approves, and then attaches provenance and
opens the baseline pull request ([ADR-1008](decisions/1008-release-follow-ups-are-jobs-publish-calls.md)).

[assurance-case.md](assurance-case.md) explains why this design meets the library's security requirements.
