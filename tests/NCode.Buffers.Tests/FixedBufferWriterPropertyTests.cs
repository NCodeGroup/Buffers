// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Xunit;

namespace NCode.Buffers.Tests;

public class FixedBufferWriterPropertyTests
{
    private const string Seed = "20261115,12345";

    #region FixedMemoryBufferWriter Tests

    [Property(Replay = Seed)]
    public bool MemoryWriter_WhenWritingInChunks_WrittenMemoryEqualsTheData(
        byte[] data,
        byte extraCapacity,
        PositiveInt chunkSize
    )
    {
        var writer = new FixedMemoryBufferWriter<byte>(new byte[data.Length + extraCapacity]);

        for (var offset = 0; offset < data.Length; offset += chunkSize.Get)
        {
            var chunk = data.AsSpan(offset, Math.Min(chunkSize.Get, data.Length - offset));
            chunk.CopyTo(writer.GetMemory(chunk.Length).Span);
            writer.Advance(chunk.Length);
        }

        return writer.WrittenMemory.Span.SequenceEqual(data)
            && writer.WrittenSpan.SequenceEqual(data)
            && writer.WrittenCount + writer.FreeCapacity == writer.Capacity;
    }

    [Property(Replay = Seed)]
    public bool MemoryWriter_GivenAnyAdvanceAndSizeHint_OnlyThrowsDocumentedExceptions(
        byte[] buffer,
        int[] counts
    )
    {
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        foreach (var count in counts)
        {
            try
            {
                writer.GetMemory(count);
                writer.Advance(count);
            }
            catch (Exception ex)
                when (ex is ArgumentOutOfRangeException or InvalidOperationException)
            {
                // documented failures leave the writer usable
            }

            if (
                writer.WrittenCount < 0
                || writer.WrittenCount > writer.Capacity
                || writer.WrittenCount + writer.FreeCapacity != writer.Capacity
            )
            {
                return false;
            }
        }

        return true;
    }

    [Property(Replay = Seed)]
    public bool MemoryWriter_Reset_ZeroesTheWrittenDataAndRewinds(byte[] data)
    {
        var buffer = new byte[data.Length];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);
        data.CopyTo(writer.GetMemory(data.Length));
        writer.Advance(data.Length);

        writer.Reset();

        return writer.WrittenCount == 0
            && writer.FreeCapacity == buffer.Length
            && buffer.All(b => b == 0);
    }

    #endregion

    #region FixedSpanBufferWriter Tests

    [Property(Replay = Seed)]
    public bool SpanWriter_WhenWritingInChunks_WrittenSpanEqualsTheData(
        byte[] data,
        byte extraCapacity,
        PositiveInt chunkSize
    )
    {
        var writer = new FixedSpanBufferWriter<byte>(new byte[data.Length + extraCapacity]);

        for (var offset = 0; offset < data.Length; offset += chunkSize.Get)
        {
            var chunk = data.AsSpan(offset, Math.Min(chunkSize.Get, data.Length - offset));
            chunk.CopyTo(writer.GetSpan(chunk.Length));
            writer.Advance(chunk.Length);
        }

        return writer.WrittenSpan.SequenceEqual(data)
            && writer.WrittenCount + writer.FreeCapacity == writer.Capacity;
    }

    [Property(Replay = Seed)]
    public bool SpanWriter_GivenAnyAdvanceAndSizeHint_OnlyThrowsDocumentedExceptions(
        byte[] buffer,
        int[] counts
    )
    {
        var writer = new FixedSpanBufferWriter<byte>(buffer);

        foreach (var count in counts)
        {
            try
            {
                writer.GetSpan(count);
                writer.Advance(count);
            }
            catch (Exception ex)
                when (ex is ArgumentOutOfRangeException or InvalidOperationException)
            {
                // documented failures leave the writer usable
            }

            if (
                writer.WrittenCount < 0
                || writer.WrittenCount > writer.Capacity
                || writer.WrittenCount + writer.FreeCapacity != writer.Capacity
            )
            {
                return false;
            }
        }

        return true;
    }

    #endregion
}
