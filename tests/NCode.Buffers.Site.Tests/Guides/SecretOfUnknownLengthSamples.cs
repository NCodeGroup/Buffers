// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using Nerdbank.Streams;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/secret-of-unknown-length.mdx. SiteGrowthLeavesCopies is also the evidence of the
// leak card L4 in docs/site/_components/data/leaks.json. The site's build includes each begin-snippet region in a
// page by its key (ADR-1010).
public class SecretOfUnknownLengthSamples
{
    private static readonly byte[] StorageKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void SiteGrowthLeavesCopiesSample_Runs()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        // begin-snippet: SiteGrowthLeavesCopies
        var growing = new ArrayBufferWriter<byte>(initialCapacity: 16);
        growing.Write(key.AsSpan(0, 16));
        MemoryMarshal.TryGetArray(growing.WrittenMemory, out ArraySegment<byte> before);

        growing.Write(key.AsSpan(16, 16)); // No room: it allocates a larger array and copies.
        MemoryMarshal.TryGetArray(growing.WrittenMemory, out ArraySegment<byte> after);

        // after.Array is a new array. before.Array was dropped with the first 16 bytes in it.

        using var pooled = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        pooled.Write(key.AsSpan(0, 16));
        ReadOnlyMemory<byte> firstSegment = pooled.AsReadOnlySequence.First;

        pooled.Write(new byte[10_000]); // It adds a segment; the first one stays where it is.

        // The first segment is the same memory, holding the same 16 bytes.
        // end-snippet

        Assert.NotSame(before.Array, after.Array);
        SecretAssert.Equal(key.AsSpan(0, 16), before.AsSpan());
        SecretAssert.Equal(key, after.AsSpan());

