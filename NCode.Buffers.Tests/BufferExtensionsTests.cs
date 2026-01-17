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

namespace NCode.Buffers.Tests;

public class BufferExtensionsTests
{
    #region Span<T>.GetFixedBufferWriter Tests

    [Fact]
    public void GetFixedBufferWriter_ReturnsWriterWithCorrectCapacity()
    {
        Span<byte> buffer = stackalloc byte[100];
        var writer = buffer.GetFixedBufferWriter();

        Assert.Equal(100, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(100, writer.FreeCapacity);
    }

    [Fact]
    public void GetFixedBufferWriter_EmptySpan_ReturnsEmptyWriter()
    {
        var writer = Span<byte>.Empty.GetFixedBufferWriter();

        Assert.Equal(0, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, writer.FreeCapacity);
    }

    [Fact]
    public void GetFixedBufferWriter_CanWriteToBuffer()
    {
        Span<byte> buffer = stackalloc byte[100];
        var writer = buffer.GetFixedBufferWriter();

        var span = writer.GetSpan(10);
        for (var i = 0; i < 10; i++)
        {
            span[i] = (byte)(i + 1);
        }

        writer.Advance(10);

        Assert.Equal(10, writer.WrittenCount);

        var written = writer.WrittenSpan;
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal((byte)(i + 1), written[i]);
        }
    }

    [Fact]
    public void GetFixedBufferWriter_WritesToOriginalBuffer()
    {
        var buffer = new byte[100];
        var writer = buffer.AsSpan().GetFixedBufferWriter();

        var span = writer.GetSpan(5);
        span[0] = 0xAA;
        span[1] = 0xBB;
        span[2] = 0xCC;
        writer.Advance(3);

        // Verify the original buffer was modified
        Assert.Equal(0xAA, buffer[0]);
        Assert.Equal(0xBB, buffer[1]);
        Assert.Equal(0xCC, buffer[2]);
    }

    [Fact]
    public void GetFixedBufferWriter_GenericType_Int32_Works()
    {
        Span<int> buffer = stackalloc int[50];
        var writer = buffer.GetFixedBufferWriter();

        var span = writer.GetSpan(3);
        span[0] = 100;
        span[1] = 200;
        span[2] = 300;
        writer.Advance(3);

        Assert.Equal(50, writer.Capacity);
        Assert.Equal(3, writer.WrittenCount);

        var written = writer.WrittenSpan;
        Assert.Equal(100, written[0]);
        Assert.Equal(200, written[1]);
        Assert.Equal(300, written[2]);
    }

    [Fact]
    public void GetFixedBufferWriter_GenericType_Char_Works()
    {
        Span<char> buffer = stackalloc char[50];
        var writer = buffer.GetFixedBufferWriter();

        var span = writer.GetSpan(5);
        "Hello".AsSpan().CopyTo(span);
        writer.Advance(5);

        Assert.Equal(5, writer.WrittenCount);
        Assert.True(writer.WrittenSpan is "Hello");
    }

    [Fact]
    public void GetFixedBufferWriter_MultipleWrites_Valid()
    {
        Span<byte> buffer = stackalloc byte[100];
        var writer = buffer.GetFixedBufferWriter();

        // First write
        var span1 = writer.GetSpan(10);
        span1[..10].Fill(0x11);
        writer.Advance(10);

        // Second write
        var span2 = writer.GetSpan(10);
        span2[..10].Fill(0x22);
        writer.Advance(10);

        Assert.Equal(20, writer.WrittenCount);

        var written = writer.WrittenSpan;
        Assert.True(written[..10].ToArray().All(b => b == 0x11));
        Assert.True(written[10..20].ToArray().All(b => b == 0x22));
    }

    [Fact]
    public void GetFixedBufferWriter_ClearAndReuse_Valid()
    {
        Span<byte> buffer = stackalloc byte[100];
        var writer = buffer.GetFixedBufferWriter();

        // First use
        var span1 = writer.GetSpan(50);
        span1[..50].Fill(0xFF);
        writer.Advance(50);

        // Clear and reuse
        writer.Clear();

        // Second use
        var span2 = writer.GetSpan(30);
        span2[..30].Fill(0xAA);
        writer.Advance(30);

        Assert.Equal(30, writer.WrittenCount);
    }

