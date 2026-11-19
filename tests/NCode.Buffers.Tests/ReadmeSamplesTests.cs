// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReadmeSamples;

namespace NCode.Buffers.Tests;

// `dotnet mdsnippets` copies each begin-snippet block into README.md and src/NCode.Buffers/README.md (ADR-1005).
public class ReadmeSamplesTests
{
    private static void Process(ReadOnlySpan<byte> data) => Assert.Equal("payload"u8, data);

    private static ulong[] ComputeMac(ReadOnlySpan<byte> data) => [(ulong)data.Length];

    private static ulong[] GetExpectedMac() => [7];

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

    #region Rental Tests

    [Fact]
    public void RandomKey_Rent_ReturnsThirtyTwoRandomBytes()
    {
        // begin-snippet: RandomKey
        // 32 random bytes in a pinned, pooled buffer that is zeroed on dispose.
        using var owner = BufferFactory.RentRandom(32, isSensitive: true, out Span<byte> key);
        // end-snippet

        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void EncodeText_RentBytesThenChars_RoundTrips()
    {
        // begin-snippet: EncodeText
        // Encode text to UTF-8 bytes in a rented buffer, and decode the bytes back to chars.
        using var bytesOwner = SecureEncoding.UTF8.RentBytes(
            "secret",
            isSensitive: true,
            out Span<byte> utf8
        );
        using var charsOwner = SecureEncoding.UTF8.RentChars(
            utf8,
            isSensitive: true,
            out Span<char> chars
        );
        // end-snippet

        var decoded = chars.ToString();

        Assert.Equal("secret"u8, utf8);
        Assert.Equal("secret", decoded);
    }

    [Fact]
    public void DecodeBinaryText_RentDecodedThenEncoded_RoundTrips()
    {
        // begin-snippet: DecodeBinaryText
        // Decode Base64Url text to bytes, then encode the bytes as lowercase hex.
        using var bytesOwner = BinaryTextEncoding.RentDecoded(
            "-_8B",
            BinaryTextFormat.Base64Url,
            isSensitive: true,
            out Span<byte> bytes
        );
        using var textOwner = BinaryTextEncoding.RentEncoded(
            bytes,
            BinaryTextFormat.HexLower,
            isSensitive: true,
            out Span<char> hex
        );
        // end-snippet

        var decoded = bytes.ToArray();
        var encoded = hex.ToString();

        Assert.Equal([0xFB, 0xFF, 0x01], decoded);
        Assert.Equal("fbff01", encoded);
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

    #region Sequence Tests

    [Fact]
    public void MemorySegmentSample_Runs()
    {
        // begin-snippet: MemorySegment
        var first = new MemorySegment<byte>((byte[])[1, 2, 3]);
        var last = first.Append((byte[])[4, 5, 6]);
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        // sequence holds 1, 2, 3, 4, 5, 6
        // end-snippet

        Assert.Equal([1, 2, 3, 4, 5, 6], sequence.ToArray());
    }

    [Fact]
    public void GetSpanLeaseSample_Runs()
    {
        // begin-snippet: GetSpanLease
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        writer.Write("payload"u8);

        // No copy when the data is in one segment; otherwise it is copied into a rented buffer.
        using (var lease = writer.GetSpanLease(isSensitive: true))
        {
            Process(lease.Span);
        }
        // end-snippet
    }

    [Fact]
    public void ReadOnlySequenceLease_Lease_ReturnsContiguousSpan()
    {
        // begin-snippet: ReadOnlySequenceLease
        byte[] head = "pay"u8.ToArray();
        byte[] tail = "load"u8.ToArray();
        var first = new MemorySegment<byte>(head);
        var last = first.Append(tail);
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        // The data spans two segments, so it is copied into a rented buffer.
        using var lease = sequence.GetSpanLease(isSensitive: true);
        Process(lease.Span);
        // end-snippet
    }

    [Fact]
    public void ConsumeAsContiguousSpanSample_Runs()
    {
        // begin-snippet: ConsumeAsContiguousSpan
        using var writer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        writer.Write("payload"u8);

        // owner now owns the data; don't use writer again.
        using var owner = writer.ConsumeAsContiguousSpan(
            isSensitive: true,
            out ReadOnlySpan<byte> span
        );
        Process(span);
        // end-snippet
    }

    [Fact]
    public void EmptyMemorySample_Runs()
    {
        // begin-snippet: EmptyMemory
        IMemoryOwner<byte> empty = EmptyMemory<byte>.Singleton;
        // end-snippet

        Assert.True(empty.Memory.IsEmpty);
    }

    #endregion

    #region Pooled Writer Tests

    [Fact]
    public void ContiguousWriter_WriteJson_HashesWrittenSpan()
    {
        // begin-snippet: ContiguousWriter
        using var writer = BufferFactory.CreateContiguousBufferWriter(isSensitive: true);
        using (var json = new Utf8JsonWriter(writer))
        {
            JsonSerializer.Serialize(json, new { Id = 42 });
        }

        // The written data is always one span, so it can be hashed without a copy.
        byte[] hash = SHA256.HashData(writer.WrittenSpan);
        // end-snippet

        var expected = SHA256.HashData("""{"Id":42}"""u8);

        Assert.Equal(expected, hash);
    }

    [Fact]
    public void PooledStream_WriteThenRead_ReturnsWrittenBytes()
    {
        // begin-snippet: PooledStream
        using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
        stream.Write("payload"u8);
        stream.Position = 0;

        Span<byte> buffer = stackalloc byte[7];
        int read = stream.Read(buffer);
        // end-snippet

        Assert.Equal(7, read);
        Assert.Equal("payload"u8, buffer);
    }

    #endregion

    #region Text Builder Tests

    [Fact]
    public void TextBuilder_AppendTextAndInt_BuildsText()
    {
        // begin-snippet: TextBuilder
        // Starts in a stack buffer, and moves to a pooled one only if the text outgrows it.
        Span<char> start = stackalloc char[64];
        using var builder = new PooledTextBuilder(start, isSensitive: true);
        builder.Append("token-");
        builder.Append(42);
        ReadOnlySpan<char> text = builder.WrittenSpan;
        // end-snippet

        var built = text.ToString();

        Assert.Equal("token-42", built);
    }

    #endregion

    #region Secret Tests

    [Fact]
    public void SecretHolder_Create_RevealsValueAndRedactsText()
    {
        // begin-snippet: SecretHolder
        using var secret = Secret.Create("hunter2"u8);
        ReadOnlySpan<byte> value = secret.Reveal();
        string text = secret.ToString(); // [REDACTED]
        // end-snippet

        Assert.Equal("hunter2"u8, value);
        Assert.Equal("[REDACTED]", text);
    }

    #endregion

    #region Metrics Tests

    [Fact]
    public void PoolMetrics_Listener_CountsARental()
    {
        // begin-snippet: PoolMetrics
        var totals = new ConcurrentDictionary<string, long>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == BufferMetrics.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, value, tags, state) =>
                totals.AddOrUpdate(instrument.Name, value, (_, total) => total + value)
        );
        listener.Start();
        // end-snippet

        using var pool = new SecureMemoryPool<byte>();
        pool.Rent(16).Dispose();

        var rentals = totals.GetValueOrDefault("ncode.buffers.secure_pool.rentals");
        Assert.True(rentals >= 1);
    }

