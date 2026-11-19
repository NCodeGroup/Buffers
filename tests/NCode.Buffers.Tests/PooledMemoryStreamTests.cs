// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace NCode.Buffers.Tests;

public class PooledMemoryStreamTests
{
    // Buffers above 4096 bytes are never cached by the secure pool, so no parallel test can rent them.
    private const int UncachedSize = 5000;

    private static void AssertFilled(ReadOnlySpan<byte> span, byte expected) =>
        Assert.Equal(-1, span.IndexOfAnyExcept(expected));

    private static Span<byte> GetArraySpan(ReadOnlyMemory<byte> memory)
    {
        Assert.True(MemoryMarshal.TryGetArray(memory, out var segment));
        Assert.NotNull(segment.Array);
        return segment.Array.AsSpan(segment.Offset, segment.Count);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultInitialCapacity_StartsEmpty()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.True(stream.WrittenSpan.IsEmpty);
        Assert.True(stream.WrittenMemory.IsEmpty);
        Assert.Equal(0, stream.Length);
        Assert.Equal(0, stream.Position);
        Assert.False(stream.IsSensitive);
    }

    [Fact]
    public void Constructor_ExplicitInitialCapacity_RentsThatMuchOnTheFirstWrite()
    {
        using var stream = BufferFactory.CreatePooledStream(
            isSensitive: true,
            initialCapacity: UncachedSize
        );

        stream.Write([1]);

        Assert.True(stream.IsSensitive);
        Assert.True(stream.Capacity >= UncachedSize);
    }

    [Fact]
    public void Constructor_DefaultInitialCapacity_RentsTheDefaultOnTheFirstWrite()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        stream.Write([1]);

