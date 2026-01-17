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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// A high-performance ref struct implementation of <see cref="IBufferWriter{T}"/> that writes to a fixed-size buffer.
/// Unlike growable buffer writers, this implementation cannot resize and will throw if the buffer capacity is exceeded.
/// </summary>
/// <typeparam name="T">The type of elements in the buffer.</typeparam>
/// <param name="buffer">The fixed-size buffer to write to.</param>
/// <remarks>
/// <para>
/// This struct is designed for scenarios where the maximum buffer size is known upfront and no heap allocations are desired.
/// </para>
/// <para>
/// Since this is a ref struct, it can only be used on the stack and cannot be boxed or stored in fields of reference types.
/// This makes it ideal for short-lived, high-performance operations.
/// </para>
/// <para>
/// The buffer writer tracks the current write position via <see cref="WrittenCount"/> and provides
/// <see cref="WrittenSpan"/> to access only the written portion of the buffer.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Span&lt;byte&gt; buffer = stackalloc byte[256];
/// var writer = new FixedSpanBufferWriter&lt;byte&gt;(buffer);
///
/// // Write some data
/// var span = writer.GetSpan(10);
/// span[0] = 0x01;
/// span[1] = 0x02;
/// writer.Advance(2);
///
/// // Access written data
/// ReadOnlySpan&lt;byte&gt; written = writer.WrittenSpan; // Contains [0x01, 0x02]
/// </code>
/// </example>
[PublicAPI]
public ref struct FixedSpanBufferWriter<T>(Span<T> buffer) : IBufferWriter<T>
{
    /// <summary>
    /// Gets the underlying fixed-size buffer.
    /// </summary>
    private Span<T> Buffer { get; } = buffer;

    /// <summary>
    /// Gets the total capacity of the underlying buffer.
    /// </summary>
    /// <value>The total number of elements that the buffer can hold.</value>
    public readonly int Capacity => Buffer.Length;

    /// <summary>
    /// Gets the number of elements that have been written to the buffer.
    /// </summary>
    /// <value>The count of elements written so far.</value>
    public int WrittenCount { get; private set; }

    /// <summary>
    /// Gets the amount of free space remaining in the buffer.
    /// </summary>
    /// <value>The number of elements that can still be written before the buffer is full.</value>
    public readonly int FreeCapacity => Buffer.Length - WrittenCount;

    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> of the data that has been written to the buffer.
    /// </summary>
    /// <value>A read-only span containing all written elements.</value>
    /// <remarks>
    /// The returned span represents only the portion of the buffer that has been written to,
    /// from index 0 to <see cref="WrittenCount"/> - 1.
    /// </remarks>
    public readonly ReadOnlySpan<T> WrittenSpan
    {
        get
        {
            Debug.Assert(WrittenCount >= 0);
            return Buffer[..WrittenCount];
        }
    }

    /// <summary>
    /// Clears the written data by resetting the write position to zero.
    /// </summary>
    /// <remarks>
    /// This method does not clear the underlying buffer contents. To securely clear sensitive data,
    /// use <see cref="Reset"/> instead.
    /// </remarks>
    public void Clear()
    {
        Debug.Assert(WrittenCount >= 0);
        WrittenCount = 0;
    }

    /// <summary>
    /// Resets the buffer writer by clearing all written data and zeroing out the underlying buffer.
    /// </summary>
    /// <remarks>
    /// Use this method when the buffer may contain sensitive data that should be securely cleared.
    /// </remarks>
    public void Reset()
    {
        Debug.Assert(WrittenCount >= 0);
        Buffer[..WrittenCount].Clear();
        WrittenCount = 0;
    }

    /// <summary>
    /// Advances the write position by the specified count.
    /// </summary>
    /// <param name="count">The number of elements that have been written to the buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Advancing by <paramref name="count"/> would exceed the buffer capacity.
    /// </exception>
    /// <remarks>
    /// Call this method after writing data to the span returned by <see cref="GetSpan"/> to update the write position.
    /// The <paramref name="count"/> should be the exact number of elements written, not the size of the span.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        Debug.Assert(WrittenCount >= 0);

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative.");
        }

        if (WrittenCount + count > Buffer.Length)
        {
            throw new InvalidOperationException(
                $"Cannot advance past the end of the buffer. Current position: {WrittenCount}, Capacity: {Buffer.Length}, Requested advance: {count}.");
        }

        WrittenCount += count;
    }

    /// <summary>
    /// Returns a <see cref="Span{T}"/> to write to that is at least the requested size.
    /// </summary>
    /// <param name="sizeHint">The minimum length of the returned <see cref="Span{T}"/>. If 0, a non-empty buffer is returned if space is available.</param>
    /// <returns>A <see cref="Span{T}"/> representing the available space for writing.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sizeHint"/> is negative.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The requested size exceeds the available free capacity. This buffer cannot grow.
    /// </exception>
    /// <remarks>
    /// <para>
    /// After writing to the returned span, you must call <see cref="Advance"/> to update the write position.
    /// </para>
    /// <para>
    /// The returned span may be larger than <paramref name="sizeHint"/>; it represents all remaining free space in the buffer.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0)
    {
        Debug.Assert(WrittenCount >= 0);

        if (sizeHint < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeHint), sizeHint, "Size hint cannot be negative.");
        }

        var freeCapacity = Buffer.Length - WrittenCount;

        if (sizeHint > freeCapacity)
        {
            throw new InvalidOperationException(
                $"The requested size ({sizeHint}) exceeds the available free capacity ({freeCapacity}). This buffer cannot grow.");
        }

        return Buffer[WrittenCount..];
    }

    /// <summary>
    /// This method is not supported because <see cref="Memory{T}"/> cannot be created from a <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="sizeHint">The minimum length of the returned <see cref="Memory{T}"/>.</param>
    /// <returns>This method always throws.</returns>
    /// <exception cref="NotSupportedException">Always thrown. Use <see cref="GetSpan"/> instead.</exception>
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        throw new NotSupportedException(
            $"{nameof(FixedSpanBufferWriter<T>)} does not support {nameof(GetMemory)} because Memory<T> cannot be created from a Span<T>. Use {nameof(GetSpan)} instead.");
    }
}