        Assert.True(
            MemoryMarshal.TryGetArray(firstSegment, out ArraySegment<byte> first)
        );
        Assert.True(
            MemoryMarshal.TryGetArray(
                pooled.AsReadOnlySequence.First,
                out ArraySegment<byte> stillFirst
            )
        );
        Assert.Same(first.Array, stillFirst.Array);
        SecretAssert.Equal(key.AsSpan(0, 16), pooled.AsReadOnlySequence.First.Span[..16]);
        Assert.False(pooled.AsReadOnlySequence.IsSingleSegment);
    }

    [Fact]
    public void SiteUnknownLengthUsualSample_Runs()
    {
        // begin-snippet: SiteUnknownLengthUsual
        static byte[] Seal(
            AesGcm storage,
            byte[] nonce,
            string keyId,
            byte[] key,
            byte[] tag
        )
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("kid", keyId);
                json.WriteBase64String("k", key);
                json.WriteEndObject();
            }

            byte[] plaintext = buffer.WrittenSpan.ToArray();
            byte[] ciphertext = new byte[plaintext.Length];
            storage.Encrypt(nonce, plaintext, ciphertext, tag);
            return ciphertext;
        }
        // end-snippet

        byte[] key = RandomNumberGenerator.GetBytes(32);
        using var storage = new AesGcm(StorageKey, AesGcm.TagByteSizes.MaxSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];

        byte[] sealedDocument = Seal(storage, nonce, "2026-11", key, tag);

        SecretAssert.Equal(
            ExpectedDocument("2026-11", key),
            Open(storage, nonce, sealedDocument, tag)
        );
    }

    [Fact]
    public void SiteUnknownLengthWriterSample_Runs()
    {
        // begin-snippet: SiteUnknownLengthWriter
        static byte[] Seal(
            AesGcm storage,
            byte[] nonce,
            string keyId,
            ReadOnlySpan<byte> key,
            byte[] tag
        )
        {
            using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
            using (var json = new Utf8JsonWriter(writer))
            {
                json.WriteStartObject();
                json.WriteString("kid", keyId);
                json.WriteBase64String("k", key);
                json.WriteEndObject();
            }

            using IDisposable owner = writer.ConsumeAsContiguousSpan(
                isSensitive: true,
                out ReadOnlySpan<byte> plaintext
            );
            byte[] ciphertext = new byte[plaintext.Length];
            storage.Encrypt(nonce, plaintext, ciphertext, tag);
            return ciphertext;
        }
        // end-snippet

        using var storage = new AesGcm(StorageKey, AesGcm.TagByteSizes.MaxSize);
        byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];

        // A 32-byte key makes a document that fits in one segment; one of 6,000 bytes needs several.
        foreach (int keyLength in (int[])[32, 6_000])
        {
            byte[] key = RandomNumberGenerator.GetBytes(keyLength);
            byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);

            using (var probe = BufferFactory.CreatePooledBufferWriter(isSensitive: true))
            {
                probe.Write(ExpectedDocument("2026-11", key));
                Assert.Equal(keyLength == 32, probe.AsReadOnlySequence.IsSingleSegment);
            }

            byte[] sealedDocument = Seal(storage, nonce, "2026-11", key, tag);

            SecretAssert.Equal(
                ExpectedDocument("2026-11", key),
                Open(storage, nonce, sealedDocument, tag)
            );
        }
    }

    [Fact]
    public void SiteContiguousIncrementalSample_Runs()
    {
        byte[] macKey = RandomNumberGenerator.GetBytes(32);
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        writer.Write(RandomNumberGenerator.GetBytes(10_000));
        byte[] whole = writer.AsReadOnlySequence.ToArray();

        // begin-snippet: SiteContiguousIncremental
        static void ComputeMac(ReadOnlySequence<byte> doc, byte[] key, Span<byte> mac)
        {
            using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
            foreach (ReadOnlyMemory<byte> segment in doc)
            {
                hmac.AppendData(segment.Span); // In place: nothing is copied.
            }
            hmac.GetHashAndReset(mac);
        }
        // end-snippet

        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        ComputeMac(writer.AsReadOnlySequence, macKey, mac);

        Assert.False(writer.AsReadOnlySequence.IsSingleSegment);
        SecretAssert.Equal(HMACSHA256.HashData(macKey, whole), mac);
    }

    [Fact]
    public void SiteUnknownLengthLeaseSample_Runs()
    {
        using var storage = new AesGcm(StorageKey, AesGcm.TagByteSizes.MaxSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        byte[] tag = new byte[AesGcm.TagByteSizes.MaxSize];
        byte[] document = RandomNumberGenerator.GetBytes(6_000);
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        writer.Write(document);

        // begin-snippet: SiteUnknownLengthLease
        byte[] ciphertext = new byte[writer.Length];
        using (RefSpanLease<byte> lease = writer.GetSpanLease(isSensitive: true))
        {
            storage.Encrypt(nonce, lease.Span, ciphertext, tag);
        }
        // The lease is released here. The writer still holds the document.
        // end-snippet

        Assert.Equal(document.Length, writer.Length);
        SecretAssert.Equal(document, writer.AsReadOnlySequence.ToArray());
        SecretAssert.Equal(document, Open(storage, nonce, ciphertext, tag));
    }

    [Fact]
    public void SiteLeaseOrConsumeSample_Runs()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(6_000);

        // begin-snippet: SiteLeaseOrConsume
        // One segment: the lease is a view of the writer's own memory,
        // and the owner is the writer.
        using var small = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        small.Write(secret.AsSpan(0, 32));
        bool smallLeaseIsTheWriter;
        using (RefSpanLease<byte> lease = small.GetSpanLease(isSensitive: true))
        {
            smallLeaseIsTheWriter = lease.Span.Overlaps(
                small.AsReadOnlySequence.FirstSpan
            );
        }
        using IDisposable smallOwner = small.ConsumeAsContiguousSpan(
            isSensitive: true,
            out _
        );

        // Several segments: the lease is a copy, and the writer keeps its data.
        using var large = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        large.Write(secret);
        bool largeLeaseIsTheWriter;
        using (RefSpanLease<byte> lease = large.GetSpanLease(isSensitive: true))
        {
            largeLeaseIsTheWriter = lease.Span.Overlaps(
                large.AsReadOnlySequence.FirstSpan
            );
        }
        long lengthAfterLease = large.Length;

        // Several segments: consuming copies the data,
        // and disposes the writer before it returns.
        using IDisposable largeOwner = large.ConsumeAsContiguousSpan(
            isSensitive: true,
            out _
        );
        long lengthAfterConsume = large.Length;
        // end-snippet

        Assert.True(smallLeaseIsTheWriter);
        Assert.Same(small, smallOwner);
        Assert.Equal(32, small.Length);

        Assert.False(largeLeaseIsTheWriter);
        Assert.Equal(secret.Length, lengthAfterLease);
        Assert.NotSame(large, largeOwner);
        Assert.IsAssignableFrom<IMemoryOwner<byte>>(largeOwner);
        Assert.Equal(0, lengthAfterConsume);
    }

    [Fact]
    public void SiteSegmentsArePinnedSample_Runs()
    {
        // begin-snippet: SiteSegmentsArePinned
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);

        Memory<byte> page = writer.GetMemory(1); // A 4096-byte page from the pool.
        writer.Advance(1);
        Memory<byte> large = writer.GetMemory(10_000); // More than a page: its own array.

        // The collector reports an array on the pinned object heap as its oldest generation.
        static int GenerationOf(Memory<byte> memory) =>
            MemoryMarshal.TryGetArray<byte>(memory, out var segment)
            && segment.Array is { } array
                ? GC.GetGeneration(array)
                : -1; // Not memory of an array.

        int pageGeneration = GenerationOf(page);
        int largeGeneration = GenerationOf(large);
        // end-snippet

        Assert.Equal(SecureMemoryPool<byte>.PageSize, page.Length);
        Assert.Equal(10_000, large.Length);
        Assert.Equal(GC.MaxGeneration, pageGeneration);
        Assert.Equal(GC.MaxGeneration, largeGeneration);
    }

    private static byte[] ExpectedDocument(string keyId, byte[] key) =>
        System.Text.Encoding.UTF8.GetBytes(
            $$"""{"kid":"{{keyId}}","k":"{{Convert.ToBase64String(key)}}"}"""
        );

    private static byte[] Open(
        AesGcm storage,
        byte[] nonce,
        byte[] ciphertext,
        byte[] tag
    )
    {
        byte[] plaintext = new byte[ciphertext.Length];
        storage.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
