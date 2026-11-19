// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// A growable <see cref="IBufferWriter{T}"/> that keeps everything written in one contiguous buffer, so
/// <see cref="WrittenSpan"/> and <see cref="WrittenMemory"/> are always a single span or memory and never a sequence.
/// </summary>
/// <typeparam name="T">The type of elements in the buffer.</typeparam>
/// <remarks>
/// <para>
/// Create an instance with <see cref="BufferFactory.CreateContiguousBufferWriter{T}"/> or
/// <see cref="BufferFactory.CreateContiguousBufferWriter"/>. The writer rents its buffer lazily, on the first call to
/// <see cref="GetSpan"/> or <see cref="GetMemory"/>, from the pool that <c>isSensitive</c> selects.
/// </para>
/// <para>
/// When the buffer is too small, the writer rents a larger one, copies the written part into it, and disposes the
/// old one. When the writer is sensitive, the old buffer is zeroed. A <see cref="Span{T}"/> or <see cref="Memory{T}"/>
/// that the writer returned from <see cref="WrittenSpan"/>, <see cref="WrittenMemory"/>, <see cref="GetSpan"/> or
/// <see cref="GetMemory"/> is therefore not valid after the next call to <see cref="GetSpan"/> or
/// <see cref="GetMemory"/>. Never write the writer's own <see cref="WrittenSpan"/> into the writer.
/// </para>
/// <para>
/// Call <see cref="Reset"/> to zero the written part and reuse the buffer, and <see cref="Dispose"/> to return the
/// buffer to its pool. After <see cref="Dispose"/>, <see cref="Capacity"/>, <see cref="WrittenCount"/> and
/// <see cref="FreeCapacity"/> are 0, and every other member except <see cref="IsSensitive"/> throws
/// <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
///
/// // Write some data
/// var span = writer.GetSpan(4);
/// span[0] = 0x01;
/// span[1] = 0x02;
/// span[2] = 0x03;
/// span[3] = 0x04;
/// writer.Advance(4);
///
/// // Use the written data as one contiguous span
/// byte[] hash = SHA256.HashData(writer.WrittenSpan);
///
/// // Zero the written data and keep the buffer for reuse
/// writer.Reset();
/// </code>
/// </example>
[PublicAPI]
public sealed class ContiguousBufferWriter<T> : IBufferWriter<T>, IDisposable
    where T : unmanaged
{
    /// <summary>
    /// Gets the size of the first buffer, and the least a growth rents.
    /// </summary>
    private int InitialCapacity { get; }

    /// <summary>
    /// Gets or sets the owner of the current buffer, or <see langword="null"/> before the first rental and after
    /// <see cref="Dispose"/>.
    /// </summary>
    private IMemoryOwner<T>? Owner { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether <see cref="Dispose"/> has run.
    /// </summary>
    private bool IsDisposed { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContiguousBufferWriter{T}"/> class.
    /// </summary>
    /// <param name="isSensitive">Whether the writer rents its buffers from the secure pool.</param>
    /// <param name="initialCapacity">
    /// The size of the first buffer, or 0 to use the default size.
    /// <c>BufferFactory.CreateContiguousBufferWriter</c> checks the arguments.
    /// </param>
    internal ContiguousBufferWriter(bool isSensitive, int initialCapacity)
    {
        IsSensitive = isSensitive;
        InitialCapacity = initialCapacity == 0 ? BufferGrowth.DefaultCapacity : initialCapacity;
    }

    /// <summary>
    /// Gets the current buffer, or an empty memory when there is none.
    /// </summary>
    private Memory<T> Buffer => Owner is null ? Memory<T>.Empty : Owner.Memory;

    /// <summary>
    /// Gets a value that indicates whether the writer rents its buffers from the secure pool, which zeroes them when
    /// they are disposed.
    /// </summary>
    /// <value><c>true</c> when the buffers hold sensitive data; otherwise, <c>false</c>.</value>
    public bool IsSensitive { get; }

    /// <summary>
    /// Gets the total capacity of the current buffer.
    /// </summary>
    /// <value>
    /// The number of elements the buffer can hold. It is 0 until the first call to <see cref="GetSpan"/> or
    /// <see cref="GetMemory"/>, and after <see cref="Dispose"/>.
    /// </value>
    public int Capacity => Buffer.Length;

    /// <summary>
    /// Gets the number of elements that have been written to the buffer.
    /// </summary>
    /// <value>The count of elements written so far. It is 0 after <see cref="Reset"/> and <see cref="Dispose"/>.</value>
    public int WrittenCount { get; private set; }

    /// <summary>
    /// Gets the amount of free space remaining in the current buffer.
    /// </summary>
    /// <value>The number of elements that can still be written before the buffer must grow.</value>
    public int FreeCapacity => Capacity - WrittenCount;

    /// <summary>
    /// Gets a <see cref="ReadOnlyMemory{T}"/> of the data that has been written to the buffer.
    /// </summary>
    /// <value>A read-only memory containing all written elements.</value>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <remarks>
    /// The returned memory is not valid after the next call to <see cref="GetSpan"/> or <see cref="GetMemory"/>,
    /// because the buffer may grow and the old buffer is then zeroed when the writer is sensitive.
    /// </remarks>
    public ReadOnlyMemory<T> WrittenMemory
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Buffer[..WrittenCount];
        }
    }

    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> of the data that has been written to the buffer.
    /// </summary>
    /// <value>A read-only span containing all written elements.</value>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <remarks>
    /// The returned span is not valid after the next call to <see cref="GetSpan"/> or <see cref="GetMemory"/>,
    /// because the buffer may grow and the old buffer is then zeroed when the writer is sensitive. Never write this
    /// span into the same writer.
    /// </remarks>
    public ReadOnlySpan<T> WrittenSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Buffer.Span[..WrittenCount];
        }
    }

    /// <summary>
    /// Zeroes the written part of the buffer, sets the write position to zero and keeps the buffer.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <remarks>
    /// The written elements are zeroed with <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory(Span{byte})"/>,
    /// which the compiler and runtime do not optimize away. Elements past the written part are not touched.
    /// </remarks>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Debug.Assert(WrittenCount >= 0);
        CryptographicOperationsExtensions.ZeroMemoryOrClear(Buffer.Span[..WrittenCount]);
        WrittenCount = 0;
    }

    /// <summary>
    /// Advances the write position by the specified count.
    /// </summary>
    /// <param name="count">The number of elements that have been written to the buffer.</param>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="count"/> is greater than <see cref="FreeCapacity"/>. The writer is left as it was.
    /// </exception>
    /// <remarks>
    /// Call this method after writing data to the span or memory returned by <see cref="GetSpan"/> or
    /// <see cref="GetMemory"/>. The <paramref name="count"/> should be the exact number of elements written.
    /// </remarks>
    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Debug.Assert(WrittenCount >= 0);

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "Count cannot be negative."
            );
        }

        if (count > FreeCapacity)
        {
            throw new InvalidOperationException(
                $"Cannot advance past the end of the buffer. Current position: {WrittenCount}, Capacity: {Capacity}, Requested advance: {count}."
            );
        }

        WrittenCount += count;
    }

    /// <summary>
    /// Returns a <see cref="Memory{T}"/> to write to that is at least the requested size, growing the buffer if needed.
    /// </summary>
    /// <param name="sizeHint">The minimum length of the returned <see cref="Memory{T}"/>. If 0, a non-empty memory is returned.</param>
    /// <returns>A <see cref="Memory{T}"/> representing the free space for writing.</returns>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sizeHint"/> is negative.
    /// </exception>
    /// <remarks>
    /// <para>
    /// After writing to the returned memory, you must call <see cref="Advance"/> to update the write position.
    /// </para>
    /// <para>
    /// Growing the buffer invalidates every span and memory the writer returned before, because the old buffer is
    /// disposed, and zeroed when the writer is sensitive.
    /// </para>
    /// </remarks>
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        var needed = Math.Max(sizeHint, 1);
        Grow(needed);
        return Buffer[WrittenCount..];
    }

    /// <summary>
    /// Returns a <see cref="Span{T}"/> to write to that is at least the requested size, growing the buffer if needed.
    /// </summary>
    /// <param name="sizeHint">The minimum length of the returned <see cref="Span{T}"/>. If 0, a non-empty span is returned.</param>
    /// <returns>A <see cref="Span{T}"/> representing the free space for writing.</returns>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sizeHint"/> is negative.
    /// </exception>
    /// <remarks>
    /// <para>
    /// After writing to the returned span, you must call <see cref="Advance"/> to update the write position.
    /// </para>
    /// <para>
    /// Growing the buffer invalidates every span and memory the writer returned before, because the old buffer is
    /// disposed, and zeroed when the writer is sensitive.
    /// </para>
    /// </remarks>
    public Span<T> GetSpan(int sizeHint = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        var needed = Math.Max(sizeHint, 1);
        Grow(needed);
        return Buffer.Span[WrittenCount..];
    }

    /// <summary>
    /// Returns the buffer to its pool, zeroing it when the writer is sensitive. Calling it again does nothing.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        var owner = Owner;
        Owner = null;
        WrittenCount = 0;
        owner?.Dispose();
    }

    /// <summary>
    /// Rents a larger buffer when fewer than <paramref name="needed"/> elements are free, copies the written part into
    /// it, and disposes the old owner, which zeroes it when the writer is sensitive.
    /// </summary>
    /// <param name="needed">The number of free elements the buffer must have.</param>
    private void Grow(int needed)
    {
        if (FreeCapacity >= needed)
        {
            return;
        }

        var required = checked(WrittenCount + needed);
        var newSize = BufferGrowth.GetNewSize(required, Capacity, InitialCapacity);
        var pool = BufferFactory.ChoosePool<T>(IsSensitive);
        var owner = pool.Rent(newSize);
        Buffer.Span[..WrittenCount].CopyTo(owner.Memory.Span);

        var previous = Owner;
        Owner = owner;
        previous?.Dispose();
    }
}
