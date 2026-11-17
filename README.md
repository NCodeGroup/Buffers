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
  <a href="https://codecov.io/gh/NCodeGroup/Buffers"><img alt="Coverage" src="https://codecov.io/gh/NCodeGroup/Buffers/branch/main/graph/badge.svg"></a>
  <a href="https://www.nuget.org/packages/NCode.Buffers#supportedframeworks-body-tab"><img alt="Target framework" src="https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2FNCodeGroup%2FBuffers%2Fmain%2Fsrc%2FNCode.Buffers%2FNCode.Buffers.csproj&query=%2F%2FTargetFramework%20%7C%20%2F%2FTargetFrameworks&label=target&logo=dotnet&color=512BD4"></a>
  <a href="LICENSE.txt"><img alt="License" src="https://img.shields.io/github/license/NCodeGroup/Buffers"></a>
  <a href="https://scorecard.dev/viewer/?uri=github.com/NCodeGroup/Buffers"><img alt="OpenSSF Scorecard" src="https://api.scorecard.dev/projects/github.com/NCodeGroup/Buffers/badge"></a>
  <a href="https://www.bestpractices.dev/projects/16727"><img alt="OpenSSF Best Practices" src="https://www.bestpractices.dev/projects/16727/badge"></a>
</p>

## Installation

```shell
dotnet add package NCode.Buffers
```

