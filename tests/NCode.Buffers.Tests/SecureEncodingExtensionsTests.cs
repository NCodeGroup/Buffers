// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace NCode.Buffers.Tests;

// The tests that read the shared secure pool's queue must not run beside other tests that rent from the pool.
[CollectionDefinition(nameof(SecureEncodingExtensionsTests), DisableParallelization = true)]
[Collection(nameof(SecureEncodingExtensionsTests))]
public class SecureEncodingExtensionsTests
{
    private sealed class FailingEncoding : Encoding
    {
        public override int GetByteCount(char[] chars, int index, int count) => count;

        public override int GetByteCount(ReadOnlySpan<char> chars) => chars.Length;

        public override int GetBytes(
            char[] chars,
            int charIndex,
            int charCount,
            byte[] bytes,
            int byteIndex
        ) => throw new InvalidOperationException();

        public override int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            bytes.Fill(0x5A);
            throw new InvalidOperationException();
        }

        public override int GetCharCount(byte[] bytes, int index, int count) => count;

        public override int GetCharCount(ReadOnlySpan<byte> bytes) => bytes.Length;

        public override int GetChars(
            byte[] bytes,
            int byteIndex,
            int byteCount,
            char[] chars,
            int charIndex
        ) => throw new InvalidOperationException();

