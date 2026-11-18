// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace NCode.Buffers.Tests;

public class FixedMemoryBufferWriterPropertyTests
{
    private const string Seed = "20261115,12345";

    #region Advance Tests

    [Property(Replay = Seed)]
    public bool Advance_WhenWritingInChunks_WrittenMemoryEqualsTheData(
        byte[] data,
        byte extraCapacity,
        PositiveInt chunkSize
    )
    {
        var buffer = new byte[data.Length + extraCapacity];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        for (var offset = 0; offset < data.Length; offset += chunkSize.Get)
        {
            var count = Math.Min(chunkSize.Get, data.Length - offset);
            var chunk = data.AsSpan(offset, count);
            var destination = writer.GetMemory(count);
            chunk.CopyTo(destination.Span);
            writer.Advance(count);
        }

        return writer.WrittenMemory.Span.SequenceEqual(data)
            && writer.WrittenSpan.SequenceEqual(data)
            && writer.WrittenCount + writer.FreeCapacity == writer.Capacity;
    }

    [Property(Replay = Seed)]
    public bool Advance_GivenAnyCounts_OnlyThrowsDocumentedExceptionsAndKeepsTheInvariant(
        byte[] buffer,
        int[] counts
    )
    {
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

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

    [Property(Replay = Seed, Arbitrary = [typeof(CountsNearInt32Limits)])]
    public bool Advance_GivenCountsNearInt32Limits_OnlyThrowsDocumentedExceptionsAndKeepsTheInvariant(
        byte[] buffer,
        int[] counts
    )
    {
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        foreach (var count in counts)
        {
            var before = writer.WrittenCount;

            try
            {
                writer.Advance(count);
            }
            catch (Exception ex)
                when (ex is ArgumentOutOfRangeException or InvalidOperationException)
            {
                if (writer.WrittenCount != before)
                {
                    return false;
                }
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

    #region GetMemory Tests

    [Property(Replay = Seed)]
    public bool GetMemory_GivenAnySizeHint_ReturnsEnoughMemoryOrThrowsADocumentedException(
        byte[] buffer,
        int sizeHint
    )
    {
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        try
        {
            var memory = writer.GetMemory(sizeHint);

            return sizeHint >= 0 && sizeHint <= buffer.Length && memory.Length >= sizeHint;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            return sizeHint < 0 || sizeHint > buffer.Length;
        }
    }

    #endregion

    #region Reset Tests

    [Property(Replay = Seed)]
    public bool Reset_AfterWriting_ZeroesTheWrittenDataAndRewinds(byte[] data)
    {
        var buffer = new byte[data.Length];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);
        var destination = writer.GetMemory(data.Length);
        data.CopyTo(destination);
        writer.Advance(data.Length);

        writer.Reset();

        return writer.WrittenCount == 0
            && writer.FreeCapacity == buffer.Length
            && buffer.All(element => element == 0);
    }

    #endregion

    // Mixes small counts with counts at the ends of int, so that a sequence of counts passes int.MaxValue.
    private static class CountsNearInt32Limits
    {
        public static Arbitrary<int> Int32()
        {
            var small = Gen.Choose(-2, 32);
            var nearLimits = Gen.Elements(
                int.MaxValue,
                int.MaxValue - 1,
                int.MaxValue - 15,
                int.MinValue
            );
            var mixed = Gen.Frequency((3, small), (1, nearLimits));
            return Arb.From(mixed);
        }
    }
}
