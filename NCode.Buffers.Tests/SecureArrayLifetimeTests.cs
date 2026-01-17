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

public class SecureArrayLifetimeTests
{
    #region Create Factory Method Tests

    [Fact]
    public void Create_ReturnsLifetimeWithCorrectLength()
    {
        using var lifetime = SecureArrayLifetime<byte>.Create(64);

        Assert.Equal(64, lifetime.PinnedArray.Length);
        Assert.Equal(64, lifetime.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    public void Create_VariousSizes_AllocatesCorrectLength(int length)
    {
        using var lifetime = SecureArrayLifetime<byte>.Create(length);

        Assert.Equal(length, lifetime.PinnedArray.Length);
        Assert.Equal(length, lifetime.Length);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_AllocatesPinnedArray()
    {
        using var lifetime = new SecureArrayLifetime<byte>(128);

        Assert.NotNull(lifetime.PinnedArray);
        Assert.Equal(128, lifetime.PinnedArray.Length);
        Assert.Equal(128, lifetime.Length);
    }

    [Fact]
    public void Constructor_WithZeroLength_CreatesEmptyArray()
    {
        using var lifetime = new SecureArrayLifetime<byte>(0);

        Assert.NotNull(lifetime.PinnedArray);
        Assert.Empty(lifetime.PinnedArray);
        Assert.Equal(0, lifetime.Length);
    }

    #endregion

    #region Length Property Tests

    [Fact]
    public void Length_ReturnsCorrectValue()
    {
        using var lifetime = new SecureArrayLifetime<byte>(256);

        Assert.Equal(256, lifetime.Length);
    }

    [Fact]
    public void Length_MatchesPinnedArrayLength()
    {
        using var lifetime = new SecureArrayLifetime<int>(32);

        Assert.Equal(lifetime.PinnedArray.Length, lifetime.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    public void Length_VariousSizes_ReturnsCorrectValue(int expectedLength)
    {
        using var lifetime = SecureArrayLifetime<byte>.Create(expectedLength);

        Assert.Equal(expectedLength, lifetime.Length);
    }

    #endregion

    #region PinnedArray Property Tests

    [Fact]
    public void PinnedArray_ReturnsSameInstance()
    {
        using var lifetime = new SecureArrayLifetime<int>(32);

        var array1 = lifetime.PinnedArray;
        var array2 = lifetime.PinnedArray;

        Assert.Same(array1, array2);
    }

    [Fact]
    public void PinnedArray_IsWritable()
    {
        using var lifetime = new SecureArrayLifetime<byte>(16);

        lifetime.PinnedArray[0] = 0xAB;
        lifetime.PinnedArray[15] = 0xCD;

        Assert.Equal(0xAB, lifetime.PinnedArray[0]);
        Assert.Equal(0xCD, lifetime.PinnedArray[15]);
    }

    #endregion

    #region Implicit Conversion to Span Tests

    [Fact]
    public void ImplicitConversion_ReturnsSpanOverPinnedArray()
    {
        using var lifetime = new SecureArrayLifetime<byte>(64);
        lifetime.PinnedArray[0] = 0xAB;
        lifetime.PinnedArray[63] = 0xCD;

        Span<byte> span = lifetime;

        Assert.Equal(64, span.Length);
        Assert.Equal(0xAB, span[0]);
        Assert.Equal(0xCD, span[63]);
    }

    [Fact]
    public void ImplicitConversionToSpan_EmptyArray_ReturnsEmptySpan()
    {
        using var lifetime = new SecureArrayLifetime<byte>(0);

        Span<byte> span = lifetime;

        Assert.True(span.IsEmpty);
    }

    [Fact]
    public void SpanModifications_AreReflectedInPinnedArray()
    {
        using var lifetime = new SecureArrayLifetime<byte>(16);

        Span<byte> span = lifetime;
        span[0] = 0xFF;
        span[15] = 0xAA;

        Assert.Equal(0xFF, lifetime.PinnedArray[0]);
        Assert.Equal(0xAA, lifetime.PinnedArray[15]);
    }

    [Fact]
    public void PinnedArrayModifications_AreReflectedInSpan()
    {
        using var lifetime = new SecureArrayLifetime<byte>(16);

        lifetime.PinnedArray[0] = 0x11;
        lifetime.PinnedArray[15] = 0x22;

        Span<byte> span = lifetime;
        Assert.Equal(0x11, span[0]);
        Assert.Equal(0x22, span[15]);
    }

    #endregion

    #region Implicit Conversion to Array Tests

    [Fact]
    public void ImplicitConversionToArray_ReturnsPinnedArray()
    {
        using var lifetime = new SecureArrayLifetime<byte>(64);
        lifetime.PinnedArray[0] = 0xAB;
        lifetime.PinnedArray[63] = 0xCD;

        byte[] array = lifetime;

        Assert.Same(lifetime.PinnedArray, array);
        Assert.Equal(64, array.Length);
        Assert.Equal(0xAB, array[0]);
        Assert.Equal(0xCD, array[63]);
    }

    [Fact]
    public void ImplicitConversionToArray_GenericType_Int32_Works()
    {
        using var lifetime = new SecureArrayLifetime<int>(16);
        lifetime.PinnedArray[0] = 100;
        lifetime.PinnedArray[15] = 200;

        int[] array = lifetime;

        Assert.Same(lifetime.PinnedArray, array);
        Assert.Equal(100, array[0]);
        Assert.Equal(200, array[15]);
    }

    [Fact]
    public void ImplicitConversionToArray_ArrayIsZeroedOnDispose()
    {
        var lifetime = SecureArrayLifetime<byte>.Create(64);
        RandomNumberGenerator.Fill(lifetime.PinnedArray);

        byte[] array = lifetime;
        Assert.False(array.All(b => b == 0));

        lifetime.Dispose();

        Assert.True(array.All(b => b == 0));
    }

    [Fact]
    public void ImplicitConversionToArray_CanBeUsedInMethodCall()
    {
        using var lifetime = new SecureArrayLifetime<byte>(32);
        RandomNumberGenerator.Fill(lifetime.PinnedArray);

        var length = ProcessArray(lifetime);

        Assert.Equal(32, length);
    }

    private static int ProcessArray(byte[] array) => array.Length;

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ZerosMemory_ByteArray()
    {
        var lifetime = SecureArrayLifetime<byte>.Create(64);
        RandomNumberGenerator.Fill(lifetime.PinnedArray);
        Assert.False(lifetime.PinnedArray.All(b => b == 0));

        lifetime.Dispose();

        Assert.True(lifetime.PinnedArray.All(b => b == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_IntArray()
    {
        var lifetime = new SecureArrayLifetime<int>(16);
        for (var i = 0; i < lifetime.PinnedArray.Length; i++)
            lifetime.PinnedArray[i] = i + 1;

        Assert.False(lifetime.PinnedArray.All(x => x == 0));

        lifetime.Dispose();

        Assert.True(lifetime.PinnedArray.All(x => x == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_LongArray()
    {
        var lifetime = new SecureArrayLifetime<long>(8);
        for (var i = 0; i < lifetime.PinnedArray.Length; i++)
            lifetime.PinnedArray[i] = long.MaxValue - i;

        Assert.False(lifetime.PinnedArray.All(x => x == 0));

        lifetime.Dispose();

        Assert.True(lifetime.PinnedArray.All(x => x == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_ShortArray()
    {
        var lifetime = new SecureArrayLifetime<short>(32);
        for (var i = 0; i < lifetime.PinnedArray.Length; i++)
            lifetime.PinnedArray[i] = (short)(i + 100);

        Assert.False(lifetime.PinnedArray.All(x => x == 0));

        lifetime.Dispose();

        Assert.True(lifetime.PinnedArray.All(x => x == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_DoubleArray()
    {
        var lifetime = new SecureArrayLifetime<double>(8);
        for (var i = 0; i < lifetime.PinnedArray.Length; i++)
            lifetime.PinnedArray[i] = i * 3.14159;

        Assert.False(lifetime.PinnedArray.All(x => x == 0));

        lifetime.Dispose();

        Assert.True(lifetime.PinnedArray.All(x => x == 0));
    }

    [Fact]
    public void Dispose_ZerosMemory_CustomStruct()
    {
        var lifetime = new SecureArrayLifetime<TestStruct>(4);
        for (var i = 0; i < lifetime.PinnedArray.Length; i++)
            lifetime.PinnedArray[i] = new TestStruct { A = i, B = (byte)(i + 1), C = i * 1.5 };

        Assert.False(lifetime.PinnedArray.All(x => x is { A: 0, B: 0, C: 0 }));

        lifetime.Dispose();

        Assert.True(lifetime.PinnedArray.All(x => x is { A: 0, B: 0, C: 0 }));
    }

    [Fact]
    public void Dispose_ZerosMemory_LargeBuffer()
    {
        const int size = 8192;
        var lifetime = SecureArrayLifetime<byte>.Create(size);
        RandomNumberGenerator.Fill(lifetime.PinnedArray);
        Assert.False(lifetime.PinnedArray.All(b => b == 0));

        lifetime.Dispose();

        Assert.True(lifetime.PinnedArray.All(b => b == 0));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var lifetime = SecureArrayLifetime<byte>.Create(32);
        RandomNumberGenerator.Fill(lifetime.PinnedArray);

        lifetime.Dispose();
        Assert.True(lifetime.PinnedArray.All(b => b == 0));

        lifetime.Dispose();
        lifetime.Dispose();

        Assert.True(lifetime.PinnedArray.All(b => b == 0));
    }

    [Fact]
    public void EmptyArray_DoesNotThrowOnDispose()
    {
        var lifetime = new SecureArrayLifetime<byte>(0);

        lifetime.Dispose();

        Assert.Empty(lifetime.PinnedArray);
    }

    #endregion

    #region Using Statement Tests

    [Fact]
    public void UsingStatement_ZerosMemoryOnExit()
    {
        byte[] capturedArray;

        using (var lifetime = SecureArrayLifetime<byte>.Create(128))
        {
            capturedArray = lifetime.PinnedArray;
            RandomNumberGenerator.Fill(capturedArray);
            Assert.False(capturedArray.All(b => b == 0));
        }

        Assert.True(capturedArray.All(b => b == 0));
    }

    [Fact]
    public void UsingStatement_CanAccessArrayInsideBlock()
    {
        using (var lifetime = new SecureArrayLifetime<byte>(16))
        {
            lifetime.PinnedArray[0] = 0x42;
            lifetime.PinnedArray[15] = 0x99;

            Assert.Equal(0x42, lifetime.PinnedArray[0]);
            Assert.Equal(0x99, lifetime.PinnedArray[15]);
        }
    }

    [Fact]
    public void UsingDeclaration_ZerosMemoryOnScopeExit()
    {
        var capturedArray = ZeroWithUsingDeclaration();

        Assert.True(capturedArray.All(b => b == 0));
    }

    private static byte[] ZeroWithUsingDeclaration()
    {
        using var lifetime = SecureArrayLifetime<byte>.Create(64);
        RandomNumberGenerator.Fill(lifetime.PinnedArray);
        var capturedArray = lifetime.PinnedArray;
        Assert.False(capturedArray.All(b => b == 0));
        return capturedArray;
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void WorksWithByteType()
    {
        using var lifetime = new SecureArrayLifetime<byte>(16);

        Assert.Equal(16, lifetime.Length);
        Assert.NotNull(lifetime.PinnedArray);
    }

    [Fact]
    public void WorksWithCharType()
    {
        using var lifetime = new SecureArrayLifetime<char>(16);
        lifetime.PinnedArray[0] = 'A';
        lifetime.PinnedArray[15] = 'Z';

        Assert.Equal(16, lifetime.Length);
        Assert.Equal('A', lifetime.PinnedArray[0]);
        Assert.Equal('Z', lifetime.PinnedArray[15]);
    }

    [Fact]
    public void WorksWithGuidType()
    {
        using var lifetime = new SecureArrayLifetime<Guid>(4);
        var guid = Guid.NewGuid();
        lifetime.PinnedArray[0] = guid;

        Assert.Equal(4, lifetime.Length);
        Assert.Equal(guid, lifetime.PinnedArray[0]);
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