    #endregion

    #region String Splitting Tests

    [Fact]
    public void StringSegmentsSample_Runs()
    {
        // begin-snippet: StringSegments
        foreach (var segment in "one,two,three".SplitSegments(','))
        {
            Console.WriteLine(segment.Memory.ToString());
        }

        StringSegments segments = StringSegments.Split(
            "aSEPbSEPc",
            "sep",
            StringComparison.OrdinalIgnoreCase
        );
        int count = segments.Count; // 3
        // end-snippet

        Assert.Equal(3, count);
    }

    #endregion

    #region Secure Encoding Tests

    [Fact]
    public void SecureEncodingSample_Runs()
    {
        // begin-snippet: SecureEncoding
        byte[] bytes = SecureEncoding.UTF8.GetBytes("Hello, World!");
        string text = SecureEncoding.UTF8.GetString(bytes);

        // Invalid UTF-8 throws instead of decoding to U+FFFD, so untrusted input can be rejected.
        bool isValid;
        try
        {
            SecureEncoding.UTF8.GetString((byte[])[0xC3, 0x28]);
            isValid = true;
        }
        catch (DecoderFallbackException)
        {
            isValid = false;
        }
        // end-snippet

        Assert.Equal("Hello, World!", text);
        Assert.False(isValid);
    }

    #endregion

    #region CryptographicOperations Extension Tests

    [Fact]
    public void CryptographicOperationsSample_Runs()
    {
        var data = "payload"u8;

        // begin-snippet: CryptographicOperations
        Span<int> sensitiveData = stackalloc int[4];
        // ... use the sensitive data ...
        CryptographicOperations.ZeroMemory(sensitiveData);

        ReadOnlySpan<ulong> computed = ComputeMac(data);
        ReadOnlySpan<ulong> expected = GetExpectedMac();
        bool isValid = CryptographicOperations.FixedTimeEquals(computed, expected);

        // The same methods, callable from any C# version:
        CryptographicOperationsExtensions.ZeroMemory(sensitiveData);
        // end-snippet

        Assert.True(isValid);
    }

    #endregion
}
