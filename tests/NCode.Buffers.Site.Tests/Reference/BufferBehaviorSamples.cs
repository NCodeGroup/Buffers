// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using NCode.Buffers;

namespace SiteSamples.Reference;

// The samples of docs/site/reference/buffer-behavior.mdx: what the secure pool hands out for each request.
// The site's build includes each begin-snippet region in the page by its key (ADR-1010).
public class BufferBehaviorSamples
{
    [StructLayout(LayoutKind.Sequential, Size = 8192)]
    private struct Huge
    {
        public byte First;
    }

    [Fact]
    public void SitePoolPageSizesSample_Runs()
    {
        // begin-snippet: SitePoolPageSizes
        using var bytePool = new SecureMemoryPool<byte>();
        using var charPool = new SecureMemoryPool<char>();
        using var intPool = new SecureMemoryPool<int>();
        using var hugePool = new SecureMemoryPool<Huge>();

        // No elements: the shared empty owner, nothing is allocated.
        using var none = bytePool.Rent(0);
        // No size given: one 4096-byte page.
        using var page = bytePool.Rent();
        // A request at or below the page: still one page, with 32 asked for.
        using var small = bytePool.Rent(32);
        // A request above the page: exactly 5000 bytes, its own array, not cached.
        using var large = bytePool.Rent(5000);
        // A page counts bytes, so a pool of chars gets 2048 elements.
        using var chars = charPool.Rent(1);
        // A pool of ints gets 1024 elements.
        using var ints = intPool.Rent(1);
        // An element larger than a page: one element, even though it exceeds the page.
        using var huge = hugePool.Rent();
        // end-snippet

        Assert.Same(EmptyMemory<byte>.Singleton, none);
        Assert.Equal(0, none.Memory.Length);
        Assert.Equal(4096, page.Memory.Length);
        Assert.Equal(4096, small.Memory.Length);
        Assert.Equal(5000, large.Memory.Length);
        Assert.Equal(2048, chars.Memory.Length);
        Assert.Equal(1024, ints.Memory.Length);
        Assert.Equal(1, huge.Memory.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => bytePool.Rent(-2));
    }
}
