// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using JetBrains.Annotations;

namespace NCode.Buffers;

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
    /// <summary>
    /// The text that <see cref="ToString"/> and the debugger show in place of the secret.
    /// </summary>
    private const string RedactedText = "[REDACTED]";

    /// <summary>
    /// The owner of the sensitive rental, or <see langword="null"/> once the secret is disposed.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IMemoryOwner<T>? _owner;

    /// <summary>
    /// Gets the elements of the secret in the rental.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Memory<T> Value { get; }

    /// <summary>
    /// Initializes a secret that takes ownership of a sensitive rental, which <c>Secret.Create</c> rents and fills.
    /// </summary>
    /// <param name="owner">The owner of the sensitive rental, which this secret disposes.</param>
    /// <param name="memory">The elements of the secret in the rental.</param>
    internal Secret(IMemoryOwner<T> owner, Memory<T> memory)
    {
        _owner = owner;
        Value = memory;
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
        var owner = Volatile.Read(ref _owner);
        ObjectDisposedException.ThrowIf(owner is null, this);
        return Value.Span;
    }

    /// <summary>
    /// Gets the secret as read-only memory, for callers that cannot hold a span.
    /// </summary>
    /// <returns>The secret, valid until this instance is disposed.</returns>
    /// <exception cref="ObjectDisposedException">This instance is disposed.</exception>
    public ReadOnlyMemory<T> RevealMemory()
    {
        var owner = Volatile.Read(ref _owner);
        ObjectDisposedException.ThrowIf(owner is null, this);
        return Value;
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
        var value = Reveal();
        return CryptographicOperationsExtensions.FixedTimeEquals(value, other);
    }

    /// <summary>
    /// Compares the contents of two secrets in a way that does not leak timing information about the contents.
    /// </summary>
    /// <param name="other">The secret to compare to.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="other"/> is this instance, or has the same length and contents;
    /// <see langword="false"/> when <paramref name="other"/> is <see langword="null"/>, also after this instance is
    /// disposed.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// <paramref name="other"/> is not <see langword="null"/>, and this instance or <paramref name="other"/> is
    /// disposed, also when <paramref name="other"/> is this instance.
    /// </exception>
    public bool Equals(Secret<T>? other)
    {
        if (other is null)
            return false;

        var value = Reveal();

        if (ReferenceEquals(this, other))
            return true;

        var otherValue = other.Reveal();
        return CryptographicOperationsExtensions.FixedTimeEquals(value, otherValue);
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">
    /// <paramref name="obj"/> is a <see cref="Secret{T}"/>, and this instance or <paramref name="obj"/> is disposed.
    /// </exception>
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
