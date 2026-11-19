// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace NCode.Buffers.Tests;

public class PooledMemoryStreamTests
{
    // Buffers above 4096 bytes are never cached by the secure pool, so no parallel test can rent them.
    private const int UncachedSize = 5000;

    private static void AssertFilled(ReadOnlySpan<byte> span, byte expected)
    {
        var index = span.IndexOfAnyExcept(expected);

        Assert.Equal(-1, index);
    }

    private static Span<byte> GetArraySpan(ReadOnlyMemory<byte> memory)
    {
        var hasArray = MemoryMarshal.TryGetArray(memory, out var segment);
        Assert.True(hasArray);
        Assert.NotNull(segment.Array);
        return segment.Array.AsSpan(segment.Offset, segment.Count);
    }

    private static void CallMember(PooledMemoryStream stream, string member)
    {
        var bytes = new byte[1];

        switch (member)
        {
            case "Length":
                _ = stream.Length;
                break;
            case "GetPosition":
                _ = stream.Position;
                break;
            case "SetPosition":
                stream.Position = 0;
                break;
            case "Seek":
                _ = stream.Seek(0, SeekOrigin.Begin);
                break;
            case "SetLength":
                stream.SetLength(0);
                break;
            case "ReadArray":
                _ = stream.Read(bytes, 0, 1);
                break;
            case "ReadSpan":
            {
                Span<byte> span = bytes;
                _ = stream.Read(span);
                break;
            }
            case "WriteArray":
                stream.Write(bytes, 0, 1);
                break;
            case "WriteSpan":
            {
                ReadOnlySpan<byte> data = bytes;
                stream.Write(data);
                break;
            }
            case "WrittenSpan":
                _ = stream.WrittenSpan;
                break;
            case "WrittenMemory":
                _ = stream.WrittenMemory;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(member),
                    member,
                    "No call is written for the member."
                );
        }
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
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            BufferFactory.CreatePooledStream(isSensitive: false, initialCapacity: -1)
        );

        Assert.Equal("initialCapacity", exception.ParamName);
    }

    #endregion

    #region Write and Read Tests

    [Fact]
    public void Write_ThenRead_RoundTrips()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);

        var hello = "hello"u8.ToArray();
        stream.Write(hello, 0, 5);
        stream.Write(" world"u8);
        stream.Position = 0;

        var buffer = new byte[11];
        var firstCount = stream.Read(buffer, 0, 5);
        Span<byte> rest = buffer.AsSpan(5);
        var secondCount = stream.Read(rest);

        var expected = "hello world"u8.ToArray();
        var writtenFromSpan = stream.WrittenSpan.ToArray();
        var writtenFromMemory = stream.WrittenMemory.ToArray();
        Assert.Equal(5, firstCount);
        Assert.Equal(6, secondCount);
        Assert.Equal(expected, buffer);
        Assert.Equal(11, stream.Length);
        Assert.Equal(11, stream.Position);
        Assert.Equal(expected, writtenFromSpan);
        Assert.Equal(expected, writtenFromMemory);
    }

    [Fact]
    public void Read_AfterWriteWhenNotSensitive_ReturnsTheWrittenBytes()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        stream.Write("hello"u8);
        stream.Position = 0;

        var buffer = new byte[5];
        var count = stream.Read(buffer, 0, buffer.Length);

        var expected = "hello"u8.ToArray();
        Assert.Equal(5, count);
        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void Read_AtEnd_ReturnsZero()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write("abc"u8);

        var buffer = new byte[4];

        var count = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(0, count);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void Read_PositionPastEnd_ReturnsZero()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write("abc"u8);
        stream.Position = 10;

        var buffer = new byte[4];

        var count = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(0, count);
        Assert.Equal(10, stream.Position);
    }

    [Fact]
    public void Read_OnFreshStream_ReturnsZero()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var buffer = new byte[4];

        var count = stream.Read(buffer, 0, buffer.Length);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Read_NullBuffer_ThrowsArgumentNullException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var exception = Assert.Throws<ArgumentNullException>(() => stream.Read(null!, 0, 1));

        Assert.Equal("buffer", exception.ParamName);
    }

    [Theory]
    [InlineData(-1, 1, "offset")]
    [InlineData(0, -1, "count")]
    [InlineData(2, 3, "count")]
    public void Read_InvalidRange_ThrowsArgumentOutOfRangeException(
        int offset,
        int count,
        string paramName
    )
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        var buffer = new byte[4];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            stream.Read(buffer, offset, count)
        );

        Assert.Equal(paramName, exception.ParamName);
    }

    [Fact]
    public void Write_NullBuffer_ThrowsArgumentNullException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var exception = Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 1));

        Assert.Equal("buffer", exception.ParamName);
    }

    [Theory]
    [InlineData(-1, 1, "offset")]
    [InlineData(0, -1, "count")]
    [InlineData(2, 3, "count")]
    public void Write_InvalidRange_ThrowsArgumentOutOfRangeException(
        int offset,
        int count,
        string paramName
    )
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        var buffer = new byte[4];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            stream.Write(buffer, offset, count)
        );

        Assert.Equal(paramName, exception.ParamName);
    }

    [Fact]
    public void Write_PastEnd_ZeroFillsTheGap()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([0x5A, 0x5A]);
        stream.Position = 6;

        stream.Write([1, 2, 3]);

        Assert.Equal(9, stream.Length);
        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([0x5A, 0x5A, 0, 0, 0, 0, 1, 2, 3], written);
    }

    [Fact]
    public void Write_PastEndOnFreshStream_ZeroFillsTheGap()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Position = 4;

        stream.Write([1]);

        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([0, 0, 0, 0, 1], written);
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
        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([1, 9, 9, 4, 5], written);
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
        var data = new byte[3000];
        Array.Fill(data, (byte)0x5A);
        stream.Write(data);

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
        var data = new byte[UncachedSize];
        Array.Fill(data, (byte)0x5A);
        stream.Write(data);
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
        var data = new byte[10];
        stream.Write(data);
        stream.Position = 5;

        var result = stream.Seek(offset, origin);

        Assert.Equal(expected, result);
        Assert.Equal(expected, stream.Position);
    }

    [Fact]
    public void Seek_BeforeBeginning_ThrowsIOException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        var data = new byte[10];
        stream.Write(data);

        Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<IOException>(() => stream.Seek(-11, SeekOrigin.End));
        Assert.Equal(10, stream.Position);
    }

    [Fact]
    public void Seek_PastMaxLength_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            stream.Seek(Array.MaxLength + 1L, SeekOrigin.Begin)
        );

        Assert.Equal("offset", exception.ParamName);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Seek_UndefinedOrigin_ThrowsArgumentException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var exception = Assert.Throws<ArgumentException>(() => stream.Seek(0, (SeekOrigin)42));

        Assert.Equal("origin", exception.ParamName);
    }

    #endregion

    #region Position Property Tests

    [Fact]
    public void Position_Negative_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Position_PastMaxLength_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            stream.Position = Array.MaxLength + 1L
        );

        Assert.Equal("value", exception.ParamName);
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
        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([0x5A, 0x5A, 0x5A, 0x5A, 0, 0, 0, 0, 0, 0], written);
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
        var head = stream.WrittenSpan[..4].ToArray();
        Assert.Equal<byte>([1, 2, 3, 0], head);
        AssertFilled(stream.WrittenSpan[3..], 0);
    }

    [Fact]
    public void SetLength_Shorter_ZeroesTheCutBytes()
    {
        using var stream = BufferFactory.CreatePooledStream(
            isSensitive: true,
            initialCapacity: UncachedSize
        );
        var data = new byte[UncachedSize];
        Array.Fill(data, (byte)0x5A);
        stream.Write(data);
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

        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([1, 2, 3], written);
    }

    [Fact]
    public void SetLength_Negative_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => stream.SetLength(-1));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void SetLength_PastMaxLength_ThrowsArgumentOutOfRangeException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            stream.SetLength(Array.MaxLength + 1L)
        );

        Assert.Equal("value", exception.ParamName);
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
        var data = new byte[UncachedSize];
        Array.Fill(data, (byte)0x5A);
        stream.Write(data);
        var array = GetArraySpan(stream.WrittenMemory);
        AssertFilled(array, 0x5A);

        stream.Dispose();

        AssertFilled(array, 0);
    }

    [Fact]
    public void Dispose_ThenCanReadCanSeekCanWrite_AreFalse()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        stream.Dispose();

        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void Dispose_ThenIsSensitive_KeepsItsValue()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        stream.Dispose();

        Assert.True(stream.IsSensitive);
    }

    [Theory]
    [InlineData("Length")]
    [InlineData("GetPosition")]
    [InlineData("SetPosition")]
    [InlineData("Seek")]
    [InlineData("SetLength")]
    [InlineData("ReadArray")]
    [InlineData("ReadSpan")]
    [InlineData("WriteArray")]
    [InlineData("WriteSpan")]
    [InlineData("WrittenSpan")]
    [InlineData("WrittenMemory")]
    public void Dispose_ThenMember_ThrowsObjectDisposedException(string member)
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        stream.Dispose();

        Assert.Throws<ObjectDisposedException>(() => CallMember(stream, member));
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
    public void Flush_WhenOpen_KeepsTheWrittenBytes()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);

        stream.Flush();

        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([1, 2, 3], written);
    }

    [Fact]
    public void Flush_AfterDispose_DoesNotThrow()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);
        stream.Dispose();

        var exception = Record.Exception(stream.Flush);

        Assert.Null(exception);
    }

    #endregion

    #region ReadByte and WriteByte Tests

    [Fact]
    public void ReadByte_ReturnsTheBytesThenMinusOne()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([7, 8]);
        stream.Position = 0;

        var first = stream.ReadByte();
        var second = stream.ReadByte();

        Assert.Equal(7, first);
        Assert.Equal(8, second);
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void ReadByte_AtEnd_ReturnsMinusOne()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([7]);

        var value = stream.ReadByte();

        Assert.Equal(-1, value);
        Assert.Equal(1, stream.Position);
    }

    [Fact]
    public void ReadByte_OnFreshStream_ReturnsMinusOne()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var value = stream.ReadByte();

        Assert.Equal(-1, value);
    }

    [Fact]
    public void ReadByte_WhenDisposed_ThrowsObjectDisposedException()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Dispose();

        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
    }

    [Fact]
    public void WriteByte_Appends()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);

        stream.WriteByte(1);
        stream.WriteByte(2);

        Assert.Equal(2, stream.Position);
        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([1, 2], written);
    }

    #endregion

    #region ReadAsync and WriteAsync Tests

    [Fact]
    public async Task ReadAsync_Memory_Reads()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);
        stream.Position = 1;
        var buffer = new byte[4];
        var memory = buffer.AsMemory();

        var count = await stream.ReadAsync(memory, CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal<byte>([2, 3, 0, 0], buffer);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public async Task ReadAsync_WithoutAToken_Reads()
    {
        using PooledMemoryStream stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);
        stream.Position = 0;
        var buffer = new byte[4];
        var memory = buffer.AsMemory();

        var count = await stream.ReadAsync(memory);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ReadAsync_Array_Reads()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);
        stream.Position = 1;
        var buffer = new byte[4];

