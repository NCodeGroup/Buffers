// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable RS0030 // BufferFactory is the sanctioned wrapper over the shared pools and buffer writers that BannedSymbols.Buffers.txt bans (ADR-1003).

using System.Buffers;
using System.Security.Cryptography;
using JetBrains.Annotations;
using Nerdbank.Streams;

namespace NCode.Buffers;

/// <summary>
/// Provides factory methods for creating and renting secure memory buffers that can be pinned
/// during their lifetime and securely zeroed when disposed.
/// </summary>
/// <remarks>
/// <para>
/// This factory provides a unified API for working with secure memory in cryptographic scenarios.
/// It offers methods to rent buffers from a pool or create pinned arrays with automatic secure disposal.
/// </para>
/// <para>
/// When <c>isSensitive</c> is <c>true</c>, buffers are:
/// <list type="bullet">
/// <item><description>Pinned in memory to prevent the garbage collector from moving them</description></item>
/// <item><description>Securely zeroed using <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory"/> when returned</description></item>
/// </list>
/// </para>
/// <para>
/// When <c>isSensitive</c> is <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Rent a sensitive buffer
/// using var owner = BufferFactory.Rent(256, isSensitive: true, out Span&lt;byte&gt; buffer);
/// // Use buffer for cryptographic operations
/// // Memory is automatically zeroed when owner is disposed
///
/// // Create a pinned array
/// using var lifetime = BufferFactory.CreatePinnedArray(128);
/// Span&lt;byte&gt; pinnedBuffer = lifetime;
/// // Memory is automatically zeroed when lifetime is disposed
///
/// // Create a pooled buffer writer
/// using var writer = BufferFactory.CreatePooledBufferWriter&lt;byte&gt;(isSensitive: true);
/// // Write data to the buffer
/// // All segments are securely zeroed when writer is disposed
/// </code>
/// </example>
[PublicAPI]
public static class BufferFactory
{
    /// <summary>
    /// Selects the appropriate memory pool based on the sensitivity requirement.
    /// </summary>
    /// <typeparam name="T">The type of elements in the pool. Must be an unmanaged value type.</typeparam>
    /// <param name="isSensitive">When <c>true</c>, returns <see cref="SecureMemoryPool{T}.Shared"/>;
    /// otherwise, returns <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <returns>The selected <see cref="MemoryPool{T}"/> instance.</returns>
    internal static MemoryPool<T> ChoosePool<T>(bool isSensitive)
        where T : unmanaged => isSensitive ? SecureMemoryPool<T>.Shared : MemoryPool<T>.Shared;

