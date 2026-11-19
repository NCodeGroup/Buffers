// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// A growable builder for text that keeps its characters in a pooled buffer, and zeroes them when it releases the
/// buffer if it is sensitive.
/// </summary>
/// <remarks>
/// <para>
/// This is a <c>ref struct</c>, so it lives on the stack, it can't be a field of a class, and it can't cross an
/// <c>await</c>. Pass it by <c>ref</c> and never copy it: a copy shares the buffer, so disposing one copy leaves the
/// other one pointing at a released buffer.
/// </para>
/// <para>
/// The builder starts with the buffer that you pass to the constructor, or with a buffer rented from the pool that
/// <c>isSensitive</c> selects, or with no buffer at all. When the buffer is too small, the builder rents a larger one
/// from that pool, copies the written part into it, and only then releases the old one. A sensitive builder zeroes the
/// old buffer. When the old buffer is a span that you passed to the constructor, the builder zeroes all of it, not only
/// the written part, because a failed <see cref="ISpanFormattable.TryFormat"/> can leave characters past the written
/// part. The same holds for <see cref="Dispose"/>. A <see cref="Span{T}"/> that <see cref="AppendSpan"/> returned is
/// therefore not valid after the next call that appends.
/// </para>
/// <para>
/// <see cref="WrittenSpan"/> stays valid when you append it to the builder, because the builder copies it before it
/// releases the old buffer.
/// </para>
/// <para>
/// Call <see cref="Reset"/> to zero the written part and reuse the buffer, and <see cref="Dispose"/> to release the
/// buffer. After <see cref="Dispose"/>, the builder is empty and has no buffer, and an append rents a new one.
/// </para>
/// <para>
/// <see cref="ToString"/> makes a <see cref="string"/>, and a <see cref="string"/> can't be zeroed, so call it only
/// when the text isn't sensitive. Use <see cref="WrittenSpan"/> for sensitive text.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var builder = new PooledTextBuilder(stackalloc char[64], isSensitive: true);
///
/// builder.Append("Password=");
/// builder.Append(42);
///
/// // Use the text as a span, so it never becomes a string
/// ReadOnlySpan&lt;char&gt; text = builder.WrittenSpan;
/// </code>
/// </example>
[PublicAPI]
public ref struct PooledTextBuilder
{
    private const int MinimumGrowCapacity = 256;

    private IMemoryOwner<char>? _owner;
    private Span<char> _chars;
    private int _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledTextBuilder"/> struct with a rented buffer.
    /// </summary>
    /// <param name="isSensitive">
    /// <c>true</c> to rent the buffers from the secure pool, which zeroes them when they are released; otherwise,
    /// <c>false</c>.
    /// </param>
    /// <param name="initialCapacity">
    /// The number of characters to rent up front. When it is 0, the builder rents its first buffer on the first append.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> is negative.</exception>
#pragma warning disable RS0027 // The (Span<char>, bool) constructor has the same number of parameters but can't take the optional one (D7).
    public PooledTextBuilder(bool isSensitive, int initialCapacity = 0)
#pragma warning restore RS0027
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        IsSensitive = isSensitive;
        _owner = null;
        _chars = default;
        _length = 0;

        if (initialCapacity > 0)
        {
            _owner = BufferFactory.ChoosePool<char>(isSensitive).Rent(initialCapacity);
            _chars = _owner.Memory.Span;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledTextBuilder"/> struct that starts in a buffer you provide.
    /// </summary>
    /// <param name="initialBuffer">
    /// The buffer to write to until it is too small, such as a <c>stackalloc</c> span. The builder doesn't own it, but
    /// it zeroes all of it when it leaves it or is disposed, if <paramref name="isSensitive"/> is <c>true</c>.
    /// </param>
    /// <param name="isSensitive">
    /// <c>true</c> to zero the buffers when they are released, and to rent the larger buffers from the secure pool;
    /// otherwise, <c>false</c>.
    /// </param>
    public PooledTextBuilder(Span<char> initialBuffer, bool isSensitive)
    {
        IsSensitive = isSensitive;
        _owner = null;
        _chars = initialBuffer;
        _length = 0;
    }

    /// <summary>
    /// Gets a value that indicates whether the builder zeroes its buffers when it releases them, and rents them from
    /// the secure pool.
    /// </summary>
    /// <value><c>true</c> when the text is sensitive; otherwise, <c>false</c>.</value>
    public bool IsSensitive { get; }

    /// <summary>
    /// Gets the number of characters that have been written.
    /// </summary>
    /// <value>The count of characters written so far. It is 0 after <see cref="Reset"/> and <see cref="Dispose"/>.</value>
    public readonly int Length => _length;

    /// <summary>
    /// Gets the number of characters the current buffer can hold.
    /// </summary>
    /// <value>The length of the current buffer. It is 0 when the builder has no buffer.</value>
    public readonly int Capacity => _chars.Length;

    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> of the characters that have been written.
    /// </summary>
    /// <value>A read-only span containing all written characters.</value>
    /// <remarks>
    /// The returned span is not valid after an append that grows the buffer, after <see cref="Reset"/>, or after
    /// <see cref="Dispose"/>. It is valid as the argument of an append to the same builder.
    /// </remarks>
    public readonly ReadOnlySpan<char> WrittenSpan => _chars[.._length];

    internal readonly ReadOnlyMemory<char> RentedMemory =>
        _owner is null ? ReadOnlyMemory<char>.Empty : _owner.Memory;

    /// <summary>
    /// Appends a character, growing the buffer if needed.
    /// </summary>
    /// <param name="value">The character to append.</param>
    public void Append(char value)
    {
        if (_length == _chars.Length)
        {
            Grow(1);
        }

        _chars[_length++] = value;
    }

    /// <summary>
    /// Appends characters, growing the buffer if needed.
    /// </summary>
    /// <param name="value">The characters to append. It may be <see cref="WrittenSpan"/> of this builder.</param>
    public void Append(ReadOnlySpan<char> value)
    {
        if (value.Length > _chars.Length - _length)
        {
            Grow(value.Length, value);
            return;
        }

        value.CopyTo(_chars[_length..]);
        _length += value.Length;
    }

    /// <summary>
    /// Appends the text that formats a value, growing the buffer if needed.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to format.</param>
    /// <param name="format">The format to use, or an empty span for the default format.</param>
    /// <param name="provider">The provider to use, or <c>null</c> for the current culture.</param>
    /// <remarks>
    /// The value formats straight into the buffer, so no <see cref="string"/> is made.
    /// </remarks>
    public void Append<T>(
        T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null
    )
        where T : ISpanFormattable
    {
        int written;
        while (!value.TryFormat(_chars[_length..], out written, format, provider))
        {
            Grow(_chars.Length - _length + 1);
        }

        _length += written;
    }

    /// <summary>
    /// Reserves characters at the end of the text, growing the buffer if needed, and counts them as written.
    /// </summary>
    /// <param name="length">The number of characters to reserve.</param>
    /// <returns>A span over the reserved characters, for the caller to fill.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <remarks>
    /// The returned span is not valid after the next append that grows the buffer.
    /// </remarks>
    public Span<char> AppendSpan(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (length > _chars.Length - _length)
        {
            Grow(length);
        }

        var span = _chars.Slice(_length, length);
        _length += length;
        return span;
    }

    /// <summary>
    /// Zeroes the written part of the buffer, sets the length to zero and keeps the buffer.
    /// </summary>
    public void Reset()
    {
        CryptographicOperationsExtensions.ZeroMemoryOrClear(_chars[.._length]);
        _length = 0;
    }

    /// <summary>
    /// Releases the buffer, zeroing it when the builder is sensitive, and leaves the builder empty.
    /// </summary>
    /// <remarks>
    /// When the buffer is a span that was passed to the constructor, a sensitive builder zeroes all of it. Otherwise,
    /// the builder disposes the rented buffer. The builder can be used again, and then it rents a new buffer.
    /// </remarks>
    public void Dispose()
    {
        ReleaseBuffer(_owner, _chars, IsSensitive);
        _owner = null;
        _chars = default;
        _length = 0;
    }

    /// <summary>
    /// Makes a <see cref="string"/> from the characters that have been written.
    /// </summary>
    /// <returns>A new string with a copy of the text.</returns>
    /// <remarks>
    /// A <see cref="string"/> can't be zeroed, so the copy stays in memory until the garbage collector reclaims it,
    /// even when the builder is sensitive. Use <see cref="WrittenSpan"/> for sensitive text.
    /// </remarks>
    public override readonly string ToString() => new(_chars[.._length]);

    private void Grow(int needed, ReadOnlySpan<char> append = default)
    {
        var required = checked(_length + needed);
        var doubled = (int)Math.Min(2L * _chars.Length, Array.MaxLength);
        var newSize = Math.Max(required, Math.Max(MinimumGrowCapacity, doubled));

        var owner = BufferFactory.ChoosePool<char>(IsSensitive).Rent(newSize);
        var chars = owner.Memory.Span;

        _chars[.._length].CopyTo(chars);
        append.CopyTo(chars[_length..]);

        ReleaseBuffer(_owner, _chars, IsSensitive);

        _owner = owner;
        _chars = chars;
        _length += append.Length;
    }

    private static void ReleaseBuffer(IMemoryOwner<char>? owner, Span<char> chars, bool isSensitive)
    {
        if (owner is null)
        {
            if (isSensitive)
            {
                CryptographicOperationsExtensions.ZeroMemoryOrClear(chars);
            }
        }
        else
        {
            owner.Dispose();
        }
    }
}
