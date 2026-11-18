// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// A ref struct that wraps a <see cref="Span{T}"/> and securely zeroes its memory upon disposal.
/// This struct assumes lifecycle ownership of the span, ensuring sensitive data is cleared
/// when the lifetime ends.
/// </summary>
/// <typeparam name="T">The type of elements in the span. Must be an unmanaged value type.</typeparam>
/// <param name="span">The span to manage the lifetime of.</param>
/// <remarks>
/// <para>
/// This struct is designed for scenarios where you need to work with sensitive data in a span
/// and want to ensure the memory is securely zeroed when you're done with it.
/// </para>
/// <para>
/// Since this is a ref struct, it can only be used on the stack and cannot be boxed or stored
/// in fields of reference types. This makes it ideal for short-lived operations with sensitive data.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Span&lt;byte&gt; buffer = stackalloc byte[256];
/// using var lifetime = new SecureSpanLifetime&lt;byte&gt;(buffer);
/// // Use lifetime.Span to work with the buffer
/// // Memory is automatically zeroed when lifetime is disposed
/// </code>
/// </example>
[PublicAPI]
public readonly ref struct SecureSpanLifetime<T>(Span<T> span) : IDisposable
    where T : unmanaged
{
    /// <summary>
    /// Gets the underlying span managed by this lifetime.
    /// </summary>
    /// <value>
    /// The <see cref="Span{T}"/> that will be securely zeroed when this lifetime is disposed.
    /// </value>
    /// <remarks>
    /// Use this property to read from or write to the underlying memory. <see cref="Dispose"/> zeroes that memory and
    /// does not invalidate the span or release the memory, which belongs to whoever supplied the span: after it, the span
    /// still reads and writes the same memory, which holds zeros until something writes to it. Stop using the span when
    /// you dispose the lifetime.
    /// </remarks>
    public Span<T> Span { get; } = span;

    /// <summary>
    /// Implicitly converts a <see cref="SecureSpanLifetime{T}"/> to its underlying <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="lifetime">The lifetime to convert.</param>
    /// <returns>The underlying span.</returns>
    /// <remarks>
    /// This conversion allows <see cref="SecureSpanLifetime{T}"/> to be used directly in APIs
    /// that expect a <see cref="Span{T}"/> without explicit conversion.
    /// </remarks>
    public static implicit operator Span<T>(SecureSpanLifetime<T> lifetime) => lifetime.Span;

    /// <summary>
    /// Securely zeroes the memory of the underlying span using cryptographic memory clearing.
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
        CryptographicOperations.ZeroMemory(Span);
    }
}
