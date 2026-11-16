// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/byte-array-apis.mdx. The site's build includes each begin-snippet region in the
// page by its key (ADR-1010). The usual code and the fix do the same job, and the tests check that they give the same
// results.
public class ByteArrayApisSamples
{
    private static readonly byte[] Data = "Ship order 42."u8.ToArray();

    [Fact]
    public void SiteByteArrayUsualSample_Runs()
    {
        // begin-snippet: SiteByteArrayUsual
        static ECDsa ImportSigningKey(ReadOnlySpan<byte> privateKey)
        {
            byte[] d = privateKey.ToArray();
            ECCurve curve = ECCurve.NamedCurves.nistP256;
            return ECDsa.Create(new ECParameters { Curve = curve, D = d });
        }

        static void Seal(
            ECDiffieHellman ours,
            ECDiffieHellmanPublicKey theirs,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> plaintext,
            Span<byte> ciphertext,
            Span<byte> tag
        )
        {
            byte[] shared = ours.DeriveRawSecretAgreement(theirs);
            byte[] key = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                shared,
                outputLength: 32
            );
            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        // end-snippet

        AssertImports(ImportSigningKey);
        AssertSeals(Seal);
    }

    [Fact]
    public void SiteByteArrayTakesSample_Runs()
    {
        // begin-snippet: SiteByteArrayTakes
        static ECDsa ImportSigningKey(ReadOnlySpan<byte> privateKey)
        {
            using var lifetime = BufferFactory.CreatePinnedArray(privateKey.Length);
            byte[] d = lifetime; // The pinned array itself, not a copy.
            privateKey.CopyTo(d);
            ECCurve curve = ECCurve.NamedCurves.nistP256;
            return ECDsa.Create(new ECParameters { Curve = curve, D = d });
        }
        // end-snippet

        AssertImports(ImportSigningKey);
    }

    [Fact]
    public void SiteByteArrayReturnsSample_Runs()
    {
        // begin-snippet: SiteByteArrayReturns
        static void Seal(
            ECDiffieHellman ours,
            ECDiffieHellmanPublicKey theirs,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> plaintext,
            Span<byte> ciphertext,
            Span<byte> tag
        )
        {
            byte[] shared = ours.DeriveRawSecretAgreement(theirs);
            using var sharedLifetime = shared.GetSecureLifetime();

            using var keyOwner = BufferFactory.Rent(
                32,
                isSensitive: true,
                out Span<byte> key
            );
            HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, key, salt: [], info: []);
            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        // end-snippet

        AssertSeals(Seal);
    }

    [Fact]
    public void SiteByteArrayReturnTrapSample_Runs()
    {
        // begin-snippet: SiteByteArrayReturnTrap
        static byte[] DeriveKey(ReadOnlySpan<byte> sharedSecret)
        {
            using var key = BufferFactory.CreatePinnedArray(32);
            HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                sharedSecret,
                key,
                salt: [],
                info: []
            );
            // Compiles: the lifetime converts to byte[].
            // Dispose runs after the return.
            return key;
        }

        byte[] key = DeriveKey(RandomNumberGenerator.GetBytes(32));

        // key is 32 zero bytes. Nothing fails: zeros work as a key,
        // so the mistake does not show.
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        // end-snippet

        Assert.Equal(32, key.Length);
        Assert.False(key.AsSpan().ContainsAnyExcept((byte)0));
        SecretAssert.Equal(new byte[32], key);
    }

    [Fact]
    public void SiteKeySetterCopiesSample_Runs()
    {
        // begin-snippet: SiteKeySetterCopies
        using var aes = Aes.Create();
        byte[] pinned;

        using (var key = BufferFactory.CreatePinnedArray(32))
        {
            pinned = key;
            RandomNumberGenerator.Fill(pinned);
            aes.Key = pinned; // The setter copies the array.
        }

        // pinned is all zeros now. aes.Key still returns the key:
        // aes keeps a copy of its own.
        // end-snippet

        Assert.False(pinned.AsSpan().ContainsAnyExcept((byte)0));
        Assert.True(aes.Key.AsSpan().ContainsAnyExcept((byte)0));
        Assert.Equal(32, aes.Key.Length);
    }

    private delegate ECDsa ImportMethod(ReadOnlySpan<byte> privateKey);

    private delegate void SealMethod(
        ECDiffieHellman ours,
        ECDiffieHellmanPublicKey theirs,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag
    );

    // The imported key signs, and the signature verifies against the original key's public half.
    private static void AssertImports(ImportMethod import)
    {
        using var original = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[]? privateKey = original.ExportParameters(includePrivateParameters: true).D;
        Assert.NotNull(privateKey);

        using var imported = import(privateKey);
        byte[] signature = imported.SignData(Data, HashAlgorithmName.SHA256);

        Assert.True(original.VerifyData(Data, signature, HashAlgorithmName.SHA256));
        SecretAssert.Equal(
            privateKey,
            imported.ExportParameters(includePrivateParameters: true).D
        );
    }

    // The other side derives the same key from its own private key and opens the message.
    private static void AssertSeals(SealMethod seal)
    {
        using var alice = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var bob = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        byte[] nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        byte[] ciphertext = new byte[Data.Length];
        byte[] tag = new byte[16];

        seal(alice, bob.PublicKey, nonce, Data, ciphertext, tag);

        byte[] shared = bob.DeriveRawSecretAgreement(alice.PublicKey);
        byte[] key = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, outputLength: 32);
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        byte[] opened = new byte[Data.Length];
        aes.Decrypt(nonce, ciphertext, tag, opened);
        Assert.Equal(Data, opened);
    }
}
