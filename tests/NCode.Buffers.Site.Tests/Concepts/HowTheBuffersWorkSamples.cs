// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using NCode.Buffers;

namespace SiteSamples.Concepts;

// The samples of docs/site/concepts/how-the-buffers-work.mdx: what the secure pool does with a page from Rent to
// Dispose. Each sample creates its own pool, so no other test shares its cache. The site's build includes each
// sample region in the page by its key (ADR-1010).
public class HowTheBuffersWorkSamples
{
    [Fact]
    public void SiteDisposeZeroesWholePageSample_Runs()
    {
        Memory<byte> page;

        // begin-snippet: SiteDisposeZeroesWholePage
        using var pool = new SecureMemoryPool<byte>();

        var owner = pool.Rent(32);
        // The owner holds a whole page: 4096 bytes, though only 32 were asked for.
        owner.Memory.Span.Fill(0x5A);
        page = owner.Memory; // Only to look at the page after Dispose.

        // Dispose writes zeros over the whole page, not only the first 32 bytes.
        owner.Dispose();
        // end-snippet

        Assert.Equal(4096, page.Length);
        Assert.False(page.Span.ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SiteSecondRenterSeesZerosSample_Runs()
    {
        // begin-snippet: SiteSecondRenterSeesZeros
        using var pool = new SecureMemoryPool<byte>();

        var first = pool.Rent(32);
        first.Memory.Span.Fill(0x5A);
        first.Dispose();

        // The cache hands the same page to the next renter, and the page holds only zeros.
        using var second = pool.Rent(32);
        // end-snippet

        Assert.True(MemoryMarshal.TryGetArray<byte>(first.Memory, out var firstSegment));
        Assert.True(
            MemoryMarshal.TryGetArray<byte>(second.Memory, out var secondSegment)
        );
        Assert.Same(firstSegment.Array, secondSegment.Array);
        Assert.Equal(4096, second.Memory.Length);
        Assert.False(second.Memory.Span.ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SiteOwnPoolSample_Runs()
    {
        // begin-snippet: SiteOwnPool
        // A pool of your own has its own cache, and its own threshold for dropping it.
        var pool = new SecureMemoryPool<byte> { HighPressureThreshold = 0.75 };

        var stillRented = pool.Rent(32);
        stillRented.Memory.Span.Fill(0x5A);

        var owner = pool.Rent(32);
        owner.Dispose(); // The zeroed page goes into this pool's cache.

        // Disposing the pool drops the cache. A buffer still rented stays valid,
        // and it is zeroed when it is disposed, but it does not go back to the pool.
        pool.Dispose();
        stillRented.Memory.Span[0] = 0x5B;
        stillRented.Dispose();
        // end-snippet

        Assert.Throws<ObjectDisposedException>(() => pool.Rent(32));
        Assert.Equal(4096, stillRented.Memory.Length);
        Assert.False(stillRented.Memory.Span.ContainsAnyExcept((byte)0));
    }
}
