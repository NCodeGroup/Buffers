// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using NCode.Buffers;

namespace SiteSamples.Concepts;

// The samples of docs/site/concepts/how-secrets-leak.mdx, which are also the evidence of the leak cards in
// docs/site/_components/data/leaks.json. A demonstration asserts only behavior that is documented or deterministic,
// and one that asserts on a pool creates its own. The site's build includes each begin-snippet region in a page by
// its key (ADR-1010).
public class HowSecretsLeakSamples
{
    // The page's usual code. Its SequenceEqual stops at the first byte that differs, one of the leaks the page
    // points out, so it stays.
    // begin-snippet: SiteHowSecretsLeakUsual
    public static bool VerifySignature(
        byte[] password,
        byte[] salt,
        byte[] payload,
        byte[] signature
    )
    {
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            600_000,
            HashAlgorithmName.SHA256,
            32
        );
        byte[] expected = HMACSHA256.HashData(key, payload);
        return expected.AsSpan().SequenceEqual(signature);
    }

    // end-snippet

    [Fact]
    public void SiteHowSecretsLeakUsualSample_Runs()
    {
        byte[] password = "correct horse battery staple"u8.ToArray();
        byte[] salt = "salt"u8.ToArray();
        byte[] payload = "payload"u8.ToArray();
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            600_000,
            HashAlgorithmName.SHA256,
            32
        );
        byte[] signature = HMACSHA256.HashData(key, payload);

        // The usual code works: it accepts the right signature and rejects a changed one.
        Assert.True(VerifySignature(password, salt, payload, signature));
        signature[0] ^= 1;
        Assert.False(VerifySignature(password, salt, payload, signature));
    }

    [Fact]
    public void SiteArrayPoolKeepsBytesSample_Runs()
    {
        // begin-snippet: SiteArrayPoolKeepsBytes
        ArrayPool<byte> pool = ArrayPool<byte>.Create();

        byte[] first = pool.Rent(32);
        first.AsSpan(0, 32).Fill(0x5A); // Stands in for a key.
        pool.Return(first);

        byte[] second = pool.Rent(32);

        // The next renter gets the same array, and the key is still in it.
        // end-snippet

        Assert.Same(first, second);
        Assert.False(second.AsSpan(0, 32).ContainsAnyExcept((byte)0x5A));
    }

    [Fact]
    public void SiteExceptionSkipsClearSample_Runs()
    {
        // begin-snippet: SiteExceptionSkipsClear
        byte[] usual = new byte[32];
        try
        {
            Decrypt(usual);
            CryptographicOperations.ZeroMemory(usual); // Never runs: Decrypt threw.
        }
        catch (AuthenticationTagMismatchException) { }

        Span<byte> pinned = default;
        try
        {
            using var lifetime = BufferFactory.CreatePinnedArray(32);
            pinned = lifetime;
            Decrypt(lifetime);
        }
        catch (AuthenticationTagMismatchException) { }

        // usual still holds its key. The pinned array was zeroed when the using scope ended.

        static void Decrypt(Span<byte> key)
        {
            RandomNumberGenerator.Fill(key);
            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            Span<byte> nonce = stackalloc byte[12];
            Span<byte> tag = stackalloc byte[16]; // Not the tag of this ciphertext.
            Span<byte> plaintext = stackalloc byte[16];
            aes.Decrypt(nonce, "sixteen bytes!!!"u8, tag, plaintext);
        }
        // end-snippet

        Assert.True(usual.AsSpan().ContainsAnyExcept((byte)0));
        Assert.False(pinned.ContainsAnyExcept((byte)0));
    }

    [Fact]
    public void SiteNoFinalizerSample_Runs()
    {
        // begin-snippet: SiteNoFinalizer
        using var pool = new SecureMemoryPool<byte>();
        Memory<byte> view = RentAndForget(pool);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Nothing disposed the owner, so nothing zeroed the buffer: view still holds the key.

        static Memory<byte> RentAndForget(SecureMemoryPool<byte> pool)
        {
            IMemoryOwner<byte> owner = pool.Rent(32);
            owner.Memory.Span.Fill(0x5A); // Stands in for a key.
            return owner.Memory; // The owner is dropped without Dispose.
        }
        // end-snippet

        Assert.False(view.Span.ContainsAnyExcept((byte)0x5A));
    }

    [Fact]
    public void SiteLenientDecodingCollisionSample_Runs()
    {
        // begin-snippet: SiteLenientDecodingCollision
        byte[] first = [0x61, 0x80];
        byte[] second = [0x61, 0xFF];

        // Neither input is valid UTF-8. The standard encoding substitutes U+FFFD in both, so
        // two different inputs decode to the same string.
        string a = Encoding.UTF8.GetString(first);
        string b = Encoding.UTF8.GetString(second);

        // SecureEncoding.UTF8 throws DecoderFallbackException instead.
        // end-snippet

        Assert.Equal("a�", a);
        Assert.Equal(a, b);
        Assert.Throws<DecoderFallbackException>(() =>
            SecureEncoding.UTF8.GetString(first)
        );
        Assert.Throws<DecoderFallbackException>(() =>
            SecureEncoding.UTF8.GetString(second)
        );
    }
}
