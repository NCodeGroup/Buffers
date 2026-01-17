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

using System.Security.Cryptography;

namespace NCode.Buffers.Tests;

public class SecureSpanLifetimeTests
{
    #region Constructor and Span Property Tests

    [Fact]
    public void Constructor_SetsSpan()
    {
        Span<byte> buffer = stackalloc byte[16];
        buffer.Fill(0xAB);

        var lifetime = new SecureSpanLifetime<byte>(buffer);

        Assert.Equal(16, lifetime.Span.Length);
        Assert.True(lifetime.Span.SequenceEqual(buffer));
    }

    [Fact]
    public void Constructor_WithEmptySpan_CreatesValidLifetime()
    {
        var lifetime = new SecureSpanLifetime<byte>(Span<byte>.Empty);

        Assert.Equal(0, lifetime.Span.Length);
        Assert.True(lifetime.Span.IsEmpty);
    }

    [Fact]
    public void Span_ReturnsOriginalSpan()
    {
        Span<int> buffer = stackalloc int[8];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = i * 10;

        var lifetime = new SecureSpanLifetime<int>(buffer);

        for (var i = 0; i < buffer.Length; i++)
            Assert.Equal(i * 10, lifetime.Span[i]);
    }

    [Fact]
    public void Span_HasCorrectLength()
    {
        const int length = 256;
        var array = new byte[length];

        var lifetime = new SecureSpanLifetime<byte>(array);

        Assert.Equal(length, lifetime.Span.Length);
    }

    [Fact]
    public void SpanModifications_AreReflectedInOriginal()
    {
        var array = new byte[16];
        var lifetime = new SecureSpanLifetime<byte>(array);

        lifetime.Span[0] = 0xFF;
        lifetime.Span[15] = 0xAA;

        Assert.Equal(0xFF, array[0]);
        Assert.Equal(0xAA, array[15]);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_ReturnsUnderlyingSpan()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        var originalCopy = buffer.ToArray();

        var lifetime = new SecureSpanLifetime<byte>(buffer);
        Span<byte> convertedSpan = lifetime;

        Assert.Equal(32, convertedSpan.Length);
        Assert.True(convertedSpan.SequenceEqual(originalCopy));
    }

    [Fact]
    public void ImplicitConversion_CanBeUsedInMethodCall()
    {
        var array = new byte[16];
        RandomNumberGenerator.Fill(array);
        var originalCopy = array.ToArray();

        var lifetime = new SecureSpanLifetime<byte>(array);

        var result = ProcessSpan(lifetime);

        Assert.Equal(16, result);
        Assert.True(array.SequenceEqual(originalCopy));
    }

    [Fact]
    public void ImplicitConversion_WithEmptySpan_ReturnsEmptySpan()
    {
        var lifetime = new SecureSpanLifetime<byte>(Span<byte>.Empty);

        Span<byte> convertedSpan = lifetime;

        Assert.True(convertedSpan.IsEmpty);
    }

