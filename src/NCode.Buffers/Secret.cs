// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
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

/// <summary>
/// Holds a secret value in a sensitive buffer, hides it from <see cref="ToString"/> and the debugger, compares it
/// in fixed time, and zeroes it when disposed.
/// </summary>
/// <typeparam name="T">The unmanaged value type of the elements.</typeparam>
/// <remarks>
/// <para>
/// <see cref="ToString"/> and the debugger display show <c>[REDACTED]</c>, so a secret that is logged or inspected
/// does not leak. <see cref="Reveal"/> is the one way to read the secret.
/// </para>
/// <para>
/// A secret lives in memory only. It is not serializable and it is not key storage.
/// </para>
/// <para>
/// A secret is not thread-safe against <see cref="Dispose"/>: do not read it on one thread while another disposes it.
/// After <see cref="Dispose"/>, the members that read the secret throw <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ReadOnlySpan&lt;byte&gt; key = [1, 2, 3, 4];
/// using var secret = Secret.Create(key);
///
/// // Prints [REDACTED], never the bytes.
/// string text = secret.ToString();
///
/// // Reveal is the one way to read the secret.
/// ReadOnlySpan&lt;byte&gt; bytes = secret.Reveal();
/// </code>
/// </example>
[PublicAPI]
[DebuggerDisplay("[REDACTED]")]
public sealed class Secret<T> : IDisposable, IEquatable<Secret<T>>
    where T : unmanaged
{
    private const string RedactedText = "[REDACTED]";

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IMemoryOwner<T>? _owner;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Memory<T> _memory;

    internal Secret(IMemoryOwner<T> owner, Memory<T> memory)
    {
        _owner = owner;
        _memory = memory;
        Length = memory.Length;
    }

    /// <summary>
    /// Gets the number of elements in the secret.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public int Length { get; }

    /// <summary>
    /// Gets the secret as a read-only span. This is the one way to read the secret.
    /// </summary>
    /// <returns>The secret, valid until this instance is disposed.</returns>
    /// <exception cref="ObjectDisposedException">This instance is disposed.</exception>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;byte&gt; key = [1, 2, 3, 4];
    /// using var secret = Secret.Create(key);
    /// ReadOnlySpan&lt;byte&gt; bytes = secret.Reveal();
    /// </code>
    /// </example>
    public ReadOnlySpan<T> Reveal()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owner) is null, this);
        return _memory.Span;
    }

    /// <summary>
    /// Gets the secret as read-only memory, for callers that cannot hold a span.
    /// </summary>
    /// <returns>The secret, valid until this instance is disposed.</returns>
    /// <exception cref="ObjectDisposedException">This instance is disposed.</exception>
    public ReadOnlyMemory<T> RevealMemory()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _owner) is null, this);
        return _memory;
    }

    /// <summary>
    /// Copies the secret to a destination span.
    /// </summary>
    /// <param name="destination">The span that receives the copy.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <see cref="Length"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance is disposed.</exception>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;byte&gt; key = [1, 2, 3, 4];
    /// using var secret = Secret.Create(key);
    /// Span&lt;byte&gt; copy = stackalloc byte[4];
    /// secret.CopyTo(copy);
    /// CryptographicOperations.ZeroMemory(copy);
    /// </code>
    /// </example>
    public void CopyTo(Span<T> destination)
    {
        Reveal().CopyTo(destination);
    }

    /// <summary>
    /// Compares the secret to a span in a way that does not leak timing information about the contents.
    /// </summary>
    /// <param name="other">The span to compare to.</param>
    /// <returns><see langword="true"/> when <paramref name="other"/> has the same length and contents.</returns>
    /// <exception cref="ObjectDisposedException">This instance is disposed.</exception>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;byte&gt; key = [1, 2, 3, 4];
    /// using var secret = Secret.Create(key);
    /// ReadOnlySpan&lt;byte&gt; received = [1, 2, 3, 4];
    /// bool isMatch = secret.FixedTimeEquals(received);
    /// </code>
    /// </example>
    public bool FixedTimeEquals(ReadOnlySpan<T> other)
    {
        return CryptographicOperationsExtensions.FixedTimeEquals(Reveal(), other);
    }

    /// <summary>
    /// Compares the contents of two secrets in a way that does not leak timing information about the contents.
    /// </summary>
    /// <param name="other">The secret to compare to.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="other"/> is this instance, or has the same length and contents;
    /// <see langword="false"/> when <paramref name="other"/> is <see langword="null"/>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">This instance or <paramref name="other"/> is disposed.</exception>
    public bool Equals(Secret<T>? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return CryptographicOperationsExtensions.FixedTimeEquals(Reveal(), other.Reveal());
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as Secret<T>);
    }

    /// <summary>
    /// Returns the length of the secret, so that the hash code reveals nothing about the contents.
    /// </summary>
    /// <returns>The value of <see cref="Length"/>.</returns>
    public override int GetHashCode()
    {
        return Length;
    }

    /// <summary>
    /// Returns <c>[REDACTED]</c>, never the secret.
    /// </summary>
    /// <returns>The text <c>[REDACTED]</c>.</returns>
    public override string ToString()
    {
        return RedactedText;
    }

    /// <summary>
    /// Zeroes the secret and returns its buffer. Calling this method more than once has no further effect.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _owner, null)?.Dispose();
    }
}
