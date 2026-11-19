// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/pipes-and-streams.mdx. The site's build includes each begin-snippet region in
// the page by its key (ADR-1010).
public class PipesAndStreamsSamples
{
    [Fact]
    public async Task SitePipelinesUsualSample_Runs()
    {
        byte[] secret = new byte[32];
        secret.AsSpan().Fill(0x5A);

        // begin-snippet: SitePipelinesUsual
        var pipe = new Pipe();

        // The writer side: copy the secret in and flush it to the reader.
        await pipe.Writer.WriteAsync(secret);
        await pipe.Writer.FlushAsync();

        // The reader side: read the secret back, then tell the pipe it is consumed.
        ReadResult result = await pipe.Reader.ReadAsync();
        byte[] readBack = result.Buffer.ToArray();
        pipe.Reader.AdvanceTo(result.Buffer.End);

        await pipe.Writer.CompleteAsync();
        await pipe.Reader.CompleteAsync();
        // end-snippet

        SecretAssert.Equal(secret, readBack);
        Assert.Same(MemoryPool<byte>.Shared, PipeOptions.Default.Pool);
    }

    [Fact]
    public async Task SitePipelinesSecurePoolSample_Runs()
    {
        byte[] secret = new byte[32];
        secret.AsSpan().Fill(0x5A);

        // begin-snippet: SitePipelinesSecurePool
        // The pool belongs to the application, so the application disposes it.
        using var pool = new SecureMemoryPool<byte>();
        var pipe = new Pipe(new PipeOptions(pool: pool));

        // The writer side: ask for a buffer and write the secret into it.
        Memory<byte> memory = pipe.Writer.GetMemory(32);
        secret.CopyTo(memory);
        pipe.Writer.Advance(secret.Length);
        await pipe.Writer.FlushAsync();

        // The reader side: read the secret back, then tell the pipe it is consumed.
        ReadResult result = await pipe.Reader.ReadAsync();
        byte[] readBack = result.Buffer.ToArray();
        pipe.Reader.AdvanceTo(result.Buffer.End);

        // Completing both ends returns any buffer the pipe still holds to the pool, which
        // zeroes it.
        await pipe.Writer.CompleteAsync();
        await pipe.Reader.CompleteAsync();
        // end-snippet

        SecretAssert.Equal(secret, readBack);
        Assert.Equal(4096, memory.Length);
        bool memoryHasData = memory.Span.ContainsAnyExcept((byte)0);
        Assert.False(memoryHasData);
        using IMemoryOwner<byte> next = pool.Rent(1);
        Assert.Equal(SecureMemoryPool<byte>.PageSize, next.Memory.Length);
    }

    [Fact]
    public async Task SitePipelinesUnconsumedSample_Runs()
    {
        byte[] secret = new byte[32];
        secret.AsSpan().Fill(0x5A);

        // begin-snippet: SitePipelinesUnconsumed
        // The pool belongs to the application, so the application disposes it.
        using var pool = new SecureMemoryPool<byte>();
        var pipe = new Pipe(new PipeOptions(pool: pool));

        Memory<byte> memory = pipe.Writer.GetMemory(32);
        secret.CopyTo(memory);
        pipe.Writer.Advance(secret.Length);
        await pipe.Writer.FlushAsync();

        // The reader looks at the bytes and consumes none of them.
        ReadResult result = await pipe.Reader.ReadAsync();
        pipe.Reader.AdvanceTo(result.Buffer.Start);

        // With one end complete, the pipe still holds the buffer, and the bytes are in it.
        await pipe.Reader.CompleteAsync();
        bool heldWithOneEnd = memory.Span.ContainsAnyExcept((byte)0);

        // Completing both ends returns the buffer to the pool, which zeroes it.
        await pipe.Writer.CompleteAsync();
        // end-snippet

        Assert.True(heldWithOneEnd);
        bool memoryHasData = memory.Span.ContainsAnyExcept((byte)0);
        Assert.False(memoryHasData);
    }

    [Fact]
    public async Task SitePipelinesSequenceLeaseSample_Runs()
    {
        var pipe = new Pipe();
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] secret = new byte[4096];
        for (int i = 0; i < secret.Length; i++)
            secret[i] = (byte)(i % 251);
        byte[] mac = new byte[HMACSHA256.HashSizeInBytes];

        var head = secret.AsMemory(0, 2048);
        await pipe.Writer.WriteAsync(head);
        var tail = secret.AsMemory(2048);
        await pipe.Writer.WriteAsync(tail);
        await pipe.Writer.CompleteAsync();

        // begin-snippet: SitePipelinesSequenceLease
        ReadResult result = await pipe.Reader.ReadAsync();
        using (var lease = result.Buffer.GetSpanLease(isSensitive: true))
        {
            HMACSHA256.HashData(key, lease.Span, mac);
        }
        pipe.Reader.AdvanceTo(result.Buffer.End);
        // end-snippet

        pipe.Reader.Complete();
        byte[] expected = HMACSHA256.HashData(key, secret);
        SecretAssert.Equal(expected, mac);
    }

    [Fact]
    public async Task SitePipelinesStreamReaderSample_Runs()
    {
        byte[] secret = new byte[32];
        secret.AsSpan().Fill(0x5A);
        using var stream = new MemoryStream(secret);

        // begin-snippet: SitePipelinesStreamReader
        // The pool belongs to the application, so the application disposes it.
        using var pool = new SecureMemoryPool<byte>();
        PipeReader reader = PipeReader.Create(
            stream,
            new StreamPipeReaderOptions(pool: pool)
        );

        // Read to the end of the stream.
        using var destination = new MemoryStream();
        await reader.CopyToAsync(destination);

        // The reader returned each buffer it consumed to the pool, which zeroed it.
        // Completing the reader returns any it still holds.
        await reader.CompleteAsync();
        // end-snippet

        byte[] copied = destination.ToArray();
        SecretAssert.Equal(secret, copied);
        using IMemoryOwner<byte> next = pool.Rent(1);
        Assert.Equal(SecureMemoryPool<byte>.PageSize, next.Memory.Length);
    }
}
