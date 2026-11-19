# Architecture

NCode.Buffers is one assembly, `NCode.Buffers`, shipped as one NuGet package. It has no runtime state beyond the
memory pools it creates and one `Meter`, starts no threads, performs no I/O and opens no network connection. It
depends on Nerdbank.Streams, whose `Sequence<T>` the pooled buffer writers return.

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
| `BufferFactory` | Rents buffers, random bytes (`RentRandom`) and creates writers (`CreateContiguousBufferWriter`) and streams (`CreatePooledStream`); `isSensitive` chooses the secure pool or the shared pool |
| `SecureMemoryPool<T>`, `SecureMemory<T>` | A `MemoryPool<T>` of pinned arrays from the Pinned Object Heap, each zeroed with `CryptographicOperations.ZeroMemory` before it is reused or released; buffers up to `PageSize` (4096 bytes) are cached, larger ones are allocated per rent; every rent returns a new `SecureMemory<T>`, whose first `Dispose` zeroes its array and returns it to the pool |
| `Gen2GcCallback` | Calls the pool on the finalizer thread after each generation 2 collection, and after the first one or two collections of any generation once the pool is created, so it drops its cached buffers when the memory load is at or above `HighPressureThreshold` times the GC's high memory load threshold |
| `SecureArrayLifetime<T>`, `SecureSpanLifetime<T>` | `ref struct`s that zero an array they allocate pinned, or a span the caller has, on `Dispose` |
| `FixedSpanBufferWriter<T>`, `FixedMemoryBufferWriter<T>` | `IBufferWriter<T>` over a fixed span or `Memory<T>`, throwing when it would pass its capacity |
| `MemorySegment<T>`, `SequenceExtensions`, `RefSpanLease<T>`, `EmptyMemory<T>` | Build a `ReadOnlySequence<T>` from blocks, and read a `ReadOnlySequence<T>` or a `Sequence<T>` as one span, copying into a rented buffer only when it spans several segments |
| `StringSegments`, `StringExtensions` | Split a string or `ReadOnlyMemory<char>` into segments that slice the original |
| `SecureEncoding` | UTF-8 and ASCII encodings that throw on invalid input instead of substituting characters |
| `SecureEncodingExtensions` | `RentBytes` and `RentChars` on `Encoding` encode or decode into a rented buffer of the exact length |
| `BinaryTextEncoding`, `BinaryTextFormat` | Decode and encode Base64, Base64url and hex into a rented buffer |
| `ContiguousBufferWriter<T>`, `PooledMemoryStream`, `PooledTextBuilder`, `BufferGrowth` | A pooled `IBufferWriter<T>` over one span, a seekable read-write `Stream` over one pooled buffer, and a `ref struct` text builder that can start in a caller's span; each zeroes the buffers it outgrows when it is sensitive, and `BufferGrowth` computes the size each grows to ([ADR-1028](decisions/1028-growable-writers-streams-and-text-builders-zero-each-buffer-they-outgrow.md)) |
| `Secret`, `Secret<T>` | Holds a sensitive rental as a value; `ToString()` and the debugger show `[REDACTED]`, and `Equals` takes fixed time |
| `BufferMetrics`, `SecureMemoryPoolMetrics` | One static `Meter` named `NCode.Buffers` with counters for the secure pool's rentals, returns and trims |
| `CryptographicOperationsExtensions`, `BufferExtensions` | `ZeroMemory` and `FixedTimeEquals` for spans of any unmanaged type, and span helpers |

## Design Rules

- Secret-holding APIs take only unmanaged element types, so a buffer can't hold references the zeroing would miss
  ([ADR-1006](decisions/1006-the-secure-apis-constrain-their-element-type-to-unmanaged.md)).
- Shipping code takes buffers only from `BufferFactory` and encodings only from `SecureEncoding`; analyzers ban the
  alternatives ([ADR-1003](decisions/1003-shipping-code-takes-buffers-and-encodings-through-bufferfactory-and-secureencoding.md)).
- Ownership is explicit: whatever rents memory returns an `IDisposable` owner, and the memory is zeroed and returned only
  through `Dispose`. There are no finalizers on buffers
  ([ADR-1011](decisions/1011-buffers-have-no-finalizer-and-are-zeroed-only-by-dispose.md)).
- Pooled secure buffers are one page, 4096 bytes; a larger request gets a pinned array of its own
  ([ADR-1012](decisions/1012-the-secure-pool-caches-buffers-of-one-4096-byte-page.md)).
- Pinned buffers are allocated without being cleared
  ([ADR-1013](decisions/1013-pinned-buffers-are-allocated-without-being-cleared.md)).
- `SecureSpanLifetime<T>` zeroes the caller's span and does not pin it
  ([ADR-1014](decisions/1014-securespanlifetime-zeroes-the-callers-span-and-does-not-pin-it.md)).
- The secure pool's cache has no size limit, and a Gen2 collection under memory pressure drops it
  ([ADR-1015](decisions/1015-the-secure-pools-cache-has-no-size-limit-and-is-dropped-under-memory-pressure.md)).
- Zeroing and fixed-time comparison call `CryptographicOperations`
  ([ADR-1016](decisions/1016-zeroing-and-fixed-time-comparison-call-cryptographicoperations.md)).
- The pooled buffer writers return Nerdbank.Streams' `Sequence<T>`
  ([ADR-1017](decisions/1017-the-pooled-buffer-writers-return-nerdbank-streams-sequence.md)).
- The library neither locks nor encrypts memory
  ([ADR-1018](decisions/1018-the-library-neither-locks-nor-encrypts-memory.md)).
- Text, Base64, hex and random helpers rent their output from `BufferFactory.Rent` and return its owner
  ([ADR-1027](decisions/1027-text-base64-hex-and-random-helpers-rent-their-output-and-return-its-owner.md)).
- Growable writers, streams and text builders zero each buffer they outgrow
  ([ADR-1028](decisions/1028-growable-writers-streams-and-text-builders-zero-each-buffer-they-outgrow.md)).
- `Secret<T>` holds a secret it never prints
  ([ADR-1029](decisions/1029-secret-t-holds-a-secret-it-never-prints.md)).
- The secure pool reports metrics through one static `Meter`
  ([ADR-1030](decisions/1030-the-secure-pool-reports-metrics-through-one-static-meter.md)).
- The public API is tracked in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, and each pack is validated against
  the last release ([ADR-1001](decisions/1001-each-package-is-validated-against-the-last-released-package.md)).

## Build and Release

`ci.yml` builds, tests and packs every pull request. CodeQL scans the C# code and the workflows through GitHub's code
scanning default setup. Merging a release's pull request makes `tag-release.yml` run `ci.yml` again on the merged
commit and, once it passes, tag that commit, signed with gitsign, and publish the GitHub release, then start
`publish.yml`, which tests and packs the tagged commit, pushes to nuget.org through trusted publishing after a
maintainer approves, and then attaches provenance and opens the baseline pull request (project-template
[ADR-0026](decisions/project-template/0026-tests-gate-coverage-at-the-gold-thresholds-and-a-release-waits-for-them.md),
[ADR-0028](decisions/project-template/0028-github-releases-are-immutable-and-their-tags-signed.md) and
[ADR-0151](decisions/project-template/0151-release-follow-ups-are-jobs-publish-calls.md)).

[assurance-case.md](assurance-case.md) explains why this design meets the library's security requirements.
