// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Buffers.Tests;

public class BinaryTextEncodingTests
{
    #region RentDecoded Tests

    [Theory]
    [InlineData("+/8B", BinaryTextFormat.Base64)]
    [InlineData("-_8B", BinaryTextFormat.Base64Url)]
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

        Assert.Equal<byte>([0xFB, 0xFF, 0x01], bytes.ToArray());
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

        Assert.Equal<byte>([0xFB, 0xFF, 0x01], bytes.Span.ToArray());
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

        Assert.Equal<byte>([0xFB, 0xFF, 0x01], bytes.ToArray());
    }

    [Theory]
    [InlineData("+/8*", BinaryTextFormat.Base64)]
    [InlineData("-_8*", BinaryTextFormat.Base64Url)]
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
        var text = Convert.ToBase64String(Enumerable.Repeat((byte)0x41, 5001).ToArray());

        var owner = BinaryTextEncoding.RentDecoded(
            text,
            BinaryTextFormat.Base64,
            isSensitive: true,
            out Memory<byte> bytes
        );
        Assert.Equal(5001, bytes.Length);
        Assert.All(bytes.ToArray(), value => Assert.Equal(0x41, value));

        owner.Dispose();

        Assert.True(owner.Memory.Span.IndexOfAnyExcept((byte)0) < 0);
    }

    #endregion

    #region RentEncoded Tests

    [Theory]
    [InlineData("+/8B", BinaryTextFormat.Base64)]
    [InlineData("-_8B", BinaryTextFormat.Base64Url)]
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

        Assert.Equal(expected, text.ToString());
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

        Assert.Equal("+/8B", text.Span.ToString());
    }

    [Theory]
    [InlineData(BinaryTextFormat.Base64)]
    [InlineData(BinaryTextFormat.Base64Url)]
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

        Assert.Equal<byte>(expected, actual.ToArray());
    }

    #endregion
}
