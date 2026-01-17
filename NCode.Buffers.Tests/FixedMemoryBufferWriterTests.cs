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

public class FixedMemoryBufferWriterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        Assert.Equal(100, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(100, writer.FreeCapacity);
        Assert.True(writer.WrittenSpan.IsEmpty);
        Assert.True(writer.WrittenMemory.IsEmpty);
    }

    [Fact]
    public void Constructor_EmptyBuffer_Valid()
    {
        var writer = new FixedMemoryBufferWriter<byte>(Memory<byte>.Empty);

        Assert.Equal(0, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, writer.FreeCapacity);
        Assert.True(writer.WrittenSpan.IsEmpty);
        Assert.True(writer.WrittenMemory.IsEmpty);
    }

    #endregion

    #region Capacity Property Tests

    [Fact]
    public void Capacity_ReturnsBufferLength()
    {
        var buffer = new byte[256];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        Assert.Equal(256, writer.Capacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(256)]
    [InlineData(1024)]
    public void Capacity_VariousSizes_ReturnsCorrectValue(int size)
    {
        var buffer = new byte[size];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        Assert.Equal(size, writer.Capacity);
    }

    #endregion

    #region WrittenCount Property Tests

    [Fact]
    public void WrittenCount_AfterAdvance_ReturnsCorrectCount()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(50);

        Assert.Equal(50, writer.WrittenCount);
    }

    [Fact]
    public void WrittenCount_AfterMultipleAdvances_ReturnsAccumulatedCount()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(10);
        writer.Advance(20);
        writer.Advance(30);

        Assert.Equal(60, writer.WrittenCount);
    }

    #endregion

    #region FreeCapacity Property Tests

    [Fact]
    public void FreeCapacity_AfterAdvance_ReturnsCorrectValue()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(30);

        Assert.Equal(70, writer.FreeCapacity);
    }

    [Fact]
    public void FreeCapacity_WhenFull_ReturnsZero()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(100);

        Assert.Equal(0, writer.FreeCapacity);
    }

    #endregion

    #region WrittenSpan Property Tests

    [Fact]
    public void WrittenSpan_ReturnsWrittenData()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(5);
        span[0] = 1;
        span[1] = 2;
        span[2] = 3;
        span[3] = 4;
        span[4] = 5;
        writer.Advance(5);

        var written = writer.WrittenSpan;
        Assert.Equal(5, written.Length);
        Assert.Equal(1, written[0]);
        Assert.Equal(2, written[1]);
        Assert.Equal(3, written[2]);
        Assert.Equal(4, written[3]);
        Assert.Equal(5, written[4]);
    }

    [Fact]
    public void WrittenSpan_WhenEmpty_ReturnsEmptySpan()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        Assert.True(writer.WrittenSpan.IsEmpty);
        Assert.Equal(0, writer.WrittenSpan.Length);
    }

    #endregion

    #region WrittenMemory Property Tests

    [Fact]
    public void WrittenMemory_ReturnsWrittenData()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var memory = writer.GetMemory(5);
        memory.Span[0] = 1;
        memory.Span[1] = 2;
        memory.Span[2] = 3;
        memory.Span[3] = 4;
        memory.Span[4] = 5;
        writer.Advance(5);

        var written = writer.WrittenMemory;
        Assert.Equal(5, written.Length);
        Assert.Equal(1, written.Span[0]);
        Assert.Equal(2, written.Span[1]);
        Assert.Equal(3, written.Span[2]);
        Assert.Equal(4, written.Span[3]);
        Assert.Equal(5, written.Span[4]);
    }

    [Fact]
    public void WrittenMemory_WhenEmpty_ReturnsEmptyMemory()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        Assert.True(writer.WrittenMemory.IsEmpty);
        Assert.Equal(0, writer.WrittenMemory.Length);
    }

    #endregion

    #region Clear Method Tests

    [Fact]
    public void Clear_ResetsWrittenCount()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(50);
        span[..50].Fill(0xFF);
        writer.Advance(50);

        Assert.Equal(50, writer.WrittenCount);

        writer.Clear();

        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(100, writer.FreeCapacity);
    }

    [Fact]
    public void Clear_DoesNotClearBufferContents()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(5);
        span[0] = 0xAA;
        span[1] = 0xBB;
        span[2] = 0xCC;
        writer.Advance(3);

        writer.Clear();

        Assert.Equal(0xAA, buffer[0]);
        Assert.Equal(0xBB, buffer[1]);
        Assert.Equal(0xCC, buffer[2]);
    }

    [Fact]
    public void Clear_OnEmptyWriter_DoesNotThrow()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Clear();

        Assert.Equal(0, writer.WrittenCount);
    }

    #endregion

    #region Reset Method Tests

    [Fact]
    public void Reset_ClearsBufferContents()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(5);
        span[0] = 0xAA;
        span[1] = 0xBB;
        span[2] = 0xCC;
        writer.Advance(3);

        writer.Reset();

        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(100, writer.FreeCapacity);

        Assert.Equal(0, buffer[0]);
        Assert.Equal(0, buffer[1]);
        Assert.Equal(0, buffer[2]);
    }

    [Fact]
    public void Reset_OnlyClearsWrittenPortion()
    {
        var buffer = new byte[100];
        buffer[10] = 0xFF;
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(5);
        span[..5].Fill(0xAA);
        writer.Advance(5);

        writer.Reset();

        Assert.Equal(0, buffer[0]);
        Assert.Equal(0, buffer[4]);
        Assert.Equal(0xFF, buffer[10]);
    }

    [Fact]
    public void Reset_OnEmptyWriter_DoesNotThrow()
    {
        var buffer = new byte[100];
        buffer[0] = 0xFF;
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Reset();

        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0xFF, buffer[0]);
    }

    #endregion

    #region Advance Method Tests

    [Fact]
    public void Advance_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(25);
        Assert.Equal(25, writer.WrittenCount);

        writer.Advance(25);
        Assert.Equal(50, writer.WrittenCount);
    }

    [Fact]
    public void Advance_Zero_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(0);

        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void Advance_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => writer.Advance(-1));
        Assert.Equal("count", ex.ParamName);
    }

    [Fact]
    public void Advance_ExceedsCapacity_ThrowsInvalidOperationException()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var ex = Assert.Throws<InvalidOperationException>(() => writer.Advance(101));
        Assert.Contains("Cannot advance past the end of the buffer", ex.Message);
    }

    [Fact]
    public void Advance_ExactlyToCapacity_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(100);

        Assert.Equal(100, writer.WrittenCount);
        Assert.Equal(0, writer.FreeCapacity);
    }

    [Fact]
    public void Advance_AfterPartialAdvance_ExceedsCapacity_ThrowsInvalidOperationException()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(50);

        var ex = Assert.Throws<InvalidOperationException>(() => writer.Advance(51));
        Assert.NotNull(ex);
    }

    #endregion

    #region GetSpan Method Tests

    [Fact]
    public void GetSpan_ReturnsAvailableSpace()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan();

        Assert.Equal(100, span.Length);
    }

    [Fact]
    public void GetSpan_AfterAdvance_ReturnsRemainingSpace()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(30);
        var span = writer.GetSpan();

        Assert.Equal(70, span.Length);
    }

    [Fact]
    public void GetSpan_WithSizeHint_ReturnsAtLeastRequestedSize()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(50);

        Assert.True(span.Length >= 50);
    }

    [Fact]
    public void GetSpan_SizeHintZero_ReturnsAllAvailableSpace()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(0);

        Assert.Equal(100, span.Length);
    }

    [Fact]
    public void GetSpan_NegativeSizeHint_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => writer.GetSpan(-1));
        Assert.Equal("sizeHint", ex.ParamName);
    }

    [Fact]
    public void GetSpan_SizeHintExceedsFreeCapacity_ThrowsInvalidOperationException()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(50);

        var ex = Assert.Throws<InvalidOperationException>(() => writer.GetSpan(60));
        Assert.Contains("exceeds the available free capacity", ex.Message);
    }

    [Fact]
    public void GetSpan_SizeHintExactlyFreeCapacity_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(50);
        var span = writer.GetSpan(50);

        Assert.Equal(50, span.Length);
    }

    [Fact]
    public void GetSpan_WhenBufferFull_WithZeroSizeHint_ReturnsEmptySpan()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(100);
        var span = writer.GetSpan(0);

        Assert.True(span.IsEmpty);
    }

    #endregion

    #region GetMemory Method Tests

    [Fact]
    public void GetMemory_ReturnsAvailableSpace()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var memory = writer.GetMemory();

        Assert.Equal(100, memory.Length);
    }

    [Fact]
    public void GetMemory_AfterAdvance_ReturnsRemainingSpace()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(30);
        var memory = writer.GetMemory();

        Assert.Equal(70, memory.Length);
    }

    [Fact]
    public void GetMemory_WithSizeHint_ReturnsAtLeastRequestedSize()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var memory = writer.GetMemory(50);

        Assert.True(memory.Length >= 50);
    }

    [Fact]
    public void GetMemory_SizeHintZero_ReturnsAllAvailableSpace()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var memory = writer.GetMemory(0);

        Assert.Equal(100, memory.Length);
    }

    [Fact]
    public void GetMemory_NegativeSizeHint_ThrowsArgumentOutOfRangeException()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => writer.GetMemory(-1));
        Assert.Equal("sizeHint", ex.ParamName);
    }

    [Fact]
    public void GetMemory_SizeHintExceedsFreeCapacity_ThrowsInvalidOperationException()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(50);

        var ex = Assert.Throws<InvalidOperationException>(() => writer.GetMemory(60));
        Assert.Contains("exceeds the available free capacity", ex.Message);
    }

    [Fact]
    public void GetMemory_SizeHintExactlyFreeCapacity_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(50);
        var memory = writer.GetMemory(50);

        Assert.Equal(50, memory.Length);
    }

    [Fact]
    public void GetMemory_WhenBufferFull_WithZeroSizeHint_ReturnsEmptyMemory()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        writer.Advance(100);
        var memory = writer.GetMemory(0);

        Assert.True(memory.IsEmpty);
    }

    #endregion

    #region Write and Read Integration Tests

    [Fact]
    public void WriteAndRead_MultipleOperations_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span1 = writer.GetSpan(10);
        for (var i = 0; i < 10; i++)
        {
            span1[i] = (byte)(i + 1);
        }
        writer.Advance(10);

        var span2 = writer.GetSpan(10);
        for (var i = 0; i < 10; i++)
        {
            span2[i] = (byte)(i + 11);
        }
        writer.Advance(10);

        var written = writer.WrittenSpan;
        Assert.Equal(20, written.Length);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal((byte)(i + 1), written[i]);
        }
    }

    [Fact]
    public void WriteAndRead_UsingMemory_MultipleOperations_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var memory1 = writer.GetMemory(10);
        for (var i = 0; i < 10; i++)
        {
            memory1.Span[i] = (byte)(i + 1);
        }
        writer.Advance(10);

        var memory2 = writer.GetMemory(10);
        for (var i = 0; i < 10; i++)
        {
            memory2.Span[i] = (byte)(i + 11);
        }
        writer.Advance(10);

        var written = writer.WrittenMemory;
        Assert.Equal(20, written.Length);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal((byte)(i + 1), written.Span[i]);
        }
    }

    [Fact]
    public void FillEntireBuffer_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(100);
        span.Fill(0xAB);
        writer.Advance(100);

        Assert.Equal(100, writer.WrittenCount);
        Assert.Equal(0, writer.FreeCapacity);

        var written = writer.WrittenSpan;
        Assert.Equal(100, written.Length);
        Assert.True(written.ToArray().All(b => b == 0xAB));
    }

    [Fact]
    public void ClearAndReuse_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span1 = writer.GetSpan(50);
        span1[..50].Fill(0x11);
        writer.Advance(50);
        Assert.Equal(50, writer.WrittenCount);

        writer.Clear();
        Assert.Equal(0, writer.WrittenCount);

        var span2 = writer.GetSpan(30);
        span2[..30].Fill(0x22);
        writer.Advance(30);

        Assert.Equal(30, writer.WrittenCount);
        Assert.True(writer.WrittenSpan.ToArray().All(b => b == 0x22));
    }

    [Fact]
    public void ResetAndReuse_Valid()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span1 = writer.GetSpan(50);
        span1[..50].Fill(0x11);
        writer.Advance(50);

        writer.Reset();

        Assert.True(buffer.Take(50).All(b => b == 0));

        var span2 = writer.GetSpan(30);
        span2[..30].Fill(0x33);
        writer.Advance(30);

        Assert.Equal(30, writer.WrittenCount);
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void GenericType_Int32_Works()
    {
        var buffer = new int[50];
        var writer = new FixedMemoryBufferWriter<int>(buffer);

        var span = writer.GetSpan(5);
        span[0] = 100;
        span[1] = 200;
        span[2] = 300;
        writer.Advance(3);

        var written = writer.WrittenSpan;
        Assert.Equal(3, written.Length);
        Assert.Equal(100, written[0]);
        Assert.Equal(200, written[1]);
        Assert.Equal(300, written[2]);
    }

    [Fact]
    public void GenericType_Char_Works()
    {
        var buffer = new char[50];
        var writer = new FixedMemoryBufferWriter<char>(buffer);

        var span = writer.GetSpan(5);
        "Hello".AsSpan().CopyTo(span);
        writer.Advance(5);

        var written = writer.WrittenSpan;
        Assert.Equal(5, written.Length);
        Assert.True(written.SequenceEqual("Hello"));
    }

    [Fact]
    public void GenericType_Long_Works()
    {
        var buffer = new long[10];
        var writer = new FixedMemoryBufferWriter<long>(buffer);

        var span = writer.GetSpan(3);
        span[0] = long.MaxValue;
        span[1] = long.MinValue;
        span[2] = 0;
        writer.Advance(3);

        var written = writer.WrittenSpan;
        Assert.Equal(3, written.Length);
        Assert.Equal(long.MaxValue, written[0]);
        Assert.Equal(long.MinValue, written[1]);
        Assert.Equal(0, written[2]);
    }

    [Fact]
    public void GenericType_Double_Works()
    {
        var buffer = new double[10];
        var writer = new FixedMemoryBufferWriter<double>(buffer);

        var span = writer.GetSpan(2);
        span[0] = 3.14159;
        span[1] = 2.71828;
        writer.Advance(2);

        var written = writer.WrittenSpan;
        Assert.Equal(2, written.Length);
        Assert.Equal(3.14159, written[0]);
        Assert.Equal(2.71828, written[1]);
    }

    #endregion

    #region IBufferWriter Interface Tests

    [Fact]
    public void ImplementsIBufferWriter()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        Assert.IsAssignableFrom<IBufferWriter<byte>>(writer);
    }

    [Fact]
    public void CanBeUsedAsIBufferWriter()
    {
        var buffer = new byte[100];
        IBufferWriter<byte> writer = new FixedMemoryBufferWriter<byte>(buffer);

        var span = writer.GetSpan(10);
        span[0] = 0xAA;
        writer.Advance(1);

        Assert.Equal(0xAA, buffer[0]);
    }

    #endregion

    #region Class vs Struct Behavior Tests

    [Fact]
    public void CanBeStoredAsField_UnlikeSpanVersion()
    {
        var holder = new BufferWriterHolder();
        holder.Writer = new FixedMemoryBufferWriter<byte>(new byte[100]);

        var span = holder.Writer.GetSpan(10);
        span[..5].Fill(0xAA);
        holder.Writer.Advance(5);

        Assert.Equal(5, holder.Writer.WrittenCount);
    }

    [Fact]
    public async Task CanBeUsedInAsyncMethods_UnlikeSpanVersion()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        await Task.Yield();

        var span = writer.GetSpan(10);
        span[..5].Fill(0xBB);
        writer.Advance(5);

        await Task.Yield();

        Assert.Equal(5, writer.WrittenCount);
        Assert.Equal(0xBB, writer.WrittenSpan[0]);
    }

    [Fact]
    public void CanBePassedByReference()
    {
        var buffer = new byte[100];
        var writer = new FixedMemoryBufferWriter<byte>(buffer);

        WriteToBuffer(writer);

        Assert.Equal(5, writer.WrittenCount);
        Assert.Equal(0xCC, buffer[0]);
    }

    private static void WriteToBuffer(FixedMemoryBufferWriter<byte> writer)
    {
        var span = writer.GetSpan(5);
        span[..5].Fill(0xCC);
        writer.Advance(5);
    }

    #endregion

    #region Test Helpers

    private class BufferWriterHolder
    {
        public FixedMemoryBufferWriter<byte>? Writer { get; set; }
    }

    #endregion
}
