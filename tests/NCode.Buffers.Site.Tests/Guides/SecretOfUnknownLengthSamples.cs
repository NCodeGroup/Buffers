// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/secret-of-unknown-length.mdx. SiteGrowthLeavesCopies is also the evidence of the
// leak card L4 in docs/site/_components/data/leaks.json. The site's build includes each begin-snippet region in a
// page by its key (ADR-1010).
public class SecretOfUnknownLengthSamples
{
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
        Assert.True(before.AsSpan().SequenceEqual(key.AsSpan(0, 16)));
        Assert.True(after.AsSpan().SequenceEqual(key));

        Assert.True(MemoryMarshal.TryGetArray(firstSegment, out ArraySegment<byte> first));
        Assert.True(
            MemoryMarshal.TryGetArray(
                pooled.AsReadOnlySequence.First,
                out ArraySegment<byte> stillFirst
            )
        );
        Assert.Same(first.Array, stillFirst.Array);
        Assert.True(pooled.AsReadOnlySequence.First.Span[..16].SequenceEqual(key.AsSpan(0, 16)));
        Assert.False(pooled.AsReadOnlySequence.IsSingleSegment);
    }
}
