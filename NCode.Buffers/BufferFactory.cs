#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
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
[ExcludeFromCodeCoverage]
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
        where T : struct
        => isSensitive ? SecureMemoryPool<T>.Shared : MemoryPool<T>.Shared;

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
        where T : struct
    {
        var pool = ChoosePool<T>(isSensitive);
        var lease = pool.Rent(minBufferSize);
        try
        {
            buffer = lease.Memory.Span[..Math.Min(lease.Memory.Length, minBufferSize)];
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
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
    public static IMemoryOwner<byte> Rent(int minBufferSize, bool isSensitive, out Span<byte> buffer)
        => Rent<byte>(minBufferSize, isSensitive, out buffer);

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
        where T : struct
    {
        var pool = ChoosePool<T>(isSensitive);
        var lease = pool.Rent(minBufferSize);
        try
        {
            buffer = lease.Memory[..Math.Min(lease.Memory.Length, minBufferSize)];
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
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
    public static IMemoryOwner<byte> Rent(int minBufferSize, bool isSensitive, out Memory<byte> buffer)
        => Rent<byte>(minBufferSize, isSensitive, out buffer);

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
        where T : struct
        => SecureArrayLifetime<T>.Create(length);

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
    public static SecureArrayLifetime<byte> CreatePinnedArray(int length)
        => CreatePinnedArray<byte>(length);

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
    public static Sequence<T> CreatePooledBufferWriter<T>(bool isSensitive, int minimumSpanLength = 0)
        where T : struct
        => new(ChoosePool<T>(isSensitive))
        {
            MinimumSpanLength = minimumSpanLength
        };

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
    public static Sequence<byte> CreatePooledBufferWriter(bool isSensitive, int minimumSpanLength = 0)
        => CreatePooledBufferWriter<byte>(isSensitive, minimumSpanLength);

    /// <summary>
    /// Creates a new <see cref="ArrayBufferWriter{T}"/> with the default initial capacity.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer. Must be an unmanaged value type.</typeparam>
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
    /// using var writer = BufferFactory.CreateArrayBufferWriter&lt;int&gt;();
    /// var span = writer.GetSpan(10);
    /// // Write data to span
    /// writer.Advance(10);
    /// // Access the written data
    /// ReadOnlySpan&lt;int&gt; data = writer.WrittenSpan;
    /// </code>
    /// </example>
    public static ArrayBufferWriter<T> CreateArrayBufferWriter<T>()
        where T : struct
        => new();

    /// <summary>
    /// Creates a new <see cref="ArrayBufferWriter{T}"/> with the specified initial capacity.
    /// </summary>
    /// <typeparam name="T">The type of elements in the buffer. Must be an unmanaged value type.</typeparam>
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
    /// using var writer = BufferFactory.CreateArrayBufferWriter&lt;int&gt;(1024);
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
        => new(initialCapacity);

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
    /// using var writer = BufferFactory.CreateArrayBufferWriter();
    /// var span = writer.GetSpan(100);
    /// // Write byte data to span
    /// writer.Advance(100);
    /// // Access the written data
    /// ReadOnlySpan&lt;byte&gt; data = writer.WrittenSpan;
    /// </code>
    /// </example>
    public static ArrayBufferWriter<byte> CreateArrayBufferWriter()
        => new();

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
    /// using var writer = BufferFactory.CreateArrayBufferWriter(1024);
    /// var span = writer.GetSpan(100);
    /// // Write byte data to span
    /// writer.Advance(100);
    /// // Access the written data
    /// ReadOnlySpan&lt;byte&gt; data = writer.WrittenSpan;
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialCapacity"/> is not positive.</exception>
    public static ArrayBufferWriter<byte> CreateArrayBufferWriter(int initialCapacity)
        => new(initialCapacity);
}
