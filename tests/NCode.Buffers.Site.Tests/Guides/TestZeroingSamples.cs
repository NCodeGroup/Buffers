// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
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

        Assert.False(seen.AsSpan().ContainsAnyExcept((byte)0));
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
        Assert.False(key.ContainsAnyExcept((byte)0));
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

        Process(pool);

        // The pool hands the same buffer out again. It holds only zeros.
        using var next = pool.Rent(32);
        Assert.False(next.Memory.Span.ContainsAnyExcept((byte)0));
        // end-snippet
    }
}
