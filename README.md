# NCode.Buffers

A high-performance .NET library providing secure memory management, zero-allocation buffer utilities, and efficient string splitting operations.

[![NuGet](https://img.shields.io/nuget/v/NCode.Buffers.svg)](https://www.nuget.org/packages/NCode.Buffers/)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE.txt)

## Overview

NCode.Buffers provides utilities for:

- **Secure Memory Management** - Pinned buffers that are securely zeroed when disposed, ideal for cryptographic operations
- **Zero-Allocation Buffer Writers** - Fixed-size `IBufferWriter<T>` implementations for high-performance scenarios
- **Memory Pooling** - Efficient buffer pooling with automatic memory pressure handling
- **String Splitting** - Zero-allocation string splitting returning `ReadOnlyMemory<char>` segments
- **Sequence Building** - Tools for building and working with `ReadOnlySequence<T>`

## Features

### Secure Memory Management

#### BufferFactory

A unified API for renting and creating secure memory buffers:

- **Rent buffers** - Rent pooled buffers with optional secure zeroing on return
- **Create pinned arrays** - Allocate GC-pinned arrays that are zeroed on disposal
- **Create pooled buffer writers** - Build sequences incrementally with automatic secure disposal
- **Create array buffer writers** - Simple, non-pooled buffer writers for general-purpose use

```csharp
// Rent a sensitive buffer (pinned + zeroed on dispose)
using var owner = BufferFactory.Rent(256, isSensitive: true, out Span<byte> buffer);
// Use buffer for cryptographic operations

// Create a pinned array
using var lifetime = BufferFactory.CreatePinnedArray(128);
Span<byte> pinnedBuffer = lifetime;

// Create a pooled buffer writer
using var writer = BufferFactory.CreatePooledBufferWriter<byte>(isSensitive: true);
var span = writer.GetSpan(100);
// Write data, then call writer.Advance(bytesWritten)

// Create a simple array buffer writer (non-sensitive data)
var arrayWriter = BufferFactory.CreateArrayBufferWriter<byte>(1024);
```

#### SecureMemoryPool&lt;T&gt;

A memory pool for sensitive data that:

- **Pins buffers** - Prevents GC from moving memory (essential for crypto/interop)
- **Securely zeroes** - Uses `CryptographicOperations.ZeroMemory` on return
- **Auto-trims** - Releases cached memory under high memory pressure

```csharp
using var owner = SecureMemoryPool<byte>.Shared.Rent(1024);
var buffer = owner.Memory.Span;
// Memory is pinned and will be zeroed when owner is disposed
```

#### SecureArrayLifetime&lt;T&gt; / SecureSpanLifetime&lt;T&gt;

Ref structs for stack-only secure memory management:

```csharp
// Pinned array with secure zeroing
using var lifetime = SecureArrayLifetime<byte>.Create(256);
Span<byte> buffer = lifetime;

// Wrap existing span for secure zeroing
Span<byte> stackBuffer = stackalloc byte[128];
using var secure = new SecureSpanLifetime<byte>(stackBuffer);
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

## Installation

```bash
dotnet add package NCode.Buffers
```

## Target Frameworks

- .NET 8.0
- .NET 10.0

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE.txt](LICENSE.txt) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Release Notes
* v4.0.0 - Consolidated from other multiple projects.
* v4.1.0 - Added `CreateArrayBufferWriter` methods to `BufferFactory` for simple, non-pooled buffer writers.
