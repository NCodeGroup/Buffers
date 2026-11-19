// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/grow-a-contiguous-secret.mdx. The site's build includes each begin-snippet region in
// the page by its key (ADR-1010).
public class GrowAContiguousSecretSamples
{
    private sealed record Payload(string ApiKey, string Scope);

    [Fact]
    public void SiteGrowUsualSample_Runs()
    {
        byte[] first = RandomNumberGenerator.GetBytes(16);
        byte[] second = RandomNumberGenerator.GetBytes(48);

        // begin-snippet: SiteGrowUsual
        var writer = new ArrayBufferWriter<byte>(16);
        writer.Write(first);
        MemoryMarshal.TryGetArray(writer.WrittenMemory, out ArraySegment<byte> before);

        // The next write outgrows the array. The writer copies to a bigger one and leaves the old one as it was.
        writer.Write(second);
        // end-snippet

        MemoryMarshal.TryGetArray(writer.WrittenMemory, out ArraySegment<byte> after);
        Assert.NotNull(before.Array);
        Assert.NotNull(after.Array);
        Assert.NotSame(before.Array, after.Array);
        SecretAssert.Equal(first, before.Array.AsSpan(0, first.Length));
    }

    [Fact]
    public void SiteGrowContiguousWriterSample_Runs()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        // begin-snippet: SiteGrowContiguousWriter
        // The writer grows through pooled buffers that it zeroes, and its written bytes stay contiguous.
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
        using (var json = new Utf8JsonWriter(writer))
        {
            JsonSerializer.Serialize(json, new { ApiKey = "k", Scope = "s" });
        }

        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, writer.WrittenSpan, mac);
        // end-snippet

        ReadOnlySpan<byte> expectedJson = "{\"ApiKey\":\"k\",\"Scope\":\"s\"}"u8;
        byte[] expected = HMACSHA256.HashData(key, expectedJson);
        SecretAssert.Equal(expected, mac);
    }

    [Fact]
    public async Task SiteGrowPooledStreamSample_Runs()
    {
        var payload = new Payload("k", "s");

        // begin-snippet: SiteGrowPooledStream
        // The stream's pooled buffers are zeroed as it grows and on dispose.
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        await JsonSerializer.SerializeAsync(stream, payload);

        stream.Position = 0;
        var roundTrip = await JsonSerializer.DeserializeAsync<Payload>(stream);
        // end-snippet

        Assert.Equal(payload, roundTrip);
    }

    [Fact]
    public void SiteGrowTextBuilderSample_Runs()
    {
        ReadOnlySpan<char> password = "s3cr3t";

        // begin-snippet: SiteGrowTextBuilder
        // The builder starts on the stack and moves to a pooled buffer that it zeroes if the text outgrows it.
        using var builder = new PooledTextBuilder(stackalloc char[64], isSensitive: true);
        builder.Append("Server=db;Password=");
        builder.Append(password);
        builder.Append(';');

        using Secret<char> connectionString = Secret.Create<char>(builder.WrittenSpan);
        // end-snippet

        Assert.Equal("Server=db;Password=s3cr3t;", new string(connectionString.Reveal()));
    }
}
