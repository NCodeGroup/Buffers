// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/encode-a-secret.mdx. The site's build includes each begin-snippet region in the
// page by its key (ADR-1010).
public class EncodeASecretSamples
{
    [Fact]
    public void SiteEncodeSecretUsualSample_Runs()
    {
        string text = Convert.ToBase64String([0x01, 0x02, 0x03, 0x04]);
        const string passwordText = "correct horse";

        // begin-snippet: SiteEncodeSecretUsual
        // Three arrays nothing zeroes.
        byte[] key = Convert.FromBase64String(text);
        byte[] password = Encoding.UTF8.GetBytes(passwordText);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        // end-snippet

        SecretAssert.Equal([0x01, 0x02, 0x03, 0x04], key);
        SecretAssert.Equal("correct horse"u8, password);
        Assert.Equal(12, nonce.Length);
    }

    [Fact]
    public void SiteEncodeSecretDecodeKeySample_Runs()
    {
        string text = Convert
            .ToBase64String([0xFB, 0xFF, 0xFE, 0x01])
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // begin-snippet: SiteEncodeSecretDecodeKey
        // The key is Base64url text, as a JWK holds it, decoded into a buffer that is zeroed on dispose.
        using var keyOwner = BinaryTextEncoding.RentDecoded(
            text,
            BinaryTextFormat.Base64Url,
            isSensitive: true,
            out Span<byte> key
        );
        // end-snippet

        Assert.Equal("-__-AQ", text);
        SecretAssert.Equal([0xFB, 0xFF, 0xFE, 0x01], key);
    }

    [Fact]
    public void SiteEncodeSecretPasswordBytesSample_Runs()
    {
        const string password = "correct horse";

        // begin-snippet: SiteEncodeSecretPasswordBytes
        // The password's bytes are strict UTF-8 in a buffer that is zeroed on dispose.
        using var passwordOwner = SecureEncoding.UTF8.RentBytes(
            password,
            isSensitive: true,
            out Span<byte> passwordBytes
        );
        // end-snippet

        SecretAssert.Equal("correct horse"u8, passwordBytes);
    }

    [Fact]
    public void SiteEncodeSecretRandomKeySample_Runs()
    {
        // begin-snippet: SiteEncodeSecretRandomKey
        // A random key and its Base64url text, each in a buffer that is zeroed on dispose.
        using var keyOwner = BufferFactory.RentRandom(
            32,
            isSensitive: true,
            out Span<byte> key
        );
        using var textOwner = BinaryTextEncoding.RentEncoded(
            key,
            BinaryTextFormat.Base64Url,
            isSensitive: true,
            out Span<char> text
        );
        // end-snippet

        Assert.Equal(32, key.Length);
        Assert.Equal(43, text.Length);

        using var decodedOwner = BinaryTextEncoding.RentDecoded(
            text,
            BinaryTextFormat.Base64Url,
            isSensitive: true,
            out Span<byte> decoded
        );
        SecretAssert.Equal(key, decoded);
    }

    [Fact]
    public void SiteEncodeSecretRejectInvalidSample_Runs()
    {
        // begin-snippet: SiteEncodeSecretRejectInvalid
        static bool IsValidKey(string text)
        {
            try
            {
                using var _ = BinaryTextEncoding.RentDecoded(
                    text,
                    BinaryTextFormat.Base64Url,
                    isSensitive: true,
                    out Span<byte> _
                );
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
        // end-snippet

        Assert.True(IsValidKey("-__-AQ"));
        Assert.False(IsValidKey("not base64url!"));
    }
}