    /// <summary>
    /// Rents a buffer that is at least the requested length from the appropriate memory pool.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer. Must be an unmanaged value type.</typeparam>
    /// <param name="minBufferSize">The minimum length of the buffer needed.</param>
    /// <param name="isSensitive">When <c>true</c>, the buffer will be pinned during its lifetime and securely zeroed when returned.
    /// When <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="buffer">When this method returns, contains a <see cref="Span{T}"/> sliced to the exact requested size.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minBufferSize"/> is negative.</exception>
    public static IMemoryOwner<T> Rent<T>(int minBufferSize, bool isSensitive, out Span<T> buffer)
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minBufferSize);
        var pool = ChoosePool<T>(isSensitive);
        var lease = pool.Rent(minBufferSize);
        buffer = lease.Memory.Span[..Math.Min(lease.Memory.Length, minBufferSize)];
        return lease;
    }

    /// <summary>
    /// Rents a byte buffer that is at least the requested length from the appropriate memory pool.
    /// </summary>
    /// <param name="minBufferSize">The minimum length of the buffer needed.</param>
    /// <param name="isSensitive">When <c>true</c>, the buffer will be pinned during its lifetime and securely zeroed when returned.
    /// When <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="buffer">When this method returns, contains a <see cref="Span{T}"/> sliced to the exact requested size.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <remarks>
    /// This is a convenience overload for the common case of working with byte buffers.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minBufferSize"/> is negative.</exception>
    public static IMemoryOwner<byte> Rent(
        int minBufferSize,
        bool isSensitive,
        out Span<byte> buffer
    ) => Rent<byte>(minBufferSize, isSensitive, out buffer);

    /// <summary>
    /// Rents a buffer that is at least the requested length from the appropriate memory pool, returning it as <see cref="Memory{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer. Must be an unmanaged value type.</typeparam>
    /// <param name="minBufferSize">The minimum length of the buffer needed.</param>
    /// <param name="isSensitive">When <c>true</c>, the buffer will be pinned during its lifetime and securely zeroed when returned.
    /// When <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="buffer">When this method returns, contains a <see cref="Memory{T}"/> sliced to the exact requested size.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <remarks>
    /// Use this overload when you need a <see cref="Memory{T}"/> instead of a <see cref="Span{T}"/>,
    /// such as when passing the buffer to asynchronous methods.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minBufferSize"/> is negative.</exception>
    public static IMemoryOwner<T> Rent<T>(int minBufferSize, bool isSensitive, out Memory<T> buffer)
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minBufferSize);
        var pool = ChoosePool<T>(isSensitive);
        var lease = pool.Rent(minBufferSize);
        buffer = lease.Memory[..Math.Min(lease.Memory.Length, minBufferSize)];
        return lease;
    }

    /// <summary>
    /// Rents a byte buffer that is at least the requested length from the appropriate memory pool, returning it as <see cref="Memory{T}"/>.
    /// </summary>
    /// <param name="minBufferSize">The minimum length of the buffer needed.</param>
    /// <param name="isSensitive">When <c>true</c>, the buffer will be pinned during its lifetime and securely zeroed when returned.
    /// When <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="buffer">When this method returns, contains a <see cref="Memory{T}"/> sliced to the exact requested size.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <remarks>
    /// This is a convenience overload for the common case of working with byte buffers.
    /// Use this overload when you need a <see cref="Memory{T}"/> instead of a <see cref="Span{T}"/>,
    /// such as when passing the buffer to asynchronous methods.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minBufferSize"/> is negative.</exception>
    public static IMemoryOwner<byte> Rent(
        int minBufferSize,
        bool isSensitive,
        out Memory<byte> buffer
    ) => Rent<byte>(minBufferSize, isSensitive, out buffer);

    /// <summary>
    /// Rents a byte buffer of exactly the requested length and fills it with cryptographically strong random bytes.
    /// </summary>
    /// <param name="length">The exact length of the buffer needed.</param>
    /// <param name="isSensitive">When <c>true</c>, the buffer will be pinned during its lifetime and securely zeroed when returned.
    /// When <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="buffer">When this method returns, contains a <see cref="Span{T}"/> of exactly <paramref name="length"/> random bytes.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <remarks>
    /// Use this method for values such as nonces and salts, which are random but not secret,
    /// by passing <c>false</c> for <paramref name="isSensitive"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var owner = BufferFactory.RentRandom(32, isSensitive: true, out Span&lt;byte&gt; buffer);
    /// // buffer holds 32 random bytes
    /// // Memory is automatically zeroed when owner is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative.</exception>
    public static IMemoryOwner<byte> RentRandom(int length, bool isSensitive, out Span<byte> buffer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var owner = Rent(length, isSensitive, out buffer);
        RandomNumberGenerator.Fill(buffer);
        return owner;
    }

    /// <summary>
    /// Rents a byte buffer of exactly the requested length and fills it with cryptographically strong random bytes,
    /// returning it as <see cref="Memory{T}"/>.
    /// </summary>
    /// <param name="length">The exact length of the buffer needed.</param>
    /// <param name="isSensitive">When <c>true</c>, the buffer will be pinned during its lifetime and securely zeroed when returned.
    /// When <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="buffer">When this method returns, contains a <see cref="Memory{T}"/> of exactly <paramref name="length"/> random bytes.</param>
    /// <returns>
    /// An <see cref="IMemoryOwner{T}"/> that manages the lifetime of the rented buffer.
    /// Dispose this owner to return the buffer to the pool.
    /// </returns>
    /// <remarks>
    /// Use this overload when you need a <see cref="Memory{T}"/> instead of a <see cref="Span{T}"/>,
    /// such as when passing the buffer to asynchronous methods.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var owner = BufferFactory.RentRandom(32, isSensitive: true, out Memory&lt;byte&gt; buffer);
    /// // buffer holds 32 random bytes
    /// // Memory is automatically zeroed when owner is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative.</exception>
    public static IMemoryOwner<byte> RentRandom(
        int length,
        bool isSensitive,
        out Memory<byte> buffer
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var owner = Rent(length, isSensitive, out buffer);
        RandomNumberGenerator.Fill(buffer.Span);
        return owner;
    }

    /// <summary>
    /// Creates a pinned array of the specified type wrapped in a <see cref="SecureArrayLifetime{T}"/> that securely zeroes the memory upon disposal.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array. Must be an unmanaged value type.</typeparam>
    /// <param name="length">The length of the array to allocate.</param>
    /// <returns>
    /// A <see cref="SecureArrayLifetime{T}"/> that manages the lifetime of the pinned array and ensures
    /// the memory is securely zeroed when disposed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned array is allocated using <see cref="GC.AllocateUninitializedArray{T}(int, bool)"/>
    /// with pinned set to true, ensuring the garbage collector will not move it in memory.
    /// This is essential for cryptographic operations or interop scenarios.
    /// </para>
    /// <para>
    /// Since <see cref="SecureArrayLifetime{T}"/> is a ref struct, it can only be used on the stack
    /// and cannot be stored in fields of reference types.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var lifetime = BufferFactory.CreatePinnedArray&lt;int&gt;(64);
    /// Span&lt;int&gt; buffer = lifetime;
    /// // Use buffer for operations requiring pinned memory
    /// // Memory is automatically zeroed when lifetime is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative.</exception>
    public static SecureArrayLifetime<T> CreatePinnedArray<T>(int length)
        where T : unmanaged => SecureArrayLifetime<T>.Create(length);

    /// <summary>
    /// Creates a pinned byte array wrapped in a <see cref="SecureArrayLifetime{T}"/> that securely zeroes the memory upon disposal.
    /// </summary>
    /// <param name="length">The length of the array to allocate.</param>
    /// <returns>
    /// A <see cref="SecureArrayLifetime{T}"/> that manages the lifetime of the pinned array and ensures
    /// the memory is securely zeroed when disposed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for the common case of working with byte arrays.
    /// The returned array is allocated using <see cref="GC.AllocateUninitializedArray{T}(int, bool)"/>
    /// with pinned set to true, ensuring the garbage collector will not move it in memory.
    /// This is essential for cryptographic operations or interop scenarios.
    /// </para>
    /// <para>
    /// Since <see cref="SecureArrayLifetime{T}"/> is a ref struct, it can only be used on the stack
    /// and cannot be stored in fields of reference types.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var lifetime = BufferFactory.CreatePinnedArray(256);
    /// Span&lt;byte&gt; buffer = lifetime;
    /// // Use buffer for cryptographic operations
    /// // Memory is automatically zeroed when lifetime is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative.</exception>
    public static SecureArrayLifetime<byte> CreatePinnedArray(int length) =>
        CreatePinnedArray<byte>(length);

    /// <summary>
    /// Creates a new <see cref="Sequence{T}"/> buffer writer for building sequences of data incrementally.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer. Must be an unmanaged value type.</typeparam>
    /// <param name="isSensitive">When <c>true</c>, the buffer will use secure memory that is pinned during its lifetime and securely zeroed when disposed.
    /// When <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="minimumSpanLength">The minimum length for each span segment allocated by the buffer writer.
    /// A value of 0 (the default) uses the pool's default segment size.</param>
    /// <returns>
    /// A new <see cref="Sequence{T}"/> instance that implements <see cref="IBufferWriter{T}"/>
    /// and will securely zero all memory when disposed (if <paramref name="isSensitive"/> is <c>true</c>).
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned <see cref="Sequence{T}"/> implements <see cref="IBufferWriter{T}"/> and can be used
    /// with APIs that write to buffer writers, such as <see cref="System.Text.Json.Utf8JsonWriter"/>.
    /// </para>
    /// <para>
    /// When <paramref name="isSensitive"/> is <c>true</c>, the buffer writer allocates memory from
    /// <see cref="SecureMemoryPool{T}"/> and ensures all segments are securely zeroed using
    /// <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory"/> when disposed.
    /// </para>
    /// <para>
    /// Use this method when you need to build up a sequence of sensitive data incrementally,
    /// such as when constructing cryptographic payloads or serializing sensitive objects.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var writer = BufferFactory.CreatePooledBufferWriter&lt;byte&gt;(isSensitive: true, minimumSpanLength: 256);
    /// var span = writer.GetSpan(100);
    /// // Write data to span
    /// writer.Advance(100);
    /// // Access the written data
    /// ReadOnlySequence&lt;byte&gt; data = writer.AsReadOnlySequence;
    /// // All memory is securely zeroed when writer is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumSpanLength"/> is negative.</exception>
    public static Sequence<T> CreatePooledBufferWriter<T>(
        bool isSensitive,
        int minimumSpanLength = 0
    )
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumSpanLength);
        return new(ChoosePool<T>(isSensitive)) { MinimumSpanLength = minimumSpanLength };
    }

    /// <summary>
    /// Creates a new <see cref="Sequence{T}"/> buffer writer for building sequences of byte data incrementally.
    /// </summary>
    /// <param name="isSensitive">When <c>true</c>, the buffer will use secure memory that is pinned during its lifetime and securely zeroed when disposed.
    /// When <c>false</c>, this implementation delegates to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="minimumSpanLength">The minimum length for each span segment allocated by the buffer writer.
    /// A value of 0 (the default) uses the pool's default segment size.</param>
    /// <returns>
    /// A new <see cref="Sequence{T}"/> instance that implements <see cref="IBufferWriter{T}"/>
    /// and will securely zero all memory when disposed (if <paramref name="isSensitive"/> is <c>true</c>).
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for the common case of working with byte buffers.
    /// </para>
    /// <para>
    /// When <paramref name="isSensitive"/> is <c>true</c>, the buffer writer allocates memory from
    /// <see cref="SecureMemoryPool{T}"/> and ensures all segments are securely zeroed using
    /// <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory"/> when disposed.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true, minimumSpanLength: 256);
    /// var span = writer.GetSpan(100);
    /// // Write sensitive byte data to span
    /// writer.Advance(100);
    /// // Access the written data
    /// ReadOnlySequence&lt;byte&gt; data = writer.AsReadOnlySequence;
    /// // All memory is securely zeroed when writer is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumSpanLength"/> is negative.</exception>
    public static Sequence<byte> CreatePooledBufferWriter(
        bool isSensitive,
        int minimumSpanLength = 0
    ) => CreatePooledBufferWriter<byte>(isSensitive, minimumSpanLength);

    /// <summary>
    /// Creates a new <see cref="ContiguousBufferWriter{T}"/> that keeps everything written in one contiguous buffer.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer. Must be an unmanaged type.</typeparam>
    /// <param name="isSensitive">When <c>true</c>, the writer rents its buffers from <see cref="SecureMemoryPool{T}"/>,
    /// which pins them and zeroes them when they are disposed, including the old buffer on every growth.</param>
    /// <param name="initialCapacity">The size of the first buffer the writer rents, which is also the smallest size
    /// it rents when it grows. A value of 0 (the default) uses 256 elements.</param>
    /// <returns>
    /// A new <see cref="ContiguousBufferWriter{T}"/>. It rents no buffer until the first call to
    /// <see cref="ContiguousBufferWriter{T}.GetSpan"/> or <see cref="ContiguousBufferWriter{T}.GetMemory"/>.
    /// </returns>
    /// <remarks>
    /// Use this method instead of <see cref="CreatePooledBufferWriter{T}"/> when the consumer needs the written data
    /// as a single span or memory rather than as a sequence.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var writer = BufferFactory.CreateContiguousBufferWriter&lt;byte&gt;(isSensitive: true, initialCapacity: 64);
    /// var span = writer.GetSpan(16);
    /// // Write data to span
    /// writer.Advance(16);
    /// // Access the written data as one span
    /// ReadOnlySpan&lt;byte&gt; data = writer.WrittenSpan;
    /// // All memory is securely zeroed when writer is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialCapacity"/> is negative.</exception>
    public static ContiguousBufferWriter<T> CreateContiguousBufferWriter<T>(
        bool isSensitive,
        int initialCapacity = 0
    )
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
        return new ContiguousBufferWriter<T>(isSensitive, initialCapacity);
    }

    /// <summary>
    /// Creates a new <see cref="ContiguousBufferWriter{T}"/> of bytes that keeps everything written in one contiguous buffer.
    /// </summary>
    /// <param name="isSensitive">When <c>true</c>, the writer rents its buffers from <see cref="SecureMemoryPool{T}"/>,
    /// which pins them and zeroes them when they are disposed, including the old buffer on every growth.</param>
    /// <param name="initialCapacity">The size of the first buffer the writer rents, which is also the smallest size
    /// it rents when it grows. A value of 0 (the default) uses 256 elements.</param>
    /// <returns>
    /// A new <see cref="ContiguousBufferWriter{T}"/> of <see cref="byte"/>. It rents no buffer until the first call to
    /// <see cref="ContiguousBufferWriter{T}.GetSpan"/> or <see cref="ContiguousBufferWriter{T}.GetMemory"/>.
    /// </returns>
    /// <remarks>
    /// This is a convenience overload for the common case of working with byte buffers.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true, initialCapacity: 64);
    /// var span = writer.GetSpan(16);
    /// // Write sensitive byte data to span
    /// writer.Advance(16);
    /// // Access the written data as one span
    /// ReadOnlySpan&lt;byte&gt; data = writer.WrittenSpan;
    /// // All memory is securely zeroed when writer is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialCapacity"/> is negative.</exception>
    public static ContiguousBufferWriter<byte> CreateContiguousBufferWriter(
        bool isSensitive,
        int initialCapacity = 0
    ) => CreateContiguousBufferWriter<byte>(isSensitive, initialCapacity);

    /// <summary>
    /// Creates a new <see cref="PooledMemoryStream"/>, a seekable, readable and writable <see cref="Stream"/> over one
    /// pooled buffer.
    /// </summary>
    /// <param name="isSensitive">When <c>true</c>, the stream rents its buffers from <see cref="SecureMemoryPool{T}"/>,
    /// which pins them and zeroes them when they are disposed, including the old buffer on every growth.</param>
    /// <param name="initialCapacity">The size of the first buffer the stream rents, which is also the smallest size
    /// it rents when it grows. A value of 0 (the default) uses 256 bytes.</param>
    /// <returns>
    /// A new <see cref="PooledMemoryStream"/>. It rents no buffer until the first write or call to
    /// <see cref="PooledMemoryStream.SetLength"/> that needs room.
    /// </returns>
    /// <remarks>
    /// Use this method instead of <see cref="MemoryStream"/> when the stream holds sensitive data or should not
    /// allocate its buffer on the managed heap.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var stream = BufferFactory.CreatePooledStream(isSensitive: true, initialCapacity: 64);
    /// stream.Write("secret"u8);
    /// // Access the written data as one span
    /// ReadOnlySpan&lt;byte&gt; data = stream.WrittenSpan;
    /// // All memory is securely zeroed when the stream is disposed
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialCapacity"/> is negative.</exception>
    public static PooledMemoryStream CreatePooledStream(bool isSensitive, int initialCapacity = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
        return new PooledMemoryStream(isSensitive, initialCapacity);
    }

    /// <summary>
    /// Creates a new <see cref="ArrayBufferWriter{T}"/> with the default initial capacity.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer. Must be a value type.</typeparam>
    /// <returns>
    /// A new <see cref="ArrayBufferWriter{T}"/> instance that implements <see cref="IBufferWriter{T}"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned <see cref="ArrayBufferWriter{T}"/> is a simple, non-pooled buffer writer that uses
    /// a single contiguous array that grows as needed. Unlike <see cref="CreatePooledBufferWriter{T}"/>,
    /// this method does not provide secure memory handling.
    /// </para>
    /// <para>
    /// Use this method when you need a simple buffer writer for non-sensitive data and do not require
    /// the overhead of memory pooling.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var writer = BufferFactory.CreateArrayBufferWriter&lt;int&gt;();
    /// var span = writer.GetSpan(10);
    /// // Write data to span
    /// writer.Advance(10);
    /// // Access the written data
    /// ReadOnlySpan&lt;int&gt; data = writer.WrittenSpan;
    /// </code>
    /// </example>
    public static ArrayBufferWriter<T> CreateArrayBufferWriter<T>()
        where T : struct => new();

    /// <summary>
    /// Creates a new <see cref="ArrayBufferWriter{T}"/> with the specified initial capacity.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer. Must be a value type.</typeparam>
    /// <param name="initialCapacity">The initial capacity of the underlying buffer.</param>
    /// <returns>
    /// A new <see cref="ArrayBufferWriter{T}"/> instance that implements <see cref="IBufferWriter{T}"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned <see cref="ArrayBufferWriter{T}"/> is a simple, non-pooled buffer writer that uses
    /// a single contiguous array. Specifying an appropriate initial capacity can help reduce reallocations
    /// when the expected data size is known in advance.
    /// </para>
    /// <para>
    /// Unlike <see cref="CreatePooledBufferWriter{T}"/>, this method does not provide secure memory handling.
    /// Use this method when you need a simple buffer writer for non-sensitive data.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var writer = BufferFactory.CreateArrayBufferWriter&lt;int&gt;(1024);
    /// var span = writer.GetSpan(100);
    /// // Write data to span
    /// writer.Advance(100);
    /// // Access the written data
    /// ReadOnlySpan&lt;int&gt; data = writer.WrittenSpan;
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialCapacity"/> is not positive.</exception>
    public static ArrayBufferWriter<T> CreateArrayBufferWriter<T>(int initialCapacity)
        where T : struct
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        return new(initialCapacity);
    }

    /// <summary>
    /// Creates a new <see cref="ArrayBufferWriter{T}"/> for bytes with the default initial capacity.
    /// </summary>
    /// <returns>
    /// A new <see cref="ArrayBufferWriter{T}"/> instance that implements <see cref="IBufferWriter{T}"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for the common case of working with byte buffers.
    /// The returned <see cref="ArrayBufferWriter{T}"/> is a simple, non-pooled buffer writer that uses
    /// a single contiguous array that grows as needed.
    /// </para>
    /// <para>
    /// Unlike <see cref="CreatePooledBufferWriter(bool, int)"/>, this method does not provide secure memory handling.
    /// Use this method when you need a simple buffer writer for non-sensitive byte data.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var writer = BufferFactory.CreateArrayBufferWriter();
    /// var span = writer.GetSpan(100);
    /// // Write byte data to span
    /// writer.Advance(100);
    /// // Access the written data
    /// ReadOnlySpan&lt;byte&gt; data = writer.WrittenSpan;
    /// </code>
    /// </example>
    public static ArrayBufferWriter<byte> CreateArrayBufferWriter() => new();

    /// <summary>
    /// Creates a new <see cref="ArrayBufferWriter{T}"/> for bytes with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the underlying buffer.</param>
    /// <returns>
    /// A new <see cref="ArrayBufferWriter{T}"/> instance that implements <see cref="IBufferWriter{T}"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for the common case of working with byte buffers.
    /// Specifying an appropriate initial capacity can help reduce reallocations when the expected
    /// data size is known in advance.
    /// </para>
    /// <para>
    /// Unlike <see cref="CreatePooledBufferWriter(bool, int)"/>, this method does not provide secure memory handling.
    /// Use this method when you need a simple buffer writer for non-sensitive byte data.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var writer = BufferFactory.CreateArrayBufferWriter(1024);
    /// var span = writer.GetSpan(100);
    /// // Write byte data to span
    /// writer.Advance(100);
    /// // Access the written data
    /// ReadOnlySpan&lt;byte&gt; data = writer.WrittenSpan;
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialCapacity"/> is not positive.</exception>
    public static ArrayBufferWriter<byte> CreateArrayBufferWriter(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        return new(initialCapacity);
    }
}
