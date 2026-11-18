// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// A ref struct that allocates a pinned array and securely zeroes its memory upon disposal.
/// This struct assumes lifecycle ownership of the allocated array, ensuring sensitive data
/// is cleared when the lifetime ends.
/// </summary>
/// <typeparam name="T">The type of elements in the array. Must be an unmanaged value type.</typeparam>
/// <remarks>
/// <para>
/// This struct allocates a pinned array using <see cref="GC.AllocateUninitializedArray{T}(int, bool)"/>
/// with pinned set to true. This prevents the garbage collector from moving the array in memory,
/// which is essential when working with cryptographic operations or interop scenarios.
/// </para>
/// <para>
/// Since this is a ref struct, it can only be used on the stack and cannot be boxed or stored
/// in fields of reference types. This makes it ideal for short-lived operations with sensitive data.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var lifetime = SecureArrayLifetime&lt;byte&gt;.Create(256);
/// Span&lt;byte&gt; buffer = lifetime;
/// // Use buffer for cryptographic operations
/// // Memory is automatically zeroed when lifetime is disposed
/// </code>
/// </example>
[PublicAPI]
public readonly ref struct SecureArrayLifetime<T> : IDisposable
    where T : unmanaged
{
    /// <summary>
    /// Allocates a pinned array of the specified length and takes ownership of it.
    /// </summary>
    /// <param name="length">The length of the array to allocate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public SecureArrayLifetime(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        PinnedArray = GC.AllocateUninitializedArray<T>(length, pinned: true);
    }

    /// <summary>
    /// Creates a new <see cref="SecureArrayLifetime{T}"/> with the specified length.
    /// </summary>
    /// <param name="length">The length of the array to allocate.</param>
    /// <returns>A new <see cref="SecureArrayLifetime{T}"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Shipped public API; renaming or moving it would be a breaking change."
    )]
    public static SecureArrayLifetime<T> Create(int length) => new(length);

    /// <summary>
    /// Gets the pinned array managed by this lifetime.
    /// </summary>
    /// <value>
    /// A pinned array of type <typeparamref name="T"/> that will be securely zeroed when this lifetime is disposed.
    /// </value>
    /// <remarks>
    /// The array is allocated using <see cref="GC.AllocateUninitializedArray{T}(int, bool)"/>
    /// with pinned set to true, ensuring it will not be moved by the garbage collector.
    /// The array contents are uninitialized and may contain arbitrary data until explicitly written.
    /// </remarks>
    public T[] PinnedArray { get; }

    /// <summary>
    /// Gets the length of the pinned array.
    /// </summary>
    /// <value>
    /// The number of elements in the pinned array.
    /// </value>
    public int Length => PinnedArray.Length;

    /// <summary>
    /// Implicitly converts a <see cref="SecureArrayLifetime{T}"/> to a <typeparamref name="T"/> array.
    /// </summary>
    /// <param name="lifetime">The lifetime to convert.</param>
    /// <returns>The underlying pinned array.</returns>
    /// <remarks>
    /// This conversion provides direct access to the underlying pinned array. The array remains
    /// managed by this lifetime and will be securely zeroed when <see cref="Dispose"/> is called.
    /// </remarks>
    public static implicit operator T[](SecureArrayLifetime<T> lifetime) => lifetime.PinnedArray;

    /// <summary>
    /// Implicitly converts a <see cref="SecureArrayLifetime{T}"/> to a <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="lifetime">The lifetime to convert.</param>
    /// <returns>A span over the pinned array.</returns>
    /// <remarks>
    /// This conversion allows <see cref="SecureArrayLifetime{T}"/> to be used directly in APIs
    /// that expect a <see cref="Span{T}"/> without explicit conversion. The span is over the pinned array itself.
    /// <see cref="Dispose"/> zeroes the array and does not invalidate the span: after it, the span still reads and
    /// writes the same array, which holds zeros. Stop using the span when you dispose the lifetime.
    /// </remarks>
    public static implicit operator Span<T>(SecureArrayLifetime<T> lifetime) =>
        lifetime.PinnedArray.AsSpan();

    /// <summary>
    /// Securely zeroes the memory of the pinned array using cryptographic memory clearing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method uses <see cref="CryptographicOperations.ZeroMemory"/> to ensure the memory
    /// is cleared in a way that cannot be optimized away by the compiler or JIT.
    /// </para>
    /// <para>
    /// It is safe to call this method multiple times; each call will zero the memory again.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(PinnedArray);
    }
}
