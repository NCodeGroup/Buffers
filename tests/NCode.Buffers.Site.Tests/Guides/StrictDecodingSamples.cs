// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Unicode;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/strict-decoding.mdx. The site's build includes each begin-snippet region in the
// page by its key (ADR-1010). The usual code of the page is SiteLenientDecodingCollision, in
// Concepts/HowSecretsLeakSamples.cs, which the page includes and this file does not copy.
public class StrictDecodingSamples
{
    [Fact]
    public void SiteStrictDecodingRejectSample_Runs()
    {
        // begin-snippet: SiteStrictDecodingReject
        static string ReadName(ReadOnlySpan<byte> input)
        {
            try
            {
                return SecureEncoding.UTF8.GetString(input);
            }
            catch (DecoderFallbackException)
            {
                return "invalid input";
            }
        }
        // end-snippet

        Assert.Equal("café", ReadName("café"u8));
        Assert.Equal("invalid input", ReadName([0x61, 0x80]));

        // The exception carries the bytes it rejected.
        byte[] invalid = [0x61, 0x80];
        var exception = Assert.Throws<DecoderFallbackException>(() =>
            SecureEncoding.UTF8.GetString(invalid)
        );
        Assert.NotNull(exception.BytesUnknown);
        Assert.Equal<byte>([0x80], exception.BytesUnknown);
    }

    [Fact]
    public void SiteStrictDecodingStreamReaderSample_Runs()
    {
        // begin-snippet: SiteStrictDecodingStreamReader
        static string ReadAll(Stream stream)
        {
            using var reader = new StreamReader(
                stream,
                SecureEncoding.UTF8,
                detectEncodingFromByteOrderMarks: false
            );
            return reader.ReadToEnd();
        }
        // end-snippet

        Assert.Throws<DecoderFallbackException>(() =>
            ReadAll(new MemoryStream([0x61, 0xFF]))
        );
        Assert.Equal("café", ReadAll(new MemoryStream("café"u8.ToArray())));

        // A byte order mark switches the decoder, so a stream that starts with one is read
        // as the encoding the mark names unless the reader is told not to look.
        byte[] withMark = [0xFF, 0xFE, 0x61, 0x00];
        Assert.Throws<DecoderFallbackException>(() =>
            ReadAll(new MemoryStream(withMark))
        );

        using var detecting = new StreamReader(
            new MemoryStream(withMark),
            SecureEncoding.UTF8,
            detectEncodingFromByteOrderMarks: true
        );
        Assert.Equal("a", detecting.ReadToEnd());
    }

    [Fact]
    public void SiteStrictAsciiSample_Runs()
    {
        // begin-snippet: SiteStrictAscii
        // Throws DecoderFallbackException on a byte above 0x7F.
        static string Decode(byte[] bytes) => SecureEncoding.ASCII.GetString(bytes);

        // Throws EncoderFallbackException on a character above U+007F.
        static byte[] Encode(string text) => SecureEncoding.ASCII.GetBytes(text);

        // The standard encoding replaces the byte with a question mark and goes on.
        static string DecodeLenient(byte[] bytes) => Encoding.ASCII.GetString(bytes);
        // end-snippet

        byte[] input = [0x48, 0x69, 0x80];
        Assert.Throws<DecoderFallbackException>(() => Decode(input));
        Assert.Throws<EncoderFallbackException>(() => Encode("é"));
        Assert.Equal("Hi?", DecodeLenient(input));
        Assert.Equal("Hi", Decode([0x48, 0x69]));
    }

    [Fact]
    public void SiteStrictDecodingValidateOnlySample_Runs()
    {
        // begin-snippet: SiteStrictDecodingValidateOnly
        ReadOnlySpan<byte> input = "café"u8;

        bool isUtf8 = Utf8.IsValid(input); // true: the bytes are well-formed UTF-8.
        bool isAscii = Ascii.IsValid(input); // false: the é is two bytes above 0x7F.
        // end-snippet

        Assert.True(isUtf8);
        Assert.False(isAscii);

        ReadOnlySpan<byte> broken = [0x61, 0x80];
        Assert.False(Utf8.IsValid(broken));
        Assert.False(Ascii.IsValid(broken));
    }

    [Fact]
    public void SiteStrictExamplesTableSample_Runs()
    {
        // begin-snippet: SiteStrictExamplesTable
        (byte[] Input, string Lenient)[] rows =
        [
            ([0x61, 0x80], "a\uFFFD"), // a continuation byte with nothing to continue
            ([0x61, 0xFF], "a\uFFFD"), // a byte that never occurs in UTF-8
            ([0xC0, 0xAF], "\uFFFD\uFFFD"), // an overlong form of "/"
            ([0xED, 0xA0, 0x80], "\uFFFD\uFFFD\uFFFD"), // a surrogate code point
            ([0xE2, 0x82], "\uFFFD"), // a sequence cut short
            ([0xC3, 0xA9], "é"), // valid: é
        ];
        // end-snippet

        foreach (var (input, lenient) in rows[..5])
        {
            Assert.Equal(lenient, Encoding.UTF8.GetString(input));
            Assert.Throws<DecoderFallbackException>(() =>
                SecureEncoding.UTF8.GetString(input)
            );
        }

        var valid = rows[5];
        Assert.Equal(valid.Lenient, Encoding.UTF8.GetString(valid.Input));
        Assert.Equal("é", SecureEncoding.UTF8.GetString(valid.Input));
    }
}