    [Fact]
    public void GetFixedBufferWriter_ResetSecurelyClearsData()
    {
        var buffer = new byte[100];
        var writer = buffer.AsSpan().GetFixedBufferWriter();

        var span = writer.GetSpan(50);
        span[..50].Fill(0xFF);
        writer.Advance(50);

        writer.Reset();

        // Verify buffer was cleared
        Assert.True(buffer.Take(50).All(b => b == 0));
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void GetFixedBufferWriter_StackAllocated_Works()
    {
        Span<byte> buffer = stackalloc byte[256];
        var writer = buffer.GetFixedBufferWriter();

        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var span = writer.GetSpan(testData.Length);
        testData.CopyTo(span);
        writer.Advance(testData.Length);

        Assert.Equal(256, writer.Capacity);
        Assert.Equal(10, writer.WrittenCount);
        Assert.True(writer.WrittenSpan.SequenceEqual(testData));
    }

    #endregion

    #region Memory<T>.GetFixedBufferWriter Tests

    [Fact]
    public void Memory_GetFixedBufferWriter_ReturnsWriterWithCorrectCapacity()
    {
        Memory<byte> buffer = new byte[100];
        var writer = buffer.GetFixedBufferWriter();

        Assert.Equal(100, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(100, writer.FreeCapacity);
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_EmptyMemory_ReturnsEmptyWriter()
    {
        var writer = Memory<byte>.Empty.GetFixedBufferWriter();

        Assert.Equal(0, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, writer.FreeCapacity);
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_CanWriteToBuffer()
    {
        Memory<byte> buffer = new byte[100];
        var writer = buffer.GetFixedBufferWriter();

        var span = writer.GetSpan(10);
        for (var i = 0; i < 10; i++)
        {
            span[i] = (byte)(i + 1);
        }

        writer.Advance(10);

        Assert.Equal(10, writer.WrittenCount);

        var written = writer.WrittenSpan;
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal((byte)(i + 1), written[i]);
        }
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_WritesToOriginalBuffer()
    {
        var buffer = new byte[100];
        Memory<byte> memory = buffer;
        var writer = memory.GetFixedBufferWriter();

        var span = writer.GetSpan(5);
        span[0] = 0xAA;
        span[1] = 0xBB;
        span[2] = 0xCC;
        writer.Advance(3);

        // Verify the original buffer was modified
        Assert.Equal(0xAA, buffer[0]);
        Assert.Equal(0xBB, buffer[1]);
        Assert.Equal(0xCC, buffer[2]);
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_GenericType_Int32_Works()
    {
        Memory<int> buffer = new int[50];
        var writer = buffer.GetFixedBufferWriter();

        var span = writer.GetSpan(3);
        span[0] = 100;
        span[1] = 200;
        span[2] = 300;
        writer.Advance(3);

        Assert.Equal(50, writer.Capacity);
        Assert.Equal(3, writer.WrittenCount);

        var written = writer.WrittenSpan;
        Assert.Equal(100, written[0]);
        Assert.Equal(200, written[1]);
        Assert.Equal(300, written[2]);
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_GenericType_Char_Works()
    {
        Memory<char> buffer = new char[50];
        var writer = buffer.GetFixedBufferWriter();

        var span = writer.GetSpan(5);
        "Hello".AsSpan().CopyTo(span);
        writer.Advance(5);

        Assert.Equal(5, writer.WrittenCount);
        Assert.True(writer.WrittenSpan.SequenceEqual("Hello"));
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_MultipleWrites_Valid()
    {
        Memory<byte> buffer = new byte[100];
        var writer = buffer.GetFixedBufferWriter();

        // First write
        var span1 = writer.GetSpan(10);
        span1[..10].Fill(0x11);
        writer.Advance(10);

        // Second write
        var span2 = writer.GetSpan(10);
        span2[..10].Fill(0x22);
        writer.Advance(10);

        Assert.Equal(20, writer.WrittenCount);

        var written = writer.WrittenSpan;
        Assert.True(written[..10].ToArray().All(b => b == 0x11));
        Assert.True(written[10..20].ToArray().All(b => b == 0x22));
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_ClearAndReuse_Valid()
    {
        Memory<byte> buffer = new byte[100];
        var writer = buffer.GetFixedBufferWriter();

        // First use
        var span1 = writer.GetSpan(50);
        span1[..50].Fill(0xFF);
        writer.Advance(50);

        // Clear and reuse
        writer.Clear();

        // Second use
        var span2 = writer.GetSpan(30);
        span2[..30].Fill(0xAA);
        writer.Advance(30);

        Assert.Equal(30, writer.WrittenCount);
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_ResetSecurelyClearsData()
    {
        var buffer = new byte[100];
        Memory<byte> memory = buffer;
        var writer = memory.GetFixedBufferWriter();

        var span = writer.GetSpan(50);
        span[..50].Fill(0xFF);
        writer.Advance(50);

        writer.Reset();

        // Verify buffer was cleared
        Assert.True(buffer.Take(50).All(b => b == 0));
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_WrittenMemory_ReturnsCorrectData()
    {
        Memory<byte> buffer = new byte[100];
        var writer = buffer.GetFixedBufferWriter();

        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var span = writer.GetSpan(testData.Length);
        testData.CopyTo(span);
        writer.Advance(testData.Length);

        Assert.Equal(100, writer.Capacity);
        Assert.Equal(10, writer.WrittenCount);
        Assert.True(writer.WrittenMemory.Span.SequenceEqual(testData));
    }

    [Fact]
    public void Memory_GetFixedBufferWriter_CanBeStoredInField()
    {
        // This test demonstrates that FixedMemoryBufferWriter can be stored in a field
        // (unlike FixedSpanBufferWriter which is a ref struct)
        var holder = new WriterHolder();
        Memory<byte> buffer = new byte[100];
        holder.Writer = buffer.GetFixedBufferWriter();

        var span = holder.Writer.GetSpan(5);
        span[0] = 0x42;
        holder.Writer.Advance(1);

        Assert.Equal(1, holder.Writer.WrittenCount);
        Assert.Equal(0x42, holder.Writer.WrittenSpan[0]);
    }

    [Fact]
    public async Task Memory_GetFixedBufferWriter_CanBeUsedInAsyncMethod()
    {
        // This test demonstrates that FixedMemoryBufferWriter can be used in async methods
        // (unlike FixedSpanBufferWriter which is a ref struct)
        Memory<byte> buffer = new byte[100];
        var writer = buffer.GetFixedBufferWriter();

        await Task.Yield();

        var span = writer.GetSpan(5);
        span[0] = 0x42;
        writer.Advance(1);

        await Task.Yield();

        Assert.Equal(1, writer.WrittenCount);
        Assert.Equal(0x42, writer.WrittenSpan[0]);
    }

    private class WriterHolder
    {
        public FixedMemoryBufferWriter<byte>? Writer { get; set; }
    }

    #endregion

    #region Span<T>.GetSecureLifetime Tests

    [Fact]
    public void GetSecureLifetime_ReturnsLifetimeWithSameSpan()
    {
        Span<byte> buffer = stackalloc byte[64];
        buffer.Fill(0xAB);

        var lifetime = buffer.GetSecureLifetime();

        Assert.Equal(64, lifetime.Span.Length);
        Assert.True(lifetime.Span.SequenceEqual(buffer));
    }

    [Fact]
    public void GetSecureLifetime_EmptySpan_ReturnsEmptyLifetime()
    {
        var lifetime = Span<byte>.Empty.GetSecureLifetime();

        Assert.Equal(0, lifetime.Span.Length);
    }

    [Fact]
    public void GetSecureLifetime_ZerosMemoryOnDispose()
    {
        var array = new byte[64];
        System.Security.Cryptography.RandomNumberGenerator.Fill(array);
        Assert.False(array.All(b => b == 0));

        var lifetime = array.AsSpan().GetSecureLifetime();
        lifetime.Dispose();

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void GetSecureLifetime_UsingStatement_ZerosMemoryOnExit()
    {
        var array = new byte[128];
        System.Security.Cryptography.RandomNumberGenerator.Fill(array);
        Assert.False(array.All(b => b == 0));

        using (var _ = array.AsSpan().GetSecureLifetime())
        {
            // Work with the span
            Assert.False(array.All(b => b == 0));
        }

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void GetSecureLifetime_GenericType_Int32_Works()
    {
        var array = new int[16];
        for (var i = 0; i < array.Length; i++)
            array[i] = i + 1;

        Assert.False(array.All(x => x == 0));

        var lifetime = array.AsSpan().GetSecureLifetime();
        lifetime.Dispose();

        Assert.True(array.All(x => x == 0));
    }

    [Fact]
    public void GetSecureLifetime_GenericType_Long_Works()
    {
        var array = new long[8];
        for (var i = 0; i < array.Length; i++)
            array[i] = long.MaxValue - i;

        Assert.False(array.All(x => x == 0));

        var lifetime = array.AsSpan().GetSecureLifetime();
        lifetime.Dispose();

        Assert.True(array.All(x => x == 0));
    }

    [Fact]
    public void GetSecureLifetime_SpanModifications_AreReflectedInOriginal()
    {
        var array = new byte[16];
        var lifetime = array.AsSpan().GetSecureLifetime();

        lifetime.Span[0] = 0xFF;
        lifetime.Span[15] = 0xAA;

        Assert.Equal(0xFF, array[0]);
        Assert.Equal(0xAA, array[15]);
    }

    [Fact]
    public void GetSecureLifetime_ImplicitConversion_Works()
    {
        var array = new byte[32];
        array[0] = 0x11;
        array[31] = 0x22;

        var lifetime = array.AsSpan().GetSecureLifetime();
        Span<byte> convertedSpan = lifetime;

        Assert.Equal(32, convertedSpan.Length);
        Assert.Equal(0x11, convertedSpan[0]);
        Assert.Equal(0x22, convertedSpan[31]);
    }

    [Fact]
    public void GetSecureLifetime_StackAllocatedBuffer_Works()
    {
        Span<byte> buffer = stackalloc byte[256];
        buffer.Fill(0xCD);

        using var lifetime = buffer.GetSecureLifetime();

        Assert.Equal(256, lifetime.Span.Length);
        Assert.True(lifetime.Span.ToArray().All(b => b == 0xCD));
    }

    [Fact]
    public void GetSecureLifetime_DisposeCanBeCalledMultipleTimes()
    {
        var array = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(array);

        var lifetime = array.AsSpan().GetSecureLifetime();
        lifetime.Dispose();
        Assert.True(array.All(b => b == 0));

        lifetime.Dispose();
        lifetime.Dispose();

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void GetSecureLifetime_GenericType_Short_Works()
    {
        var array = new short[16];
        for (var i = 0; i < array.Length; i++)
            array[i] = (short)(i + 100);

        Assert.False(array.All(x => x == 0));

        var lifetime = array.AsSpan().GetSecureLifetime();
        lifetime.Dispose();

        Assert.True(array.All(x => x == 0));
    }

    [Fact]
    public void GetSecureLifetime_GenericType_Double_Works()
    {
        var array = new double[8];
        for (var i = 0; i < array.Length; i++)
            array[i] = i * 3.14159;

        Assert.False(array.All(x => x == 0));

        var lifetime = array.AsSpan().GetSecureLifetime();
        lifetime.Dispose();

        Assert.True(array.All(x => x == 0));
    }

    [Fact]
    public void GetSecureLifetime_CustomStruct_Works()
    {
        var array = new TestStruct[4];
        for (var i = 0; i < array.Length; i++)
            array[i] = new TestStruct { A = i, B = (byte)(i + 1), C = i * 1.5 };

        Assert.False(array.All(x => x is { A: 0, B: 0, C: 0 }));

        var lifetime = array.AsSpan().GetSecureLifetime();
        lifetime.Dispose();

        Assert.True(array.All(x => x is { A: 0, B: 0, C: 0 }));
    }

    private struct TestStruct
    {
        public int A;
        public byte B;
        public double C;
    }

    #endregion
}
