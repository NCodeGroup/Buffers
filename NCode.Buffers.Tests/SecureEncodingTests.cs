#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Text;

namespace NCode.Buffers.Tests;

public class SecureEncodingTests
{
    #region ASCII Property Tests

    [Fact]
    public void ASCII_ReturnsASCIIEncodingType()
    {
        var encoding = SecureEncoding.ASCII;

        Assert.IsType<ASCIIEncoding>(encoding);
    }

    [Fact]
    public void ASCII_HasNoPreamble()
    {
        var encoding = SecureEncoding.ASCII;

        Assert.Empty(encoding.GetPreamble());
    }

    [Fact]
    public void ASCII_HasExceptionEncoderFallback()
    {
        var encoding = SecureEncoding.ASCII;

        Assert.Same(EncoderFallback.ExceptionFallback, encoding.EncoderFallback);
    }

    [Fact]
    public void ASCII_HasExceptionDecoderFallback()
    {
        var encoding = SecureEncoding.ASCII;

        Assert.Same(DecoderFallback.ExceptionFallback, encoding.DecoderFallback);
    }

    [Fact]
    public void ASCII_ReturnsSameInstance()
    {
        var encoding1 = SecureEncoding.ASCII;
        var encoding2 = SecureEncoding.ASCII;

        Assert.Same(encoding1, encoding2);
    }

    #endregion

    #region ASCII Encoding Tests

    [Fact]
    public void ASCII_EncodeValidString_Succeeds()
    {
        var encoding = SecureEncoding.ASCII;
        const string input = "Hello, World!";

        var bytes = encoding.GetBytes(input);

        Assert.Equal(13, bytes.Length);
        Assert.Equal((byte)'H', bytes[0]);
        Assert.Equal((byte)'!', bytes[12]);
    }

    [Fact]
    public void ASCII_EncodeEmptyString_ReturnsEmptyArray()
    {
        var encoding = SecureEncoding.ASCII;

        var bytes = encoding.GetBytes("");

        Assert.Empty(bytes);
    }

    [Fact]
    public void ASCII_EncodeAllValidAsciiCharacters_Succeeds()
    {
        var encoding = SecureEncoding.ASCII;
        var allAscii = new string(Enumerable.Range(0, 128).Select(i => (char)i).ToArray());

        var bytes = encoding.GetBytes(allAscii);

        Assert.Equal(128, bytes.Length);
        for (var i = 0; i < 128; i++)
        {
            Assert.Equal((byte)i, bytes[i]);
        }
    }

    [Fact]
    public void ASCII_EncodeNonAsciiCharacter_ThrowsEncoderFallbackException()
    {
        var encoding = SecureEncoding.ASCII;

        var exception = Assert.Throws<EncoderFallbackException>(() =>
            encoding.GetBytes("caf\u00E9"));

        Assert.Contains("00E9", exception.Message);
    }

    [Fact]
    public void ASCII_EncodeEmoji_ThrowsEncoderFallbackException()
    {
        var encoding = SecureEncoding.ASCII;

        var exception = Assert.Throws<EncoderFallbackException>(() =>
            encoding.GetBytes("\U0001F600"));

        Assert.Contains("1F600", exception.Message);
    }

    [Fact]
    public void ASCII_EncodeHighAsciiCharacter_ThrowsEncoderFallbackException()
    {
        var encoding = SecureEncoding.ASCII;

        Assert.Throws<EncoderFallbackException>(() =>
            encoding.GetBytes("\x80"));
    }

    #endregion

    #region ASCII Decoding Tests

