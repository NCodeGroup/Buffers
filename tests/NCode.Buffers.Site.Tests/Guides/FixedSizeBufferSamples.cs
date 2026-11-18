// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/fixed-size-buffer.mdx. The site's build includes each begin-snippet region in the
// page by its key (ADR-1010). The pitfalls "passing the writer by value" and "GetSpan(n) returns everything that is
// free" are SitePitfallWriterByValue and SitePitfallAdvanceBySpanLength, in Reference/PitfallsSamples.cs, which the
// page includes and this file does not copy.
public class FixedSizeBufferSamples
{
    [Fact]
    public void SiteFixedBufferUsualSample_Runs()
    {
        byte[] frame = RandomNumberGenerator.GetBytes(300);

        // begin-snippet: SiteFixedBufferUsual
        var writer = new ArrayBufferWriter<byte>(256);

        // The writer only starts at 256 bytes. The 300-byte frame makes it grow:
        // it allocates a larger array and copies the bytes written so far.
        writer.Write(frame);
        // end-snippet

        Assert.Equal(300, writer.WrittenCount);
        Assert.True(writer.Capacity > 256);
        SecretAssert.Equal(frame, writer.WrittenSpan);
    }

    [Fact]
    public void SiteFixedBufferFrameSample_Runs()
    {
        // begin-snippet: SiteFixedBufferFrame
        static void WriteFrame<TWriter>(ref TWriter writer, ReadOnlySpan<byte> payload)
            where TWriter : IBufferWriter<byte>, allows ref struct
        {
            Span<byte> prefix = writer.GetSpan(4);
            BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);
            writer.Advance(4);

            Span<byte> body = writer.GetSpan(payload.Length);
            payload.CopyTo(body);
            writer.Advance(payload.Length);
        }

        Span<byte> frame = stackalloc byte[64];
        var writer = frame.GetFixedBufferWriter();
        WriteFrame(ref writer, "hello"u8);
        // end-snippet

        Assert.Equal(9, writer.WrittenCount);
        ReadOnlySpan<byte> expectedFrame = [0, 0, 0, 5, 0x68, 0x65, 0x6C, 0x6C, 0x6F];
        bool frameMatches = writer.WrittenSpan.SequenceEqual(expectedFrame);
        Assert.True(frameMatches);
    }

    [Fact]
    public void SiteFixedBufferOverSensitiveRentalSample_Runs()
    {
        // begin-snippet: SiteFixedBufferOverSensitiveRental
        using var owner = BufferFactory.Rent(
            64,
            isSensitive: true,
            out Memory<byte> buffer
        );
        var writer = buffer.GetFixedBufferWriter();

        Span<byte> key = writer.GetSpan(32)[..32];
        RandomNumberGenerator.Fill(key); // A 32-byte key.
        writer.Advance(32);
        // end-snippet

        Assert.Equal(32, writer.WrittenCount);
        Assert.Equal(64, buffer.Length);
        bool hasData = writer.WrittenSpan.ContainsAnyExcept((byte)0);
        Assert.True(hasData);
        ReadOnlySpan<byte> head = buffer.Span[..32];
        SecretAssert.Equal(head, writer.WrittenSpan);

        Memory<byte> kept = buffer;
        owner.Dispose();

        bool keptHasData = kept.Span.ContainsAnyExcept((byte)0);
        Assert.False(keptHasData);
    }

    [Fact]
    public void SiteFixedBufferTooSmallForJsonSample_Runs()
    {
        // begin-snippet: SiteFixedBufferTooSmallForJson
        static void WriteToken(IBufferWriter<byte> output)
        {
            using var json = new Utf8JsonWriter(output);

            // The JSON writer asks for 256 bytes before it writes the first.
            json.WriteStartObject();
            json.WriteString("sub", "42");
            json.WriteEndObject();
            json.Flush();
        }
        // end-snippet

        var tooSmall = new FixedMemoryBufferWriter<byte>(new byte[128]);
        Assert.Throws<InvalidOperationException>(() => WriteToken(tooSmall));

        var exact = new FixedMemoryBufferWriter<byte>(new byte[256]);
        WriteToken(exact);

        Assert.Equal(12, exact.WrittenCount);
        ReadOnlySpan<byte> expectedJson = """{"sub":"42"}"""u8;
        bool jsonMatches = exact.WrittenSpan.SequenceEqual(expectedJson);
        Assert.True(jsonMatches);
    }

    [Fact]
    public void SiteFixedBufferPartialWriteSample_Runs()
    {
        Span<byte> buffer = stackalloc byte[8];
        buffer.Clear();

        // begin-snippet: SiteFixedBufferPartialWrite
        var writer = buffer.GetFixedBufferWriter();
        try
        {
            Span<byte> head = writer.GetSpan(4);
            "HEAD"u8.CopyTo(head);
            writer.Advance(4);

            writer.GetSpan(7); // Only 4 bytes are free.
        }
        catch (InvalidOperationException)
        {
            writer.Reset(); // Zeroes the 4 bytes written so far.
        }
        // end-snippet

        Assert.Equal(0, writer.WrittenCount);
        bool bufferHasData = buffer.ContainsAnyExcept((byte)0);
        Assert.False(bufferHasData);
    }
}
