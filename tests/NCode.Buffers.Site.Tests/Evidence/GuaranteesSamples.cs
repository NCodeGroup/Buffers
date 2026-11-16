// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Buffers;

namespace SiteSamples.Evidence;

// The samples of docs/site/evidence/guarantees.mdx: the two ways the library's tests look at memory. The site's build
// includes each begin-snippet region in the page by its key (ADR-1010).
public class GuaranteesSamples
{
    [Fact]
    public void SiteGuaranteesPinnedGenerationSample_Runs()
    {
        // begin-snippet: SiteGuaranteesPinnedGeneration
        using var lifetime = BufferFactory.CreatePinnedArray(32);
        byte[] array = lifetime;

        // The collector reports an array on the pinned object heap as its oldest generation,
        // from the moment it is allocated.
        int generation = GC.GetGeneration(array);
        // end-snippet

        Assert.Equal(GC.MaxGeneration, generation);
    }

    [Fact]
    public void SiteGuaranteesZeroedThroughAViewSample_Runs()
    {
        // begin-snippet: SiteGuaranteesZeroedThroughAView
        Span<byte> view;

        using (var lifetime = BufferFactory.CreatePinnedArray(32))
        {
            view = lifetime;
            view.Fill(0xA5);
        }

        // The view still points at the array,
        // and Dispose has overwritten every byte with zeros.
        // end-snippet

        Assert.Equal(32, view.Length);
        Assert.False(view.ContainsAnyExcept((byte)0));
    }
}
