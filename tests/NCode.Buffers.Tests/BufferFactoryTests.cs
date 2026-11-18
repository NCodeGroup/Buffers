// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NCode.Buffers.Tests;

public class BufferFactoryTests
{
    #region ChoosePool Tests

    [Fact]
    public void ChoosePool_WhenSensitive_ReturnsSecureSharedPool()
    {
        var pool = BufferFactory.ChoosePool<int>(isSensitive: true);

        Assert.Same(SecureMemoryPool<int>.Shared, pool);
    }

    [Fact]
    public void ChoosePool_WhenNotSensitive_ReturnsSharedPool()
    {
        var pool = BufferFactory.ChoosePool<int>(isSensitive: false);

        Assert.Same(MemoryPool<int>.Shared, pool);
    }

    #endregion

    #region Rent Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rent_GenericSpan_ReturnsBufferOfRequestedLengthFromChosenPool(bool isSensitive)
    {
        using var owner = BufferFactory.Rent<int>(10, isSensitive, out Span<int> buffer);

        Assert.Equal(10, buffer.Length);
        Assert.True(owner.Memory.Length >= 10);
        Assert.Equal(isSensitive, owner is SecureMemory<int>);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rent_ByteSpan_ReturnsBufferOfRequestedLengthFromChosenPool(bool isSensitive)
    {
        using var owner = BufferFactory.Rent(16, isSensitive, out Span<byte> buffer);

        Assert.Equal(16, buffer.Length);
        Assert.True(owner.Memory.Length >= 16);
        Assert.Equal(isSensitive, owner is SecureMemory<byte>);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rent_GenericMemory_ReturnsBufferOfRequestedLengthFromChosenPool(bool isSensitive)
    {
        using var owner = BufferFactory.Rent<int>(10, isSensitive, out Memory<int> buffer);

        Assert.Equal(10, buffer.Length);
        Assert.True(owner.Memory.Length >= 10);
        Assert.Equal(isSensitive, owner is SecureMemory<int>);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rent_ByteMemory_ReturnsBufferOfRequestedLengthFromChosenPool(bool isSensitive)
    {
        using var owner = BufferFactory.Rent(16, isSensitive, out Memory<byte> buffer);

        Assert.Equal(16, buffer.Length);
        Assert.True(owner.Memory.Length >= 16);
        Assert.Equal(isSensitive, owner is SecureMemory<byte>);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rent_Span_WritesReachOwnerMemory(bool isSensitive)
    {
        using var owner = BufferFactory.Rent(16, isSensitive, out Span<byte> buffer);

        buffer[0] = 42;

        Assert.Equal(42, owner.Memory.Span[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rent_Memory_WritesReachOwnerMemory(bool isSensitive)
    {
        using var owner = BufferFactory.Rent(16, isSensitive, out Memory<byte> buffer);

        buffer.Span[0] = 42;

        Assert.Equal(42, owner.Memory.Span[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rent_WhenSizeIsZero_ReturnsEmptyBuffer(bool isSensitive)
    {
        using var owner = BufferFactory.Rent(0, isSensitive, out Span<byte> buffer);

        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void Rent_WhenSizeIsZeroAndSensitive_ReturnsEmptyMemorySingleton()
    {
        using var owner = BufferFactory.Rent(0, isSensitive: true, out Span<byte> _);

        Assert.Same(EmptyMemory<byte>.Singleton, owner);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-1, false)]
    [InlineData(-2, true)]
    [InlineData(-2, false)]
    public void Rent_Span_WhenSizeIsNegative_ThrowsArgumentOutOfRangeException(
        int size,
        bool isSensitive
    )
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BufferFactory.Rent(size, isSensitive, out Span<byte> _)
        );

        Assert.Equal("minBufferSize", exception.ParamName);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-1, false)]
    [InlineData(-2, true)]
    [InlineData(-2, false)]
    public void Rent_Memory_WhenSizeIsNegative_ThrowsArgumentOutOfRangeException(
        int size,
        bool isSensitive
    )
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BufferFactory.Rent(size, isSensitive, out Memory<byte> _)
        );

        Assert.Equal("minBufferSize", exception.ParamName);
    }

    [Fact]
    public void Rent_WhenSensitive_AfterReuse_DisposeZeroesWholeBuffer()
    {
        // uint because no other test rents it from the shared pool, so the second rental reuses the first one's buffer.
        for (var i = 0; i < 3; i++)
        {
            var owner = BufferFactory.Rent<uint>(4, isSensitive: true, out Memory<uint> _);
            owner.Memory.Span.Fill(0xAAAAAAAA);
            var memory = owner.Memory;

            owner.Dispose();

            Assert.False(memory.Span.ContainsAnyExcept(0u));
        }
    }

    [Fact]
    public void Rent_WhenSensitiveAndTheSecureSharedPoolWasDisposed_ReturnsABuffer()
    {
        var shared = SecureMemoryPool<SharedDisposeProbe>.Shared;
        shared.Dispose();

        using var owner = BufferFactory.Rent<SharedDisposeProbe>(
            16,
            isSensitive: true,
            out Span<SharedDisposeProbe> buffer
        );

        Assert.Equal(16, buffer.Length);
    }

    [Fact]
    public void Rent_Span_WhenSensitive_DisposeZeroesTheWholeBuffer()
    {
        var filler = new RentSpanElement(-1, -1);
        var owner = BufferFactory.Rent<RentSpanElement>(
            10,
            isSensitive: true,
            out Span<RentSpanElement> buffer
        );
        var memory = owner.Memory;
        memory.Span.Fill(filler);
        Assert.Equal(10, buffer.Length);
        Assert.True(memory.Length > buffer.Length);

        owner.Dispose();

        Assert.All(memory.ToArray(), element => Assert.Equal(default, element));
    }

    [Fact]
    public void Rent_Span_WhenSensitiveAndLargerThanAPage_DisposeZeroesTheBuffer()
    {
        const byte filler = 0xA5;
        const int size = SecureMemoryPool<byte>.PageSize + 1;
        var owner = BufferFactory.Rent(size, isSensitive: true, out Span<byte> buffer);
        var memory = owner.Memory;
        memory.Span.Fill(filler);
        Assert.Equal(size, buffer.Length);

        owner.Dispose();

        Assert.False(memory.Span.ContainsAnyExcept((byte)0));
    }

    #endregion

    #region CreatePinnedArray Tests

    [Fact]
    public void CreatePinnedArray_Generic_ReturnsArrayOfRequestedLength()
    {
        using var lifetime = BufferFactory.CreatePinnedArray<int>(32);

        Assert.Equal(32, lifetime.Length);
        Assert.Equal(32, lifetime.PinnedArray.Length);
    }

    [Fact]
    public void CreatePinnedArray_Byte_ReturnsArrayOfRequestedLength()
    {
        using var lifetime = BufferFactory.CreatePinnedArray(64);

        Assert.Equal(64, lifetime.Length);
        Assert.Equal(64, lifetime.PinnedArray.Length);
    }

    [Fact]
    public void CreatePinnedArray_AllocatesOnPinnedObjectHeap()
    {
        using var lifetime = BufferFactory.CreatePinnedArray(64);

        // The GC reports pinned-object-heap objects as its oldest generation; a new 64-byte array on the normal heap starts in generation 0.
        Assert.Equal(GC.MaxGeneration, GC.GetGeneration(lifetime.PinnedArray));
    }

    [Fact]
    public void CreatePinnedArray_WhenLengthIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var lifetime = BufferFactory.CreatePinnedArray(-1);
        });

        Assert.Equal("length", exception.ParamName);
    }

    [Fact]
    public void CreatePinnedArray_Generic_WhenLengthIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var lifetime = BufferFactory.CreatePinnedArray<int>(int.MinValue);
        });

