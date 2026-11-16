// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Concepts;

// The samples of docs/site/concepts/ownership.mdx: who zeroes a buffer, when, and what a view kept afterwards reads.
// A sample that asserts on a pool's behavior creates its own pool. The site's build includes each begin-snippet
// region in the page by its key (ADR-1010).
public class OwnershipSamples
{
    [Fact]
    public void SiteOwnershipTightScopeSample_Runs()
    {
        Span<byte> keyView;

        // begin-snippet: SiteOwnershipTightScope
        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];

        using (var key = BufferFactory.CreatePinnedArray(32))
        {
            RandomNumberGenerator.Fill(key);
            HMACSHA256.HashData(key, "payload"u8, mac);
            keyView = key; // Only to look at the array after the block.
        } // The key is zeroed here, at the end of its block.

        // The rest of the method runs with the key already gone: keyView reads zeros.
        // end-snippet

        Assert.False(keyView.ContainsAnyExcept((byte)0));
        Assert.True(mac.ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SiteOwnershipUsingOrderSample_Runs()
    {
        // begin-snippet: SiteOwnershipUsingOrder
        ArrayPool<byte> pool = ArrayPool<byte>.Create();

        byte[] rented = pool.Rent(32);
        try
        {
            using var lifetime = rented.AsSpan(0, 32).GetSecureLifetime();
            RandomNumberGenerator.Fill(lifetime.Span);
            // ... use the secret ...
        } // The lifetime zeroes the 32 bytes here,
        finally
        {
            pool.Return(rented); // and only then does the memory go back to its owner.
        }
        // end-snippet

        byte[] next = pool.Rent(32);
        Assert.Same(rented, next);
        Assert.False(next.AsSpan(0, 32).ContainsAnyExcept((byte)0));
    }

    // begin-snippet: SiteOwnershipReturnLifetime
    // The caller owns what this returns, and disposes it.
    public static SecureArrayLifetime<byte> DeriveKey(ReadOnlySpan<byte> password)
    {
        var key = BufferFactory.CreatePinnedArray(32);
        Rfc2898DeriveBytes.Pbkdf2(password, "salt"u8, key, 600_000, HashAlgorithmName.SHA256);
        return key;
    }

    // end-snippet

    [Fact]
    public void SiteOwnershipReturnLifetimeSample_Runs()
    {
        Span<byte> view;
        using (var key = DeriveKey("correct horse battery staple"u8))
        {
            view = key;
            Assert.True(view.ContainsAnyExcept((byte)0));
        }

        Assert.False(view.ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SiteOwnershipSecondDisposeSample_Runs()
    {
        // begin-snippet: SiteOwnershipSecondDispose
        var pinned = BufferFactory.CreatePinnedArray(16);
        pinned.Dispose();
        pinned.PinnedArray.AsSpan().Fill(0x5A); // Written after the first Dispose.
        pinned.Dispose(); // Zeroes again.

        Span<byte> memory = stackalloc byte[16];
        var span = memory.GetSecureLifetime();
        span.Dispose();
        memory.Fill(0x5A); // Written after the first Dispose.
        span.Dispose(); // Zeroes whatever the memory holds by then.
        // end-snippet

        Assert.False(pinned.PinnedArray.AsSpan().ContainsAnyExcept((byte)0));
        Assert.False(memory.ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SiteOwnershipUseAfterDisposeSample_Runs()
    {
        // begin-snippet: SiteOwnershipUseAfterDispose
        using var pool = new SecureMemoryPool<byte>();

        IMemoryOwner<byte> first = pool.Rent(32);
        Memory<byte> kept = first.Memory[..32];
        first.Dispose(); // kept still points at the buffer.

        using IMemoryOwner<byte> second = pool.Rent(32);
        "another caller's data"u8.CopyTo(second.Memory.Span);

        // kept now reads what the second renter wrote: the pool gave it the same buffer.
        // end-snippet

        Assert.True(kept.Span[..21].SequenceEqual("another caller's data"u8));
    }
}
