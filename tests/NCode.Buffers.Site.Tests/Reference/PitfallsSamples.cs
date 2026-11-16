// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Reference;

// The samples of docs/site/reference/pitfalls.mdx: code that compiles and is wrong, each asserting what goes wrong.
// The site's build includes each begin-snippet region in the page by its key (ADR-1010).
public class PitfallsSamples
{
    [Fact]
    public void SitePitfallCopyOfALifetimeSample_Runs()
    {
        // begin-snippet: SitePitfallCopyOfALifetime
        using var key = BufferFactory.CreatePinnedArray(32);
        RandomNumberGenerator.Fill(key);

        var copy = key; // A second SecureArrayLifetime<byte> over the same array, not a span.
        copy.Dispose(); // Zeroes the array that key still uses.
        // end-snippet

        Assert.Same(key.PinnedArray, copy.PinnedArray);
        Assert.False(key.PinnedArray.AsSpan().ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SitePitfallLifetimeAsObjectSample_Runs()
    {
        using var key = BufferFactory.CreatePinnedArray(32);

        // begin-snippet: SitePitfallLifetimeAsObject
        // Compiles: the lifetime converts to byte[], and byte[] to object.
        object value = key;
        string text = $"Derived {key}";

        // value is the key's own array. text is "Derived System.Byte[]".
        // end-snippet

        Assert.Same(key.PinnedArray, value);
        Assert.Equal("Derived System.Byte[]", text);
    }

    [Fact]
    public void SitePitfallSliceLifetimeSample_Runs()
    {
        // begin-snippet: SitePitfallSliceLifetime
        Span<byte> buffer = stackalloc byte[64];
        RandomNumberGenerator.Fill(buffer); // A secret in all 64 bytes.

        using (buffer[..32].GetSecureLifetime())
        {
            // ...
        }

        // The first 32 bytes are zeros. The last 32 still hold the secret.
        // end-snippet

        Assert.False(buffer[..32].ContainsAnyExcept((byte)0));
        Assert.True(buffer[32..].ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SitePitfallWriterByValueSample_Runs()
    {
        // begin-snippet: SitePitfallWriterByValue
        Span<byte> buffer = stackalloc byte[16];
        var writer = new FixedSpanBufferWriter<byte>(buffer);

        WriteHeader(writer); // Writes into buffer, but advances a copy of the writer.
        WriteHeader(writer); // So this writes over the first header.

        static void WriteHeader(FixedSpanBufferWriter<byte> writer)
        {
            "HEAD"u8.CopyTo(writer.GetSpan(4));
            writer.Advance(4);
        }
        // end-snippet

        Assert.Equal(0, writer.WrittenCount);
        Assert.True(buffer[..4].SequenceEqual("HEAD"u8));
        Assert.False(buffer[4..].ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SitePitfallAdvanceBySpanLengthSample_Runs()
    {
        // begin-snippet: SitePitfallAdvanceBySpanLength
        Span<byte> buffer = stackalloc byte[64];
        var writer = new FixedSpanBufferWriter<byte>(buffer);

        Span<byte> span = writer.GetSpan(4); // All 64 free bytes, not 4.
        "abcd"u8.CopyTo(span);
        writer.Advance(span.Length); // Commits 64 bytes, 60 of which were never written.
        // end-snippet

        Assert.Equal(64, span.Length);
        Assert.Equal(64, writer.WrittenCount);
    }

    [Fact]
    public void SitePitfallResetAfterClearSample_Runs()
    {
        // begin-snippet: SitePitfallResetAfterClear
        Span<byte> buffer = stackalloc byte[16];
        var writer = new FixedSpanBufferWriter<byte>(buffer);
        RandomNumberGenerator.Fill(writer.GetSpan(8)[..8]);
        writer.Advance(8);

        writer.Clear(); // Forgets the 8 bytes. It does not touch them.
        writer.Reset(); // Clears what was written since the last Clear: nothing.

        // The 8 bytes are still in buffer.
        // end-snippet

        Assert.Equal(0, writer.WrittenCount);
        Assert.True(buffer[..8].ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SitePitfallCompareBytesNotValuesSample_Runs()
    {
        // begin-snippet: SitePitfallCompareBytesNotValues
        double[] zero = [0.0];
        double[] negativeZero = [-0.0];
        bool sameValue = zero[0] == negativeZero[0];
        bool sameBytes = CryptographicOperations.FixedTimeEquals<double>(
            zero,
            negativeZero
        );

        // sameValue is true, sameBytes is false: the sign bit differs.
        // end-snippet

        Assert.True(sameValue);
        Assert.False(sameBytes);
    }

    [Fact]
    public void SitePitfallPaddingBytesCountSample_Runs()
    {
        // begin-snippet: SitePitfallPaddingBytesCount
        // Reading is a struct of a byte Flags and an int Value,
        // and three bytes of padding follow Flags.
        byte[] stored = [1, 0, 0, 0, 42, 0, 0, 0];
        byte[] received = [1, 0xFF, 0xFF, 0xFF, 42, 0, 0, 0];
        ReadOnlySpan<Reading> left = MemoryMarshal.Cast<byte, Reading>(stored);
        ReadOnlySpan<Reading> right = MemoryMarshal.Cast<byte, Reading>(received);

        bool sameFields =
            left[0].Flags == right[0].Flags && left[0].Value == right[0].Value;
        bool sameBytes = CryptographicOperations.FixedTimeEquals(left, right);

        // sameFields is true, sameBytes is false: the padding differs.
        // end-snippet

        Assert.Equal(8, Unsafe.SizeOf<Reading>());
        Assert.Equal(1, left.Length);
        Assert.True(sameFields);
        Assert.False(sameBytes);
    }

    [Fact]
    public void SitePitfallDefaultValuesSample_Runs()
    {
        // begin-snippet: SitePitfallDefaultValues
        SecureArrayLifetime<byte> lifetime = default;
        StringSegments segments = default;

        // lifetime.PinnedArray is null, and lifetime.Length throws NullReferenceException.
        // segments.IsEmpty is true, and segments.First throws InvalidOperationException.
        // end-snippet

        Assert.Null(lifetime.PinnedArray);
        Assert.Throws<NullReferenceException>(() => LengthOfADefaultLifetime());
        lifetime.Dispose();
        Assert.True(segments.IsEmpty);
        Assert.Empty(segments);
        Assert.Throws<InvalidOperationException>(() => segments.First);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Reading
    {
        public byte Flags;
        public int Value;
    }

    private static int LengthOfADefaultLifetime()
    {
        SecureArrayLifetime<byte> lifetime = default;
        return lifetime.Length;
    }
}
