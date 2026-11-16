// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/derive-a-key-from-a-password.mdx. The site's build includes each begin-snippet
// region in the page by its key (ADR-1010). Each fact decrypts a document that the test encrypted with the same
// password, salt and iteration count, so a sample that derived the wrong key would fail.
public class DeriveAKeyFromAPasswordSamples
{
    private const string Password = "correct horse battery staple";
    private static readonly byte[] Plaintext = "The quarterly figures."u8.ToArray();

    [Fact]
    public void SitePasswordKeyUsualSample_Runs()
    {
        // begin-snippet: SitePasswordKeyUsual
        static byte[] DecryptDocument(
            string password,
            byte[] salt,
            byte[] nonce,
            byte[] ciphertext,
            byte[] tag
        )
        {
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 600_000,
                HashAlgorithmName.SHA256,
                outputLength: 32
            );
            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            byte[] plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        // end-snippet

        var document = Encrypt();

        byte[] plaintext = DecryptDocument(
            Password,
            document.Salt,
            document.Nonce,
            document.Ciphertext,
            document.Tag
        );

        Assert.Equal(Plaintext, plaintext);

        // A tag that does not match throws from Decrypt, so a line after it never runs.
        byte[] wrongTag = document.Tag.ToArray();
        wrongTag[0] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() =>
            DecryptDocument(Password, document.Salt, document.Nonce, document.Ciphertext, wrongTag)
        );
    }

    [Fact]
    public void SitePasswordKeyDecryptSample_Runs()
    {
        // begin-snippet: SitePasswordKeyDecrypt
        static void DecryptDocument(
            ReadOnlySpan<char> password,
            ReadOnlySpan<byte> salt,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> tag,
            Span<byte> plaintext
        )
        {
            using var keyOwner = BufferFactory.Rent(32, isSensitive: true, out Span<byte> key);
            Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                key,
                iterations: 600_000,
                HashAlgorithmName.SHA256
            );
            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        // end-snippet

        var document = Encrypt();
        byte[] plaintext = new byte[document.Ciphertext.Length];

        DecryptDocument(
            Password.AsSpan(),
            document.Salt,
            document.Nonce,
            document.Ciphertext,
            document.Tag,
            plaintext
        );

        Assert.Equal(Plaintext, plaintext);

        // A tag that does not match throws from Decrypt; the using declarations still run on the way out.
        byte[] wrongTag = document.Tag.ToArray();
        wrongTag[0] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() =>
            DecryptDocument(
                Password.AsSpan(),
                document.Salt,
                document.Nonce,
                document.Ciphertext,
                wrongTag,
                new byte[document.Ciphertext.Length]
            )
        );
    }

    [Fact]
    public void SitePasswordKeyVerifySample_Runs()
    {
        // begin-snippet: SitePasswordKeyVerify
        static bool VerifyPassword(
            ReadOnlySpan<char> password,
            ReadOnlySpan<byte> salt,
            ReadOnlySpan<byte> storedHash
        )
        {
            using var hashOwner = BufferFactory.Rent(32, isSensitive: true, out Span<byte> hash);
            Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                hash,
                iterations: 600_000,
                HashAlgorithmName.SHA256
            );
            return CryptographicOperations.FixedTimeEquals(hash, storedHash);
        }
        // end-snippet

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] storedHash = Rfc2898DeriveBytes.Pbkdf2(
            Password,
            salt,
            600_000,
            HashAlgorithmName.SHA256,
            32
        );

        Assert.True(VerifyPassword(Password, salt, storedHash));
        Assert.False(VerifyPassword("Correct horse battery staple", salt, storedHash));
        Assert.False(VerifyPassword(Password, salt, storedHash.AsSpan(0, 16)));
    }

    // What the sender did: the same derivation, then AES-GCM with a 16-byte tag.
    private static (byte[] Salt, byte[] Nonce, byte[] Ciphertext, byte[] Tag) Encrypt()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            Password,
            salt,
            600_000,
            HashAlgorithmName.SHA256,
            32
        );
        byte[] ciphertext = new byte[Plaintext.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Encrypt(nonce, Plaintext, ciphertext, tag);
        return (salt, nonce, ciphertext, tag);
    }
}
