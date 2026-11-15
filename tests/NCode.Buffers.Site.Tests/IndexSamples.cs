// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples;

// The samples of docs/site/index.mdx. The site's build includes each begin-snippet region in the page by its key (ADR-1010).
public class IndexSamples
{
    [Fact]
    public void SiteZeroedAfterUseSample_Runs()
    {
        // begin-snippet: SiteZeroedAfterUse
        Span<byte> key = stackalloc byte[32];
        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];

        using (key.GetSecureLifetime())
        {
            RandomNumberGenerator.Fill(key);
            HMACSHA256.HashData(key, "payload"u8, mac);
        }

        // Every byte of key is zero here: disposing the lifetime overwrote it.
        // end-snippet

        // The MAC of a key that was never filled: the sample's MAC differs, so its key held data inside the block.
        Span<byte> zeroKey = stackalloc byte[32];
        Span<byte> zeroKeyMac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(zeroKey, "payload"u8, zeroKeyMac);

        Assert.False(mac.SequenceEqual(zeroKeyMac));
        Assert.False(key.ContainsAnyExcept((byte)0));
    }
}
