// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Creates <see cref="Secret{T}"/> instances.
/// </summary>
/// <remarks>
/// The factory is a non-generic class so that <see cref="Secret{T}"/> has no static members.
/// </remarks>
[PublicAPI]
public static class Secret
{
    /// <summary>
    /// Creates a <see cref="Secret{T}"/> that holds a copy of the given value.
    /// </summary>
    /// <typeparam name="T">The unmanaged value type of the elements.</typeparam>
    /// <param name="value">The value to copy into the secret.</param>
    /// <returns>A new <see cref="Secret{T}"/> that the caller must dispose.</returns>
    /// <remarks>
    /// The secret is held in a sensitive buffer from <see cref="BufferFactory"/>, which is zeroed when the secret
    /// is disposed. The caller still owns <paramref name="value"/> and should zero it when it is sensitive.
    /// </remarks>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;byte&gt; key = [1, 2, 3, 4];
    /// using var secret = Secret.Create(key);
    /// bool isMatch = secret.FixedTimeEquals(key);
    /// </code>
    /// </example>
    public static Secret<T> Create<T>(ReadOnlySpan<T> value)
        where T : unmanaged
    {
        var owner = BufferFactory.Rent(value.Length, isSensitive: true, out Memory<T> memory);
        value.CopyTo(memory.Span);
        return new Secret<T>(owner, memory);
    }

    /// <summary>
    /// Creates a <see cref="Secret{T}"/> of the given length that an action fills in place, so the value never
    /// needs a second copy.
    /// </summary>
    /// <typeparam name="T">The unmanaged value type of the elements.</typeparam>
    /// <typeparam name="TState">The type of the state passed to the action.</typeparam>
    /// <param name="length">The number of elements in the secret.</param>
    /// <param name="state">The state passed to <paramref name="action"/>.</param>
    /// <param name="action">The action that fills the secret's buffer.</param>
    /// <returns>A new <see cref="Secret{T}"/> that the caller must dispose.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// If <paramref name="action"/> throws, the buffer is zeroed and returned, and the exception is rethrown.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var secret = Secret.Create&lt;byte, int&gt;(
    ///     16,
    ///     42,
    ///     static (span, seed) =>
    ///     {
    ///         RandomNumberGenerator.Fill(span);
    ///         span[0] = (byte)seed;
    ///     }
    /// );
    /// int length = secret.Length;
    /// </code>
    /// </example>
    public static Secret<T> Create<T, TState>(
        int length,
        TState state,
        SpanAction<T, TState> action
    )
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentNullException.ThrowIfNull(action);

        var owner = BufferFactory.Rent(length, isSensitive: true, out Memory<T> memory);
        try
        {
            action(memory.Span, state);
        }
        catch
        {
            owner.Dispose();
            throw;
        }

        return new Secret<T>(owner, memory);
    }
}
