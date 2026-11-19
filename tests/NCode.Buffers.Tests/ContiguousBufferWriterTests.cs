// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace NCode.Buffers.Tests;

public class ContiguousBufferWriterTests
{
    private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory)
    {
        Assert.True(MemoryMarshal.TryGetArray(memory, out var segment));
        Assert.NotNull(segment.Array);
        return segment;
    }

    #region Create Tests

    [Fact]
    public void Create_NegativeInitialCapacity_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BufferFactory.CreateContiguousBufferWriter(isSensitive: false, initialCapacity: -1)
        );

        Assert.Equal("initialCapacity", exception.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateContiguousBufferWriter_ByteOverload_KeepsIsSensitive(bool isSensitive)
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive);

        Assert.Equal(isSensitive, writer.IsSensitive);
    }

    #endregion

    #region Capacity Property Tests

    [Fact]
    public void Capacity_BeforeFirstWrite_IsZero()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter<int>(isSensitive: false);

        Assert.Equal(0, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, writer.FreeCapacity);
        Assert.True(writer.WrittenSpan.IsEmpty);
        Assert.True(writer.WrittenMemory.IsEmpty);
    }

    #endregion

    #region GetSpan Tests

    [Fact]
    public void GetSpan_ZeroSizeHint_ReturnsANonEmptySpan()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: false);

        var span = writer.GetSpan();

        Assert.False(span.IsEmpty);
    }

    [Fact]
    public void GetSpan_NegativeSizeHint_ThrowsArgumentOutOfRangeException()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: false);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = writer.GetSpan(-1);
        });

        Assert.Equal("sizeHint", exception.ParamName);
    }

    #endregion

    #region GetMemory Tests

    [Fact]
    public void GetMemory_NegativeSizeHint_ThrowsArgumentOutOfRangeException()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: false);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = writer.GetMemory(-1);
        });

        Assert.Equal("sizeHint", exception.ParamName);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_PastCapacity_GrowsAndKeepsWrittenData()
    {
        const int chunkSize = 100;
        const int chunkCount = 50;
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
        var expected = new byte[chunkSize * chunkCount];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = (byte)(i % 251 + 1);
        }

        for (var chunk = 0; chunk < chunkCount; chunk++)
        {
            var span = writer.GetSpan(chunkSize);
            expected.AsSpan(chunk * chunkSize, chunkSize).CopyTo(span);
            writer.Advance(chunkSize);
        }

        Assert.Equal(expected.Length, writer.WrittenCount);
        Assert.True(writer.Capacity >= expected.Length);
        Assert.Equal(expected, writer.WrittenSpan.ToArray());
        Assert.Equal(expected, writer.WrittenMemory.ToArray());
    }

    #endregion

    #region Grow Tests

    [Fact]
    public void Grow_ToMoreThanDouble_RentsAtLeastTheRequiredSize()
    {
        const int required = 1001;
        using var writer = BufferFactory.CreateContiguousBufferWriter(
            isSensitive: false,
            initialCapacity: 16
        );
        _ = writer.GetSpan(1);
        writer.Advance(1);
        var capacityBefore = writer.Capacity;
        Assert.True(capacityBefore * 2 < required);

        var span = writer.GetSpan(required - 1);

        Assert.True(span.Length >= required - 1);
        Assert.True(writer.Capacity >= required);
        Assert.Equal(1, writer.WrittenCount);
    }

    [Fact]
    public void Grow_WhenFreeCapacitySuffices_KeepsTheBuffer()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(
            isSensitive: false,
            initialCapacity: 64
        );
        _ = writer.GetSpan(10);
        var capacity = writer.Capacity;
        var segment = GetArray(writer.GetMemory(10));

        _ = writer.GetSpan(10);

        Assert.Equal(capacity, writer.Capacity);
        Assert.Same(segment.Array, GetArray(writer.GetMemory(10)).Array);
    }

    [Fact]
    public void Grow_WhenSensitive_ZeroesTheOutgrownBuffer()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(
            isSensitive: true,
            initialCapacity: 5000
        );
        writer.GetSpan(10)[..10].Fill(0xAB);
        writer.Advance(10);
        var segment = GetArray(writer.WrittenMemory);
        var oldArray = segment.Array!;
        Assert.Equal(0xAB, oldArray[0]);

        _ = writer.GetSpan(6000);

        Assert.All(oldArray, x => Assert.Equal(0, x));
        Assert.Equal(Enumerable.Repeat((byte)0xAB, 10), writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void Grow_WhenNotSensitive_KeepsWrittenData()
    {
        // ArrayPool<T>.Shared, behind MemoryPool<T>.Shared, doesn't clear an array it takes back, and no other test of this
        // project rents a non-sensitive byte array in this size's bucket, so the old array still holds the written bytes.
        const int initialCapacity = 2_000_000;
        using var writer = BufferFactory.CreateContiguousBufferWriter(
            isSensitive: false,
            initialCapacity
        );
        writer.GetSpan(10)[..10].Fill(0xAB);
        writer.Advance(10);
        var oldArray = GetArray(writer.WrittenMemory).Array!;

        _ = writer.GetSpan(3_000_000);

        Assert.Equal(0xAB, oldArray[0]);
        Assert.Equal(Enumerable.Repeat((byte)0xAB, 10), writer.WrittenSpan.ToArray());
    }

    #endregion

    #region Advance Tests

    [Fact]
    public void Advance_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: false);
        _ = writer.GetSpan(10);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => writer.Advance(-1));

        Assert.Equal("count", exception.ParamName);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void Advance_PastFreeCapacity_ThrowsInvalidOperationExceptionAndKeepsWrittenCount()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: false);
        _ = writer.GetSpan(10);
        writer.Advance(5);
        var freeCapacity = writer.FreeCapacity;

        Assert.Throws<InvalidOperationException>(() => writer.Advance(freeCapacity + 1));

        Assert.Equal(5, writer.WrittenCount);
        Assert.Equal(freeCapacity, writer.FreeCapacity);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ZeroesTheWrittenPartAndKeepsCapacity()
    {
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
        writer.GetSpan(10)[..10].Fill(0xAB);
        writer.Advance(10);
        var capacity = writer.Capacity;
        var segment = GetArray(writer.WrittenMemory);
        Assert.Equal(0xAB, segment.Array![segment.Offset]);

        writer.Reset();

        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(capacity, writer.Capacity);
        Assert.True(writer.WrittenSpan.IsEmpty);
        Assert.All(segment.Array.AsSpan(segment.Offset, 10).ToArray(), x => Assert.Equal(0, x));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WhenSensitive_ZeroesTheBuffer()
    {
        var writer = BufferFactory.CreateContiguousBufferWriter(
            isSensitive: true,
            initialCapacity: 5000
        );
        writer.GetSpan(10)[..10].Fill(0xAB);
        writer.Advance(10);
        var array = GetArray(writer.WrittenMemory).Array!;
        Assert.Equal(0xAB, array[0]);

        writer.Dispose();

        Assert.All(array, x => Assert.Equal(0, x));
    }

    [Fact]
    public void Dispose_BeforeFirstWrite_DoesNothing()
    {
        var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);

        writer.Dispose();

        Assert.Equal(0, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void Dispose_Twice_DoesNothing()
    {
        var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
        _ = writer.GetSpan(10);

        writer.Dispose();
        writer.Dispose();

        Assert.Equal(0, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void Dispose_ThenSizes_AreZero()
    {
        var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
        _ = writer.GetSpan(10);
        writer.Advance(5);

        writer.Dispose();

        Assert.Equal(0, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, writer.FreeCapacity);
    }

    [Fact]
    public void Dispose_ThenIsSensitive_KeepsItsValue()
    {
        var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
        _ = writer.GetSpan(10);
        writer.Advance(5);

        writer.Dispose();

        Assert.True(writer.IsSensitive);
    }

    [Theory]
    [InlineData("GetSpan")]
    [InlineData("GetMemory")]
    [InlineData("Advance")]
    [InlineData("Reset")]
    [InlineData("WrittenSpan")]
    [InlineData("WrittenMemory")]
    public void Dispose_ThenMember_ThrowsObjectDisposedException(string member)
    {
        var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
        _ = writer.GetSpan(10);
        writer.Advance(5);

        writer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => CallMember(writer, member));
    }

    private static void CallMember(ContiguousBufferWriter<byte> writer, string member)
    {
        switch (member)
        {
            case "GetSpan":
                _ = writer.GetSpan();
                break;
            case "GetMemory":
                _ = writer.GetMemory();
                break;
            case "Advance":
                writer.Advance(0);
                break;
            case "Reset":
                writer.Reset();
                break;
            case "WrittenSpan":
                _ = writer.WrittenSpan.Length;
                break;
            case "WrittenMemory":
                _ = writer.WrittenMemory.Length;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(member),
                    member,
                    "No call is written for the member."
                );
        }
    }

    #endregion
}
