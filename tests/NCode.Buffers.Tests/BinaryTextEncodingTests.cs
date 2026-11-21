// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Buffers.Tests;

// The tests that read the shared secure pool's queue must not run beside other tests that rent from the pool.
[CollectionDefinition(nameof(BinaryTextEncodingTests), DisableParallelization = true)]
[Collection(nameof(BinaryTextEncodingTests))]
public class BinaryTextEncodingTests
{
    #region RentDecoded Tests

    [Theory]
    [InlineData("+/8B", BinaryTextFormat.Base64)]
    [InlineData("-_8B", BinaryTextFormat.Base64Url)]
    [InlineData("-_8B", BinaryTextFormat.Base64UrlStrict)]
    [InlineData("FBFF01", BinaryTextFormat.HexUpper)]
    [InlineData("fbff01", BinaryTextFormat.HexLower)]
    public void RentDecoded_KnownText_ReturnsTheExactBytes(string text, BinaryTextFormat format)
    {
        using var owner = BinaryTextEncoding.RentDecoded(
            text,
            format,
            isSensitive: false,
            out Span<byte> bytes
        );

        var actual = bytes.ToArray();

        Assert.Equal<byte>([0xFB, 0xFF, 0x01], actual);
    }

    [Fact]
    public void RentDecoded_WithMemory_ReturnsTheExactBytes()
    {
        const string text = "+/8B";

        using var owner = BinaryTextEncoding.RentDecoded(
            text,
            BinaryTextFormat.Base64,
            isSensitive: false,
            out Memory<byte> bytes
        );

        var actual = bytes.Span.ToArray();

        Assert.Equal<byte>([0xFB, 0xFF, 0x01], actual);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("AA", "00")]
    [InlineData("AQ", "01")]
    [InlineData("AAE", "0001")]
    [InlineData("QUJD", "414243")]
    [InlineData("-_8B", "FBFF01")]
    public void RentDecoded_Base64UrlStrictCanonicalText_ReturnsTheBytes(
        string text,
        string expectedHex
    )
    {
        using var owner = BinaryTextEncoding.RentDecoded(
            text,
            BinaryTextFormat.Base64UrlStrict,
            isSensitive: false,
            out Span<byte> bytes
        );

        var expected = Convert.FromHexString(expectedHex);
        var actual = bytes.ToArray();

        Assert.Equal<byte>(expected, actual);
    }

    [Theory]
    [InlineData("AA==")]
    [InlineData("AAA=")]
    [InlineData("AA=")]
    [InlineData("A A")]
    [InlineData(" AA")]
    [InlineData("AA\n")]
    [InlineData("+/8B")]
    public void RentDecoded_Base64UrlStrictTextOutsideTheAlphabet_ThrowsTheAlphabetMessage(
        string text
    )
    {
        var exception = Assert.Throws<FormatException>(() =>
        {
            using var owner = BinaryTextEncoding.RentDecoded(
                text,
                BinaryTextFormat.Base64UrlStrict,
                isSensitive: false,
                out Span<byte> _
            );
        });

        Assert.Equal(BinaryTextEncoding.StrictBase64UrlAlphabetMessage, exception.Message);
    }

    [Theory]
    [InlineData("AR")]
    [InlineData("AAB")]
    [InlineData("A")]
    [InlineData("AAAAA")]
    public void RentDecoded_Base64UrlStrictTextWithBitsOrACharacterOver_ThrowsTheLengthMessage(
        string text
    )
    {
        var exception = Assert.Throws<FormatException>(() =>
        {
            using var owner = BinaryTextEncoding.RentDecoded(
                text,
                BinaryTextFormat.Base64UrlStrict,
                isSensitive: false,
                out Span<byte> _
            );
        });

        Assert.Equal(BinaryTextEncoding.StrictBase64UrlLengthMessage, exception.Message);
    }

    [Theory]
    [InlineData("AA==")]
    [InlineData("AA=")]
    [InlineData("A A")]
    [InlineData("AA\n")]
    public void RentDecoded_Base64UrlPaddedOrSpacedText_Decodes(string text)
    {
        using var owner = BinaryTextEncoding.RentDecoded(
            text,
            BinaryTextFormat.Base64Url,
            isSensitive: false,
            out Span<byte> bytes
        );

        var actual = bytes.ToArray();

        Assert.Equal<byte>([0x00], actual);
    }

