// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Start;

// The samples of docs/site/start/limits.mdx. SiteCopiesAreNotCleared is also the evidence of the leak card L9 in
// docs/site/_components/data/leaks.json. The site's build includes each begin-snippet region in a page by its key
// (ADR-1010).
public class LimitsSamples
{
    [Fact]
    public void SiteCopiesAreNotClearedSample_Runs()
    {
        // begin-snippet: SiteCopiesAreNotCleared
        Span<byte> original;
        byte[] copy;

        using (var key = BufferFactory.CreatePinnedArray(32))
        {
            original = key;
            RandomNumberGenerator.Fill(original);
            copy = original.ToArray();
        }

        // original is all zeros now. copy still holds the key: Dispose never saw it.
        // end-snippet

        Assert.False(original.ContainsAnyExcept((byte)0));
        Assert.True(copy.AsSpan().ContainsAnyExcept((byte)0));
    }
}