        Assert.Equal("length", exception.ParamName);
    }

    #endregion

    #region CreatePooledBufferWriter Tests

    [Fact]
    public void CreatePooledBufferWriter_WhenSensitive_RentsWholePagesFromSecurePool()
    {
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);

        // The secure pool hands out whole pages; the shared pool returns a far smaller array for a 1-element request.
        Assert.Equal(SecureMemoryPool<byte>.PageSize, writer.GetMemory(1).Length);
    }

    [Fact]
    public void CreatePooledBufferWriter_WhenSensitive_DisposeZeroesEverySegment()
    {
        var filler = new WriterElement(-1, -1);
        var elementsPerPage =
            SecureMemoryPool<WriterElement>.PageSize / Unsafe.SizeOf<WriterElement>();
        var writer = BufferFactory.CreatePooledBufferWriter<WriterElement>(isSensitive: true);
        writer.GetSpan(elementsPerPage)[..elementsPerPage].Fill(filler);
        writer.Advance(elementsPerPage);
        writer.GetSpan(1)[..1].Fill(filler);
        writer.Advance(1);
        List<WriterElement[]> segments = [];
        foreach (var memory in writer.AsReadOnlySequence)
        {
            Assert.True(MemoryMarshal.TryGetArray(memory, out var segment));
            Assert.NotNull(segment.Array);
            segments.Add(segment.Array);
        }
        Assert.Equal(2, segments.Count);

        writer.Dispose();

        Assert.All(segments, array => Assert.All(array, element => Assert.Equal(default, element)));
    }

    [Fact]
    public void CreatePooledBufferWriter_WhenNotSensitive_RentsFromSharedPool()
    {
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);

        Assert.True(writer.GetMemory(1).Length < SecureMemoryPool<byte>.PageSize);
    }

    [Fact]
    public void CreatePooledBufferWriter_Generic_WhenSensitive_RentsWholePagesFromSecurePool()
    {
        using var writer = BufferFactory.CreatePooledBufferWriter<int>(isSensitive: true);

        Assert.Equal(SecureMemoryPool<int>.PageSize / sizeof(int), writer.GetMemory(1).Length);
    }

    [Fact]
    public void CreatePooledBufferWriter_DefaultMinimumSpanLength_IsZero()
    {
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);

        Assert.Equal(0, writer.MinimumSpanLength);
    }

    [Fact]
    public void CreatePooledBufferWriter_WithMinimumSpanLength_RentsAtLeastThatLength()
    {
        using var writer = BufferFactory.CreatePooledBufferWriter<int>(
            isSensitive: false,
            minimumSpanLength: 256
        );

        Assert.Equal(256, writer.MinimumSpanLength);
        Assert.True(writer.GetMemory(1).Length >= 256);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreatePooledBufferWriter_WhenMinimumSpanLengthIsNegative_ThrowsArgumentOutOfRangeException(
        bool isSensitive
    )
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BufferFactory.CreatePooledBufferWriter(isSensitive, minimumSpanLength: -1)
        );

        Assert.Equal("minimumSpanLength", exception.ParamName);
    }

    [Fact]
    public void CreatePooledBufferWriter_WhenSensitive_AfterReuse_DisposeZeroesEverySegment()
    {
        // ushort because no other test rents it from the shared pool, so the second rental reuses the first one's buffer.
        for (var i = 0; i < 3; i++)
        {
            var writer = BufferFactory.CreatePooledBufferWriter<ushort>(isSensitive: true);
            writer.GetSpan(16).Fill(0xAAAA);
            writer.Advance(16);

            Assert.True(
                MemoryMarshal.TryGetArray(writer.AsReadOnlySequence.First, out var segment)
            );
            Assert.NotNull(segment.Array);
            var array = segment.Array;

            writer.Dispose();

            Assert.False(array.AsSpan().ContainsAnyExcept((ushort)0));
        }
    }

    #endregion

    #region CreateArrayBufferWriter Tests

    [Fact]
    public void CreateArrayBufferWriter_Generic_ReturnsEmptyWriter()
    {
        var writer = BufferFactory.CreateArrayBufferWriter<int>();

        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, writer.Capacity);
    }

    [Fact]
    public void CreateArrayBufferWriter_Generic_WithCapacity_ReturnsWriterWithThatCapacity()
    {
        var writer = BufferFactory.CreateArrayBufferWriter<int>(64);

        Assert.Equal(64, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void CreateArrayBufferWriter_Byte_ReturnsEmptyWriter()
    {
        var writer = BufferFactory.CreateArrayBufferWriter();

        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, writer.Capacity);
    }

    [Fact]
    public void CreateArrayBufferWriter_Byte_WithCapacity_ReturnsWriterWithThatCapacity()
    {
        var writer = BufferFactory.CreateArrayBufferWriter(128);

        Assert.Equal(128, writer.Capacity);
    }

    [Fact]
    public void CreateArrayBufferWriter_ReturnsNewInstanceOnEachCall()
    {
        Assert.NotSame(
            BufferFactory.CreateArrayBufferWriter(),
            BufferFactory.CreateArrayBufferWriter()
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateArrayBufferWriter_Byte_WhenCapacityIsNotPositive_ThrowsArgumentOutOfRangeException(
        int capacity
    )
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BufferFactory.CreateArrayBufferWriter(capacity)
        );

        Assert.Equal("initialCapacity", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateArrayBufferWriter_Generic_WhenCapacityIsNotPositive_ThrowsArgumentOutOfRangeException(
        int capacity
    )
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BufferFactory.CreateArrayBufferWriter<int>(capacity)
        );

        Assert.Equal("initialCapacity", exception.ParamName);
    }

    #endregion

    // Rent_WhenSensitiveAndTheSecureSharedPoolWasDisposed_ReturnsABuffer alone uses this type.
    // It disposes this type's Shared pool, so no other test's pool is affected.
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    private struct SharedDisposeProbe { }

    // Each zeroing test rents an element type of its own, so no other test's rentals reach its SecureMemoryPool<T>.Shared.
    private readonly record struct RentSpanElement(long First, long Second);

    private readonly record struct WriterElement(long First, long Second);
}
