// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Start;

// The samples of docs/site/start/which-buffer.mdx: the call on each of the ten cards, whose keys
// docs/site/_components/data/buffers.json names. Each fact asserts the length the call gives. The site's build
// includes each begin-snippet region in the page by its key (ADR-1010).
public class WhichBufferSamples
{
    [Fact]
    public void SiteAnswerRentSpanSample_Runs()
    {
        // begin-snippet: SiteAnswerRentSpan
        using var keyOwner = BufferFactory.Rent(
            32,
            isSensitive: true,
            out Span<byte> key
        );
        RandomNumberGenerator.Fill(key);
        // end-snippet

        Assert.Equal(32, key.Length);
        Assert.Equal(4096, keyOwner.Memory.Length);
    }

    [Fact]
    public async Task SiteAnswerRentMemorySample_Runs()
    {
        using var stream = new MemoryStream(new byte[64]);

        // begin-snippet: SiteAnswerRentMemory
        using var keyOwner = BufferFactory.Rent(
            32,
            isSensitive: true,
            out Memory<byte> key
        );
        await stream.ReadExactlyAsync(key);
        // end-snippet

        Assert.Equal(32, key.Length);
        Assert.Equal(32, stream.Position);
    }

    [Fact]
    public void SiteAnswerPooledWriterSample_Runs()
    {
        // begin-snippet: SiteAnswerPooledWriter
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        "a secret of any length"u8.CopyTo(writer.GetSpan(22));
        writer.Advance(22);
        // end-snippet

        Assert.Equal(22, writer.Length);
    }

    [Fact]
    public void SiteAnswerSecurePoolSample_Runs()
    {
        // begin-snippet: SiteAnswerSecurePool
        // In an application, one pool for its lifetime.
        using var pool = new SecureMemoryPool<byte>();
        using var owner = pool.Rent(32);
        // owner.Memory is the whole 4096-byte buffer.
        Memory<byte> key = owner.Memory[..32];
        // end-snippet

        Assert.Equal(4096, owner.Memory.Length);
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void SiteAnswerPinnedArraySample_Runs()
    {
        // begin-snippet: SiteAnswerPinnedArray
        using var lifetime = BufferFactory.CreatePinnedArray(32);
        byte[] key = lifetime; // for an API that takes or fills a byte[]
        // end-snippet

        Assert.Equal(32, key.Length);
        Assert.Equal(GC.MaxGeneration, GC.GetGeneration(key));
    }

    [Fact]
    public void SiteAnswerSecureLifetimeSample_Runs()
    {
        // begin-snippet: SiteAnswerSecureLifetime
        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        using var macLifetime = mac.GetSecureLifetime();
        // end-snippet

        Assert.Equal(32, mac.Length);
    }

    [Fact]
    public void SiteAnswerPlainPooledSample_Runs()
    {
        // begin-snippet: SiteAnswerPlainPooled
        using var owner = BufferFactory.Rent(
            1024,
            isSensitive: false,
            out Span<byte> buffer
        );
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        // end-snippet

        Assert.Equal(1024, buffer.Length);
        Assert.Equal(0, writer.Length);
    }

    [Fact]
    public void SiteAnswerArrayWriterSample_Runs()
    {
        // begin-snippet: SiteAnswerArrayWriter
        ArrayBufferWriter<byte> writer = BufferFactory.CreateArrayBufferWriter(256);
        "output that is not secret"u8.CopyTo(writer.GetSpan(25));
        writer.Advance(25);
        // end-snippet

        Assert.Equal(25, writer.WrittenCount);
    }

    [Fact]
    public void SiteAnswerFixedSpanWriterSample_Runs()
    {
        // begin-snippet: SiteAnswerFixedSpanWriter
        Span<byte> buffer = stackalloc byte[64];
        var writer = new FixedSpanBufferWriter<byte>(buffer);
        "header"u8.CopyTo(writer.GetSpan(6));
        writer.Advance(6);
        // end-snippet

        Assert.Equal("header"u8, writer.WrittenSpan);
        Assert.Equal(58, writer.FreeCapacity);
    }

    [Fact]
    public void SiteAnswerFixedMemoryWriterSample_Runs()
    {
        // begin-snippet: SiteAnswerFixedMemoryWriter
        var writer = new FixedMemoryBufferWriter<byte>(new byte[64]);
        "header"u8.CopyTo(writer.GetSpan(6));
        writer.Advance(6);
        // end-snippet

        Assert.Equal("header"u8, writer.WrittenSpan);
        Assert.Equal(58, writer.FreeCapacity);
    }
}