    [Fact]
    public void ASCII_DecodeValidBytes_Succeeds()
    {
        var encoding = SecureEncoding.ASCII;
        var bytes = "Hello"u8.ToArray();

        var result = encoding.GetString(bytes);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void ASCII_DecodeEmptyArray_ReturnsEmptyString()
    {
        var encoding = SecureEncoding.ASCII;

        var result = encoding.GetString([]);

        Assert.Equal("", result);
    }

    [Fact]
    public void ASCII_DecodeAllValidAsciiBytes_Succeeds()
    {
        var encoding = SecureEncoding.ASCII;
        var allBytes = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();

        var result = encoding.GetString(allBytes);

        Assert.Equal(128, result.Length);
        for (var i = 0; i < 128; i++)
        {
            Assert.Equal((char)i, result[i]);
        }
    }

    [Fact]
    public void ASCII_DecodeInvalidByte_ThrowsDecoderFallbackException()
    {
        var encoding = SecureEncoding.ASCII;

        var exception = Assert.Throws<DecoderFallbackException>(() =>
            encoding.GetString((byte[])[0x80]));

        Assert.Equal(
            "Unable to translate bytes [80] at index 0 from specified code page to Unicode.",
            exception.Message);
    }

    [Fact]
    public void ASCII_DecodeInvalidByteInMiddle_ThrowsDecoderFallbackException()
    {
        var encoding = SecureEncoding.ASCII;

        var exception = Assert.Throws<DecoderFallbackException>(() =>
            encoding.GetString((byte[])[0x41, 0x42, 0xFF, 0x43]));

        Assert.Contains("index 2", exception.Message);
    }

    [Fact]
    public void ASCII_DecodeMultipleInvalidBytes_ThrowsOnFirst()
    {
        var encoding = SecureEncoding.ASCII;

        var exception = Assert.Throws<DecoderFallbackException>(() =>
            encoding.GetString((byte[])[0x80, 0x81, 0x82]));

        Assert.Contains("index 0", exception.Message);
    }

    #endregion

    #region UTF8 Property Tests

    [Fact]
    public void UTF8_ReturnsUTF8EncodingType()
    {
        var encoding = SecureEncoding.UTF8;

        Assert.IsType<UTF8Encoding>(encoding);
    }

    [Fact]
    public void UTF8_HasNoPreamble()
    {
        var encoding = SecureEncoding.UTF8;

        Assert.Empty(encoding.GetPreamble());
    }

    [Fact]
    public void UTF8_HasExceptionEncoderFallback()
    {
        var encoding = SecureEncoding.UTF8;

        Assert.Same(EncoderFallback.ExceptionFallback, encoding.EncoderFallback);
    }

    [Fact]
    public void UTF8_HasExceptionDecoderFallback()
    {
        var encoding = SecureEncoding.UTF8;

        Assert.Same(DecoderFallback.ExceptionFallback, encoding.DecoderFallback);
    }

    [Fact]
    public void UTF8_ReturnsSameInstance()
    {
        var encoding1 = SecureEncoding.UTF8;
        var encoding2 = SecureEncoding.UTF8;

        Assert.Same(encoding1, encoding2);
    }

    #endregion

    #region UTF8 Encoding Tests

    [Fact]
    public void UTF8_EncodeValidAsciiString_Succeeds()
    {
        var encoding = SecureEncoding.UTF8;
        const string input = "Hello, World!";

        var bytes = encoding.GetBytes(input);

        Assert.Equal(13, bytes.Length);
    }

    [Fact]
    public void UTF8_EncodeEmptyString_ReturnsEmptyArray()
    {
        var encoding = SecureEncoding.UTF8;

        var bytes = encoding.GetBytes("");

        Assert.Empty(bytes);
    }

    [Fact]
    public void UTF8_EncodeUnicodeString_Succeeds()
    {
        var encoding = SecureEncoding.UTF8;
        const string input = "caf\u00E9";

        var bytes = encoding.GetBytes(input);

        Assert.Equal(5, bytes.Length); // 'c', 'a', 'f', 'é' (2 bytes)
    }

    [Fact]
    public void UTF8_EncodeEmoji_Succeeds()
    {
        var encoding = SecureEncoding.UTF8;
        const string input = "\U0001F600";

        var bytes = encoding.GetBytes(input);

        Assert.Equal(4, bytes.Length); // Emoji is 4 bytes in UTF-8
    }

    [Fact]
    public void UTF8_EncodeUnpairedHighSurrogate_ThrowsEncoderFallbackException()
    {
        var encoding = SecureEncoding.UTF8;

        var exception = Assert.Throws<EncoderFallbackException>(() =>
            encoding.GetBytes("a\ud800b"));

        Assert.Equal(
            @"Unable to translate Unicode character \\uD800 at index 1 to specified code page.",
            exception.Message);
    }

    [Fact]
    public void UTF8_EncodeUnpairedLowSurrogate_ThrowsEncoderFallbackException()
    {
        var encoding = SecureEncoding.UTF8;

        Assert.Throws<EncoderFallbackException>(() =>
            encoding.GetBytes("a\udc00b"));
    }

    [Fact]
    public void UTF8_EncodeReversedSurrogatePair_ThrowsEncoderFallbackException()
    {
        var encoding = SecureEncoding.UTF8;

        Assert.Throws<EncoderFallbackException>(() =>
            encoding.GetBytes("\udc00\ud800")); // Low followed by high is invalid
    }

    #endregion

    #region UTF8 Decoding Tests

    [Fact]
    public void UTF8_DecodeValidAsciiBytes_Succeeds()
    {
        var encoding = SecureEncoding.UTF8;
        var bytes = "Hello"u8.ToArray();

        var result = encoding.GetString(bytes);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public void UTF8_DecodeEmptyArray_ReturnsEmptyString()
    {
        var encoding = SecureEncoding.UTF8;

        var result = encoding.GetString([]);

        Assert.Equal("", result);
    }

    [Fact]
    public void UTF8_DecodeValidMultiByteSequence_Succeeds()
    {
        var encoding = SecureEncoding.UTF8;
        var bytes = "\u00E9"u8.ToArray();

        var result = encoding.GetString(bytes);

        Assert.Equal("\u00E9", result);
    }

    [Fact]
    public void UTF8_DecodeValidEmoji_Succeeds()
    {
        var encoding = SecureEncoding.UTF8;
        var bytes = "\U0001F600"u8.ToArray();

        var result = encoding.GetString(bytes);

        Assert.Equal("\U0001F600", result);
    }

    [Fact]
    public void UTF8_DecodeInvalidStartByte_ThrowsDecoderFallbackException()
    {
        var encoding = SecureEncoding.UTF8;

        var exception = Assert.Throws<DecoderFallbackException>(() =>
            encoding.GetString((byte[])[0x80]));

        Assert.Equal(
            "Unable to translate bytes [80] at index 0 from specified code page to Unicode.",
            exception.Message);
    }

    [Fact]
    public void UTF8_DecodeIncompleteMultiByteSequence_ThrowsDecoderFallbackException()
    {
        var encoding = SecureEncoding.UTF8;

        Assert.Throws<DecoderFallbackException>(() =>
            encoding.GetString((byte[])[0xC3])); // Incomplete 2-byte sequence
    }

    [Fact]
    public void UTF8_DecodeInvalidContinuationByte_ThrowsDecoderFallbackException()
    {
        var encoding = SecureEncoding.UTF8;

        Assert.Throws<DecoderFallbackException>(() =>
            encoding.GetString((byte[])[0xC3, 0x00])); // Invalid continuation byte
    }

    [Fact]
    public void UTF8_DecodeOverlongEncoding_ThrowsDecoderFallbackException()
    {
        var encoding = SecureEncoding.UTF8;

        // Overlong encoding of '/' (U+002F) as 2 bytes instead of 1
        Assert.Throws<DecoderFallbackException>(() =>
            encoding.GetString((byte[])[0xC0, 0xAF]));
    }

    [Fact]
    public void UTF8_DecodeInvalidFourByteSequence_ThrowsDecoderFallbackException()
    {
        var encoding = SecureEncoding.UTF8;

        // Invalid 4-byte sequence (starts with F5, which is invalid)
        Assert.Throws<DecoderFallbackException>(() =>
            encoding.GetString((byte[])[0xF5, 0x80, 0x80, 0x80]));
    }

    #endregion

    #region Comparison with Standard Encoding Tests

    [Fact]
    public void ASCII_DiffersFromStandardEncoding()
    {
        var secure = SecureEncoding.ASCII;
        var standard = Encoding.ASCII;

        Assert.NotSame(secure, standard);
        Assert.NotSame(secure.EncoderFallback, standard.EncoderFallback);
        Assert.NotSame(secure.DecoderFallback, standard.DecoderFallback);
    }

    [Fact]
    public void UTF8_DiffersFromStandardEncoding()
    {
        var secure = SecureEncoding.UTF8;
        var standard = Encoding.UTF8;

        Assert.NotSame(secure, standard);
    }

    [Fact]
    public void StandardASCII_DoesNotThrowOnInvalidByte()
    {
        var standard = Encoding.ASCII;

        var result = standard.GetString((byte[])[0x80]);

        Assert.Equal("?", result); // Standard encoding uses replacement character
    }

    [Fact]
    public void StandardUTF8_DoesNotThrowOnInvalidByte()
    {
        var standard = Encoding.UTF8;

        var result = standard.GetString((byte[])[0x80]);

        Assert.Equal("\uFFFD", result); // Standard encoding uses replacement character
    }

    #endregion
}
