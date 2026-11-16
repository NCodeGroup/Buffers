// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Buffers;

namespace SiteSamples.Guides;

// The samples of docs/site/guides/secrets-across-await.mdx. The site's build includes each begin-snippet region in
// the page by its key (ADR-1010).
public class SecretsAcrossAwaitSamples
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] Message = """{"event":"invoice.paid","id":42}"""u8.ToArray();

    // begin-snippet: SiteAcrossAwaitUsual
    static async Task<bool> VerifyAsync(Stream keySource, byte[] message, byte[] mac)
    {
        byte[] key = new byte[32];
        await keySource.ReadExactlyAsync(key);

        byte[] expected = HMACSHA256.HashData(key, message);
        return CryptographicOperations.FixedTimeEquals(expected, mac);
    }

    public sealed class Signer(byte[] key)
    {
        private readonly byte[] _key = key;

        public byte[] Sign(byte[] message) => HMACSHA256.HashData(_key, message);
    }

    // end-snippet

    [Fact]
    public async Task SiteAcrossAwaitUsualSample_Runs()
    {
        byte[] mac = HMACSHA256.HashData(Key, Message);

        Assert.True(await VerifyAsync(new MemoryStream(Key), Message, mac));
        Assert.False(await VerifyAsync(new MemoryStream(Key), "{}"u8.ToArray(), mac));
        Assert.Equal(mac, new Signer(Key).Sign(Message));
    }

    [Fact]
    public async Task SiteAcrossAwaitMemorySample_Runs()
    {
        // begin-snippet: SiteAcrossAwaitMemory
        static async Task<bool> VerifyAsync(
            Stream keySource,
            ReadOnlyMemory<byte> message,
            ReadOnlyMemory<byte> mac
        )
        {
            using IMemoryOwner<byte> keyOwner = BufferFactory.Rent(
                32,
                isSensitive: true,
                out Memory<byte> key
            );
            await keySource.ReadExactlyAsync(key);

            return Verify(key.Span, message.Span, mac.Span);

            // The span work, in a synchronous helper, where spans are allowed.
            static bool Verify(
                ReadOnlySpan<byte> key,
                ReadOnlySpan<byte> message,
                ReadOnlySpan<byte> mac
            )
            {
                Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
                using var expectedLifetime = expected.GetSecureLifetime();
                HMACSHA256.HashData(key, message, expected);
                return CryptographicOperations.FixedTimeEquals(expected, mac);
            }
        }
        // end-snippet

        byte[] mac = HMACSHA256.HashData(Key, Message);

        Assert.True(await VerifyAsync(new MemoryStream(Key), Message, mac));
        Assert.False(await VerifyAsync(new MemoryStream(Key), "{}"u8.ToArray(), mac));
        Assert.False(await VerifyAsync(new MemoryStream(Key), Message, mac.AsMemory(0, 16)));
        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            VerifyAsync(new MemoryStream(Key, 0, 16), Message, mac)
        );
    }

    // begin-snippet: SiteSigningKey
    /// <summary>An HMAC-SHA256 key, held for as long as it is needed.</summary>
    public sealed class SigningKey : IDisposable
    {
        private readonly IMemoryOwner<byte> _owner;

        // The key's length: owner.Memory is the whole rented buffer.
        private readonly int _length;
        private bool _disposed;

        private SigningKey(IMemoryOwner<byte> owner, int length)
        {
            _owner = owner;
            _length = length;
        }

        public static async Task<SigningKey> ReadAsync(Stream source, int length)
        {
            IMemoryOwner<byte> owner = BufferFactory.Rent(
                length,
                isSensitive: true,
                out Memory<byte> key
            );
            try
            {
                await source.ReadExactlyAsync(key);
                return new SigningKey(owner, length);
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }

        public int Sign(ReadOnlySpan<byte> message, Span<byte> destination)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ReadOnlySpan<byte> key = _owner.Memory.Span[.._length];
            return HMACSHA256.HashData(key, message, destination);
        }

        public void Dispose()
        {
            _disposed = true;
            _owner.Dispose(); // Overwrites the key with zeros.
        }
    }

    // end-snippet

    [Fact]
    public async Task SiteSigningKeySample_Runs()
    {
        using SigningKey signingKey = await SigningKey.ReadAsync(new MemoryStream(Key), Key.Length);
        byte[] mac = new byte[HMACSHA256.HashSizeInBytes];

        int written = signingKey.Sign(Message, mac);

        Assert.Equal(HMACSHA256.HashSizeInBytes, written);
        Assert.Equal(HMACSHA256.HashData(Key, Message), mac);
        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            SigningKey.ReadAsync(new MemoryStream(Key, 0, 16), Key.Length)
        );
    }

    [Fact]
    public async Task SiteSigningKeyUseSample_Runs()
    {
        Stream keyFile = new MemoryStream(Key);
        byte[] message = Message;
        byte[] mac = new byte[HMACSHA256.HashSizeInBytes];

        // begin-snippet: SiteSigningKeyUse
        // At startup: read the key once, and keep the holder.
        SigningKey signingKey = await SigningKey.ReadAsync(keyFile, length: 32);

        // For each message:
        signingKey.Sign(message, mac);

        // At shutdown, or when the key is replaced:
        signingKey.Dispose();
        // end-snippet

        Assert.Equal(HMACSHA256.HashData(Key, Message), mac);
        Assert.Throws<ObjectDisposedException>(() => signingKey.Sign(Message, mac));
        signingKey.Dispose();
    }

    [Fact]
    public async Task SiteBlockBetweenAwaitsSample_Runs()
    {
        // begin-snippet: SiteBlockBetweenAwaits
        static async Task WriteMacAsync(
            Stream output,
            ReadOnlyMemory<byte> masterKey,
            ReadOnlyMemory<byte> message,
            ReadOnlyMemory<byte> context
        )
        {
            byte[] mac = new byte[HMACSHA256.HashSizeInBytes]; // A MAC is not a secret.
            {
                Span<byte> messageKey = stackalloc byte[32];
                using var messageKeyLifetime = messageKey.GetSecureLifetime();
                HKDF.DeriveKey(
                    HashAlgorithmName.SHA256,
                    masterKey.Span,
                    messageKey,
                    [],
                    context.Span
                );
                HMACSHA256.HashData(messageKey, message.Span, mac);
            } // The message key is zeroed here, before the await.

            await output.WriteAsync(mac);
        }
        // end-snippet

        byte[] context = "message-key"u8.ToArray();
        var output = new MemoryStream();

        await WriteMacAsync(output, Key, Message, context);

        byte[] messageKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, Key, 32, [], context);
        Assert.Equal(HMACSHA256.HashData(messageKey, Message), output.ToArray());
    }
}