        public override int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars)
        {
            chars.Fill('x');
            throw new InvalidOperationException();
        }

        public override int GetMaxByteCount(int charCount) => charCount;

        public override int GetMaxCharCount(int byteCount) => byteCount;
    }

    #region RentBytes Tests

    [Fact]
    public void RentBytes_Utf8Text_ReturnsTheExactBytes()
    {
        const string text = "café";

        using var owner = SecureEncoding.UTF8.RentBytes(
            text,
            isSensitive: false,
            out Span<byte> bytes
        );

        Assert.Equal(5, bytes.Length);
        Assert.True(bytes.SequenceEqual("café"u8));
    }

    [Fact]
    public void RentBytes_WithMemory_ReturnsTheExactBytes()
    {
        const string text = "café";

        using var owner = SecureEncoding.UTF8.RentBytes(
            text,
            isSensitive: true,
            out Memory<byte> bytes
        );

        Assert.Equal(5, bytes.Length);
        Assert.True(bytes.Span.SequenceEqual("café"u8));
    }

    [Fact]
    public void RentBytes_EmptyText_ReturnsEmpty()
    {
        using var owner = SecureEncoding.UTF8.RentBytes(
            "",
            isSensitive: false,
            out Span<byte> bytes
        );

        Assert.True(bytes.IsEmpty);
    }

    [Fact]
    public void RentBytes_InvalidText_ThrowsEncoderFallbackException()
    {
        const string text = "\uD800";

        Assert.Throws<EncoderFallbackException>(() =>
        {
            using var owner = SecureEncoding.UTF8.RentBytes(
                text,
                isSensitive: true,
                out Span<byte> _
            );
        });
    }

    [Fact]
    public void RentBytes_NullEncoding_ThrowsArgumentNullException()
    {
        Encoding encoding = null!;

        Assert.Throws<ArgumentNullException>(() =>
        {
            using var owner = encoding.RentBytes("text", isSensitive: false, out Span<byte> _);
        });
    }

    [Fact]
    public void RentBytes_WhenSensitive_ZeroesTheBytesOnDispose()
    {
        var text = new string('a', 5000);

        var owner = SecureEncoding.UTF8.RentBytes(text, isSensitive: true, out Memory<byte> bytes);
        Assert.Equal(5000, bytes.Length);
        Assert.True(bytes.Span.SequenceEqual(new string('a', 5000).Select(c => (byte)c).ToArray()));

        owner.Dispose();

        Assert.True(owner.Memory.Span[..5000].IndexOfAnyExcept((byte)0) < 0);
    }

    [Fact]
    public void RentBytes_EncodingThrowsWhileFilling_Rethrows()
    {
        var encoding = new FailingEncoding();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var owner = encoding.RentBytes("text", isSensitive: true, out Span<byte> _);
        });
    }

    [Fact]
    public void RentBytes_EncodingThrowsWhileFillingMemory_Rethrows()
    {
        var encoding = new FailingEncoding();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var owner = encoding.RentBytes("text", isSensitive: true, out Memory<byte> _);
        });
    }

    [Fact]
    public void RentBytes_EncodingThrowsWhileFilling_ReturnsTheZeroedBuffer()
    {
        var encoding = new FailingEncoding();
        SecureMemoryPool<byte>.Shared.MemoryQueue.Clear();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var owner = encoding.RentBytes("text", isSensitive: true, out Span<byte> _);
        });

        var isReturned = SecureMemoryPool<byte>.Shared.MemoryQueue.TryPeek(out var page);
        Assert.True(isReturned);
        Assert.NotNull(page);
        Span<byte> pageBytes = page;
        var firstNonZero = pageBytes.IndexOfAnyExcept((byte)0);
        Assert.Equal(-1, firstNonZero);
    }

    [Fact]
    public void RentBytes_EncodingThrowsWhileFillingMemory_ReturnsTheZeroedBuffer()
    {
        var encoding = new FailingEncoding();
        SecureMemoryPool<byte>.Shared.MemoryQueue.Clear();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var owner = encoding.RentBytes("text", isSensitive: true, out Memory<byte> _);
        });

        var isReturned = SecureMemoryPool<byte>.Shared.MemoryQueue.TryPeek(out var page);
        Assert.True(isReturned);
        Assert.NotNull(page);
        Span<byte> pageBytes = page;
        var firstNonZero = pageBytes.IndexOfAnyExcept((byte)0);
        Assert.Equal(-1, firstNonZero);
    }

    #endregion

    #region RentChars Tests

    [Fact]
    public void RentChars_Utf8Bytes_ReturnsTheExactChars()
    {
        using var owner = SecureEncoding.UTF8.RentChars(
            "café"u8,
            isSensitive: false,
            out Span<char> chars
        );

        Assert.Equal(4, chars.Length);
        Assert.True(chars.SequenceEqual("café"));
    }

    [Fact]
    public void RentChars_WithMemory_ReturnsTheExactChars()
    {
        using var owner = SecureEncoding.UTF8.RentChars(
            "café"u8,
            isSensitive: true,
            out Memory<char> chars
        );

        Assert.Equal(4, chars.Length);
        Assert.True(chars.Span.SequenceEqual("café"));
    }

    [Fact]
    public void RentChars_InvalidBytes_ThrowsDecoderFallbackException()
    {
        Assert.Throws<DecoderFallbackException>(() =>
        {
            using var owner = SecureEncoding.UTF8.RentChars(
                [0xC3, 0x28],
                isSensitive: true,
                out Span<char> _
            );
        });
    }

    [Fact]
    public void RentChars_NullEncoding_ThrowsArgumentNullException()
    {
        Encoding encoding = null!;

        Assert.Throws<ArgumentNullException>(() =>
        {
            using var owner = encoding.RentChars("text"u8, isSensitive: false, out Span<char> _);
        });
    }

    [Fact]
    public void RentChars_EncodingThrowsWhileFilling_Rethrows()
    {
        var encoding = new FailingEncoding();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var owner = encoding.RentChars("text"u8, isSensitive: true, out Span<char> _);
        });
    }

    [Fact]
    public void RentChars_EncodingThrowsWhileFillingMemory_Rethrows()
    {
        var encoding = new FailingEncoding();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var owner = encoding.RentChars("text"u8, isSensitive: true, out Memory<char> _);
        });
    }

    [Fact]
    public void RentChars_EncodingThrowsWhileFilling_ReturnsTheZeroedBuffer()
    {
        var encoding = new FailingEncoding();
        SecureMemoryPool<char>.Shared.MemoryQueue.Clear();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var owner = encoding.RentChars("text"u8, isSensitive: true, out Span<char> _);
        });

        var isReturned = SecureMemoryPool<char>.Shared.MemoryQueue.TryPeek(out var page);
        Assert.True(isReturned);
        Assert.NotNull(page);
        Span<char> pageChars = page;
        var firstNonZero = pageChars.IndexOfAnyExcept('\0');
        Assert.Equal(-1, firstNonZero);
    }

    [Fact]
    public void RentChars_EncodingThrowsWhileFillingMemory_ReturnsTheZeroedBuffer()
    {
        var encoding = new FailingEncoding();
        SecureMemoryPool<char>.Shared.MemoryQueue.Clear();

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var owner = encoding.RentChars("text"u8, isSensitive: true, out Memory<char> _);
        });

        var isReturned = SecureMemoryPool<char>.Shared.MemoryQueue.TryPeek(out var page);
        Assert.True(isReturned);
        Assert.NotNull(page);
        Span<char> pageChars = page;
        var firstNonZero = pageChars.IndexOfAnyExcept('\0');
        Assert.Equal(-1, firstNonZero);
    }

    #endregion
}
