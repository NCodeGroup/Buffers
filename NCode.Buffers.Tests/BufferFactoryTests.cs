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
    public void Rent_Span_WhenSizeIsNegative_ThrowsArgumentOutOfRangeException(int size, bool isSensitive)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BufferFactory.Rent(size, isSensitive, out Span<byte> _));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-1, false)]
    [InlineData(-2, true)]
    [InlineData(-2, false)]
    public void Rent_Memory_WhenSizeIsNegative_ThrowsArgumentOutOfRangeException(int size, bool isSensitive)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BufferFactory.Rent(size, isSensitive, out Memory<byte> _));
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
    public void CreatePooledBufferWriter_WhenNotSensitive_RentsFromSharedPool()
    {
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);

        Assert.True(writer.GetMemory(1).Length < SecureMemoryPool<byte>.PageSize);
    }

    [Fact]
    public void CreatePooledBufferWriter_Generic_WhenSensitive_RentsWholePagesFromSecurePool()
    {
        using var writer = BufferFactory.CreatePooledBufferWriter<int>(isSensitive: true);

        Assert.Equal(SecureMemoryPool<int>.PageSize, writer.GetMemory(1).Length);
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
        using var writer = BufferFactory.CreatePooledBufferWriter<int>(isSensitive: false, minimumSpanLength: 256);

        Assert.Equal(256, writer.MinimumSpanLength);
        Assert.True(writer.GetMemory(1).Length >= 256);
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
        Assert.NotSame(BufferFactory.CreateArrayBufferWriter(), BufferFactory.CreateArrayBufferWriter());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateArrayBufferWriter_WhenCapacityIsNotPositive_ThrowsArgumentException(int capacity)
    {
        Assert.ThrowsAny<ArgumentException>(() => BufferFactory.CreateArrayBufferWriter(capacity));
    }

    #endregion
}
