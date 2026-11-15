// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;

namespace NCode.Buffers.Tests;

public class FixedSpanBufferWriterPropertyTests
{
    private const string Seed = "20261115,12345";

    #region Advance Tests

    [Property(Replay = Seed)]
    public bool Advance_WhenWritingInChunks_WrittenSpanEqualsTheData(
        byte[] data,
        byte extraCapacity,
        PositiveInt chunkSize
    )
    {
        var buffer = new byte[data.Length + extraCapacity];
        var writer = new FixedSpanBufferWriter<byte>(buffer);

        for (var offset = 0; offset < data.Length; offset += chunkSize.Get)
        {
            var count = Math.Min(chunkSize.Get, data.Length - offset);
            var chunk = data.AsSpan(offset, count);
            var destination = writer.GetSpan(count);
            chunk.CopyTo(destination);
            writer.Advance(count);
        }

        return writer.WrittenSpan.SequenceEqual(data)
            && writer.WrittenCount + writer.FreeCapacity == writer.Capacity;
    }

    [Property(Replay = Seed)]
    public bool Advance_GivenAnyCounts_OnlyThrowsDocumentedExceptionsAndKeepsTheInvariant(
        byte[] buffer,
        int[] counts
    )
    {
        var writer = new FixedSpanBufferWriter<byte>(buffer);

        foreach (var count in counts)
        {
            try
            {
                writer.Advance(count);
            }
            catch (Exception ex)
                when (ex is ArgumentOutOfRangeException or InvalidOperationException)
            {
                // a documented failure leaves the writer usable
            }

            if (
                writer.WrittenCount < 0
                || writer.WrittenCount + writer.FreeCapacity != writer.Capacity
            )
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region GetSpan Tests

    [Property(Replay = Seed)]
    public bool GetSpan_GivenAnySizeHint_ReturnsEnoughSpanOrThrowsADocumentedException(
        byte[] buffer,
        int sizeHint
    )
    {
        var writer = new FixedSpanBufferWriter<byte>(buffer);

        try
        {
            var span = writer.GetSpan(sizeHint);

            return sizeHint >= 0 && sizeHint <= buffer.Length && span.Length >= sizeHint;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return sizeHint < 0 || sizeHint > buffer.Length;
        }
    }

    #endregion
}
