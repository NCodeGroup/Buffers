// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.IO.Pipelines;
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

        // Completing both ends returns the pipe's buffers to the pool, which zeroes them.
        await pipe.Writer.CompleteAsync();
        await pipe.Reader.CompleteAsync();
        // end-snippet

        SecretAssert.Equal(secret, readBack);
        Assert.Equal(4096, memory.Length);
        Assert.False(memory.Span.ContainsAnyExcept((byte)0));
        using IMemoryOwner<byte> next = pool.Rent(1);
        Assert.NotNull(next);
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

        // Completing the reader returns its buffers to the pool, which zeroes them.
        await reader.CompleteAsync();
        // end-snippet

        SecretAssert.Equal(secret, destination.ToArray());
        using IMemoryOwner<byte> next = pool.Rent(1);
        Assert.NotNull(next);
    }
}