The target frameworks are the ones the package's `.csproj` declares; they are listed on [nuget.org's Frameworks tab](https://www.nuget.org/packages/NCode.Buffers#supportedframeworks-body-tab) and shown by the badge above. [CHANGELOG.md](CHANGELOG.md) records when a framework is added or dropped and which release line to stay on. The package depends on Nerdbank.Streams, whose `Sequence<T>` the pooled buffer writers return.

## Quick start

This example derives a key from a password, verifies an HMAC-SHA256 signature with it, and zeroes the derived key and the expected MAC when the method returns.

<!-- snippet: QuickStart -->
```cs
using System.Security.Cryptography;
using NCode.Buffers;

public static class Signatures
{
    public static bool VerifySignature(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> payload,
        ReadOnlySpan<byte> signature
    )
    {
        // A pinned, pooled 32-byte key, zeroed when keyOwner is disposed.
        using var keyOwner = BufferFactory.Rent(32, isSensitive: true, out Span<byte> key);
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            key,
            iterations: 600_000,
            HashAlgorithmName.SHA256
        );

        // The expected MAC lives on the stack and is zeroed when expectedLifetime is disposed.
        Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
        using var expectedLifetime = expected.GetSecureLifetime();
        HMACSHA256.HashData(key, payload, expected);

        // Constant time, so the comparison doesn't reveal where the MACs differ.
        return CryptographicOperations.FixedTimeEquals(expected, signature);
    }
}
```
<!-- endSnippet -->

See [Choosing a type](#choosing-a-type) for the other buffers.

## Choosing a type

Pick the row that matches your data and where the buffer lives. `Rent` and the `Create…` methods are on `BufferFactory`, `GetSecureLifetime` is an extension method on `Span<T>`, and the other rows are types.

### For secrets

Pass `isSensitive: true`. Each of these zeroes its memory on dispose, and all but `GetSecureLifetime`, which wraps memory you already have, pin it so the GC never copies it.

| API | Use it when | Pooled |
|---|---|---|
| `Rent` | You need a temporary buffer for a key, password or other secret | Yes¹ |
| `CreatePooledBufferWriter` | You write a secret whose length you don't know up front | Yes¹ |
| `SecureMemoryPool<T>` | An API takes a `MemoryPool<T>` | Yes¹ |
| `CreatePinnedArray` | An API needs a `T[]`, within one synchronous scope | No |
| `GetSecureLifetime` | You already have the memory, usually `stackalloc` | No |

¹ Up to 4096 bytes, `SecureMemoryPool<T>.PageSize`. A larger buffer gets its own pinned array, still zeroed on dispose.

### For other data

Nothing here is pinned or zeroed.

| API | Use it when | Pooled |
|---|---|---|
| `Rent`, `CreatePooledBufferWriter` | You want pooled buffers: pass `isSensitive: false` | Yes |
| `CreateArrayBufferWriter` | You want a simple growable writer | No |
| `FixedSpanBufferWriter<T>` | You write into a fixed-size span, such as `stackalloc`, in synchronous code | No |
| `FixedMemoryBufferWriter<T>` | You write into fixed-size `Memory<T>` and keep the writer in a field or across `await` | No |

## Features

### Secure memory

#### BufferFactory

The entry point. `isSensitive: true` takes buffers from `SecureMemoryPool<T>` (pinned, zeroed on dispose), while `isSensitive: false` takes them from `MemoryPool<T>.Shared` (neither). `Rent` hands back a span sliced to exactly the requested length.

<!-- snippet: BufferFactory -->
```cs
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
<!-- endSnippet -->

#### SecureMemoryPool&lt;T&gt;

A `MemoryPool<T>` whose buffers are pinned and zeroed with `CryptographicOperations.ZeroMemory` when returned. It drops its cached buffers during a Gen2 collection when memory load passes `HighPressureThreshold` (default 0.9).

<!-- snippet: SecureMemoryPool -->
```cs
using IMemoryOwner<byte> owner = SecureMemoryPool<byte>.Shared.Rent(1024);
Memory<byte> memory = owner.Memory; // at least 1024 bytes: slice it to the length you need
```
<!-- endSnippet -->

#### SecureArrayLifetime&lt;T&gt; and SecureSpanLifetime&lt;T&gt;

`ref struct`s that zero their memory on `Dispose`. `SecureArrayLifetime<T>` allocates a pinned array, while `SecureSpanLifetime<T>` wraps memory you already have and doesn't pin it.

<!-- snippet: SecureLifetime -->
```cs
// A pinned array, zeroed when lifetime is disposed.
using var lifetime = SecureArrayLifetime<byte>.Create(256);
Span<byte> buffer = lifetime;

// Memory you already have, zeroed when secure is disposed.
Span<byte> stackBuffer = stackalloc byte[128];
using var secure = stackBuffer.GetSecureLifetime();
```
<!-- endSnippet -->

### Buffer writers

#### FixedSpanBufferWriter&lt;T&gt;

A `ref struct` `IBufferWriter<T>` over a span you supply. `GetSpan` or `Advance` past its capacity throws `InvalidOperationException`.

<!-- snippet: FixedSpanBufferWriter -->
```cs
Span<byte> buffer = stackalloc byte[256];
var writer = new FixedSpanBufferWriter<byte>(buffer);

"abcd"u8.CopyTo(writer.GetSpan(4));
writer.Advance(4);
ReadOnlySpan<byte> written = writer.WrittenSpan;
```
<!-- endSnippet -->

#### FixedMemoryBufferWriter&lt;T&gt;

The same over `Memory<T>`, as a class you can keep in a field or use across `await`.

<!-- snippet: FixedMemoryBufferWriter -->
```cs
var writer = new FixedMemoryBufferWriter<byte>(new byte[256]);
using (var json = new Utf8JsonWriter(writer))
{
    JsonSerializer.Serialize(json, new { Id = 42, Name = "example" });
}
await stream.WriteAsync(writer.WrittenMemory);
```
<!-- endSnippet -->

### Sequences

#### MemorySegment&lt;T&gt;

Chains memory blocks into a `ReadOnlySequence<T>`.

<!-- snippet: MemorySegment -->
```cs
var first = new MemorySegment<byte>((byte[])[1, 2, 3]);
var last = first.Append((byte[])[4, 5, 6]);
var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
// sequence holds 1, 2, 3, 4, 5, 6
```
<!-- endSnippet -->

#### SequenceExtensions and RefSpanLease&lt;T&gt;

`GetSpanLease` returns a `RefSpanLease<T>`, a span paired with the owner to dispose. Disposing the lease leaves the writer valid when its data is in one segment; when it spans several segments, it only releases the rented copy.

<!-- snippet: GetSpanLease -->
```cs
using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
writer.Write("payload"u8);

// No copy when the data is in one segment; otherwise it is copied into a rented buffer.
using (var lease = writer.GetSpanLease(isSensitive: true))
{
    Process(lease.Span);
}
```
<!-- endSnippet -->

`ConsumeAsContiguousSpan` takes ownership of the data and returns its owner.

<!-- snippet: ConsumeAsContiguousSpan -->
```cs
using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
writer.Write("payload"u8);

// owner now owns the data; don't use writer again.
using var owner = writer.ConsumeAsContiguousSpan(
    isSensitive: true,
    out ReadOnlySpan<byte> span
);
Process(span);
```
<!-- endSnippet -->

#### EmptyMemory&lt;T&gt;

A shared, empty `IMemoryOwner<T>` whose `Dispose` does nothing.

<!-- snippet: EmptyMemory -->
```cs
IMemoryOwner<byte> empty = EmptyMemory<byte>.Singleton;
```
<!-- endSnippet -->

### String splitting

`StringSegments.Split` and the `SplitSegments` extension split a `string` or `ReadOnlyMemory<char>` on a `char` or a string separator, optionally with a `StringComparison`, without copying substrings: each segment's `Memory` is a slice of the original, and the result is an `IReadOnlyCollection<ReadOnlySequenceSegment<char>>`. It isn't zero-allocation: it allocates one small segment object per piece. A separator that matches no text, such as an empty one, returns one segment that holds the whole value, as `string.Split(string)` does.

<!-- snippet: StringSegments -->
```cs
foreach (var segment in "one,two,three".SplitSegments(','))
{
    Console.WriteLine(segment.Memory.ToString());
}

StringSegments segments = StringSegments.Split(
    "aSEPbSEPc",
    "sep",
    StringComparison.OrdinalIgnoreCase
);
int count = segments.Count; // 3
```
<!-- endSnippet -->

### Secure encoding

`SecureEncoding.UTF8` and `SecureEncoding.ASCII` throw `DecoderFallbackException` or `EncoderFallbackException` on invalid input instead of substituting U+FFFD or `?`. `SecureEncoding.UTF8` writes no byte order mark.

<!-- snippet: SecureEncoding -->
```cs
byte[] bytes = SecureEncoding.UTF8.GetBytes("Hello, World!");
string text = SecureEncoding.UTF8.GetString(bytes);

// Invalid UTF-8 throws instead of decoding to U+FFFD, so untrusted input can be rejected.
bool isValid;
try
{
    SecureEncoding.UTF8.GetString((byte[])[0xC3, 0x28]);
    isValid = true;
}
catch (DecoderFallbackException)
{
    isValid = false;
}
```
<!-- endSnippet -->

### CryptographicOperations extensions

`ZeroMemory<T>` and `FixedTimeEquals<T>` work on spans of any unmanaged type, a value type without reference fields at any depth; a `T` with references doesn't compile. These are C# 14 extension members on `CryptographicOperations`, which a project compiles with by default on any framework this package targets; a project that sets an older `LangVersion` calls them on `CryptographicOperationsExtensions` instead, as the sample's last line does.

<!-- snippet: CryptographicOperations -->
```cs
Span<int> sensitiveData = stackalloc int[4];
// ... use the sensitive data ...
CryptographicOperations.ZeroMemory(sensitiveData);

ReadOnlySpan<ulong> computed = ComputeMac(data);
ReadOnlySpan<ulong> expected = GetExpectedMac();
bool isValid = CryptographicOperations.FixedTimeEquals(computed, expected);

// The same methods, callable from any C# version:
CryptographicOperationsExtensions.ZeroMemory(sensitiveData);
```
<!-- endSnippet -->

## Security notes

### Pinning

- Does: sensitive buffers come from `GC.AllocateUninitializedArray<T>(length, pinned: true)` on the Pinned Object Heap, so the GC never moves them and leaves no stale copy at an old address, and native code can rely on the address.
- Does not: lock the memory into RAM. The OS can still write it to a page file, swap or hibernation file.
- Does not: hide the memory from a debugger, a crash dump, or another process that can read this one's memory.
- `SecureSpanLifetime<T>` doesn't pin.

### Zeroing

- Does: `Dispose` zeroes the whole underlying buffer with `CryptographicOperations.ZeroMemory`, which the JIT doesn't remove. That includes the part beyond the slice `Rent` returned.
- Does: zero a pooled buffer before another caller can rent it.
- Does not: run without `Dispose`. There is no finalizer, so always use `using`.
- Does not: clear copies, such as a `string`, a `ToArray()` result, or a buffer rented with `isSensitive: false`.
- A newly allocated buffer is not zeroed when rented, so write before you read.
- Using a span after its owner is disposed reads memory another caller may have rented.

### Comparing and encoding

- `FixedTimeEquals` takes the same time for any contents of equal length, but returns `false` at once when the lengths differ, so it doesn't hide the length.
- A `string` decoded with `SecureEncoding` can't be zeroed. Decode a secret into a sensitive buffer with `GetChars(ReadOnlySpan<byte>, Span<char>)` instead.

### References

- [Pinned Object Heap (POH)](https://devblogs.microsoft.com/dotnet/internals-of-the-poh/)
- [Character Encoding with Exception Fallback](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding#exception-fallback)
- [CryptographicOperations.ZeroMemory](https://learn.microsoft.com/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory)

## Project

- [CONTRIBUTING.md](CONTRIBUTING.md): how to set up, build, test and propose a change.
- [SECURITY.md](SECURITY.md): report a vulnerability privately through the repository's Security tab, never in a public issue.
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [GOVERNANCE.md](GOVERNANCE.md) and [ROADMAP.md](ROADMAP.md): how we work together, who decides, and where the project is going.
- [docs/architecture.md](docs/architecture.md) and [docs/assurance-case.md](docs/assurance-case.md): how the library is built, and why it meets its security requirements.
- [docs/decisions/](docs/decisions/README.md): the decisions behind the design.
- [CHANGELOG.md](CHANGELOG.md): what changed in each release.
- [LICENSE.txt](LICENSE.txt): Apache License 2.0, copyright NCode Group.
