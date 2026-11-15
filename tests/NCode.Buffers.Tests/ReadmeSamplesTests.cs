// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using ReadmeSamples;

namespace NCode.Buffers.Tests;

// `dotnet mdsnippets` copies each begin-snippet block into README.md and src/NCode.Buffers/README.md (ADR-1005).
public class ReadmeSamplesTests
{
    #region Quick Start Tests

    [Fact]
    public void QuickStart_ValidSignature_ReturnsTrue()
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(
            "correct horse battery staple"u8,
            "salt"u8,
            600_000,
            HashAlgorithmName.SHA256,
            32
        );
        var signature = HMACSHA256.HashData(key, "payload"u8);

        var result = Signatures.VerifySignature(
            "correct horse battery staple"u8,
            "salt"u8,
            "payload"u8,
            signature
        );

        Assert.True(result);
    }

    [Fact]
    public void QuickStart_TamperedSignature_ReturnsFalse()
    {
        var key = Rfc2898DeriveBytes.Pbkdf2(
            "correct horse battery staple"u8,
            "salt"u8,
            600_000,
            HashAlgorithmName.SHA256,
            32
        );
        var signature = HMACSHA256.HashData(key, "payload"u8);
        signature[0] ^= 1;

        var result = Signatures.VerifySignature(
            "correct horse battery staple"u8,
            "salt"u8,
            "payload"u8,
            signature
        );

        Assert.False(result);
    }

    #endregion

    #region Secure Memory Tests

    [Fact]
    public void BufferFactorySample_Runs()
    {
        // begin-snippet: BufferFactory
        // A pooled buffer that is pinned and zeroed on dispose, sliced to exactly 256 bytes.
        using var owner = BufferFactory.Rent(256, isSensitive: true, out Span<byte> buffer);

        // A pinned array, for an API that needs a byte[].
        using var lifetime = BufferFactory.CreatePinnedArray(128);
        byte[] array = lifetime;

        // A writer for a secret of unknown length, built from pinned, pooled segments.
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        "payload"u8.CopyTo(writer.GetSpan(7));
        writer.Advance(7);

        // A growable writer for data that isn't secret.
        ArrayBufferWriter<byte> arrayWriter = BufferFactory.CreateArrayBufferWriter(1024);
        // end-snippet

        Assert.Equal(256, buffer.Length);
        Assert.Equal(128, array.Length);
        Assert.Equal(7, writer.Length);
        Assert.True(arrayWriter.Capacity >= 1024);
    }

    [Fact]
    public void SecureMemoryPoolSample_Runs()
    {
        // begin-snippet: SecureMemoryPool
        using IMemoryOwner<byte> owner = SecureMemoryPool<byte>.Shared.Rent(1024);
        Memory<byte> memory = owner.Memory; // at least 1024 bytes: slice it to the length you need
        // end-snippet

        Assert.True(memory.Length >= 1024);
    }

    [Fact]
    public void SecureLifetimeSample_Runs()
    {
        // begin-snippet: SecureLifetime
        // A pinned array, zeroed when lifetime is disposed.
        using var lifetime = SecureArrayLifetime<byte>.Create(256);
        Span<byte> buffer = lifetime;

        // Memory you already have, zeroed when secure is disposed.
        Span<byte> stackBuffer = stackalloc byte[128];
        using var secure = stackBuffer.GetSecureLifetime();
        // end-snippet

        Assert.Equal(256, buffer.Length);
    }

    #endregion

    #region Buffer Writer Tests

    [Fact]
    public void FixedSpanBufferWriterSample_Runs()
    {
        // begin-snippet: FixedSpanBufferWriter
        Span<byte> buffer = stackalloc byte[256];
        var writer = new FixedSpanBufferWriter<byte>(buffer);

        "abcd"u8.CopyTo(writer.GetSpan(4));
        writer.Advance(4);
        ReadOnlySpan<byte> written = writer.WrittenSpan;
        // end-snippet

        Assert.Equal("abcd"u8, written);
    }

    [Fact]
    public async Task FixedMemoryBufferWriterSample_Runs()
    {
        using var stream = new MemoryStream();

        // begin-snippet: FixedMemoryBufferWriter
        var writer = new FixedMemoryBufferWriter<byte>(new byte[256]);
        using (var json = new Utf8JsonWriter(writer))
        {
            JsonSerializer.Serialize(json, new { Id = 42, Name = "example" });
        }
        await stream.WriteAsync(writer.WrittenMemory);
        // end-snippet

        Assert.Equal("""{"Id":42,"Name":"example"}"""u8, stream.ToArray());
    }

    #endregion
}
