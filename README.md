<p align="center">
  <img src="NCodeGroup.png" alt="NCode" width="96" height="96">
</p>

<h1 align="center">NCode.Buffers</h1>

<p align="center">
  Pinned, pooled .NET buffers that zero themselves on dispose, for keys and other secrets, plus fixed-size buffer writers and string splitting without substring copies.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/NCode.Buffers/"><img alt="NuGet version" src="https://img.shields.io/nuget/v/NCode.Buffers?logo=nuget"></a>
  <a href="https://www.nuget.org/packages/NCode.Buffers/"><img alt="NuGet downloads" src="https://img.shields.io/nuget/dt/NCode.Buffers?logo=nuget"></a>
  <a href="https://github.com/NCodeGroup/Buffers/actions/workflows/ci.yml?query=branch%3Amain"><img alt="CI" src="https://github.com/NCodeGroup/Buffers/actions/workflows/ci.yml/badge.svg?branch=main"></a>
  <a href="https://www.nuget.org/packages/NCode.Buffers#supportedframeworks-body-tab"><img alt="Target framework" src="https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FNCodeGroup%2FBuffers%2Fmain%2Fsrc%2FNCode.Buffers%2FNCode.Buffers.csproj&query=%2F%2FTargetFramework%20%7C%20%2F%2FTargetFrameworks&label=target&logo=dotnet&color=512BD4"></a>
  <a href="LICENSE.txt"><img alt="License" src="https://img.shields.io/github/license/NCodeGroup/Buffers"></a>
</p>

## Installation

```shell
dotnet add package NCode.Buffers
```