#pragma warning disable CA1835 // The test deliberately calls the array overload.
        var count = await stream.ReadAsync(buffer, 1, 3, CancellationToken.None);
#pragma warning restore CA1835

        Assert.Equal(2, count);
        Assert.Equal<byte>([0, 2, 3, 0], buffer);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public async Task WriteAsync_Memory_Writes()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        byte[] data = [1, 2, 3];
        var memory = data.AsMemory();

        await stream.WriteAsync(memory, CancellationToken.None);

        Assert.Equal(3, stream.Position);
        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([1, 2, 3], written);
    }

    [Fact]
    public async Task WriteAsync_WithoutAToken_Writes()
    {
        using PooledMemoryStream stream = BufferFactory.CreatePooledStream(isSensitive: false);
        byte[] data = [1, 2, 3];
        var memory = data.AsMemory();

        await stream.WriteAsync(memory);

        Assert.Equal(3, stream.Length);
    }

    [Fact]
    public async Task WriteAsync_Array_Writes()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        byte[] data = [1, 2, 3, 4];

#pragma warning disable CA1835 // The test deliberately calls the array overload.
        await stream.WriteAsync(data, 1, 2, CancellationToken.None);
#pragma warning restore CA1835

        Assert.Equal(2, stream.Position);
        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([2, 3], written);
    }

    [Fact]
    public void ReadAsync_Canceled_ReturnsACanceledTask()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);
        stream.Position = 0;
        var buffer = new byte[3];
        var memory = buffer.AsMemory();
        var canceled = new CancellationToken(canceled: true);

        var task = stream.ReadAsync(memory, canceled);

        Assert.True(task.IsCanceled);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void ReadAsync_ArrayCanceled_ReturnsACanceledTask()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);
        stream.Position = 0;
        var buffer = new byte[3];
        var canceled = new CancellationToken(canceled: true);

        var task = stream.ReadAsync(buffer, 0, 3, canceled);

        Assert.True(task.IsCanceled);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void WriteAsync_Canceled_ReturnsACanceledTask()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        byte[] data = [1, 2, 3];
        var memory = data.AsMemory();
        var canceled = new CancellationToken(canceled: true);

        var task = stream.WriteAsync(memory, canceled);

        Assert.True(task.IsCanceled);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void WriteAsync_ArrayCanceled_ReturnsACanceledTask()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        byte[] data = [1, 2, 3];
        var canceled = new CancellationToken(canceled: true);

        var task = stream.WriteAsync(data, 0, 3, canceled);

        Assert.True(task.IsCanceled);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task FlushAsync_Completes()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);

        var task = stream.FlushAsync(CancellationToken.None);
        await task;

        Assert.True(task.IsCompletedSuccessfully);
        var written = stream.WrittenSpan.ToArray();
        Assert.Equal<byte>([1, 2, 3], written);
    }

    [Fact]
    public void FlushAsync_Canceled_ReturnsACanceledTask()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        var canceled = new CancellationToken(canceled: true);

        var task = stream.FlushAsync(canceled);

        Assert.True(task.IsCanceled);
    }

    [Fact]
    public void FlushAsync_AfterDispose_CompletesSuccessfully()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Dispose();

        var task = stream.FlushAsync(CancellationToken.None);

        Assert.True(task.IsCompletedSuccessfully);
    }

    #endregion

    #region CopyTo Tests

    [Fact]
    public void CopyTo_WritesTheRestAndMovesToTheEnd()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3, 4, 5]);
        stream.Position = 2;
        using var destination = new MemoryStream();

        stream.CopyTo(destination);

        var copied = destination.ToArray();
        Assert.Equal<byte>([3, 4, 5], copied);
        Assert.Equal(5, stream.Position);
    }

    [Fact]
    public void CopyTo_AtEnd_WritesNothing()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        using var destination = new MemoryStream();

        stream.CopyTo(destination);

        Assert.Equal(0, destination.Length);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void CopyTo_PastEnd_WritesNothingAndKeepsPosition()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        stream.Position = 10;
        using var destination = new MemoryStream();

        stream.CopyTo(destination);

        Assert.Equal(0, destination.Length);
        Assert.Equal(10, stream.Position);
    }

    [Fact]
    public void CopyTo_NullDestination_ThrowsArgumentNullException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.Throws<ArgumentNullException>(() => stream.CopyTo(null!));
    }

    [Fact]
    public void CopyTo_WhenDisposed_ThrowsObjectDisposedException()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Dispose();
        using var destination = new MemoryStream();

        Assert.Throws<ObjectDisposedException>(() => stream.CopyTo(destination));
    }

    #endregion

    #region CopyToAsync Tests

    [Fact]
    public async Task CopyToAsync_WritesTheRestAndMovesToTheEnd()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3, 4, 5]);
        stream.Position = 2;
        using var destination = new MemoryStream();

        await stream.CopyToAsync(destination, CancellationToken.None);

        var copied = destination.ToArray();
        Assert.Equal<byte>([3, 4, 5], copied);
        Assert.Equal(5, stream.Position);
    }

    [Fact]
    public async Task CopyToAsync_PastEnd_WritesNothingAndKeepsPosition()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        stream.Position = 10;
        using var destination = new MemoryStream();

        await stream.CopyToAsync(destination, CancellationToken.None);

        Assert.Equal(0, destination.Length);
        Assert.Equal(10, stream.Position);
    }

    [Fact]
    public void CopyToAsync_Canceled_ReturnsACanceledTask()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write([1, 2, 3]);
        stream.Position = 0;
        using var destination = new MemoryStream();
        var canceled = new CancellationToken(canceled: true);

        var task = stream.CopyToAsync(destination, canceled);

        Assert.True(task.IsCanceled);
        Assert.Equal(0, destination.Length);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void CopyToAsync_NullDestination_ThrowsArgumentNullException()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = stream.CopyToAsync(null!, CancellationToken.None);
        });
    }

    [Fact]
    public void CopyToAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Dispose();
        using var destination = new MemoryStream();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = stream.CopyToAsync(destination, CancellationToken.None);
        });
    }

    [Fact]
    public async Task CopyToAsync_DestinationFails_ReturnsAFaultedTask()
    {
        using var stream = BufferFactory.CreatePooledStream(isSensitive: false);
        stream.Write([1, 2, 3]);
        stream.Position = 1;
        using var destination = new FaultingStream();

        var task = stream.CopyToAsync(destination, CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => task);
        Assert.Equal(3, stream.Position);
    }

    private sealed class FaultingStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            var exception = new IOException();
            return ValueTask.FromException(exception);
        }
    }

    #endregion
}
