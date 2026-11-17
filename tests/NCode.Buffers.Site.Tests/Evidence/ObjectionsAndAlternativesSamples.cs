// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace SiteSamples.Evidence;

// The samples of docs/site/evidence/objections-and-alternatives.mdx: the two ways to hold a secret without the
// library. The site's build includes each begin-snippet region in a page by its key (ADR-1010).
public class ObjectionsAndAlternativesSamples
{
    [Fact]
    public void SiteAlternativePinnedArrayByHandSample_Runs()
    {
        // begin-snippet: SiteAlternativePinnedArrayByHand
        // This is what CreatePinnedArray does for you, minus the using.
        byte[] key = GC.AllocateArray<byte>(32, pinned: true);
        try
        {
            RandomNumberGenerator.Fill(key);
            // Use the key here.
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
        // end-snippet

        Assert.Equal(GC.MaxGeneration, GC.GetGeneration(key));
        Assert.Equal(32, key.Length);
        Assert.False(key.AsSpan().ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SiteAlternativeStackByHandSample_Runs()
    {
        // begin-snippet: SiteAlternativeStackByHand
        Span<byte> key = stackalloc byte[32];
        try
        {
            RandomNumberGenerator.Fill(key);
            // Use the key here.
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
        // end-snippet

        Assert.Equal(32, key.Length);
        Assert.False(key.ContainsAnyExcept((byte)0));
    }
}
