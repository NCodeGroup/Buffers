// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/test-zeroing.mdx. Each region is a test a reader copies, so it holds its own
// assertions. The site's build includes each begin-snippet region in the page by its key (ADR-1010).
public class TestZeroingSamples
{
    [Fact]
    public void SiteTestZeroingThroughAPinnedArraySample_Runs()
    {
        // begin-snippet: SiteTestZeroingThroughAPinnedArray
        // The code under test: it puts its key under a lifetime, so the key is zeroed on return.
        static void UseKeyOnce(Span<byte> key)
        {
            using var lifetime = key.GetSecureLifetime();
            RandomNumberGenerator.Fill(lifetime.Span);
            // ... use the key ...
        }

        // The test gives the code a buffer it can read afterwards.
        using var lifetime = BufferFactory.CreatePinnedArray(32);
        byte[] seen = lifetime;
        seen.AsSpan().Fill(0xFF); // A non-zero start, so zeros prove the code cleared it.

        UseKeyOnce(lifetime);

        bool seenHasData = seen.AsSpan().ContainsAnyExcept((byte)0);
        Assert.False(seenHasData);
        // end-snippet
    }

    [Fact]
    public void SiteTestZeroingOnTheStackSample_Runs()
    {
        // begin-snippet: SiteTestZeroingOnTheStack
        Span<byte> key = stackalloc byte[32];
        key.Fill(0xFF); // A non-zero start, so zeros prove the lifetime cleared it.

        {
            using var lifetime = key.GetSecureLifetime();
            RandomNumberGenerator.Fill(lifetime.Span);
            // ... use the key ...
        } // The lifetime zeroes the span here.

        // The span outlives the block, so the test reads it after the block.
        bool keyHasData = key.ContainsAnyExcept((byte)0);
        Assert.False(keyHasData);
        // end-snippet
    }

    [Fact]
    public void SiteTestZeroingWithAPoolOfYourOwnSample_Runs()
    {
        // begin-snippet: SiteTestZeroingWithAPoolOfYourOwn
        // The code under test: it rents a buffer for its secret and disposes it.
        static void Process(MemoryPool<byte> pool)
        {
            using var owner = pool.Rent(32);
            owner.Memory.Span[..32].Fill(0x5A);
            // ... use the secret ...
        }

        // The test passes a pool it created, so no other caller shares the buffer it reads.
        // Never read a buffer of the shared pool after disposing it: another caller may hold it.
        using var pool = new SecureMemoryPool<byte>();

        // Rent and dispose one page first, so the pool's cache holds exactly that page.
        // The test keeps the page's array to read it after the code runs.
        var first = pool.Rent(32);
        bool hasPage = MemoryMarshal.TryGetArray<byte>(
            first.Memory,
            out ArraySegment<byte> page
        );
        Assert.True(hasPage);
        first.Dispose();

        Process(pool);

        // The code took the cached page. If it gave the page back, the next Rent returns
        // the same array. If it never disposed, the pool allocates a new array and the
        // assertion below fails, so this test fails when nothing disposes.
        using var next = pool.Rent(32);
        bool hasNext = MemoryMarshal.TryGetArray<byte>(
            next.Memory,
            out ArraySegment<byte> again
        );
        Assert.True(hasNext);
        Assert.Same(page.Array, again.Array);

        // The page holds only zeros.
        bool pageHasData = page.AsSpan().ContainsAnyExcept((byte)0);
        Assert.False(pageHasData);
        // end-snippet
    }
}
