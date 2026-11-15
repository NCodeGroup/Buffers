// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck.Xunit;

namespace NCode.Buffers.Tests;

public class BufferFactoryPropertyTests
{
    private const string Seed = "20261115,12345";

    #region Rent Tests

    [Property(Replay = Seed)]
    public bool Rent_WithMemory_ReturnsABufferOfTheExactRequestedLength(
        byte size,
        ushort extra,
        bool isSensitive
    )
    {
        var minBufferSize = size + extra % 8192;

        using var owner = BufferFactory.Rent(minBufferSize, isSensitive, out Memory<byte> buffer);

        return buffer.Length == minBufferSize && owner.Memory.Length >= minBufferSize;
    }

    [Property(Replay = Seed)]
    public bool Rent_WithSpan_ReturnsABufferOfTheExactRequestedLength(
        byte size,
        ushort extra,
        bool isSensitive
    )
    {
        var minBufferSize = size + extra % 8192;

        using var owner = BufferFactory.Rent(minBufferSize, isSensitive, out Span<byte> buffer);

        return buffer.Length == minBufferSize;
    }

    #endregion

    #region CreatePooledBufferWriter Tests

    [Property(Replay = Seed)]
    public bool CreatePooledBufferWriter_WhenWritingChunks_SpanLeaseEqualsTheConcatenation(
        byte[][] chunks,
        bool isSensitive
    )
    {
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive);

        foreach (var chunk in chunks)
        {
            var destination = writer.GetSpan(chunk.Length);
            chunk.CopyTo(destination);
            writer.Advance(chunk.Length);
        }

        var expected = chunks.SelectMany(chunk => chunk).ToArray();

        using var lease = writer.GetSpanLease(isSensitive);

        return writer.Length == expected.Length && lease.Span.SequenceEqual(expected);
    }

    #endregion

    #region CreatePinnedArray Tests

    // A new pinned array is uninitialized, as README's Security notes say, so only Dispose's zeroing is asserted: the
    // array is filled with a non-zero byte first, so nothing but that zeroing can leave it all zeros.
    [Property(Replay = Seed)]
    public bool CreatePinnedArray_GivenAnyLength_ReturnsAnArrayOfThatLengthThatDisposeZeroes(
        byte length
    )
    {
        const byte Filler = 0xA5;
        var lifetime = BufferFactory.CreatePinnedArray(length);
        var array = lifetime.PinnedArray;
        array.AsSpan().Fill(Filler);
        var hasLength = lifetime.Length == length && array.Length == length;

        lifetime.Dispose();

        return hasLength && array.All(element => element == 0);
    }

    #endregion
}