        Assert.True(stream.Capacity >= 256);
    }

    [Fact]
    public void CreatePooledStream_NegativeInitialCapacity_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BufferFactory.CreatePooledStream(isSensitive: false, initialCapacity: -1)
        );
    }

    #endregion

    #region Write and Read Tests

    [Fact]
    public void Write_ThenRead_RoundTrips()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);

        stream.Write("hello"u8.ToArray(), 0, 5);
        stream.Write(" world"u8);
        stream.Position = 0;

        var buffer = new byte[11];
        Assert.Equal(5, stream.Read(buffer, 0, 5));
        Span<byte> rest = buffer.AsSpan(5);
        Assert.Equal(6, stream.Read(rest));

        Assert.Equal("hello world"u8.ToArray(), buffer);
        Assert.Equal(11, stream.Length);
        Assert.Equal(11, stream.Position);
        Assert.Equal("hello world"u8.ToArray(), stream.WrittenSpan.ToArray());
        Assert.Equal("hello world"u8.ToArray(), stream.WrittenMemory.ToArray());
    }

    [Fact]
    public void NotSensitive_WriteThenRead_RoundTrips()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        stream.Write("hello"u8);
        stream.Position = 0;

        var buffer = new byte[5];
        Assert.Equal(5, stream.Read(buffer, 0, buffer.Length));

        Assert.Equal("hello"u8.ToArray(), buffer);
    }

    [Fact]
    public void Read_AtEnd_ReturnsZero()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write("abc"u8);

        var buffer = new byte[4];

        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void Read_PositionPastEnd_ReturnsZero()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write("abc"u8);
        stream.Position = 10;

        var buffer = new byte[4];

        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(10, stream.Position);
    }

    [Fact]
    public void Read_OnFreshStream_ReturnsZero()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var buffer = new byte[4];

        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    public void Read_InvalidArguments_Throws()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        var buffer = new byte[4];

        Assert.Throws<ArgumentNullException>(() => stream.Read(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(buffer, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(buffer, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(buffer, 2, 3));
    }

    [Fact]
    public void Write_InvalidArguments_Throws()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        var buffer = new byte[4];

        Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(buffer, 2, 3));
    }

    [Fact]
    public void Write_PastEnd_ZeroFillsTheGap()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([0x5A, 0x5A]);
        stream.Position = 6;

        stream.Write([1, 2, 3]);

        Assert.Equal(9, stream.Length);
        Assert.Equal<byte>([0x5A, 0x5A, 0, 0, 0, 0, 1, 2, 3], stream.WrittenSpan.ToArray());
    }

    [Fact]
    public void Write_PastEndOnFreshStream_ZeroFillsTheGap()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Position = 4;

        stream.Write([1]);

        Assert.Equal<byte>([0, 0, 0, 0, 1], stream.WrittenSpan.ToArray());
    }

    [Fact]
    public void Write_Overwrite_KeepsTheLength()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3, 4, 5]);
        stream.Position = 1;

        stream.Write([9, 9]);

        Assert.Equal(5, stream.Length);
        Assert.Equal(3, stream.Position);
        Assert.Equal<byte>([1, 9, 9, 4, 5], stream.WrittenSpan.ToArray());
    }

    [Fact]
    public void Write_EndingPastMaxLength_ThrowsIOException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Position = Array.MaxLength;

        Assert.Throws<IOException>(() => stream.Write([1]));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Write_ItsOwnWrittenSpanWhileGrowing_CopiesTheData()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write(Enumerable.Repeat((byte)0x5A, 3000).ToArray());

        stream.Write(stream.WrittenSpan);

        Assert.Equal(6000, stream.Length);
        AssertFilled(stream.WrittenSpan, 0x5A);
    }

    [Fact]
    public void Write_PastCapacity_ZeroesTheOutgrownBuffer()
    {
        using var stream = BufferFactory.CreatePooledStream(
            isSensitive: true,
            initialCapacity: UncachedSize
        );
        stream.Write(Enumerable.Repeat((byte)0x5A, UncachedSize).ToArray());
        var oldBytes = GetArraySpan(stream.WrittenMemory);
        AssertFilled(oldBytes, 0x5A);

        stream.Write([1]);

        AssertFilled(oldBytes, 0);
        Assert.Equal(UncachedSize + 1, stream.Length);
        AssertFilled(stream.WrittenSpan[..UncachedSize], 0x5A);
        Assert.Equal(1, stream.WrittenSpan[UncachedSize]);
    }

    #endregion

    #region Seek Tests

    [Theory]
    [InlineData(SeekOrigin.Begin, 3, 3)]
    [InlineData(SeekOrigin.Current, 2, 7)]
    [InlineData(SeekOrigin.End, -4, 6)]
    [InlineData(SeekOrigin.End, 5, 15)]
    public void Seek_EachOrigin_MovesThePosition(SeekOrigin origin, long offset, long expected)
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write(new byte[10]);
        stream.Position = 5;

        var result = stream.Seek(offset, origin);

        Assert.Equal(expected, result);
        Assert.Equal(expected, stream.Position);
    }

    [Fact]
    public void Seek_BeforeBeginning_ThrowsIOException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write(new byte[10]);

        Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<IOException>(() => stream.Seek(-11, SeekOrigin.End));
        Assert.Equal(10, stream.Position);
    }

    [Fact]
    public void Seek_PastMaxLength_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            stream.Seek(Array.MaxLength + 1L, SeekOrigin.Begin)
        );
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Seek_UndefinedOrigin_ThrowsArgumentException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.Throws<ArgumentException>(() => stream.Seek(0, (SeekOrigin)42));
    }

    #endregion

    #region Position Tests

    [Fact]
    public void Position_Negative_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
    }

    [Fact]
    public void Position_PastMaxLength_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = Array.MaxLength + 1L);
    }

    [Fact]
    public void Position_AtMaxLength_IsAllowed()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        stream.Position = Array.MaxLength;

        Assert.Equal(Array.MaxLength, stream.Position);
    }

    #endregion

    #region SetLength Tests

    [Fact]
    public void SetLength_Longer_ZeroFills()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([0x5A, 0x5A, 0x5A, 0x5A]);
        stream.Position = 2;

        stream.SetLength(10);

        Assert.Equal(10, stream.Length);
        Assert.Equal(2, stream.Position);
        Assert.Equal<byte>(
            [0x5A, 0x5A, 0x5A, 0x5A, 0, 0, 0, 0, 0, 0],
            stream.WrittenSpan.ToArray()
        );
    }

    [Fact]
    public void SetLength_OnFreshStream_ZeroFills()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);

        stream.SetLength(10);

        Assert.Equal(10, stream.Length);
        AssertFilled(stream.WrittenSpan, 0);
    }

    [Fact]
    public void SetLength_PastCapacityAfterAWrite_GrowsAndKeepsTheData()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);

        stream.SetLength(100_000);

        Assert.Equal(100_000, stream.Length);
        Assert.Equal<byte>([1, 2, 3, 0], stream.WrittenSpan[..4].ToArray());
        AssertFilled(stream.WrittenSpan[3..], 0);
    }

    [Fact]
    public void SetLength_Shorter_ZeroesTheCutBytes()
    {
        using var stream = BufferFactory.CreatePooledStream(
            isSensitive: true,
            initialCapacity: UncachedSize
        );
        stream.Write(Enumerable.Repeat((byte)0x5A, UncachedSize).ToArray());
        var array = GetArraySpan(stream.WrittenMemory);

        stream.SetLength(1000);

        Assert.Equal(1000, stream.Length);
        AssertFilled(array[..1000], 0x5A);
        AssertFilled(array[1000..UncachedSize], 0);
    }

    [Fact]
    public void SetLength_SameLength_DoesNothing()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);

        stream.SetLength(3);

        Assert.Equal<byte>([1, 2, 3], stream.WrittenSpan.ToArray());
    }

    [Fact]
    public void SetLength_Negative_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SetLength(-1));
    }

    [Fact]
    public void SetLength_PastMaxLength_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SetLength(Array.MaxLength + 1L));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WhenSensitive_ZeroesTheBuffer()
    {
        var stream = BufferFactory.CreatePooledStream(
            isSensitive: true,
            initialCapacity: UncachedSize
        );
        stream.Write(Enumerable.Repeat((byte)0x5A, UncachedSize).ToArray());
        var array = GetArraySpan(stream.WrittenMemory);
        AssertFilled(array, 0x5A);

        stream.Dispose();

        AssertFilled(array, 0);
    }

    [Fact]
    public void Dispose_ThenMembers_ThrowObjectDisposedException()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        stream.Dispose();

        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.True(stream.IsSensitive);

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = stream.Length;
        });
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = stream.Position;
        });
        Assert.Throws<ObjectDisposedException>(() => stream.Position = 0);
        Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<ObjectDisposedException>(() => stream.SetLength(0));
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<ObjectDisposedException>(() =>
        {
            Span<byte> buffer = stackalloc byte[1];
            _ = stream.Read(buffer);
        });
        Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.Throws<ObjectDisposedException>(() => stream.Write([1]));
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = stream.WrittenSpan;
        });
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = stream.WrittenMemory;
        });
    }

    [Fact]
    public void Dispose_Twice_DoesNothing()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        stream.Dispose();

        var exception = Record.Exception(stream.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_WithoutEverRenting_DoesNothing()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        stream.Dispose();
        stream.Dispose();

        Assert.False(stream.CanRead);
    }

    #endregion

    #region Flush Tests

    [Fact]
    public void Flush_DoesNothing()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);

        stream.Flush();

        Assert.Equal<byte>([1, 2, 3], stream.WrittenSpan.ToArray());

        stream.Dispose();
        var exception = Record.Exception(stream.Flush);

        Assert.Null(exception);
    }

    #endregion
}
