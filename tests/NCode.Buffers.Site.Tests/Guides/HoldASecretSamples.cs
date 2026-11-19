// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/hold-a-secret.mdx. The site's build includes each begin-snippet region in the
// page by its key (ADR-1010).
public class HoldASecretSamples
{
    [Fact]
    public void SiteHoldUsualSample_Runs()
    {
        // begin-snippet: SiteHoldUsual
        // A string prints in full, so the secret lands in the log.
        var credentials = new { ClientId = "app", ClientSecret = "s3cr3t" };
        string logLine = $"Loaded {credentials}";
        // end-snippet

        Assert.Contains("s3cr3t", logLine);
    }

    [Fact]
    public void SiteHoldSecretSample_Runs()
    {
        // begin-snippet: SiteHoldSecret
        // A Secret<byte> prints as [REDACTED], so the same line leaks nothing.
        using Secret<byte> clientSecret = Secret.Create("s3cr3t"u8);
        var credentials = new { ClientId = "app", ClientSecret = clientSecret };
        string logLine = $"Loaded {credentials}";
        // end-snippet

        Assert.Contains("[REDACTED]", logLine);
        Assert.DoesNotContain("s3cr3t", logLine);
    }

    [Fact]
    public void SiteHoldRevealSample_Runs()
    {
        using Secret<byte> clientSecret = Secret.Create("s3cr3t"u8);
        byte[] payload = [0x01, 0x02, 0x03, 0x04];
        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];

        // begin-snippet: SiteHoldReveal
        // Reveal exposes the value only for the call that needs it.
        HMACSHA256.HashData(clientSecret.Reveal(), payload, mac);
        // end-snippet

        var key = "s3cr3t"u8.ToArray();
        var expected = HMACSHA256.HashData(key, payload);
        SecretAssert.Equal(expected, mac);
    }

    [Fact]
    public void SiteHoldCompareSample_Runs()
    {
        using Secret<byte> stored = Secret.Create("s3cr3t"u8);
        ReadOnlySpan<byte> presented = "s3cr3t"u8;

        // begin-snippet: SiteHoldCompare
        // The comparison takes the same time whether or not the values match.
        bool matches = stored.FixedTimeEquals(presented);
        // end-snippet

        Assert.True(matches);
        var matchesOther = stored.FixedTimeEquals("s3cr3x"u8);
        Assert.False(matchesOther);
    }

    [Fact]
    public void SiteHoldCreateInPlaceSample_Runs()
    {
        // begin-snippet: SiteHoldCreateInPlace
        // The key is filled straight into the secret, so no other copy exists.
        using Secret<byte> key = Secret.Create<byte, int>(32, 0, static (span, _) =>
            RandomNumberGenerator.Fill(span)
        );
        // end-snippet

        Assert.Equal(32, key.Length);
        var value = key.Reveal().ToArray();
        Assert.Contains(value, b => b != 0);
    }
}