    private static int ProcessSpan(Span<byte> span) => span.Length;

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ZerosMemory_ByteSpan()
    {
        var array = new byte[64];
        RandomNumberGenerator.Fill(array);
        Assert.False(array.All(b => b == 0));

        var lifetime = new SecureSpanLifetime<byte>(array);
        lifetime.Dispose();

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_IntSpan()
    {
        var array = new int[16];
        for (var i = 0; i < array.Length; i++)
            array[i] = i + 1;

        Assert.False(array.All(x => x == 0));

        var lifetime = new SecureSpanLifetime<int>(array);
        lifetime.Dispose();

        Assert.True(array.All(x => x == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_LongSpan()
    {
        var array = new long[8];
        for (var i = 0; i < array.Length; i++)
            array[i] = long.MaxValue - i;

        Assert.False(array.All(x => x == 0));

        var lifetime = new SecureSpanLifetime<long>(array);
        lifetime.Dispose();

        Assert.True(array.All(x => x == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_ShortSpan()
    {
        var array = new short[32];
        for (var i = 0; i < array.Length; i++)
            array[i] = (short)(i + 100);

        Assert.False(array.All(x => x == 0));

        var lifetime = new SecureSpanLifetime<short>(array);
        lifetime.Dispose();

        Assert.True(array.All(x => x == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_DoubleSpan()
    {
        var array = new double[8];
        for (var i = 0; i < array.Length; i++)
            array[i] = i * 3.14159;

        Assert.False(array.All(x => x == 0));

        var lifetime = new SecureSpanLifetime<double>(array);
        lifetime.Dispose();

        Assert.True(array.All(x => x == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_CustomStruct()
    {
        var array = new TestStruct[4];
        for (var i = 0; i < array.Length; i++)
            array[i] = new TestStruct { A = i, B = (byte)(i + 1), C = i * 1.5 };

        Assert.False(array.All(x => x is { A: 0, B: 0, C: 0 }));

        var lifetime = new SecureSpanLifetime<TestStruct>(array);
        lifetime.Dispose();

        Assert.True(array.All(x => x is { A: 0, B: 0, C: 0 }));
    }

    [Fact]
    public void Dispose_ZerosAllBytes_LargeBuffer()
    {
        const int size = 4096;
        var array = new byte[size];
        RandomNumberGenerator.Fill(array);
        Assert.False(array.All(b => b == 0));

        var lifetime = new SecureSpanLifetime<byte>(array);
        lifetime.Dispose();

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var array = new byte[32];
        RandomNumberGenerator.Fill(array);

        var lifetime = new SecureSpanLifetime<byte>(array);
        lifetime.Dispose();
        Assert.True(array.All(b => b == 0));

        lifetime.Dispose();
        lifetime.Dispose();

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void EmptySpan_DoesNotThrowOnDispose()
    {
        var lifetime = new SecureSpanLifetime<byte>(Span<byte>.Empty);

        lifetime.Dispose();

        Assert.True(lifetime.Span.IsEmpty);
    }

    #endregion

    #region Using Statement Tests

    [Fact]
    public void UsingStatement_ZerosMemoryOnExit()
    {
        var array = new byte[128];
        RandomNumberGenerator.Fill(array);
        Assert.False(array.All(b => b == 0));

        using (var _ = new SecureSpanLifetime<byte>(array))
        {
            Assert.False(array.All(b => b == 0));
        }

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void UsingStatement_CanAccessSpanInsideBlock()
    {
        var array = new byte[16];

        using (var lifetime = new SecureSpanLifetime<byte>(array))
        {
            lifetime.Span[0] = 0x42;
            lifetime.Span[15] = 0x99;

            Assert.Equal(0x42, array[0]);
            Assert.Equal(0x99, array[15]);
        }

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void UsingDeclaration_ZerosMemoryOnScopeExit()
    {
        var array = new byte[64];
        RandomNumberGenerator.Fill(array);

        ZeroWithUsingDeclaration(array);

        Assert.True(array.All(b => b == 0));
    }

    private static void ZeroWithUsingDeclaration(byte[] array)
    {
        using var lifetime = new SecureSpanLifetime<byte>(array);
        Assert.False(array.All(b => b == 0));
    }

    #endregion

    #region Stackalloc Tests

    [Fact]
    public void WorksWithStackalloc_Byte()
    {
        Span<byte> buffer = stackalloc byte[64];
        buffer.Fill(0xFF);

        using (var lifetime = new SecureSpanLifetime<byte>(buffer))
        {
            Assert.True(lifetime.Span.ToArray().All(b => b == 0xFF));
        }

        Assert.True(buffer.ToArray().All(b => b == 0));
    }

    [Fact]
    public void WorksWithStackalloc_Int()
    {
        Span<int> buffer = stackalloc int[16];
        buffer.Fill(42);

        using (var lifetime = new SecureSpanLifetime<int>(buffer))
        {
            Assert.True(lifetime.Span.ToArray().All(x => x == 42));
        }

        Assert.True(buffer.ToArray().All(x => x == 0));
    }

    #endregion

    #region IDisposable Interface Tests

    [Fact]
    public void CanBeUsedWithUsingStatement()
    {
        var array = new byte[16];
        array[0] = 0xFF;

        using (var lifetime = new SecureSpanLifetime<byte>(array))
        {
            Assert.Equal(0xFF, lifetime.Span[0]);
        }

        Assert.Equal(0, array[0]);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void SingleElementSpan_IsZeroed()
    {
        var array = new byte[1];
        array[0] = 0xFF;

        var lifetime = new SecureSpanLifetime<byte>(array);
        lifetime.Dispose();

        Assert.Equal(0, array[0]);
    }

    [Fact]
    public void OddSizedBuffer_IsFullyZeroed()
    {
        var array = new byte[17];
        RandomNumberGenerator.Fill(array);

        var lifetime = new SecureSpanLifetime<byte>(array);
        lifetime.Dispose();

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void NonPowerOfTwoSize_IsFullyZeroed()
    {
        var array = new byte[100];
        RandomNumberGenerator.Fill(array);

        var lifetime = new SecureSpanLifetime<byte>(array);
        lifetime.Dispose();

        Assert.True(array.All(b => b == 0));
    }

    #endregion

    #region Test Helpers

    private struct TestStruct
    {
        public int A;
        public byte B;
        public double C;
    }

    #endregion
}
