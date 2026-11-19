// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Buffers;

/// <summary>
/// Computes the size of the buffer that a growable writer, stream or text builder rents when it outgrows its buffer.
/// </summary>
// ADR-1028 states the growth rule.
internal static class BufferGrowth
{
    /// <summary>
    /// The number of elements a growable buffer rents first when the caller names no initial capacity, and the size a
    /// text builder grows to at least.
    /// </summary>
    internal const int DefaultCapacity = 256;

    /// <summary>
    /// Gets the size of the next buffer: the largest of the required size, the minimum and twice the capacity, which
    /// is capped at <see cref="Array.MaxLength"/>.
    /// </summary>
    /// <param name="required">The number of elements the next buffer must hold at least.</param>
    /// <param name="capacity">The size of the current buffer.</param>
    /// <param name="minimum">The least size the next buffer may have.</param>
    /// <returns>The number of elements to rent.</returns>
    internal static int GetNewSize(int required, int capacity, int minimum)
    {
        var doubled = (int)Math.Min(2L * capacity, Array.MaxLength);
        var floor = Math.Max(minimum, doubled);
        return Math.Max(required, floor);
    }
}