    [Fact]
    public void RentDecoded_Base64WithWhitespace_Decodes()
    {
        const string text = "+/8B ";

        using var owner = BinaryTextEncoding.RentDecoded(
            text,
            BinaryTextFormat.Base64,
            isSensitive: false,
            out Span<byte> bytes
        );

        var actual = bytes.ToArray();

        Assert.Equal<byte>([0xFB, 0xFF, 0x01], actual);
    }

    [Theory]
    [InlineData("+/8*", BinaryTextFormat.Base64)]
    [InlineData("-_8*", BinaryTextFormat.Base64Url)]
    [InlineData("-_8*", BinaryTextFormat.Base64UrlStrict)]
    [InlineData("FBF", BinaryTextFormat.HexUpper)]
    [InlineData("ZZ", BinaryTextFormat.HexLower)]
    public void RentDecoded_InvalidText_ThrowsFormatException(string text, BinaryTextFormat format)
    {
        Assert.Throws<FormatException>(() =>
        {
            using var owner = BinaryTextEncoding.RentDecoded(
                text,
                format,
                isSensitive: false,
                out Span<byte> _
            );
        });
    }

    [Theory]
    [InlineData("+/8*", BinaryTextFormat.Base64)]
    [InlineData("-_8*", BinaryTextFormat.Base64Url)]
    [InlineData("-_8*", BinaryTextFormat.Base64UrlStrict)]
    [InlineData("FBF", BinaryTextFormat.HexUpper)]
    [InlineData("ZZ", BinaryTextFormat.HexLower)]
    public void RentDecoded_InvalidTextWithMemory_ThrowsFormatException(
        string text,
        BinaryTextFormat format
    )
    {
        Assert.Throws<FormatException>(() =>
        {
            using var owner = BinaryTextEncoding.RentDecoded(
                text,
                format,
                isSensitive: true,
                out Memory<byte> _
            );
        });
    }

    [Fact]
    public void RentDecoded_InvalidTextWhenSensitive_ReturnsTheZeroedBuffer()
    {
        const string text = "4141ZZ";
        SecureMemoryPool<byte>.Shared.MemoryQueue.Clear();

        Assert.Throws<FormatException>(() =>
        {
            using var owner = BinaryTextEncoding.RentDecoded(
                text,
                BinaryTextFormat.HexUpper,
                isSensitive: true,
                out Span<byte> _
            );
        });

        var isReturned = SecureMemoryPool<byte>.Shared.MemoryQueue.TryPeek(out var page);
        Assert.True(isReturned);
        Assert.NotNull(page);
        Span<byte> pageBytes = page;
        var firstNonZero = pageBytes.IndexOfAnyExcept((byte)0);
        Assert.Equal(-1, firstNonZero);
    }

    [Fact]
    public void RentDecoded_InvalidTextWithMemoryWhenSensitive_ReturnsTheZeroedBuffer()
    {
        const string text = "4141ZZ";
        SecureMemoryPool<byte>.Shared.MemoryQueue.Clear();

        Assert.Throws<FormatException>(() =>
        {
            using var owner = BinaryTextEncoding.RentDecoded(
                text,
                BinaryTextFormat.HexUpper,
                isSensitive: true,
                out Memory<byte> _
            );
        });

        var isReturned = SecureMemoryPool<byte>.Shared.MemoryQueue.TryPeek(out var page);
        Assert.True(isReturned);
        Assert.NotNull(page);
        Span<byte> pageBytes = page;
        var firstNonZero = pageBytes.IndexOfAnyExcept((byte)0);
        Assert.Equal(-1, firstNonZero);
    }

