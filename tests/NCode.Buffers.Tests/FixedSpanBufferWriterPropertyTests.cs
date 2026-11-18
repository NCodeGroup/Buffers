// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
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

    [Property(Replay = Seed, Arbitrary = [typeof(CountsNearInt32Limits)])]
    public bool Advance_GivenCountsNearInt32Limits_OnlyThrowsDocumentedExceptionsAndKeepsTheInvariant(
        byte[] buffer,
        int[] counts
    )
    {
        var writer = new FixedSpanBufferWriter<byte>(buffer);

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
