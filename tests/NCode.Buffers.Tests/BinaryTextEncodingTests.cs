// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
}