    [Fact]
    public void RentDecoded_UndefinedFormat_ThrowsArgumentOutOfRangeException()
    {
        const string text = "+/8B";

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var owner = BinaryTextEncoding.RentDecoded(
                text,
                (BinaryTextFormat)42,
                isSensitive: false,
                out Span<byte> _
            );
        });

        Assert.Equal("format", exception.ParamName);
    }

    [Fact]
    public void RentDecoded_WhenSensitive_ZeroesTheBytesOnDispose()
    {
        const int length = 5001;
        var data = new byte[length];
        Array.Fill(data, (byte)0x41);
        var text = Convert.ToBase64String(data);

        var owner = BinaryTextEncoding.RentDecoded(
            text,
            BinaryTextFormat.Base64,
            isSensitive: true,
            out Memory<byte> bytes
        );
        var decoded = bytes.ToArray();
        Assert.Equal(length, bytes.Length);
        Assert.All(decoded, value => Assert.Equal(0x41, value));

        owner.Dispose();

        var firstNonZero = owner.Memory.Span.IndexOfAnyExcept((byte)0);
        Assert.Equal(-1, firstNonZero);
    }

    #endregion

    #region RentEncoded Tests

    [Theory]
    [InlineData("+/8B", BinaryTextFormat.Base64)]
    [InlineData("-_8B", BinaryTextFormat.Base64Url)]
    [InlineData("-_8B", BinaryTextFormat.Base64UrlStrict)]
    [InlineData("FBFF01", BinaryTextFormat.HexUpper)]
    [InlineData("fbff01", BinaryTextFormat.HexLower)]
    public void RentEncoded_KnownBytes_ReturnsTheExactText(string expected, BinaryTextFormat format)
    {
        using var owner = BinaryTextEncoding.RentEncoded(
            [0xFB, 0xFF, 0x01],
            format,
            isSensitive: false,
            out Span<char> text
        );

        var actual = text.ToString();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RentEncoded_WithMemory_ReturnsTheExactText()
    {
        using var owner = BinaryTextEncoding.RentEncoded(
            [0xFB, 0xFF, 0x01],
            BinaryTextFormat.Base64,
            isSensitive: false,
            out Memory<char> text
        );

        var actual = text.Span.ToString();

        Assert.Equal("+/8B", actual);
    }

    [Theory]
    [InlineData(BinaryTextFormat.Base64)]
    [InlineData(BinaryTextFormat.Base64Url)]
    [InlineData(BinaryTextFormat.Base64UrlStrict)]
    [InlineData(BinaryTextFormat.HexUpper)]
    [InlineData(BinaryTextFormat.HexLower)]
    public void RentEncoded_EmptyBytes_ReturnsEmpty(BinaryTextFormat format)
    {
        using var owner = BinaryTextEncoding.RentEncoded(
            [],
            format,
            isSensitive: false,
            out Span<char> text
        );

        Assert.True(text.IsEmpty);
    }

    [Fact]
    public void RentEncoded_UndefinedFormat_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var owner = BinaryTextEncoding.RentEncoded(
                [0xFB, 0xFF, 0x01],
                (BinaryTextFormat)42,
                isSensitive: false,
                out Span<char> _
            );
        });

        Assert.Equal("format", exception.ParamName);
    }

    [Theory]
    [InlineData(BinaryTextFormat.Base64, 0)]
    [InlineData(BinaryTextFormat.Base64, 1)]
    [InlineData(BinaryTextFormat.Base64, 2)]
    [InlineData(BinaryTextFormat.Base64, 3)]
    [InlineData(BinaryTextFormat.Base64, 64)]
    [InlineData(BinaryTextFormat.Base64Url, 0)]
    [InlineData(BinaryTextFormat.Base64Url, 1)]
    [InlineData(BinaryTextFormat.Base64Url, 2)]
    [InlineData(BinaryTextFormat.Base64Url, 3)]
    [InlineData(BinaryTextFormat.Base64Url, 64)]
    [InlineData(BinaryTextFormat.Base64UrlStrict, 0)]
    [InlineData(BinaryTextFormat.Base64UrlStrict, 1)]
    [InlineData(BinaryTextFormat.Base64UrlStrict, 2)]
    [InlineData(BinaryTextFormat.Base64UrlStrict, 3)]
    [InlineData(BinaryTextFormat.Base64UrlStrict, 64)]
    [InlineData(BinaryTextFormat.HexUpper, 0)]
    [InlineData(BinaryTextFormat.HexUpper, 1)]
    [InlineData(BinaryTextFormat.HexUpper, 2)]
    [InlineData(BinaryTextFormat.HexUpper, 3)]
    [InlineData(BinaryTextFormat.HexUpper, 64)]
    [InlineData(BinaryTextFormat.HexLower, 0)]
    [InlineData(BinaryTextFormat.HexLower, 1)]
    [InlineData(BinaryTextFormat.HexLower, 2)]
    [InlineData(BinaryTextFormat.HexLower, 3)]
    [InlineData(BinaryTextFormat.HexLower, 64)]
    public void RentEncoded_ThenRentDecoded_RoundTrips(BinaryTextFormat format, int length)
    {
        var expected = RandomNumberGenerator.GetBytes(length);

        using var encodedOwner = BinaryTextEncoding.RentEncoded(
            expected,
            format,
            isSensitive: false,
            out Span<char> text
        );
        using var decodedOwner = BinaryTextEncoding.RentDecoded(
            text,
            format,
            isSensitive: false,
            out Span<byte> actual
        );

        var actualBytes = actual.ToArray();

        Assert.Equal<byte>(expected, actualBytes);
    }

    #endregion
}