The target frameworks are the ones the package's `.csproj` declares; they are listed on [nuget.org's Frameworks tab](https://www.nuget.org/packages/NCode.Buffers#supportedframeworks-body-tab) and shown by the badge above. [CHANGELOG.md](CHANGELOG.md) records when a framework is added or dropped and which release line to stay on. The package depends on Nerdbank.Streams, whose `Sequence<T>` the pooled buffer writers return.

## Quick start

This example derives a key from a password, verifies an HMAC-SHA256 signature with it, and zeroes the derived key and the expected MAC when the method returns.

```csharp
using System.Security.Cryptography;
using NCode.Buffers;

static bool VerifySignature(
    ReadOnlySpan<byte> password,
    ReadOnlySpan<byte> salt,
    ReadOnlySpan<byte> payload,
    ReadOnlySpan<byte> signature)
{
    // A pinned, pooled 32-byte key, zeroed when keyOwner is disposed.
    using var keyOwner = BufferFactory.Rent(32, isSensitive: true, out Span<byte> key);
    Rfc2898DeriveBytes.Pbkdf2(password, salt, key, iterations: 600_000, HashAlgorithmName.SHA256);

    // The expected MAC lives on the stack and is zeroed when expectedLifetime is disposed.
    Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
    using var expectedLifetime = expected.GetSecureLifetime();
    HMACSHA256.HashData(key, payload, expected);

    // Constant time, so the comparison doesn't reveal where the MACs differ.
    return CryptographicOperations.FixedTimeEquals(expected, signature);
}
```

See [Choosing a type](#choosing-a-type) for the other buffers.

## Choosing a type

Pick the row that matches your data and where the buffer lives.

| Type | Use it when | Heap or stack | Pooled | Pinned | Zeroed on dispose |
|---|---|---|---|---|---|
| `BufferFactory.Rent(…, isSensitive: true, …)` | You need a temporary buffer for a key, password or other secret | Heap | Yes, up to 4096 bytes | Yes | Yes |
| `SecureMemoryPool<T>.Shared` | An API takes a `MemoryPool<T>` and the data is secret | Heap | Yes, up to 4096 bytes | Yes | Yes |
| `BufferFactory.CreatePinnedArray(…)` or `SecureArrayLifetime<T>` | An API needs a `T[]` that holds a secret, within one synchronous scope | Heap array, stack-only owner | No | Yes | Yes |
| `SecureSpanLifetime<T>` or `span.GetSecureLifetime()` | You already have the memory, usually `stackalloc`, and want it zeroed when the scope ends | The memory you wrap | No | No | Yes |
| `BufferFactory.CreatePooledBufferWriter(isSensitive: true)` | You write a secret whose length you don't know up front | Heap | Yes, segments up to 4096 bytes | Yes | Yes |
| `BufferFactory.Rent(…, isSensitive: false, …)` or `CreatePooledBufferWriter(isSensitive: false)` | The data isn't secret and you want pooling | Heap | Yes | No | No |
| `BufferFactory.CreateArrayBufferWriter<T>()` | The data isn't secret and you want a simple growable writer | Heap | No | No | No |
| `FixedSpanBufferWriter<T>` | You write into a fixed-size span, such as `stackalloc`, in synchronous code | The span you pass | No | No | No |
| `FixedMemoryBufferWriter<T>` | You write into fixed-size `Memory<T>` and keep the writer in a field or across `await` | The memory you pass | No | No | No |

"Up to 4096 bytes" is `SecureMemoryPool<T>.PageSize`, in bytes for every `T`; a larger request gets its own pinned array, which is still zeroed on dispose but not pooled.

Stack memory never moves, so a `stackalloc` buffer needs no pinning.

## Features

### Secure memory

#### BufferFactory

The entry point. `isSensitive: true` takes buffers from `SecureMemoryPool<T>` (pinned, zeroed on dispose), while `isSensitive: false` takes them from `MemoryPool<T>.Shared` (neither). `Rent` hands back a span sliced to exactly the requested length.

```csharp
// A pooled buffer that is pinned and zeroed on dispose, sliced to exactly 256 bytes.
using var owner = BufferFactory.Rent(256, isSensitive: true, out Span<byte> buffer);

// A pinned array, for an API that needs a byte[].
using var lifetime = BufferFactory.CreatePinnedArray(128);
byte[] array = lifetime;

// A writer for a secret of unknown length, built from pinned, pooled segments.
using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
"payload"u8.CopyTo(writer.GetSpan(7));
writer.Advance(7);

// A growable writer for data that isn't secret.
ArrayBufferWriter<byte> arrayWriter = BufferFactory.CreateArrayBufferWriter(1024);
```

#### SecureMemoryPool&lt;T&gt;

A `MemoryPool<T>` whose buffers are pinned and zeroed with `CryptographicOperations.ZeroMemory` when returned. It drops its cached buffers during a Gen2 collection when memory load passes `HighPressureThreshold` (default 0.9).

```csharp
using IMemoryOwner<byte> owner = SecureMemoryPool<byte>.Shared.Rent(1024);
Memory<byte> memory = owner.Memory; // at least 1024 bytes: slice it to the length you need
```

#### SecureArrayLifetime&lt;T&gt; and SecureSpanLifetime&lt;T&gt;

`ref struct`s that zero their memory on `Dispose`. `SecureArrayLifetime<T>` allocates a pinned array, while `SecureSpanLifetime<T>` wraps memory you already have and doesn't pin it.

```csharp
// A pinned array, zeroed when lifetime is disposed.
using var lifetime = SecureArrayLifetime<byte>.Create(256);
Span<byte> buffer = lifetime;

// Memory you already have, zeroed when secure is disposed.
Span<byte> stackBuffer = stackalloc byte[128];
using var secure = stackBuffer.GetSecureLifetime();
```

### Buffer Writers

#### FixedSpanBufferWriter&lt;T&gt; (ref struct)

A stack-only `IBufferWriter<T>` for fixed-size buffers:

```csharp
Span<byte> buffer = stackalloc byte[256];
var writer = new FixedSpanBufferWriter<byte>(buffer);

writer.Write([1, 2, 3, 4]);
var written = writer.WrittenSpan; // Access written data
```

#### FixedMemoryBufferWriter&lt;T&gt; (class)

A heap-storable `IBufferWriter<T>` for fixed-size buffers, usable with async methods:

```csharp
var buffer = new byte[256];
var writer = new FixedMemoryBufferWriter<byte>(buffer);

await JsonSerializer.SerializeAsync(stream, obj, writer);
var written = writer.WrittenMemory;
```

### Sequence Utilities

#### MemorySegment&lt;T&gt;

Build `ReadOnlySequence<T>` from multiple discontiguous memory blocks:

```csharp
var first = new MemorySegment<byte>([1, 2, 3]);
var last = first.Append([4, 5, 6]);
var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
// sequence contains: [1, 2, 3, 4, 5, 6]
```

#### SequenceExtensions

Convert `Sequence<T>` to contiguous spans efficiently:

```csharp
using var writer = BufferFactory.CreatePooledBufferWriter<byte>(isSensitive: true);
// Write data to writer...

// Get a contiguous span (zero-copy for single-segment)
using var lease = writer.GetSpanLease(isSensitive: true);
ProcessData(lease.Span);

// Or consume and transfer ownership
using var owner = writer.ConsumeAsContiguousSpan(isSensitive: true, out var span);
ProcessData(span);
```

#### RefSpanLease&lt;T&gt;

Pairs a `ReadOnlySpan<T>` with its lifetime owner for safe resource management.

#### EmptyMemory&lt;T&gt;

A singleton `IMemoryOwner<T>` for when an empty buffer is needed:

```csharp
IMemoryOwner<byte> buffer = EmptyMemory<byte>.Singleton;
```

### String Splitting

#### StringSegments

Zero-allocation string splitting returning `ReadOnlyMemory<char>` segments:

- **Zero heap allocations** - Unlike `string.Split()`, no arrays allocated
- **Character or string delimiters** - Flexible splitting options
- **Case-sensitive or case-insensitive** - Full `StringComparison` support
- **IReadOnlyCollection** - Full LINQ support

```csharp
// Split on character
foreach (var segment in StringSegments.Split("one,two,three", ','))
{
    Console.WriteLine(segment.Memory.ToString());
}

// Split on string with case-insensitive comparison
var segments = StringSegments.Split("aSEPbSEPc", "sep", StringComparison.OrdinalIgnoreCase);

// Extension methods
foreach (var segment in "a,b,c".SplitSegments(','))
{
    // Process each segment
}
```

### Secure Encoding

#### SecureEncoding

Pre-configured encodings that throw on invalid bytes instead of using replacement characters:

```csharp
// Throws on invalid UTF-8 sequences (no silent replacement)
byte[] bytes = SecureEncoding.UTF8.GetBytes(text);
string text = SecureEncoding.UTF8.GetString(bytes);

// Also available: SecureEncoding.ASCII
```

### Cryptographic Operations Extensions

#### CryptographicOperationsExtensions

Extension methods for `CryptographicOperations` that enable secure memory operations on generic value type spans:

- **ZeroMemory&lt;T&gt;** - Securely zero any value type span (not just bytes)
- **FixedTimeEquals&lt;T&gt;** - Constant-time comparison to prevent timing attacks

```csharp
// Securely zero a span of any value type
Span<int> sensitiveData = stackalloc int[4];
// ... use the sensitive data ...
CryptographicOperations.ZeroMemory(sensitiveData);

// Constant-time comparison for typed spans
ReadOnlySpan<ulong> computedMac = ComputeMac(data);
ReadOnlySpan<ulong> expectedMac = GetExpectedMac();
bool isValid = CryptographicOperations.FixedTimeEquals(computedMac, expectedMac);
```

## References
* [Pinned Object Heap (POH)](https://devblogs.microsoft.com/dotnet/internals-of-the-poh/)
* [Character Encoding with Exception Fallback](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding#exception-fallback)

## Project

- [CONTRIBUTING.md](CONTRIBUTING.md): how to set up, build, test and propose a change.
- [SECURITY.md](SECURITY.md): how to report a vulnerability privately.
- [docs/decisions/](docs/decisions/README.md): the decisions behind the design, the project's and the template's.
- [CHANGELOG.md](CHANGELOG.md): what changed in each release, in the Keep a Changelog format.
- [LICENSE.txt](LICENSE.txt): the license.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE.txt](LICENSE.txt) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Release Notes
See [CHANGELOG.md](CHANGELOG.md) for what changed in each release.
