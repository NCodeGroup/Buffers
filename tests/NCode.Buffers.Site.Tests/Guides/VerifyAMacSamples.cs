// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/verify-a-mac.mdx. The site's build includes each begin-snippet region in the page
// by its key (ADR-1010). The tests assert results only: no test measures how long a comparison takes.
public class VerifyAMacSamples
{
    private static readonly byte[] Secret = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] Body = """{"event":"invoice.paid","id":42}"""u8.ToArray();

    [Fact]
    public void SiteVerifyMacUsualSample_Runs()
    {
        // begin-snippet: SiteVerifyMacUsual
        static bool VerifyWebhook(byte[] secret, byte[] body, string signatureHeader)
        {
            byte[] expected = HMACSHA256.HashData(secret, body);
            string expectedHex = Convert.ToHexStringLower(expected);
            return expectedHex == signatureHeader;
        }
        // end-snippet

        string header = Convert.ToHexStringLower(HMACSHA256.HashData(Secret, Body));

        Assert.True(VerifyWebhook(Secret, Body, header));
        Assert.False(VerifyWebhook(Secret, Body, Tamper(header)));
    }

    [Fact]
    public void SiteVerifyMacWebhookSample_Runs()
    {
        // begin-snippet: SiteVerifyMacWebhook
        static bool VerifyWebhook(
            ReadOnlySpan<byte> secret,
            ReadOnlySpan<byte> body,
            ReadOnlySpan<char> signatureHeader
        )
        {
            if (signatureHeader.Length != 2 * HMACSHA256.HashSizeInBytes)
                return false; // A wrong length is a wrong signature, not an exception.

            Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
            using var expectedLifetime = expected.GetSecureLifetime();
            HMACSHA256.HashData(secret, body, expected);

            Span<byte> received = stackalloc byte[HMACSHA256.HashSizeInBytes];
            var decoded = Convert.FromHexString(signatureHeader, received, out _, out _);
            if (decoded != OperationStatus.Done)
                return false;

            return CryptographicOperations.FixedTimeEquals(expected, received);
        }
        // end-snippet

        string header = Convert.ToHexStringLower(HMACSHA256.HashData(Secret, Body));

        Assert.True(VerifyWebhook(Secret, Body, header));
        Assert.True(VerifyWebhook(Secret, Body, header.ToUpperInvariant()));
        Assert.False(VerifyWebhook(Secret, Body, Tamper(header)));
        Assert.False(VerifyWebhook(Secret, Body, header.AsSpan(0, header.Length - 2)));
        Assert.False(VerifyWebhook(Secret, Body, header + "00"));
        Assert.False(VerifyWebhook(Secret, Body, string.Concat("zz", header.AsSpan(2))));
        Assert.False(VerifyWebhook(Secret, "{}"u8, header));
    }

    [Fact]
    public void SiteVerifyMacTextSecretSample_Runs()
    {
        // begin-snippet: SiteVerifyMacTextSecret
        static bool IsExpectedApiKey(ReadOnlySpan<char> presented, ReadOnlySpan<char> expected)
        {
            // Two spans of char: this is NCode.Buffers' FixedTimeEquals<char>, which compares
            // their bytes with .NET's fixed-time comparison. Ordinal and case-sensitive.
            return CryptographicOperations.FixedTimeEquals(presented, expected);
        }
        // end-snippet

        const string apiKey = "k3y-9QzX1fT7";

        Assert.True(IsExpectedApiKey("k3y-9QzX1fT7", apiKey));
        Assert.False(IsExpectedApiKey("K3Y-9QZX1FT7", apiKey));
        Assert.False(IsExpectedApiKey("k3y-9QzX1fT", apiKey));
        Assert.False(IsExpectedApiKey("", apiKey));
    }

    // Changes the last hex digit, so the header keeps its length and stays valid hex.
    private static string Tamper(string header) =>
        string.Concat(header.AsSpan(0, header.Length - 1), header[^1] == '0' ? "1" : "0");
}
